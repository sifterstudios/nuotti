namespace Nuotti.Contracts.V1.Message.Phase;

public interface IPhaseRestricted
{
    IReadOnlyCollection<Enum.Phase> AllowedPhases { get; }
}