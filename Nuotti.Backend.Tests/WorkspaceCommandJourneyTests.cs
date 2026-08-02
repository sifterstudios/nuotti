using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Running a show through the surface a deployed Nuotti actually exposes.
/// </summary>
/// <remarks>
/// Until now the deployed build could create a session and start a game and then stop: every other
/// command lived on /v1/message/phase/*, which is local-only because it takes the issuing role from
/// the request body. A band on nuotti.app could open the app and not run a quiz with it.
/// </remarks>
public sealed class WorkspaceCommandJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task A_performer_runs_a_round_from_question_to_reveal()
    {
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client);
        var state = _factory.Services.GetRequiredService<IGameStateStore>();

        await CommandAsync(client, band, "start-game");
        await CommandAsync(client, band, "push-question",
            new { text = "Which song?", options = new[] { "A", "B", "C" } });
        await CommandAsync(client, band, "open-answers", new { windowSeconds = 30 });

        // A phone can only answer once the round is open, so this is where the audience half of
        // the journey meets the performer half.
        var ticket = await PostAsync<AudienceJoinEndpoints.JoinResponse>(client,
            $"/v1/sessions/{band.SessionCode}/join", null,
            new { deviceSecret = "device-secret-0123456789", displayName = "Fan" });
        Assert.Equal(band.SessionCode, ticket.SessionCode);

        await CommandAsync(client, band, "lock-answers");
        await CommandAsync(client, band, "reveal-answer", new
        {
            songRef = new { songId = new { value = "song-1" }, title = "Which song?", artist = "The Satellites" },
            correctChoiceIndex = 0
        });

        Assert.True(state.TryGet(band.SessionCode, out var snapshot));
        Assert.Equal(Phase.Reveal, snapshot!.Phase);
    }

    [Fact]
    public async Task The_server_stamps_who_issued_a_command_over_whatever_the_caller_claimed()
    {
        // IssuedById lands in the audit trail and in every event derived from the command. The
        // server writes it rather than checking it, so a client cannot put somebody else's name on
        // its own actions - and an honest client does not have to know its own user id to be
        // recorded correctly. QuestionPushed is relayed untouched, which is what makes the stamp
        // observable from outside.
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client);

        await using var watcher = Connect(band);
        var pushed = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.On<JsonElement>("QuestionPushed", q => pushed.TrySetResult(q));
        await watcher.StartAsync();

        var response = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{band.WorkspaceId}/sessions/{band.SessionCode}/commands/push-question",
            band.SessionToken, Body(band, issuedById: "somebody-else",
                payload: new { text = "Which song?", options = new[] { "A", "B" } }));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var question = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // The hub's serializer casing is its own business; the assertion is about the value.
        var issuedBy = question.EnumerateObject()
            .First(p => p.NameEquals("issuedById") || p.NameEquals("IssuedById"));
        Assert.Equal(band.UserId, issuedBy.Value.GetString());
    }

    HubConnection Connect(Band band) => new HubConnectionBuilder()
        .WithUrl(new Uri(_factory.Server.BaseAddress,
                $"/hub?sessionCode={band.SessionCode}&workspaceId={band.WorkspaceId}&access_token={band.SessionToken}"),
            options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
        .Build();

    [Fact]
    public async Task A_command_aimed_at_another_bands_session_is_refused()
    {
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client);
        var rival = await StartSessionAsync(client, "rival");

        // The rival's token, the first band's workspace: membership is what fails here, and it
        // fails before the command reaches the processor.
        var response = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{band.WorkspaceId}/sessions/{band.SessionCode}/commands/start-game",
            rival.SessionToken, Body(band, issuedById: rival.UserId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_drive_a_session()
    {
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client);

        var response = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{band.WorkspaceId}/sessions/{band.SessionCode}/commands/end-game",
            null, Body(band, issuedById: band.UserId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    static async Task CommandAsync(HttpClient client, Band band, string route, object? payload = null)
    {
        var response = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{band.WorkspaceId}/sessions/{band.SessionCode}/commands/{route}",
            band.SessionToken, Body(band, band.UserId, payload));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>
    /// Builds a command payload: the command's own fields plus the three every command carries.
    /// </summary>
    static Dictionary<string, object?> Body(Band band, string issuedById, object? payload = null)
    {
        var body = new Dictionary<string, object?>();
        if (payload is not null)
            foreach (var property in System.Text.Json.JsonSerializer.SerializeToElement(payload).EnumerateObject())
                body[property.Name] = property.Value;
        body["sessionCode"] = band.SessionCode;
        body["issuedByRole"] = Role.Performer.ToString();
        body["issuedById"] = issuedById;
        return body;
    }

    sealed record Band(string SessionToken, string WorkspaceId, string SessionCode, string UserId);

    static async Task<Band> StartSessionAsync(HttpClient client, string prefix = "band")
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        var member = await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null,
            new { token = link.Token });
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "The Satellites" });
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspace.WorkspaceId}/select",
            member.SessionToken)).EnsureSuccessStatusCode();
        var sessionCode = $"{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{sessionCode}/create",
            member.SessionToken)).EnsureSuccessStatusCode();
        return new Band(member.SessionToken, workspace.WorkspaceId, sessionCode, member.Principal.UserId);
    }

    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
