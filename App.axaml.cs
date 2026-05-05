using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrosshairOverlay.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CrosshairOverlay;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private OverlaySettingsStore? _settingsStore;
    private readonly IWindowsOverlayPlatformService _platformService =
        OperatingSystem.IsMacOS()
            ? new MacOsOverlayPlatformService()
            : new WindowsOverlayPlatformService();
    private readonly WindowsDisplayService _displayService = new();
    private readonly List<MainWindow> _overlayWindows = [];
    private ConfigWindow? _configWindow;
    private string _lastMonitorSelectionKey = string.Empty;
    private NativeMenuItem? _openSettingsItem;
    private NativeMenuItem? _exitItem;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsService = new SettingsService();
            _settingsStore = new OverlaySettingsStore(settingsService);
            _settingsStore.SettingsChanged += OnSettingsChanged;

            CreateTrayIcon();
            RebuildOverlayWindows(_settingsStore.Current);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        if (_settingsStore is null)
        {
            return;
        }

        _openSettingsItem = new NativeMenuItem("Ajustes");
        _openSettingsItem.Click += (_, _) => OpenOrFocusConfigWindow();

        _exitItem = new NativeMenuItem("Exit");
        _exitItem.Click += (_, _) => _desktop?.Shutdown();

        var menu = new NativeMenu
        {
            _openSettingsItem,
            new NativeMenuItemSeparator(),
            _exitItem
        };

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Crosshair Overlay",
            Menu = menu,
            IsVisible = true
        };

        var icon = TryCreateTrayIcon();
        if (icon is not null)
        {
            _trayIcon.Icon = icon;
        }

        _trayIcon.Clicked += (_, _) => OpenOrFocusConfigWindow();

        var icons = new TrayIcons
        {
            _trayIcon
        };

        TrayIcon.SetIcons(this, icons);
        UpdateTrayLocalization(_settingsStore.Current.Language);
    }

    private void AppMenuSettings_OnClick(object? sender, EventArgs e)
    {
        OpenOrFocusConfigWindow();
    }

    private void AppMenuExit_OnClick(object? sender, EventArgs e)
    {
        _desktop?.Shutdown();
    }

    private void OpenOrFocusConfigWindow()
    {
        if (_settingsStore is null)
        {
            return;
        }

        if (_configWindow is not null)
        {
            FocusConfigWindow(_configWindow);
            return;
        }

        _configWindow = new ConfigWindow(_settingsStore, _displayService.GetMonitorBounds());
        _configWindow.Opened += OnConfigWindowOpened;
        _configWindow.Closed += (_, _) => _configWindow = null;
        FocusConfigWindow(_configWindow);
    }

    private void OnConfigWindowOpened(object? sender, EventArgs e)
    {
        if (sender is ConfigWindow window && _settingsStore is not null)
        {
            _platformService.SetExcludeFromCapture(window, !_settingsStore.Current.DebugAllowConfigWindowCapture);
        }
    }

    private static void FocusConfigWindow(ConfigWindow window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void OnSettingsChanged(object? sender, OverlaySettings settings)
    {
        UpdateTrayLocalization(settings.Language);

        if (_configWindow is not null)
        {
            _platformService.SetExcludeFromCapture(_configWindow, !settings.DebugAllowConfigWindowCapture);
        }

        var selectionKey = BuildMonitorSelectionKey(settings.EnabledMonitorIndices);
        if (!string.Equals(selectionKey, _lastMonitorSelectionKey, StringComparison.Ordinal))
        {
            RebuildOverlayWindows(settings);
        }
    }

    private void RebuildOverlayWindows(OverlaySettings settings)
    {
        if (_desktop is null || _settingsStore is null)
        {
            return;
        }

        foreach (var window in _overlayWindows)
        {
            window.Close();
        }

        _overlayWindows.Clear();

        var monitorBounds = _displayService.GetMonitorBounds();
        var selectedIndices = (settings.EnabledMonitorIndices ?? [])
            .Distinct()
            .Where(index => index >= 0 && index < monitorBounds.Count)
            .ToList();

        foreach (var index in selectedIndices)
        {
            var window = new MainWindow(_settingsStore, _platformService, monitorBounds[index]);
            _overlayWindows.Add(window);
            window.Show();
        }

        _desktop.MainWindow = _overlayWindows.Count > 0 ? _overlayWindows[0] : null;
        _lastMonitorSelectionKey = BuildMonitorSelectionKey(settings.EnabledMonitorIndices);
    }

    private void UpdateTrayLocalization(string language)
    {
        var isSpanish = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase);
        if (_openSettingsItem is not null)
        {
            _openSettingsItem.Header = isSpanish ? "Ajustes" : "Settings";
        }

        if (_exitItem is not null)
        {
            _exitItem.Header = isSpanish ? "Salir" : "Exit";
        }

        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = isSpanish ? "Overlay de mira" : "Crosshair Overlay";
        }
    }

    private static string BuildMonitorSelectionKey(IReadOnlyList<int>? selected)
    {
        if (selected is null || selected.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(",", selected.OrderBy(x => x));
    }

    internal static WindowIcon? TryCreateTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "tray.ico");
            if (!File.Exists(iconPath))
            {
                Trace.WriteLine($"Tray icon file not found: {iconPath}");
                return null;
            }

            using var stream = File.OpenRead(iconPath);
            return new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Tray icon load failed: {ex}");
            return null;
        }
    }
}