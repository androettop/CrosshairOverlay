using Avalonia;
using System;
using System.Diagnostics;
using System.IO;

namespace CrosshairOverlay;

class Program
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrosshairOverlay");

    private static readonly string LogPath = Path.Combine(LogDirectory, "app.log");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogging();

        try
        {
            Log("Starting app");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Log("App exited normally");
        }
        catch (Exception ex)
        {
            Log($"Fatal exception: {ex}");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                DisableDefaultApplicationMenuItems = true,
                DisableSetProcessName = true
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static void ConfigureLogging()
    {
        Directory.CreateDirectory(LogDirectory);
        var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        Trace.Listeners.Clear();
        Trace.Listeners.Add(new TextWriterTraceListener(writer));
        Trace.AutoFlush = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log($"Unhandled exception: {e.ExceptionObject}");
        };

        Log($"Logging initialized at {DateTime.Now:O}");
    }

    private static void Log(string message)
    {
        Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
