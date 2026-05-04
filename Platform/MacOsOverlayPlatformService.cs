using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CrosshairOverlay.Platform;

/// <summary>
/// Enables click-through and compositor transparency on macOS
/// by calling Objective-C runtime methods on the underlying NSWindow.
/// </summary>
public sealed class MacOsOverlayPlatformService : IWindowsOverlayPlatformService
{
    public void EnableClickThrough(Window window)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero)
            {
                return;
            }

            // On macOS, Avalonia returns the NSView; get its parent NSWindow.
            var nsView = handle.Handle;
            var nsWindow = objc_msgSend_ptr(nsView, sel_registerName("window"));
            if (nsWindow == IntPtr.Zero)
            {
                // Handle is already an NSWindow (fallback)
                nsWindow = nsView;
            }

            // Mouse events pass through the window entirely
            objc_msgSend_void_bool(nsWindow, sel_registerName("setIgnoresMouseEvents:"), true);

            // Disable window's own opaque background so compositor transparency works
            objc_msgSend_void_bool(nsWindow, sel_registerName("setOpaque:"), false);

            // Set the NSWindow background to clear so no black fill leaks through
            var nsColorClass = objc_getClass("NSColor");
            var clearColor = objc_msgSend_ptr(nsColorClass, sel_registerName("clearColor"));
            objc_msgSend_void_ptr(nsWindow, sel_registerName("setBackgroundColor:"), clearColor);
        }
        catch
        {
            // Best effort only: overlay keeps running even if native calls fail.
        }
    }

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.I1)] bool arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);
}
