using System;
using System.Runtime.InteropServices;

namespace CrosshairOverlay.Platform;

/// <summary>
/// Minimal P/Invoke surface for the X11 calls the Linux overlay needs.
/// Under Wayland the app still runs through XWayland (Avalonia 12 has no
/// native Wayland backend), so these calls reach the compositor by way of
/// the XWayland server.
/// </summary>
internal static class X11Interop
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXext = "libXext.so.6";

    // XShape: kinds and operations.
    internal const int ShapeBounding = 0;
    internal const int ShapeClip = 1;
    internal const int ShapeInput = 2;
    internal const int ShapeSet = 0;
    internal const int Unsorted = 0;

    // XChangeWindowAttributes value mask.
    internal const ulong CwOverrideRedirect = 1UL << 9;

    // XChangeProperty modes and formats.
    internal const int PropModeReplace = 0;
    internal const int XaAtom = 4;
    internal const int XaCardinal = 6;

    [DllImport(LibX11, EntryPoint = "XOpenDisplay")]
    internal static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport(LibX11, EntryPoint = "XCloseDisplay")]
    internal static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XFlush")]
    internal static extern int XFlush(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XSync")]
    internal static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    [DllImport(LibX11, EntryPoint = "XInternAtom")]
    internal static extern IntPtr XInternAtom(IntPtr display, [MarshalAs(UnmanagedType.LPStr)] string atomName,
        [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [DllImport(LibX11, EntryPoint = "XChangeProperty")]
    internal static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr data, int elementCount);

    [DllImport(LibX11, EntryPoint = "XDeleteProperty")]
    internal static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

    [DllImport(LibX11, EntryPoint = "XQueryTree")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent,
        out IntPtr children, out uint childCount);

    [DllImport(LibX11, EntryPoint = "XFree")]
    internal static extern int XFree(IntPtr data);

    [DllImport(LibX11, EntryPoint = "XChangeWindowAttributes")]
    internal static extern int XChangeWindowAttributes(IntPtr display, IntPtr window, ulong valueMask,
        ref XSetWindowAttributes attributes);

    [DllImport(LibX11, EntryPoint = "XUnmapWindow")]
    internal static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11, EntryPoint = "XMapWindow")]
    internal static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11, EntryPoint = "XRaiseWindow")]
    internal static extern int XRaiseWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11, EntryPoint = "XMoveResizeWindow")]
    internal static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [DllImport(LibXext, EntryPoint = "XShapeCombineRectangles")]
    internal static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int destKind,
        int xOffset, int yOffset, IntPtr rectangles, int rectangleCount, int operation, int ordering);

    [DllImport(LibXext, EntryPoint = "XShapeQueryExtension")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool XShapeQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    /// <summary>
    /// Layout mirrors the head of the X11 XSetWindowAttributes struct up to
    /// override_redirect; trailing fields are unused but kept for correct sizing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct XSetWindowAttributes
    {
        public IntPtr BackgroundPixmap;
        public nuint BackgroundPixel;
        public IntPtr BorderPixmap;
        public nuint BorderPixel;
        public int BitGravity;
        public int WinGravity;
        public int BackingStore;
        public nuint BackingPlanes;
        public nuint BackingPixel;
        public int SaveUnder;
        public nint EventMask;
        public nint DoNotPropagateMask;
        public int OverrideRedirect;
        public IntPtr Colormap;
        public IntPtr Cursor;
    }
}
