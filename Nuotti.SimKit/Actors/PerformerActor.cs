using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Script;
using RoleEnum = Nuotti.Contracts.V1.Enum.Role;

namespace Nuotti.SimKit.Actors;

public sealed class PerformerActor(IHubClientFactory hubClientFactory, Uri baseUri, string session) : BaseActor(hubClientFactory, baseUri, session)
{

    protected override string Role => "performer";

    public IEnumerable<CommandBase> BuildCommandsFromScript(ScriptModel script, string issuedById = "performer-script")
    {
        foreach (var step in script.Steps)
        {
            yield return step.Kind switch
            {
                StepKind.StartSet => new StartGame
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.NextSong => new NextRound(step.RequireSongId())
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.GiveHint => new GiveHint(new Hint(
                        step.HintIndex ?? 0,
                        step.HintText,
                        step.PerformerInstructions,
                        step.RequireSongId()))
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.LockAnswers => new LockAnswers
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.RevealAnswer => new RevealAnswer(step.RequireSongRef())
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.EndSong => new EndSong(step.RequireSongId())
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.Play => new PlaySong(step.RequireSongId())
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                StepKind.Stop => new EndSong(step.RequireSongId())
                {
                    SessionCode = SessionCode,
                    IssuedByRole = RoleEnum.Performer,
                    IssuedById = issuedById
                },
                _ => throw new NotSupportedException($"Unsupported step kind: {step.Kind}")
            };
        }
    }

    public async Task RunScriptAsync(ScriptModel script, ICommandEmitter emitter, string issuedById = "performer-script", CancellationToken cancellationToken = default)
    {
        foreach (var cmd in BuildCommandsFromScript(script, issuedById))
        {
            await emitter.EmitAsync(cmd, cancellationToken);
        }
    }
}