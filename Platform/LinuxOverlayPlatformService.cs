using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using CrosshairOverlay.Platform.Linux;

namespace CrosshairOverlay.Platform;

/// <summary>
/// Linux implementation of the overlay platform hooks.
///
/// Avalonia 12 has no native Wayland backend, so the app always renders through
/// XWayland when the session is Wayland. Window behaviour is therefore driven with
/// plain X11 calls, which XWayland forwards to the compositor:
///   • click-through comes from an empty XShape *input* region;
///   • the overlay is kept out of alt-tab and the taskbar by marking it
///     override-redirect, which makes compositors treat it as unmanaged.
///
/// Screen capture cannot go through X11 at all (a rootless XWayland root window does
/// not contain native Wayland surfaces), so it is delegated to the xdg-desktop-portal
/// ScreenCast session owned by <see cref="WaylandScreenCaptureService"/>.
/// </summary>
public sealed class LinuxOverlayPlatformService : IWindowsOverlayPlatformService
{
    private readonly WaylandScreenCaptureService _capture;
    private IntPtr _display;
    private bool _displayProbed;
    private bool _excludeFromCaptureWarned;

    public LinuxOverlayPlatformService(WaylandScreenCaptureService capture)
    {
        _capture = capture;
    }

    public void PrepareOverlayWindow(Window window)
    {
        WithXWindow(window, (display, handle) =>
        {
            HideFromWindowSwitchers(display, handle);
            MakeInputTransparent(display, handle);
        });
    }

    public void EnableClickThrough(Window window)
    {
        WithXWindow(window, (display, handle) =>
        {
            // Re-applied after mapping: an unmanaged window is positioned by nobody but us, and the
            // input region has to survive whatever Avalonia did while showing the window.
            MakeInputTransparent(display, handle);
            ApplyGeometry(display, window, handle);
        });
    }

    private void WithXWindow(Window window, Action<IntPtr, IntPtr> action)
    {
        var handle = TryGetXWindow(window);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var display = GetDisplay();
        if (display == IntPtr.Zero)
        {
            return;
        }

        try
        {
            action(display, handle);
            X11Interop.XSync(display, false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Linux overlay setup failed: {ex}");
        }
    }

    public void SetExcludeFromCapture(Window window, bool exclude)
    {
        // Wayland has no equivalent of SetWindowDisplayAffinity / NSWindowSharingNone:
        // a client cannot ask the compositor to omit it from a ScreenCast stream.
        if (exclude && !_excludeFromCaptureWarned)
        {
            _excludeFromCaptureWarned = true;
            Trace.WriteLine("Exclude-from-capture is not supported on Wayland; overlays stay visible in the capture stream.");
        }
    }

    public bool TryCaptureRegion(int x, int y, int width, int height, byte[] buffer)
    {
        return _capture.TryCaptureRegion(x, y, width, height, buffer);
    }

    /// <summary>
    /// Clears the XShape input region so every pointer event lands on whatever is underneath. The
    /// bounding (visual) shape is left untouched so the overlay still draws.
    ///
    /// This has to cover the child windows as well: Avalonia's X11 backend renders into a child of
    /// the toplevel, and X11 delivers pointer events to the deepest window under the cursor, so an
    /// empty region on the toplevel alone is bypassed entirely.
    /// </summary>
    private static void MakeInputTransparent(IntPtr display, IntPtr window)
    {
        if (!X11Interop.XShapeQueryExtension(display, out _, out _))
        {
            Trace.WriteLine("XShape extension unavailable; overlay will capture mouse input.");
            return;
        }

        ClearInputRegion(display, window);
    }

    private static void ClearInputRegion(IntPtr display, IntPtr window)
    {
        X11Interop.XShapeCombineRectangles(
            display,
            window,
            X11Interop.ShapeInput,
            0,
            0,
            IntPtr.Zero,
            0,
            X11Interop.ShapeSet,
            X11Interop.Unsorted);

        if (!X11Interop.XQueryTree(display, window, out _, out _, out var children, out var childCount)
            || children == IntPtr.Zero)
        {
            return;
        }

        try
        {
            for (var i = 0; i < childCount; i++)
            {
                ClearInputRegion(display, Marshal.ReadIntPtr(children, i * IntPtr.Size));
            }
        }
        finally
        {
            X11Interop.XFree(children);
        }
    }

    /// <summary>
    /// Takes the overlay out of alt-tab, the taskbar and the pager. The EWMH hints cover switchers
    /// that honour them; override-redirect additionally makes compositors such as Hyprland treat the
    /// window as unmanaged, which keeps it out of focus cycling altogether. Must run before the
    /// window is mapped, because that is when the flag is read.
    /// </summary>
    private static void HideFromWindowSwitchers(IntPtr display, IntPtr handle)
    {
        SetAtomProperty(display, handle, "_NET_WM_WINDOW_TYPE", ["_NET_WM_WINDOW_TYPE_NOTIFICATION"]);
        SetAtomProperty(display, handle, "_NET_WM_STATE",
        [
            "_NET_WM_STATE_SKIP_TASKBAR",
            "_NET_WM_STATE_SKIP_PAGER",
            "_NET_WM_STATE_ABOVE",
        ]);

        var attributes = new X11Interop.XSetWindowAttributes { OverrideRedirect = 1 };
        X11Interop.XChangeWindowAttributes(display, handle, X11Interop.CwOverrideRedirect, ref attributes);
    }

    /// <summary>An unmanaged window is positioned by nobody but us, so restore the requested bounds.</summary>
    private static void ApplyGeometry(IntPtr display, Window window, IntPtr handle)
    {
        var position = window.Position;
        var width = (uint)Math.Max(1, (int)Math.Round(window.Width));
        var height = (uint)Math.Max(1, (int)Math.Round(window.Height));
        X11Interop.XMoveResizeWindow(display, handle, position.X, position.Y, width, height);
        X11Interop.XRaiseWindow(display, handle);
    }

    private static void SetAtomProperty(IntPtr display, IntPtr window, string property, string[] values)
    {
        var propertyAtom = X11Interop.XInternAtom(display, property, false);
        if (propertyAtom == IntPtr.Zero)
        {
            return;
        }

        var atoms = new IntPtr[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            atoms[i] = X11Interop.XInternAtom(display, values[i], false);
        }

        var buffer = Marshal.AllocHGlobal(IntPtr.Size * atoms.Length);
        try
        {
            Marshal.Copy(atoms, 0, buffer, atoms.Length);
            X11Interop.XChangeProperty(display, window, propertyAtom, new IntPtr(X11Interop.XaAtom), 32,
                X11Interop.PropModeReplace, buffer, atoms.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IntPtr TryGetXWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        // Avalonia's X11 backend reports "XID"; anything else is a backend we cannot drive here.
        if (!string.Equals(handle.HandleDescriptor, "XID", StringComparison.Ordinal))
        {
            Trace.WriteLine($"Unexpected Linux window handle '{handle.HandleDescriptor}'; skipping X11 overlay setup.");
            return IntPtr.Zero;
        }

        return handle.Handle;
    }

    private IntPtr GetDisplay()
    {
        if (_displayProbed)
        {
            return _display;
        }

        _displayProbed = true;
        try
        {
            _display = X11Interop.XOpenDisplay(IntPtr.Zero);
            if (_display == IntPtr.Zero)
            {
                Trace.WriteLine("XOpenDisplay failed; overlay window tweaks are unavailable.");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"XOpenDisplay threw: {ex}");
            _display = IntPtr.Zero;
        }

        return _display;
    }
}
