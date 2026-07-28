using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Nuotti.Projector.Services;

public class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "en";
    private readonly string[] _supportedLanguages = { "en" }; // Start with English only
    
    public event Action<string>? LanguageChanged;
    
    public string CurrentLanguage => _currentLanguage;
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages;
    
    public LocalizationService()
    {
        // Initialize with default English translations
        InitializeDefaultTranslations();
    }
    
    public async Task LoadTranslationsAsync()
    {
        try
        {
            // For now, we'll use embedded translations
            // In the future, this could load from files or remote sources
            await LoadEmbeddedTranslationsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading translations: {ex.Message}");
            // Continue with default translations
        }
    }
    
    private void InitializeDefaultTranslations()
    {
        var englishTranslations = new Dictionary<string, string>
        {
            // Common UI
            ["common.loading"] = "Loading...",
            ["common.retry"] = "Try Again",
            ["common.cancel"] = "Cancel",
            ["common.ok"] = "OK",
            ["common.back"] = "Back",
            ["common.close"] = "Close",
            ["common.refresh"] = "Refresh",
            
            // Window controls
            ["window.fullscreen"] = "Fullscreen",
            ["window.exit_fullscreen"] = "Exit Fullscreen",
            ["window.black_screen"] = "Black Screen",
            ["window.always_on_top"] = "Always on Top",
            ["window.cursor_visibility"] = "Cursor Visibility",
            
            // Game states
            ["game.waiting"] = "Waiting for Game",
            ["game.lobby"] = "Lobby",
            ["game.ready"] = "Ready",
            ["game.song_intro"] = "Song Introduction",
            ["game.guessing"] = "Guessing",
            ["game.hint"] = "Hint",
            ["game.reveal"] = "Reveal",
            ["game.intermission"] = "Intermission",
            ["game.finished"] = "Game Finished",
            
            // Player counts and pluralization
            ["players.count.zero"] = "No players",
            ["players.count.one"] = "1 player",
            ["players.count.other"] = "{0} players",
            
            // Points and scoring
            ["points.count.zero"] = "No points",
            ["points.count.one"] = "1 point",
            ["points.count.other"] = "{0} points",
            
            // Error messages
            ["error.network"] = "Connection Problem",
            ["error.network.message"] = "Unable to connect to the game server. Please check your network connection.",
            ["error.session_not_found"] = "Session Not Found",
            ["error.session_not_found.message"] = "The game session could not be found. It may have ended or the session code is incorrect.",
            ["error.invalid_data"] = "Data Error",
            ["error.invalid_data.message"] = "There was a problem with the game data. Some information may be missing or corrupted.",
            ["error.theme"] = "Display Error",
            ["error.theme.message"] = "There was a problem loading the display theme. The app may not look as expected.",
            ["error.font"] = "Font Loading Error",
            ["error.font.message"] = "Some fonts could not be loaded. Text may appear different than expected.",
            ["error.generic"] = "Something went wrong",
            ["error.generic.message"] = "We encountered an unexpected error. Please try again.",
            ["error.retry"] = "🔄 Try Again",
            ["error.back_to_lobby"] = "🏠 Back to Lobby",
            ["error.show_details"] = "🔍 Show Details",
            ["error.hide_details"] = "🔼 Hide Details",
            
            // Empty states
            ["empty.waiting_for_game"] = "The game hasn't started yet. Please wait for the host to begin.",
            ["empty.no_players"] = "Waiting for players to join the game session.",
            ["empty.no_songs"] = "There are no songs in the current playlist.",
            ["empty.no_scores"] = "Scores will appear here once the game begins.",
            ["empty.loading"] = "Please wait while we load the content.",
            ["empty.disconnected"] = "Connection lost. Attempting to reconnect...",
            
            // Debug overlay
            ["debug.title"] = "🐛 DEBUG OVERLAY",
            ["debug.performance"] = "PERFORMANCE",
            ["debug.game_state"] = "GAME STATE",
            ["debug.connection"] = "CONNECTION",
            ["debug.actions"] = "ACTIONS",
            ["debug.copy_diagnostics"] = "📋 Copy Diagnostics",
            ["debug.toggle_help"] = "Ctrl+D to toggle",
            
            // Performance metrics
            ["perf.fps"] = "FPS: {0:F1}",
            ["perf.frame_time"] = "Frame: {0:F1} ms",
            ["perf.animations_enabled"] = "Animations: Enabled",
            ["perf.animations_disabled"] = "Animations: DISABLED",
            
            // Connection info
            ["connection.phase"] = "Phase: {0}",
            ["connection.song"] = "Song: {0}",
            ["connection.session"] = "Session: {0}",
            ["connection.tallies"] = "Tallies: {0}",
            ["connection.id"] = "ID: {0}",
            
            // Now playing
            ["now_playing.by"] = "by {0}",
            ["now_playing.playing"] = "Playing: {0}",
            
            // Monitor selection
            ["monitor.title"] = "Select Display",
            ["monitor.primary"] = "Primary Display",
            ["monitor.select"] = "Select & Go Fullscreen",
            ["monitor.cancel"] = "Cancel",
            
            // Safe area
            ["safe_area.enabled"] = "Safe Area: ON",
            ["safe_area.disabled"] = "Safe Area: OFF",
            
            // Keyboard shortcuts help
            ["help.keyboard_shortcuts"] = "🎮 KEYBOARD SHORTCUTS",
            ["help.window_controls"] = "Window Controls:",
            ["help.debug"] = "Debug (DEV only):",
            ["help.monitor_display"] = "Monitor & Display:",
            ["help.fonts_typography"] = "Fonts & Typography:"
        };
        
        _translations["en"] = englishTranslations;
    }
    
    private Task LoadEmbeddedTranslationsAsync()
    {
        // Placeholder for loading additional translations
        // In the future, this could load from embedded resources or external files
        return Task.CompletedTask;
    }
    
    public string GetString(string key, params object[] args)
    {
        if (_translations.TryGetValue(_currentLanguage, out var languageDict) &&
            languageDict.TryGetValue(key, out var translation))
        {
            try
            {
                return args.Length > 0 ? string.Format(translation, args) : translation;
            }
            catch (FormatException)
            {
                // Return the unformatted string if formatting fails
                return translation;
            }
        }
        
        // Fallback to key if translation not found
        return $"[{key}]";
    }
    
    public string GetPluralString(string baseKey, int count, params object[] args)
    {
        var pluralKey = count switch
        {
            0 => $"{baseKey}.zero",
            1 => $"{baseKey}.one",
            _ => $"{baseKey}.other"
        };
        
        var allArgs = new object[args.Length + 1];
        allArgs[0] = count;
        Array.Copy(args, 0, allArgs, 1, args.Length);
        
        return GetString(pluralKey, allArgs);
    }
    
    public bool SetLanguage(string languageCode)
    {
        if (!Array.Exists(_supportedLanguages, lang => lang == languageCode))
        {
            return false;
        }
        
        if (_currentLanguage != languageCode)
        {
            _currentLanguage = languageCode;
            LanguageChanged?.Invoke(_currentLanguage);
        }
        
        return true;
    }
    
    public string GetCurrentCultureName()
    {
        return _currentLanguage switch
        {
            "en" => "English",
            _ => _currentLanguage.ToUpper()
        };
    }
    
    public void SetCultureFromSystem()
    {
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        
        // Try to set the system language if supported, otherwise keep English
        if (!SetLanguage(systemLanguage))
        {
            SetLanguage("en");
        }
    }
    
    // Helper methods for common translations
    public string Loading => GetString("common.loading");
    public string Retry => GetString("common.retry");
    public string Cancel => GetString("common.cancel");
    public string Back => GetString("common.back");
    
    public string PlayersCount(int count) => GetPluralString("players.count", count);
    public string PointsCount(int count) => GetPluralString("points.count", count);
}
