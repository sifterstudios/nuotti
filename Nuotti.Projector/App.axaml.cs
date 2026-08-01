using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Presentation;
using Nuotti.Projector.Services;
using System.Diagnostics;
namespace Nuotti.Projector;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Debug.WriteLine("[App] Composing services...");

            // Composition root. Deliberately explicit rather than a DI container: one window needs
            // no registration list, and the presenter is testable without one either way.
            var contentSafety = new ContentSafetyService();
            var localization = new LocalizationService();
            var typography = new ResponsiveTypographyService();
            var settings = new SettingsService();
            var presenter = new PhasePresenter(contentSafety, localization, typography);

            Debug.WriteLine("[App] Creating MainWindow...");
            desktop.MainWindow = new MainWindow(presenter, contentSafety, localization, settings);
            Debug.WriteLine($"[App] MainWindow created. WindowState={desktop.MainWindow.WindowState}, IsVisible={desktop.MainWindow.IsVisible}");

            desktop.MainWindow.Show();
            Debug.WriteLine($"[App] MainWindow.Show() called. WindowState={desktop.MainWindow.WindowState}, IsVisible={desktop.MainWindow.IsVisible}");

            // Ensure window is visible and on-screen
            if (desktop.MainWindow.WindowState == WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = WindowState.Normal;
                Debug.WriteLine("[App] Window was minimized, set to Normal");
            }

            // No hardcoded placement here: SettingsService restores the saved monitor, size and
            // fullscreen state, and overriding it made those settings look broken.

            // Bring window to front
            desktop.MainWindow.Activate();
            Debug.WriteLine("[App] MainWindow.Activate() called");

            Debug.WriteLine($"[App] Final state - WindowState={desktop.MainWindow.WindowState}, IsVisible={desktop.MainWindow.IsVisible}, Position={desktop.MainWindow.Position}");
        }
        else
        {
            Debug.WriteLine("[App] WARNING: ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime!");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
