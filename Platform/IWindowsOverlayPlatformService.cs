using Avalonia.Controls;

namespace CrosshairOverlay.Platform;

public interface IWindowsOverlayPlatformService
{
    void EnableClickThrough(Window window);

    /// <summary>
    /// Requests that the OS exclude this window from screen capture output.
    /// On Windows 10 Build 2004+: uses SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE).
    /// On macOS: sets NSWindowSharingNone.
    /// Best-effort; silently ignored on older OS versions.
    /// </summary>
    void SetExcludeFromCapture(Window window, bool exclude);

    /// <summary>
    /// Captures the specified screen region and writes it as 8-bit grayscale into <paramref name="buffer"/>.
    /// The buffer must be pre-allocated to at least <c>width * height</c> bytes.
    /// Returns <c>false</c> if the capture is unsupported or fails.
    /// </summary>
    bool TryCaptureRegion(int x, int y, int width, int height, byte[] buffer);
}
