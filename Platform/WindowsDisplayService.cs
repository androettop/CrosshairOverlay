using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;

namespace CrosshairOverlay.Platform;

/// <summary>A monitor as seen by X11, paired with the output name the compositor uses for it.</summary>
public readonly record struct LinuxMonitor(string Name, PixelRect Bounds);

public sealed class WindowsDisplayService
{
    public IReadOnlyList<PixelRect> GetMonitorBounds()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsMonitorBounds();

        if (OperatingSystem.IsMacOS())
            return GetMacOsMonitorBounds();

        if (OperatingSystem.IsLinux())
            return GetLinuxMonitorBounds();

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

    // ── Linux ────────────────────────────────────────────────────────────────

    private IReadOnlyList<PixelRect> GetLinuxMonitorBounds()
        => [.. GetLinuxMonitors().Select(monitor => monitor.Bounds)];

    /// <summary>
    /// Enumerates monitors through XRandR, which also reports each output's name. Under Wayland the
    /// app runs on XWayland, which mirrors the compositor's outputs one for one, so those names match
    /// the <c>mapping_id</c> the ScreenCast portal attaches to its streams — the only reliable way to
    /// tell which monitor a stream came from.
    /// </summary>
    public IReadOnlyList<LinuxMonitor> GetLinuxMonitors()
    {
        if (!OperatingSystem.IsLinux())
            return [];

        var display = IntPtr.Zero;
        var monitorList = IntPtr.Zero;

        try
        {
            display = X11Interop.XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
                return [new LinuxMonitor(string.Empty, new PixelRect(0, 0, 1920, 1080))];

            var root = XDefaultRootWindow(display);
            monitorList = XRRGetMonitors(display, root, true, out var count);
            if (monitorList == IntPtr.Zero || count <= 0)
                return [new LinuxMonitor(string.Empty, new PixelRect(0, 0, 1920, 1080))];

            var monitors = new List<LinuxMonitor>(count);
            var itemSize = Marshal.SizeOf<XRRMonitorInfo>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<XRRMonitorInfo>(monitorList + (i * itemSize));
                var namePointer = XGetAtomName(display, info.Name);
                var name = namePointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(namePointer) ?? string.Empty;
                if (namePointer != IntPtr.Zero)
                    XFree(namePointer);

                monitors.Add(new LinuxMonitor(name, new PixelRect(info.X, info.Y, info.Width, info.Height)));
            }

            return monitors;
        }
        catch (Exception)
        {
            return [new LinuxMonitor(string.Empty, new PixelRect(0, 0, 1920, 1080))];
        }
        finally
        {
            if (monitorList != IntPtr.Zero)
                XRRFreeMonitors(monitorList);
            if (display != IntPtr.Zero)
                X11Interop.XCloseDisplay(display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XGetAtomName(IntPtr display, IntPtr atom);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    [DllImport("libXrandr.so.2")]
    private static extern IntPtr XRRGetMonitors(IntPtr display, IntPtr window,
        [MarshalAs(UnmanagedType.Bool)] bool getActive, out int monitorCount);

    [DllImport("libXrandr.so.2")]
    private static extern void XRRFreeMonitors(IntPtr monitors);

    [StructLayout(LayoutKind.Sequential)]
    private struct XRRMonitorInfo
    {
        public IntPtr Name;
        public int Primary;
        public int Automatic;
        public int OutputCount;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int PhysicalWidth;
        public int PhysicalHeight;
        public IntPtr Outputs;
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
