using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Enum;

/// <summary>
/// Explains why an operation failed or was rejected.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReasonCode
{
    /// <summary>
    /// No specific reason; success or not applicable.
    /// </summary>
    None,
    /// <summary>
    /// An attempted state change was not valid for the current <see cref="Phase"/>
    /// </summary>
    InvalidStateTransition,
    /// <summary>
    /// The acting <sse cref="Role"/> is not allowed to perform this action.
    /// </summary>
    UnauthorizedRole,
    /// <summary>
    /// The same command was sent multiple times and rejected as a duplicate.
    /// </summary>
    DuplicateCommand,
}