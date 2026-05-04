using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CrosshairOverlay.Platform;

namespace CrosshairOverlay;

public partial class MainWindow : Window
{
    private readonly IWindowsOverlayPlatformService _platformService;
    private readonly OverlaySettingsStore _settingsStore;
    private readonly PixelRect _monitorBounds;

    public MainWindow()
        : this(
            new OverlaySettingsStore(new SettingsService()),
            new WindowsOverlayPlatformService(),
            new PixelRect(0, 0, 1920, 1080))
    {
    }

    public MainWindow(OverlaySettingsStore settingsStore, IWindowsOverlayPlatformService platformService, PixelRect monitorBounds)
    {
        _settingsStore = settingsStore;
        _platformService = platformService;
        _monitorBounds = monitorBounds;
        InitializeComponent();

        ConfigureOverlayWindow();
        ApplySettings(_settingsStore.Current);

        Opened += OnOpened;
        KeyDown += OnKeyDown;
        Closed += OnClosed;
        _settingsStore.SettingsChanged += OnSettingsChanged;
    }

    private void ConfigureOverlayWindow()
    {
        Background = Brushes.Transparent;
        Topmost = true;
        CanResize = false;
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Position = new PixelPoint(_monitorBounds.X, _monitorBounds.Y);
        Width = _monitorBounds.Width;
        Height = _monitorBounds.Height;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ExtendClientAreaToDecorationsHint = true;
        WindowState = WindowState.FullScreen;
    }

    private void ApplySettings(OverlaySettings settings)
    {
        Crosshair.ApplySettings(settings);
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _platformService.EnableClickThrough(this);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnSettingsChanged(object? sender, OverlaySettings settings)
    {
        ApplySettings(settings);
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        _settingsStore.SettingsChanged -= OnSettingsChanged;
    }
}