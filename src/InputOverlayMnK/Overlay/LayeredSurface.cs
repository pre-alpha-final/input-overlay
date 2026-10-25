using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace InputOverlayMnK.Overlay;

internal sealed class LayeredSurface : IDisposable
{
    private nint _dc, _dib, _previous;
    public Bitmap Bitmap { get; private set; } = null!;
    public Graphics Graphics { get; private set; } = null!;
    public int Width { get; }
    public int Height { get; }

    public LayeredSurface(int width, int height)
    {
        Width = width;
        Height = height;
        try
        {
            _dc = Native.CreateCompatibleDC(0);
            if (_dc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new Native.BitmapInfo
            {
                Size = 40, Width = width, Height = -height,
                Planes = 1, BitCount = 32, SizeImage = checked((uint)(width * height * 4))
            };
            _dib = Native.CreateDIBSection(_dc, ref info, 0, out nint pixels, 0, 0);
            if (_dib == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            _previous = Native.SelectObject(_dc, _dib);
            if (_previous == 0 || _previous == -1) throw new Win32Exception("Could not select overlay bitmap.");
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, pixels);
            Graphics = Graphics.FromImage(Bitmap);
        }
        catch { Dispose(); throw; }
    }

    public void Submit(nint window, System.Drawing.Point location)
    {
        // GDI+ must finish writing to the DIB before Windows reads it.
        Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
        var position = new Native.Point(location.X, location.Y);
        var size = new Native.Size(Width, Height);
        var source = new Native.Point(0, 0);
        var blend = new Native.BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
        if (!Native.UpdateLayeredWindow(window, 0, ref position, ref size, _dc, ref source, 0, ref blend, 2))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateLayeredWindow failed.");
    }

    public void Dispose()
    {
        Graphics?.Dispose();
        Bitmap?.Dispose();
        if (_dc != 0 && _previous != 0 && _previous != -1) Native.SelectObject(_dc, _previous);
        if (_dib != 0) Native.DeleteObject(_dib);
        if (_dc != 0) Native.DeleteDC(_dc);
        _previous = _dib = _dc = 0;
    }
}
