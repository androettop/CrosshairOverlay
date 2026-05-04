using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrosshairOverlay.Platform;
using System;
using System.Diagnostics;
using System.IO;

namespace CrosshairOverlay;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private OverlaySettingsStore? _settingsStore;
    private MainWindow? _overlayWindow;
    private ConfigWindow? _configWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            var settingsService = new SettingsService();
            _settingsStore = new OverlaySettingsStore(settingsService);
            var platformService = new WindowsOverlayPlatformService();

            _overlayWindow = new MainWindow(_settingsStore, platformService);
            desktop.MainWindow = _overlayWindow;

            CreateTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        if (_settingsStore is null)
        {
            return;
        }

        var openSettingsItem = new NativeMenuItem("Open configuration");
        openSettingsItem.Click += (_, _) => OpenOrFocusConfigWindow();

        var closeSettingsItem = new NativeMenuItem("Close configuration");
        closeSettingsItem.Click += (_, _) => CloseConfigWindow();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => _desktop?.Shutdown();

        var menu = new NativeMenu
        {
            openSettingsItem,
            closeSettingsItem,
            new NativeMenuItemSeparator(),
            exitItem
        };

        var trayIcon = new TrayIcon
        {
            ToolTipText = "Crosshair Overlay",
            Menu = menu,
            IsVisible = true
        };

        var icon = TryCreateTrayIcon();
        if (icon is not null)
        {
            trayIcon.Icon = icon;
        }

        trayIcon.Clicked += (_, _) => OpenOrFocusConfigWindow();

        var icons = new TrayIcons
        {
            trayIcon
        };

        TrayIcon.SetIcons(this, icons);
    }

    private void OpenOrFocusConfigWindow()
    {
        if (_settingsStore is null)
        {
            return;
        }

        if (_configWindow is null)
        {
            _configWindow = new ConfigWindow(_settingsStore);
            _configWindow.Closed += (_, _) => _configWindow = null;
            _configWindow.Show();
            _configWindow.Activate();
            return;
        }

        if (!_configWindow.IsVisible)
        {
            _configWindow.Show();
        }

        _configWindow.WindowState = WindowState.Normal;
        _configWindow.Activate();
    }

    private void CloseConfigWindow()
    {
        _configWindow?.Close();
    }

    private static WindowIcon? TryCreateTrayIcon()
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