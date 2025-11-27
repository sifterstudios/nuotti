using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Nuotti.Projector.Controls;

public partial class SafeAreaFrame : UserControl
{
    public static readonly StyledProperty<bool> ShowFrameProperty =
        AvaloniaProperty.Register<SafeAreaFrame, bool>(nameof(ShowFrame));
    
    public bool ShowFrame
    {
        get => GetValue(ShowFrameProperty);
        set => SetValue(ShowFrameProperty, value);
    }
    
    public SafeAreaFrame()
    {
        InitializeComponent();
        DataContext = this;
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
