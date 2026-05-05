using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CrosshairOverlay.Platform;

namespace CrosshairOverlay;

public partial class MainWindow : Window
{
    private readonly IWindowsOverlayPlatformService _platformService;
    private readonly OverlaySettingsStore _settingsStore;
    private readonly PixelRect _monitorBounds;

    // Motion detection
    private readonly MotionDetectionEngine _motionEngine = new();
    private DispatcherTimer? _captureTimer;
    private double _gridOffsetX;
    private double _gridOffsetY;
    private int _captureInProgress; // Interlocked flag to prevent concurrent captures

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
        Icon = App.TryCreateTrayIcon();
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
        WindowState = OperatingSystem.IsWindows() ? WindowState.FullScreen : WindowState.Normal;
    }

    private void ApplySettings(OverlaySettings settings)
    {
        Crosshair.ApplySettings(settings);
        var excludeFromCapture = settings.EnableMotionDetection && !settings.DebugAllowOverlayCapture;
        _platformService.SetExcludeFromCapture(this, excludeFromCapture);
        RestartCaptureTimer(settings);
    }

    private void RestartCaptureTimer(OverlaySettings settings)
    {
        _captureTimer?.Stop();
        _captureTimer = null;

        if (!settings.EnableMotionDetection)
        {
            _motionEngine.Reset();
            _gridOffsetX = 0;
            _gridOffsetY = 0;
            Crosshair.SetGridOffset(0, 0);
            Crosshair.SetMotionCapturePreview(null, 0, 0);
            return;
        }

        var interval = TimeSpan.FromSeconds(1.0 / Math.Max(10, settings.MotionCaptureFps));
        _captureTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval
        };
        _captureTimer.Tick += (_, _) => OnCaptureTick();
        _captureTimer.Start();
    }

    private void OnCaptureTick()
    {
        // Skip if a previous capture task is still running
        if (Interlocked.CompareExchange(ref _captureInProgress, 1, 0) != 0)
        {
            return;
        }

        var settings = _settingsStore.Current;
        if (!settings.EnableMotionDetection)
        {
            Interlocked.Exchange(ref _captureInProgress, 0);
            return;
        }

        var regionSize = Math.Max(64, settings.MotionRegionSize);

        var captureX = _monitorBounds.X + (_monitorBounds.Width - regionSize) / 2;
        var captureY = _monitorBounds.Y + (_monitorBounds.Height - regionSize) / 2;

        var smoothingFrames = settings.MotionSmoothingFrames;
        var deadZone = settings.MotionDeadZonePixels;
        var intensity = settings.MotionCancellationIntensity;

        var platformService = _platformService;
        var engine = _motionEngine;
        var buffer = new byte[regionSize * regionSize];

        _ = Task.Run(() =>
        {
            try
            {
                if (!platformService.TryCaptureRegion(captureX, captureY, regionSize, regionSize, buffer))
                {
                    return;
                }

                engine.PushFrame(buffer, regionSize, regionSize);
                var (dx, dy) = engine.Estimate(smoothingFrames, deadZone);

                Dispatcher.UIThread.Post(() =>
                {
                    Crosshair.SetMotionCapturePreview(buffer, regionSize, regionSize);
                    _gridOffsetX -= dx * intensity;
                    _gridOffsetY -= dy * intensity;
                    Crosshair.SetGridOffset(_gridOffsetX, _gridOffsetY);
                });
            }
            finally
            {
                Interlocked.Exchange(ref _captureInProgress, 0);
            }
        });
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
        _captureTimer?.Stop();
        _captureTimer = null;
        _settingsStore.SettingsChanged -= OnSettingsChanged;
    }
}