using Microsoft.AspNetCore.Components.Web;
namespace Nuotti.Performer.Services;

/// <summary>
/// Very small service used to coordinate global keyboard shortcut handling.
/// For now it only exposes a Suspended flag which can be set when a modal is open
/// so keyboard shortcuts are ignored. Tests can also toggle this flag.
/// </summary>
public class KeyboardShortcutsService
{
    /// <summary>
    /// When true, global keyboard shortcuts should be ignored (e.g., when a modal is open).
    /// </summary>
    public bool Suspended { get; set; }

    /// <summary>
    /// Helper to check if a key should be processed.
    /// </summary>
    public bool ShouldHandle(KeyboardEventArgs e) => !Suspended;
}