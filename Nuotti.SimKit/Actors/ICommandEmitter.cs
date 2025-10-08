using Nuotti.Contracts.V1.Message;
namespace Nuotti.SimKit.Actors;

public interface ICommandEmitter
{
    Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default);
}