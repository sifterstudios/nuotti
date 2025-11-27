using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Views;

public abstract partial class PhaseViewBase : UserControl
{
    
    protected PhaseViewBase()
    {
        InitializeComponent();
    }
    
    public abstract void UpdateState(GameState state);
    
    protected virtual void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
