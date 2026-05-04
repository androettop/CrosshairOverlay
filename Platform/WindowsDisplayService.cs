using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;

namespace CrosshairOverlay.Platform;

public sealed class WindowsDisplayService
{
    public IReadOnlyList<PixelRect> GetMonitorBounds()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsMonitorBounds();

        if (OperatingSystem.IsMacOS())
            return GetMacOsMonitorBounds();

        return [new PixelRect(0, 0, 1920, 1080)];
    }

    // ── Windows ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<PixelRect> GetWindowsMonitorBounds()
    {
        var monitors = new List<PixelRect>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RectStruct rect, IntPtr data) =>
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            monitors.Add(new PixelRect(rect.Left, rect.Top, width, height));
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
            monitors.Add(new PixelRect(0, 0, 1920, 1080));

        return monitors;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left, Top, Right, Bottom;
    }

    // ── macOS ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<PixelRect> GetMacOsMonitorBounds()
    {
        try
        {
            var displayIds = new uint[32];
            if (CGGetActiveDisplayList(32, displayIds, out uint count) != 0 || count == 0)
            {
                // Fallback to main display dimensions only
                var mainId = CGMainDisplayID();
                return [new PixelRect(0, 0, (int)CGDisplayPixelsWide(mainId), (int)CGDisplayPixelsHigh(mainId))];
            }

            var result = new List<PixelRect>((int)count);
            for (uint i = 0; i < count; i++)
            {
                // CGDisplayBounds uses top-left origin in logical (point) coordinates
                var b = CGDisplayBounds(displayIds[i]);
                result.Add(new PixelRect((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height));
            }
            return result;
        }
        catch
        {
            return [new PixelRect(0, 0, 1920, 1080)];
        }
    }

    // CGRect: 4 × double = HFA on ARM64 (returned in d0–d3); handled by .NET
    // on both ARM64 and x86_64 macOS via P/Invoke struct-return conventions.
    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X, Y, Width, Height;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGDisplayPixelsWide(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGDisplayPixelsHigh(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] activeDisplays, out uint displayCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGRect CGDisplayBounds(uint display);
}
