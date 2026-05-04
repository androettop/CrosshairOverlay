using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CrosshairOverlay.Platform;

namespace CrosshairOverlay;

public partial class MainWindow : Window
{
    private readonly IWindowsOverlayPlatformService _platformService;

    public MainWindow()
        : this(new OverlaySettings(), new WindowsOverlayPlatformService())
    {
    }

    public MainWindow(OverlaySettings settings, IWindowsOverlayPlatformService platformService)
    {
        _platformService = platformService;
        InitializeComponent();

        ConfigureOverlayWindow();
        ApplySettings(settings);

        Opened += OnOpened;
        KeyDown += OnKeyDown;
    }

    private void ConfigureOverlayWindow()
    {
        Background = Brushes.Transparent;
        Topmost = true;
        CanResize = false;
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowState = WindowState.FullScreen;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ExtendClientAreaToDecorationsHint = true;
    }

    private void ApplySettings(OverlaySettings settings)
    {
        Crosshair.Color = Color.TryParse(settings.Color, out var parsedColor)
            ? parsedColor
            : Colors.Red;
        Crosshair.OpacityLevel = settings.Opacity;
        Crosshair.Size = settings.Size;
        Crosshair.Gap = settings.Gap;
        Crosshair.Thickness = settings.Thickness;
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
}