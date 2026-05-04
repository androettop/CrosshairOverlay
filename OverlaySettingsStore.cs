using System;

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
}
