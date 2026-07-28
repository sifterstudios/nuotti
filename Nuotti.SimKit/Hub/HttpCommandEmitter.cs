using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
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

    /// <summary>
    /// True when a Command has a phase endpoint. This is the single source of truth for "is this
    /// a phase command" — <see cref="Nuotti.SimKit.InProc.InProcCommandEmitter"/> gates on it too,
    /// so a scenario written once against <see cref="ICommandEmitter"/> cannot accept a command
    /// type at one fidelity that the other fidelity refuses.
    /// </summary>
    public static bool IsPhaseCommand(CommandBase command) => Routes.ContainsKey(command.GetType());

    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        if (!IsPhaseCommand(command))
        {
            throw new NotSupportedException(
                $"{command.GetType().Name} has no phase endpoint. Commands that are not phase " +
                "commands (for example SubmitAnswer) go through the hub, not this emitter.");
        }

        var route = Routes[command.GetType()];
        var uri = $"/v1/message/phase/{route}/{command.SessionCode}";
        using var content = JsonContent.Create(command, command.GetType(), options: ContractsJson.RestOptions);
        using var response = await http.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

        // Duplicate is reported as Accepted by the Backend: the caller's intent is satisfied,
        // just not twice. Anything else is a rejection.
        if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // The Backend serializes rejections as a NuottiProblem document (see ProblemResults /
        // PhaseEndpoints.ToHttpResult). Try to recover the structured reason so a caller can ask
        // "was this rejected for UnauthorizedRole?" without string-matching the body. A body that
        // is not a NuottiProblem document (a proxy error page, an empty body, etc.) just leaves
        // Problem null rather than failing the emitter over an unparseable rejection.
        NuottiProblem? problem = null;
        try
        {
            problem = JsonSerializer.Deserialize<NuottiProblem>(body, ContractsJson.RestOptions);
        }
        catch (JsonException)
        {
            // Not a NuottiProblem document; RawPayload still carries the raw body.
        }

        throw new CommandRejectedException(command, body) { Problem = problem };
    }
}
