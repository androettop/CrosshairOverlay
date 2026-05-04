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

            var selWindow = sel_registerName("window");
            var selRespondsToSelector = sel_registerName("respondsToSelector:");
            var selSetIgnoresMouseEvents = sel_registerName("setIgnoresMouseEvents:");
            var selSetOpaque = sel_registerName("setOpaque:");
            var selSetBackgroundColor = sel_registerName("setBackgroundColor:");

            // Avalonia may expose NSView or NSWindow depending on platform backend details.
            // Calling an unsupported selector crashes, so we probe first.
            var nativeHandle = handle.Handle;
            var supportsWindowSelector = objc_msgSend_bool_selector(nativeHandle, selRespondsToSelector, selWindow);
            var nsWindow = supportsWindowSelector ? objc_msgSend_ptr(nativeHandle, selWindow) : nativeHandle;

            if (nsWindow == IntPtr.Zero)
            {
                return;
            }

            // Mouse events pass through the window entirely
            if (objc_msgSend_bool_selector(nsWindow, selRespondsToSelector, selSetIgnoresMouseEvents))
            {
                objc_msgSend_void_bool(nsWindow, selSetIgnoresMouseEvents, true);
            }

            // Disable window's own opaque background so compositor transparency works
            if (objc_msgSend_bool_selector(nsWindow, selRespondsToSelector, selSetOpaque))
            {
                objc_msgSend_void_bool(nsWindow, selSetOpaque, false);
            }

            // Set the NSWindow background to clear so no black fill leaks through
            if (objc_msgSend_bool_selector(nsWindow, selRespondsToSelector, selSetBackgroundColor))
            {
                var nsColorClass = objc_getClass("NSColor");
                if (nsColorClass != IntPtr.Zero)
                {
                    var selClearColor = sel_registerName("clearColor");
                    var clearColor = objc_msgSend_ptr(nsColorClass, selClearColor);
                    if (clearColor != IntPtr.Zero)
                    {
                        objc_msgSend_void_ptr(nsWindow, selSetBackgroundColor, clearColor);
                    }
                }
            }
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

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_selector(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);
}
