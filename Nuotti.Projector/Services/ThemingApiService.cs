using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Microsoft.AspNetCore.SignalR.Client;

namespace Nuotti.Projector.Services;

public class ThemingApiService : IDisposable
{
    private HubConnection? _connection;
    private readonly string _backendUrl;
    private bool _isConnected = false;
    
    public event Action<ThemeVariant>? ThemeChangeRequested;
    public event Action<TallyDisplayMode>? TallyModeChangeRequested;
    public event Action<ProjectorStyleSettings>? StyleSettingsChanged;
    
    public bool IsConnected => _isConnected;
    
    public ThemingApiService(string backendUrl)
    {
        _backendUrl = backendUrl;
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_backendUrl}/projectorStyleHub")
                .WithAutomaticReconnect()
                .Build();
            
            // Subscribe to theming events
            _connection.On<string>("ThemeChanged", OnThemeChanged);
            _connection.On<string>("TallyModeChanged", OnTallyModeChanged);
            _connection.On<ProjectorStyleSettings>("StyleSettingsChanged", OnStyleSettingsChanged);
            
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;
            _connection.Closed += OnConnectionClosed;
            
            await _connection.StartAsync();
            _isConnected = true;
            
            Console.WriteLine("[theming-api] Connected to theming API");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[theming-api] Connection failed: {ex.Message}");
            _isConnected = false;
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[theming-api] Disconnect error: {ex.Message}");
            }
            finally
            {
                _connection = null;
                _isConnected = false;
            }
        }
    }
    
    public async Task RegisterProjectorAsync(string sessionCode)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("RegisterProjector", sessionCode);
                Console.WriteLine($"[theming-api] Registered projector for session: {sessionCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[theming-api] Registration failed: {ex.Message}");
            }
        }
    }
    
    public async Task SendCurrentStyleAsync(ProjectorStyleSettings currentStyle)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("UpdateProjectorStyle", currentStyle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[theming-api] Style update failed: {ex.Message}");
            }
        }
    }
    
    private void OnThemeChanged(string themeName)
    {
        var themeVariant = themeName.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            "default" => ThemeVariant.Default,
            _ => ThemeVariant.Default
        };
        
        Console.WriteLine($"[theming-api] Theme change requested: {themeName}");
        ThemeChangeRequested?.Invoke(themeVariant);
    }
    
    private void OnTallyModeChanged(string tallyMode)
    {
        var mode = Enum.TryParse<TallyDisplayMode>(tallyMode, true, out var parsedMode) 
            ? parsedMode 
            : TallyDisplayMode.Animated;
        
        Console.WriteLine($"[theming-api] Tally mode change requested: {tallyMode}");
        TallyModeChangeRequested?.Invoke(mode);
    }
    
    private void OnStyleSettingsChanged(ProjectorStyleSettings settings)
    {
        Console.WriteLine($"[theming-api] Style settings changed");
        StyleSettingsChanged?.Invoke(settings);
    }
    
    private Task OnReconnecting(Exception? exception)
    {
        Console.WriteLine("[theming-api] Reconnecting...");
        _isConnected = false;
        return Task.CompletedTask;
    }
    
    private Task OnReconnected(string? connectionId)
    {
        Console.WriteLine($"[theming-api] Reconnected with ID: {connectionId}");
        _isConnected = true;
        return Task.CompletedTask;
    }
    
    private Task OnConnectionClosed(Exception? exception)
    {
        Console.WriteLine($"[theming-api] Connection closed: {exception?.Message ?? "Normal closure"}");
        _isConnected = false;
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _ = Task.Run(async () => await DisconnectAsync());
    }
}

public class ProjectorStyleSettings
{
    public string ThemeVariant { get; set; } = "Default";
    public string TallyMode { get; set; } = "Animated";
    public bool ShowSafeArea { get; set; } = false;
    public double SafeAreaMargin { get; set; } = 0.05;
    public bool HideTalliesUntilReveal { get; set; } = true;
    public string? CustomCssOverrides { get; set; }
    public ProjectorColors? Colors { get; set; }
}

public class ProjectorColors
{
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? SuccessColor { get; set; }
    public string? ErrorColor { get; set; }
}

public enum TallyDisplayMode
{
    Hidden,
    Numeric,
    Animated,
    Percentage
}
