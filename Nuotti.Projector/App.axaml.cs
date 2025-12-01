using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            Debug.WriteLine("[App] Creating MainWindow...");
            desktop.MainWindow = new MainWindow();
            Debug.WriteLine($"[App] MainWindow created. WindowState={desktop.MainWindow.WindowState}, IsVisible={desktop.MainWindow.IsVisible}");
            
            desktop.MainWindow.Show();
            Debug.WriteLine($"[App] MainWindow.Show() called. WindowState={desktop.MainWindow.WindowState}, IsVisible={desktop.MainWindow.IsVisible}");
            
            // Ensure window is visible and on-screen
            if (desktop.MainWindow.WindowState == WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = WindowState.Normal;
                Debug.WriteLine("[App] Window was minimized, set to Normal");
            }
            
            // Set explicit position and size
            desktop.MainWindow.Position = new PixelPoint(100, 100);
            desktop.MainWindow.Width = 1280;
            desktop.MainWindow.Height = 720;
            Debug.WriteLine($"[App] Window positioned at {desktop.MainWindow.Position}, size {desktop.MainWindow.Width}x{desktop.MainWindow.Height}");
            
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