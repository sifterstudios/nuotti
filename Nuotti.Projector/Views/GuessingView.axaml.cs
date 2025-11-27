using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Projector.Models;
using Nuotti.Projector.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Nuotti.Projector.Views;

public partial class GuessingView : PhaseViewBase
{
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _songArtistText;
    private readonly TextBlock _questionText;
    private readonly TextBlock[] _optionTexts;
    private readonly TextBlock[] _optionCounts;
    private readonly Border[] _optionBorders;
    private readonly AnimationService _animationService;
    private int[] _lastTallies = new int[4];
    private bool _hideTalliesUntilReveal;
    private ProjectorSettings? _settings;
    
    public GuessingView()
    {
        InitializeComponent();
        
        _animationService = new AnimationService();
        
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
        
        // Check if we should hide tallies
        _hideTalliesUntilReveal = ShouldHideTallies(state.Phase);
        
        // Update options
        for (int i = 0; i < _optionTexts.Length; i++)
        {
            if (i < state.Choices.Count)
            {
                _optionTexts[i].Text = state.Choices[i];
                _optionBorders[i].IsVisible = true;
                
                // Update tallies with animation
                var newCount = i < state.Tallies.Count ? state.Tallies[i] : 0;
                var oldCount = i < _lastTallies.Length ? _lastTallies[i] : 0;
                
                if (_hideTalliesUntilReveal)
                {
                    _optionCounts[i].Text = "?";
                }
                else
                {
                    if (newCount != oldCount)
                    {
                        _ = _animationService.AnimateCounterUpdate(_optionCounts[i], oldCount, newCount);
                    }
                    else
                    {
                        _optionCounts[i].Text = newCount.ToString();
                    }
                }
            }
            else
            {
                _optionBorders[i].IsVisible = false;
            }
        }
        
        // Store current tallies for next update
        _lastTallies = state.Tallies.ToArray();
        
        // Highlight leading options (only if not hiding tallies)
        if (!_hideTalliesUntilReveal)
        {
            _ = HighlightLeadersAnimated(state.Tallies);
        }
    }
    
    public void UpdateSettings(ProjectorSettings settings)
    {
        _settings = settings;
    }
    
    private bool ShouldHideTallies(Phase phase)
    {
        // Hide tallies during guessing phase if setting is enabled
        return _settings?.HideTalliesUntilReveal == true && phase == Phase.Guessing;
    }
    
    private async Task HighlightLeadersAnimated(IReadOnlyList<int> tallies)
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
        
        // Animate background changes
        var tasks = new List<Task>();
        for (int i = 0; i < _optionBorders.Length && i < tallies.Count; i++)
        {
            var newBrush = tallies[i] == max && max > 0 ? successBrush : defaultBrush;
            if (_optionBorders[i].Background != newBrush)
            {
                tasks.Add(_animationService.AnimateBackgroundChange(_optionBorders[i], newBrush));
            }
        }
        
        // Wait for all animations to complete
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
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
