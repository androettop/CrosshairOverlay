using System;
using System.Collections.Generic;
using System.Globalization;
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
            return Validate(settings ?? new OverlaySettings());
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

        var json = JsonSerializer.Serialize(Validate(settings), JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static OverlaySettings Validate(OverlaySettings settings)
    {
        settings.CenterDotSize = Math.Max(0, settings.CenterDotSize);
        settings.CenterDotOpacity = Math.Clamp(settings.CenterDotOpacity, 0, 1);

        settings.DotGridPointSize = Math.Max(0, settings.DotGridPointSize);
        settings.DotGridOpacity = Math.Clamp(settings.DotGridOpacity, 0, 1);
        settings.DotGridRows = Math.Max(1, settings.DotGridRows);
        settings.DotGridColumns = Math.Max(1, settings.DotGridColumns);
        settings.DotGridRadiusPoints = Math.Max(1, settings.DotGridRadiusPoints);
        settings.DotGridSpacing = Math.Max(1, settings.DotGridSpacing);

        settings.CrosshairOpacity = Math.Clamp(settings.CrosshairOpacity, 0, 1);
        settings.CrosshairHorizontalLength = Math.Max(0, settings.CrosshairHorizontalLength);
        settings.CrosshairVerticalLength = Math.Max(0, settings.CrosshairVerticalLength);
        settings.CrosshairGap = Math.Max(0, settings.CrosshairGap);
        settings.CrosshairThickness = Math.Max(1, settings.CrosshairThickness);
        settings.MonitorIndex = Math.Max(0, settings.MonitorIndex);

        settings.EnabledMonitorIndices ??= [];
        settings.EnabledMonitorIndices = SanitizeMonitorIndices(settings.EnabledMonitorIndices);

        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            settings.Language = DetectDefaultLanguage();
        }
        else
        {
            settings.Language = NormalizeLanguage(settings.Language);
        }

        settings.CenterDotShape = NormalizeEnum(settings.CenterDotShape, "Circle", "Square");
        settings.DotGridPointShape = NormalizeEnum(settings.DotGridPointShape, "Circle", "Square");
        settings.DotGridAreaShape = NormalizeEnum(settings.DotGridAreaShape, "Square", "Circle");

        if (string.IsNullOrWhiteSpace(settings.CenterDotColor))
        {
            settings.CenterDotColor = "#FFFFFF";
        }

        if (string.IsNullOrWhiteSpace(settings.DotGridColor))
        {
            settings.DotGridColor = "#FFFFFF";
        }

        if (string.IsNullOrWhiteSpace(settings.CrosshairColor))
        {
            settings.CrosshairColor = "#FFFFFF";
        }

        return settings;
    }

    private static List<int> SanitizeMonitorIndices(List<int> values)
    {
        var unique = new HashSet<int>();
        var result = new List<int>();

        foreach (var value in values)
        {
            if (value < 0 || !unique.Add(value))
            {
                continue;
            }

            result.Add(value);
        }

        return result;
    }

    private static string DetectDefaultLanguage()
    {
        var twoLetters = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(twoLetters, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
    }

    private static string NormalizeLanguage(string value)
    {
        return string.Equals(value, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
    }

    private static string NormalizeEnum(string? value, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowed[0];
        }

        foreach (var item in allowed)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return allowed[0];
    }
}
