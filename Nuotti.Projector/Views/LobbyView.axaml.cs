using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Views;

public partial class LobbyView : PhaseViewBase
{
    private readonly TextBlock _sessionCodeText;
    private readonly TextBlock _playerCountText;
    
    public LobbyView()
    {
        InitializeComponent();
        _sessionCodeText = this.FindControl<TextBlock>("SessionCodeText")!;
        _playerCountText = this.FindControl<TextBlock>("PlayerCountText")!;
    }
    
    public override void UpdateState(GameState state)
    {
        _sessionCodeText.Text = state.SessionCode.ToUpperInvariant();
        
        var playerCount = state.Scores.Count;
        _playerCountText.Text = playerCount switch
        {
            0 => "Waiting for players...",
            1 => "1 player joined",
            _ => $"{playerCount} players joined"
        };
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
