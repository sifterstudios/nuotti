using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Nuotti.Projector.Controls;

public partial class NowPlayingBanner : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<NowPlayingBanner, bool>(nameof(IsPlaying));
    
    public static readonly StyledProperty<string> SongTitleProperty =
        AvaloniaProperty.Register<NowPlayingBanner, string>(nameof(SongTitle), "Unknown Song");
    
    public static readonly StyledProperty<string?> ArtistProperty =
        AvaloniaProperty.Register<NowPlayingBanner, string?>(nameof(Artist));
    
    public static readonly StyledProperty<bool> HasArtistProperty =
        AvaloniaProperty.Register<NowPlayingBanner, bool>(nameof(HasArtist));
    
    private readonly TextBlock _songTitleText;
    private readonly TextBlock _artistText;
    
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }
    
    public string SongTitle
    {
        get => GetValue(SongTitleProperty);
        set => SetValue(SongTitleProperty, value);
    }
    
    public string? Artist
    {
        get => GetValue(ArtistProperty);
        set
        {
            SetValue(ArtistProperty, value);
            HasArtist = !string.IsNullOrEmpty(value);
        }
    }
    
    public bool HasArtist
    {
        get => GetValue(HasArtistProperty);
        private set => SetValue(HasArtistProperty, value);
    }
    
    public NowPlayingBanner()
    {
        InitializeComponent();
        DataContext = this;
        
        _songTitleText = this.FindControl<TextBlock>("SongTitleText")!;
        _artistText = this.FindControl<TextBlock>("ArtistText")!;
        
        // Property change handlers will be handled via binding in XAML
    }
    
    public void UpdateSong(string title, string? artist = null)
    {
        SongTitle = title;
        Artist = artist;
    }
    
    public void Show()
    {
        IsPlaying = true;
    }
    
    public void Hide()
    {
        IsPlaying = false;
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
