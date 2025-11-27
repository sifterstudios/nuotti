using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Views;

public abstract partial class PhaseViewBase : UserControl
{
    protected Grid PhaseContainer { get; private set; } = null!;
    
    protected PhaseViewBase()
    {
        InitializeComponent();
        PhaseContainer = this.FindControl<Grid>("PhaseContainer")!;
    }
    
    public abstract void UpdateState(GameState state);
    
    protected virtual void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
