using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace CrosshairOverlay.Platform;

public sealed class WindowsOverlayPlatformService : IWindowsOverlayPlatformService
{
    private const int GwlExStyle = -20;

    private const long WsExLayered = 0x00080000;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;

    private const int LwaAlpha = 0x2;

    private const uint WdaNone = 0;
    private const uint WdaExcludeFromCapture = 0x00000011; // Windows 10 Build 2004+

    private const uint SrcCopy = 0x00CC0020;
    private const uint BiRgb = 0;

    public void EnableClickThrough(Window window)
    {
        if (!OperatingSystem.IsWindows())
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

            var exStyle = GetWindowLongPtr(handle.Handle, GwlExStyle).ToInt64();
            exStyle |= WsExLayered;
            exStyle |= WsExTransparent;
            exStyle |= WsExToolWindow;

            SetWindowLongPtr(handle.Handle, GwlExStyle, new IntPtr(exStyle));
            SetLayeredWindowAttributes(handle.Handle, 0, 255, LwaAlpha);
        }
        catch
        {
            // Best effort only: overlay should keep running even if native styles fail.
        }
    }

    public void SetExcludeFromCapture(Window window, bool exclude)
    {
        if (!OperatingSystem.IsWindows())
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

            SetWindowDisplayAffinity(handle.Handle, exclude ? WdaExcludeFromCapture : WdaNone);
        }
        catch
        {
            // Windows 10 Build 2004+ only; silently ignored on older versions.
        }
    }

    public bool TryCaptureRegion(int x, int y, int width, int height, byte[] buffer)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (buffer.Length < width * height || width <= 0 || height <= 0)
        {
            return false;
        }

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var oldBitmap = SelectObject(memDc, bitmap);

        try
        {
            if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SrcCopy))
            {
                return false;
            }

            var bmi = new BitmapInfo();
            bmi.Header.BiSize = (uint)Marshal.SizeOf<BitmapInfoHeader>();
            bmi.Header.BiWidth = width;
            bmi.Header.BiHeight = -height; // negative = top-down scan order
            bmi.Header.BiPlanes = 1;
            bmi.Header.BiBitCount = 32;
            bmi.Header.BiCompression = BiRgb;

            var pixelBuffer = new byte[width * height * 4];
            var lines = GetDIBits(memDc, bitmap, 0, (uint)height, pixelBuffer, ref bmi, 0);
            if (lines == 0)
            {
                return false;
            }

            // Convert BGRX → 8-bit grayscale (Rec.601 luma approximation)
            for (int i = 0, j = 0; i < width * height; i++, j += 4)
            {
                var b = pixelBuffer[j];
                var g = pixelBuffer[j + 1];
                var r = pixelBuffer[j + 2];
                buffer[i] = (byte)((r * 77 + g * 150 + b * 29) >> 8);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines,
        byte[] lpvBits, ref BitmapInfo lpbmi, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint BiSize;
        public int BiWidth;
        public int BiHeight;
        public ushort BiPlanes;
        public ushort BiBitCount;
        public uint BiCompression;
        public uint BiSizeImage;
        public int BiXPelsPerMeter;
        public int BiYPelsPerMeter;
        public uint BiClrUsed;
        public uint BiClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint BmiColors; // unused for BI_RGB 32-bpp but required for struct layout
    }
}
