using System.Runtime.InteropServices;

namespace InputOverlay;

internal static class Native
{
    public const int WmInput = 0x00ff, WmInputDeviceChange = 0x00fe;
    public const int WmClose = 0x0010, WmDisplayChange = 0x007e, WmDpiChanged = 0x02e0;
    public const int WmSessionChange = 0x02b1, WmPowerBroadcast = 0x0218;
    public const int WsExLayered = 0x00080000, WsExTransparent = 0x20;
    public const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x80, WsExTopmost = 8;
    public const uint RidevInputSink = 0x100, RidevDevNotify = 0x2000, RidevRemove = 1;
    public const uint RidInput = 0x10000003;
    public static readonly nint HwndMessage = -3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawHeader { public uint Type, Size; public nint Device, WParam; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawKeyboard
    {
        public ushort MakeCode, Flags, Reserved, VKey;
        public uint Message, ExtraInformation;
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct RawMouse
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public ushort ButtonFlags;
        [FieldOffset(6)] public short ButtonData;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int LastX;
        [FieldOffset(16)] public int LastY;
        [FieldOffset(20)] public uint ExtraInformation;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point(int x, int y) { public int X = x, Y = y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Size(int width, int height) { public int Width = width, Height = height; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BlendFunction
    { public byte Operation, Flags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
        public uint Color;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputData(nint raw, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint window, int message, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(nint window, nint destDc, ref Point position,
        ref Size size, nint sourceDc, ref Point source, uint colorKey, ref BlendFunction blend, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSRegisterSessionNotification(nint window, uint flags);
    [DllImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSUnRegisterSessionNotification(nint window);
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")]
    internal static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint CreateDC(string driver, string device, string? output, nint initData);
    [DllImport("gdi32.dll")]
    internal static extern int GetDeviceCaps(nint dc, int index);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWaitableTimerEx(nint attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWaitableTimerEx(nint timer, in long dueTime, int period,
        nint completion, nint argument, nint wakeContext, uint tolerableDelay);
}
