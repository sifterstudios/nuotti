using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private ProjectorSettings? _cachedSettings;
    
    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "Nuotti.Projector");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }
    
    public async Task<ProjectorSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;
            
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _cachedSettings = JsonSerializer.Deserialize<ProjectorSettings>(json) ?? new ProjectorSettings();
            }
            else
            {
                _cachedSettings = new ProjectorSettings();
            }
        }
        catch
        {
            _cachedSettings = new ProjectorSettings();
        }
        
        return _cachedSettings;
    }
    
    public async Task SaveSettingsAsync(ProjectorSettings settings)
    {
        _cachedSettings = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - settings persistence is not critical
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
    
    public ProjectorSettings GetCachedSettings()
    {
        return _cachedSettings ?? new ProjectorSettings();
    }
}
