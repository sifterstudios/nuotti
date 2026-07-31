using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Nuotti.Backend.InfrastructureProof;

public static class InfrastructureProofEndpoints
{
    public static IEndpointRouteBuilder MapInfrastructureProofEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("Nuotti:InfrastructureProofEnabled", false)
            || string.IsNullOrWhiteSpace(configuration.GetConnectionString("nuotti"))
            || string.IsNullOrWhiteSpace(configuration.GetConnectionString("assets"))
            || string.IsNullOrWhiteSpace(configuration.GetConnectionString("realtime")))
        {
            return endpoints;
        }

        var group = endpoints.MapGroup("/infrastructure-proof");
        group.MapHub<InfrastructureProofHub>("/hub");
        group.MapPut("/records/{id}", PutRecord);
        group.MapGet("/records/{id}", GetRecord);
        group.MapPost("/object-grants/{name}", CreateObjectGrant);
        group.MapPost("/fanout/{message}", PublishFanout);
        return endpoints;
    }

    static async Task<IResult> PutRecord(string id, ProofRecord record, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS infrastructure_proof (
                id text PRIMARY KEY,
                value text NOT NULL,
                written_at timestamptz NOT NULL
            );
            INSERT INTO infrastructure_proof (id, value, written_at)
            VALUES ($1, $2, now())
            ON CONFLICT (id) DO UPDATE SET value = EXCLUDED.value, written_at = EXCLUDED.written_at;
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(record.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Results.Accepted($"/infrastructure-proof/records/{Uri.EscapeDataString(id)}");
    }

    static async Task<IResult> GetRecord(string id, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT value, written_at FROM infrastructure_proof WHERE id = $1");
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Results.Ok(new ProofRecordResult(id, reader.GetString(0), reader.GetDateTime(1)))
            : Results.NotFound();
    }

    static async Task<IResult> CreateObjectGrant(string name, BlobServiceClient blobs, CancellationToken cancellationToken)
    {
        var container = blobs.GetBlobContainerClient("infrastructure-proof");
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(name);
        if (!blob.CanGenerateSasUri)
        {
            return Results.Problem("The configured storage identity cannot generate a direct proof grant.", statusCode: 503);
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var uri = blob.GenerateSasUri(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create, expiresAt);
        return Results.Ok(new ObjectGrant(uri, expiresAt));
    }

    static async Task<IResult> PublishFanout(string message, IHubContext<InfrastructureProofHub> hub, CancellationToken cancellationToken)
    {
        var eventId = Guid.NewGuid().ToString("N");
        await hub.Clients.All.SendAsync("ProofFanout", new { eventId, message }, cancellationToken);
        return Results.Accepted(value: new { eventId });
    }
}

public sealed record ProofRecord(string Value);
public sealed record ProofRecordResult(string Id, string Value, DateTime WrittenAtUtc);
public sealed record ObjectGrant(Uri Uri, DateTimeOffset ExpiresAt);
