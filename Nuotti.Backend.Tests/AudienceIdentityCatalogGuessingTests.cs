using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.Catalog;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Issue #256 — anonymous Audience identity, large-Catalog search privacy, and idempotent waiting answers.
/// </summary>
public sealed class AudienceIdentityCatalogGuessingTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Device_identity_moderated_name_catalog_search_and_waiting_answer_roundtrip()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "aud256");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Audience band" });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);

        const string session = "AUD256";
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{session}/create", member.SessionToken))
            .EnsureSuccessStatusCode();

        await _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>()
            .CreateEntryAsync(workspace.WorkspaceId, "Private Hit", "Us", member.Principal.UserId);
        var other = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Other tenant" });
        await _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>()
            .CreateEntryAsync(other.WorkspaceId, "Leaked Song", "Them", member.Principal.UserId);

        var shared = _factory.Services.GetRequiredService<ISharedSongCatalog>();
        shared.Seed([
            new SongRef(new SongId("shared-hit"), "Shared Hit", "Nuotti"),
            ..Enumerable.Range(0, 1100).Select(i =>
                new SongRef(new SongId($"pad-{i}"), $"Pad {i:D4}", "Pad Artist"))
        ]);

        // The audience surfaces now require the join token rather than a session code and a
        // participant id in the query string, so the journey starts where a real phone starts.
        const string deviceSecret = "device-aud-256-0123456789";
        var ticket = await PostAsync<AudienceJoinEndpoints.JoinResponse>(client,
            $"/v1/sessions/{session}/join", null, new { deviceSecret, displayName = "PlayerOne" });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(client, HttpMethod.Get, $"/api/sessions/{session}/catalog/search?q=Hit", null)).StatusCode);

        var searchResponse = await SendAsync(client, HttpMethod.Get,
            $"/api/sessions/{session}/catalog/search?q=Hit&limit=20", ticket.Token);
        searchResponse.EnsureSuccessStatusCode();
        var search = await searchResponse.Content.ReadFromJsonAsync<SongRef[]>();
        Assert.NotNull(search);
        Assert.Contains(search!, s => s.Title == "Shared Hit");
        Assert.Contains(search!, s => s.Title == "Private Hit");
        Assert.DoesNotContain(search!, s => s.Title == "Leaked Song");

        var participants = _factory.Services.GetRequiredService<IParticipantIdentityStore>();
        // InvokeAsync("Join") completes when the hub method returns, but ParticipantRestored is
        // broadcast back as a separate message. Asserting on the captured id straight after the
        // invoke is a race the test loses whenever the machine is busy: it read null on three of
        // six local runs and on CI. Wait for the message itself rather than for a moment in time.
        var restoredSignal = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hub"),
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        hub.On<object>("ParticipantRestored", payload =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            restoredSignal.TrySetResult(doc.RootElement.GetProperty("ParticipantId").GetString());
        });
        await hub.StartAsync();
        await hub.InvokeAsync("Join", session, "audience", "PlayerOne", deviceSecret);
        var participantId = await restoredSignal.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(string.IsNullOrWhiteSpace(participantId));

        // One participant, whichever door the phone came through. Two identity stores minting ids
        // from the same device secret would split its answers and its score between them.
        Assert.Equal(ticket.ParticipantId, participantId);

        Assert.True(participants.TryModerateName(session, participantId!, "StageSafe", out var moderated));
        Assert.Equal("StageSafe", moderated!.DisplayName);

        var restored = participants.JoinOrRestore(session, deviceSecret, "ignored-because-moderated");
        Assert.Equal(participantId, restored.ParticipantId);
        Assert.Equal("StageSafe", restored.DisplayName);
        Assert.NotEqual(participantId,
            participants.JoinOrRestore("OTHER", deviceSecret, "OtherRoom").ParticipantId);

        var state = _factory.Services.GetRequiredService<IGameStateStore>();
        await PostPhaseAsync(client, "start-game", session, new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = member.Principal.UserId
        });
        await PostJsonAsync(client, $"/api/pushQuestion/{session}", new QuestionPushed("Guess?", ["A", "B", "C"])
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = member.Principal.UserId
        });
        await PostPhaseAsync(client, "open-answers", session, new OpenAnswers
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = member.Principal.UserId
        });

        var commandId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await hub.InvokeAsync("SubmitAnswer", session, 1, commandId);
        await Task.Delay(600); // outside SubmitAnswer debounce window
        await hub.InvokeAsync("SubmitAnswer", session, 1, commandId);

        Assert.True(state.TryGet(session, out var mid));
        Assert.Equal(1, mid!.Answers[participantId!]);
        Assert.Equal(new[] { 0, 1, 0 }, mid.Tallies.ToArray());

        await Task.Delay(600);
        await hub.InvokeAsync("SubmitAnswer", session, 2, Guid.NewGuid());
        Assert.True(state.TryGet(session, out mid));
        Assert.Equal(2, mid!.Answers[participantId!]);
        Assert.Equal(new[] { 0, 0, 1 }, mid.Tallies.ToArray());

        // The answer read is scoped to the token's own participant, so one phone can no longer
        // read what the phone next to it has already answered while the round is open.
        var answerResponse = await SendAsync(client, HttpMethod.Get, $"/status/{session}/answer", ticket.Token);
        answerResponse.EnsureSuccessStatusCode();
        var answerResp = await answerResponse.Content.ReadFromJsonAsync<AudienceAnswerStatusEndpoints.MyAnswerResponse>();
        Assert.Equal(2, answerResp!.ChoiceIndex);

        // Idempotent waiting retry: same CommandId through the hub is accepted without double-counting.
        Assert.Equal(1, mid.Tallies.Sum());
    }

    static async Task PostPhaseAsync(HttpClient client, string route, string session, CommandBase cmd)
    {
        var response = await client.PostAsJsonAsync($"/v1/message/phase/{route}/{session}", cmd);
        response.EnsureSuccessStatusCode();
    }

    static async Task PostJsonAsync(HttpClient client, string path, object body)
    {
        (await SendAsync(client, HttpMethod.Post, path, null, body)).EnsureSuccessStatusCode();
    }

    static async Task SelectAsync(HttpClient client, string token, string workspaceId) =>
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token)).EnsureSuccessStatusCode();

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = link.Token });
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
