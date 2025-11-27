using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Nuotti.Projector.Controls;

public partial class ReconnectOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsReconnectingProperty =
        AvaloniaProperty.Register<ReconnectOverlay, bool>(nameof(IsReconnecting));
    
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<ReconnectOverlay, string>(nameof(StatusText), "Reconnecting...");
    
    public static readonly StyledProperty<string> InfoTextProperty =
        AvaloniaProperty.Register<ReconnectOverlay, string>(nameof(InfoText), "Please wait while we restore the connection");
    
    private readonly TextBlock _statusText;
    private readonly TextBlock _infoText;
    
    public bool IsReconnecting
    {
        get => GetValue(IsReconnectingProperty);
        set => SetValue(IsReconnectingProperty, value);
    }
    
    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }
    
    public string InfoText
    {
        get => GetValue(InfoTextProperty);
        set => SetValue(InfoTextProperty, value);
    }
    
    public ReconnectOverlay()
    {
        InitializeComponent();
        DataContext = this;
        
        _statusText = this.FindControl<TextBlock>("StatusTextBlock")!;
        _infoText = this.FindControl<TextBlock>("InfoTextBlock")!;
        
        // Property change handlers will be handled via binding in XAML
    }
    
    public void Show(string status = "Reconnecting...", string info = "Please wait while we restore the connection")
    {
        StatusText = status;
        InfoText = info;
        IsReconnecting = true;
    }
    
    public void Hide()
    {
        IsReconnecting = false;
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
