using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace InputOverlayPS5;

internal static partial class Native
{
    internal const uint RidiDeviceInfo = 0x2000000b, RidiDeviceName = 0x20000007;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawDeviceList { public nint Device; public uint Type; }

    // RID_DEVICE_INFO's 24-byte union is padded to the largest (mouse) member.
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct HidDeviceInfo
    {
        public uint Size, Type, VendorId, ProductId, Version;
        public ushort UsagePage, Usage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort LinkCollectionNodes, InputButtonCaps, InputValueCaps, InputDataIndices;
        public ushort OutputButtonCaps, OutputValueCaps, OutputDataIndices;
        public ushort FeatureButtonCaps, FeatureValueCaps, FeatureDataIndices;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputDeviceList([Out] RawDeviceList[]? devices, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(nint device, uint command, ref HidDeviceInfo info, ref uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceName(nint device, uint command, StringBuilder? name, ref uint size);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle OpenHid(string path, uint access, uint share, nint security,
        uint creation, uint flags, nint template);
    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out nint data);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(nint data, out HidCaps caps);
}
