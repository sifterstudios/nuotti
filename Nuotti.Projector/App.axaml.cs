using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Show();
            
            // Ensure window is visible and on-screen
            if (desktop.MainWindow.WindowState == WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = WindowState.Normal;
            }
            
            // Bring window to front
            desktop.MainWindow.Activate();
        }

        base.OnFrameworkInitializationCompleted();
    }
}