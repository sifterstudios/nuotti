using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Thrown when a command is attempted in a session phase that is not allowed for that command.
/// </summary>
[Serializable]
public sealed class PhaseViolationException : Exception
{
    public PhaseEnum CurrentPhase { get; }
    public Type CommandType { get; }
    public IReadOnlyCollection<PhaseEnum> AllowedPhases { get; }

    public PhaseViolationException(PhaseEnum currentPhase, Type commandType, IReadOnlyCollection<PhaseEnum> allowedPhases)
        : base($"Command '{commandType.Name}' is not allowed in phase '{currentPhase}'. Allowed phases: {string.Join(", ", allowedPhases)}")
    {
        CurrentPhase = currentPhase;
        CommandType = commandType;
        AllowedPhases = allowedPhases;
    }

    PhaseViolationException(SerializationInfo info, StreamingContext context)
    {
        CurrentPhase = (PhaseEnum)info.GetValue(nameof(CurrentPhase), typeof(PhaseEnum))!;
        CommandType = (Type)info.GetValue(nameof(CommandType), typeof(Type))!;
        AllowedPhases = (IReadOnlyCollection<PhaseEnum>)info.GetValue(nameof(AllowedPhases), typeof(IReadOnlyCollection<PhaseEnum>))!;
    }
}