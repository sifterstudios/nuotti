using Bunit;
using MudBlazor.Services;
namespace Nuotti.Performer.Tests;

/// <summary>
/// Shared bUnit TestContext that registers MudBlazor services required by components
/// (e.g., ISnackbar, IKeyInterceptorService, IDialogService, IResizeObserverFactory).
/// </summary>
public class MudTestContext : TestContext
{
    public MudTestContext()
    {
        // Register MudBlazor service singletons used by components under test
        Services.AddMudServices();

        // Loosen JS interop to avoid failing on MudBlazor's internal JS calls during unit tests
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Common MudBlazor JS calls we can safely ignore in tests
        JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
    }
}
