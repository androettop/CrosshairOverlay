using System;
using System.IO;
using System.Text.Json;

namespace CrosshairOverlay;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "CrosshairOverlay");
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public OverlaySettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new OverlaySettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<OverlaySettings>(json, JsonOptions);
            return settings ?? new OverlaySettings();
        }
        catch
        {
            return new OverlaySettings();
        }
    }

    public void Save(OverlaySettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
