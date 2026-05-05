using System;
using System.Linq;

namespace CrosshairOverlay;

public sealed class OverlaySettingsStore
{
    private readonly SettingsService _settingsService;

    public OverlaySettingsStore(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Current = _settingsService.Load();
    }

    public OverlaySettings Current { get; private set; }

    public event EventHandler<OverlaySettings>? SettingsChanged;

    public void Update(Action<OverlaySettings> mutate)
    {
        mutate(Current);
        _settingsService.Save(Current);
        SettingsChanged?.Invoke(this, Current);
    }

    public string CreateUserPreset(string name)
    {
        var id = Guid.NewGuid().ToString();
        Update(s => s.UserPresets.Add(new UserPreset
        {
            Id = id,
            Name = name,
            Values = OverlayPresets.CaptureFromSettings(s),
        }));
        return id;
    }

    public void OverwriteUserPreset(string id)
    {
        Update(s =>
        {
            var preset = s.UserPresets.FirstOrDefault(p => p.Id == id);
            if (preset is not null)
            {
                preset.Values = OverlayPresets.CaptureFromSettings(s);
            }
        });
    }

    public void RenameUserPreset(string id, string newName)
    {
        Update(s =>
        {
            var preset = s.UserPresets.FirstOrDefault(p => p.Id == id);
            if (preset is not null)
            {
                preset.Name = newName;
            }
        });
    }

    public void DeleteUserPreset(string id)
    {
        Update(s => s.UserPresets.RemoveAll(p => p.Id == id));
    }

    public void ApplyUserPreset(string id)
    {
        var preset = Current.UserPresets.FirstOrDefault(p => p.Id == id);
        if (preset is null)
        {
            return;
        }

        Update(s => OverlayPresets.ApplyToSettings(preset.Values, s));
    }
}
