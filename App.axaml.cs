using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrosshairOverlay.Platform;

namespace CrosshairOverlay;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            var platformService = new WindowsOverlayPlatformService();

            desktop.MainWindow = new MainWindow(settings, platformService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}