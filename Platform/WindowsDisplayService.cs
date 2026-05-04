using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;

namespace CrosshairOverlay.Platform;

public sealed class WindowsDisplayService
{
    public IReadOnlyList<PixelRect> GetMonitorBounds()
    {
        var monitors = new List<PixelRect>();

        if (!OperatingSystem.IsWindows())
        {
            monitors.Add(new PixelRect(0, 0, 1920, 1080));
            return monitors;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RectStruct rect, IntPtr data) =>
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            monitors.Add(new PixelRect(rect.Left, rect.Top, width, height));

            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(new PixelRect(0, 0, 1920, 1080));
        }

        return monitors;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
