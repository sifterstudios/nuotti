using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Projector.Models;
using System.Linq;

namespace Nuotti.Projector.Views;

public partial class GuessingView : PhaseViewBase
{
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _questionText;
    private readonly TextBlock[] _optionTexts;
    private readonly TextBlock[] _optionCounts;
    private readonly Border[] _optionBorders;
    
    public GuessingView()
    {
        InitializeComponent();
        
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _songArtistText = this.FindControl<TextBlock>("SongArtistText")!;
        _questionText = this.FindControl<TextBlock>("QuestionText")!;
        
        _optionTexts = new[]
        {
            this.FindControl<TextBlock>("OptionAText")!,
            this.FindControl<TextBlock>("OptionBText")!,
            this.FindControl<TextBlock>("OptionCText")!,
            this.FindControl<TextBlock>("OptionDText")!
        };
        
        _optionCounts = new[]
        {
            this.FindControl<TextBlock>("OptionACount")!,
            this.FindControl<TextBlock>("OptionBCount")!,
            this.FindControl<TextBlock>("OptionCCount")!,
            this.FindControl<TextBlock>("OptionDCount")!
        };
        
        _optionBorders = new[]
        {
            this.FindControl<Border>("OptionA")!,
            this.FindControl<Border>("OptionB")!,
            this.FindControl<Border>("OptionC")!,
            this.FindControl<Border>("OptionD")!
        };
    }
    
    public override void UpdateState(GameState state)
    {
        // Update song info
        _songTitleText.Text = state.CurrentSongTitle;
        _songArtistText.Text = state.CurrentSongArtist;
        
        // Update question (could be dynamic based on game type)
        _questionText.Text = "What song is this?";
        
        // Update options
        for (int i = 0; i < _optionTexts.Length; i++)
        {
            if (i < state.Choices.Count)
            {
                _optionTexts[i].Text = state.Choices[i];
                _optionBorders[i].IsVisible = true;
                
                // Update tallies
                var count = i < state.Tallies.Count ? state.Tallies[i] : 0;
                _optionCounts[i].Text = count.ToString();
            }
            else
            {
                _optionBorders[i].IsVisible = false;
            }
        }
        
        // Highlight leading options
        HighlightLeaders(state.Tallies);
    }
    
    private void HighlightLeaders(IReadOnlyList<int> tallies)
    {
        if (tallies.Count == 0) return;
        
        var max = tallies.Max();
        
        // Get theme brushes
        IBrush? successBrush = null;
        IBrush? defaultBrush = null;
        
        if (Application.Current?.Resources.TryGetResource("SuccessBrush", Application.Current?.ActualThemeVariant, out var successObj) == true && successObj is IBrush s)
            successBrush = s;
        if (Application.Current?.Resources.TryGetResource("OptionBackgroundBrush", Application.Current?.ActualThemeVariant, out var defaultObj) == true && defaultObj is IBrush d)
            defaultBrush = d;
        
        successBrush ??= new SolidColorBrush(Color.Parse("#46B283"));
        defaultBrush ??= new SolidColorBrush(Color.Parse("#F5F5F5"));
        
        for (int i = 0; i < _optionBorders.Length && i < tallies.Count; i++)
        {
            _optionBorders[i].Background = tallies[i] == max && max > 0 ? successBrush : defaultBrush;
        }
    }
    
    protected override void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
