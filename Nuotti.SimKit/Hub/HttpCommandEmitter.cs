using System.Net;
using System.Net.Http.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Actors;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Sends Commands to a running Backend over its phase endpoints.
/// </summary>
/// <remarks>
/// Mirrors Nuotti.Backend.Endpoints.PhaseEndpoints.MapPhaseEndpoints. If a Command type is
/// added there, add it to <see cref="Routes"/> too — the unmapped case throws rather than
/// guessing a route.
/// </remarks>
public sealed class HttpCommandEmitter(HttpClient http) : ICommandEmitter
{
    public static IReadOnlyDictionary<Type, string> Routes { get; } = new Dictionary<Type, string>
    {
        [typeof(CreateSession)] = "create-session",
        [typeof(StartGame)] = "start-game",
        [typeof(OpenAnswers)] = "open-answers",
        [typeof(EndSong)] = "end-song",
        [typeof(LockAnswers)] = "lock-answers",
        [typeof(RevealAnswer)] = "reveal-answer",
        [typeof(NextRound)] = "next-round",
        [typeof(PlaySong)] = "play-song",
        [typeof(GiveHint)] = "give-hint",
        [typeof(EndGame)] = "end-game",
    };

    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        if (!Routes.TryGetValue(command.GetType(), out var route))
        {
            throw new NotSupportedException(
                $"{command.GetType().Name} has no phase endpoint. Commands that are not phase " +
                "commands (for example SubmitAnswer) go through the hub, not this emitter.");
        }

        var uri = $"/v1/message/phase/{route}/{command.SessionCode}";
        using var content = JsonContent.Create(command, command.GetType(), options: ContractsJson.RestOptions);
        using var response = await http.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

        // Duplicate is reported as Accepted by the Backend: the caller's intent is satisfied,
        // just not twice. Anything else is a rejection.
        if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new CommandRejectedException(command, body);
    }
}
