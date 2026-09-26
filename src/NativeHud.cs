using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VoK.ReactionTimer;
internal static class NativeHud
{
    internal const int Layered = 0x80000, Transparent = 0x20, NoActivate = 0x08000000, ToolWindow = 0x80;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int X, Y; public Size(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { public byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapHeader
    {
        public uint Size; public int Width, Height; public ushort Planes, Bits;
        public uint Compression, ImageSize; public int XPixels, YPixels; public uint Used, Important;
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] internal static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination,
        ref Size size, IntPtr sourceDc, ref Point source, uint key, ref Blend blend, uint flags);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr dc,
        ref BitmapHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);
}

// One reusable premultiplied-alpha DIB. No per-frame native bitmap/DC allocation.
internal sealed class HudSurface : IDisposable
{
    private IntPtr _dc, _bitmap, _original;
    private Bitmap? _image;
    internal Graphics? Graphics { get; private set; }
    internal System.Drawing.Size Size { get; private set; }
    public HudSurface()
    {
        _dc = NativeHud.CreateCompatibleDC(IntPtr.Zero);
        if (_dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal void Resize(int width, int height)
    {
        if (Size.Width == width && Size.Height == height) return;
        ClearBitmap();
        var header = new NativeHud.BitmapHeader { Size = 40, Width = width, Height = -height, Planes = 1, Bits = 32 };
        _bitmap = NativeHud.CreateDIBSection(_dc, ref header, 0, out var bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        _original = NativeHud.SelectObject(_dc, _bitmap);
        _image = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
        Graphics = System.Drawing.Graphics.FromImage(_image);
        Size = new System.Drawing.Size(width, height);
    }
    internal void Present(IntPtr window, System.Drawing.Point location)
    {
        var destination = new NativeHud.Point(location.X, location.Y);
        var source = new NativeHud.Point(0, 0);
        var size = new NativeHud.Size(Size.Width, Size.Height);
        var blend = new NativeHud.Blend { Alpha = 255, Format = 1 };
        if (!NativeHud.UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref size, _dc,
            ref source, 0, ref blend, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private void ClearBitmap()
    {
        Graphics?.Dispose(); Graphics = null; _image?.Dispose(); _image = null;
        if (_bitmap != IntPtr.Zero)
        {
            NativeHud.SelectObject(_dc, _original); NativeHud.DeleteObject(_bitmap);
            _bitmap = IntPtr.Zero; _original = IntPtr.Zero;
        }
        Size = System.Drawing.Size.Empty;
    }
    public void Dispose()
    {
        ClearBitmap(); if (_dc != IntPtr.Zero) { NativeHud.DeleteDC(_dc); _dc = IntPtr.Zero; }
    }
}
