using Avalonia.Controls;

namespace CrosshairOverlay.Platform;

public interface IWindowsOverlayPlatformService
{
    void EnableClickThrough(Window window);
}
