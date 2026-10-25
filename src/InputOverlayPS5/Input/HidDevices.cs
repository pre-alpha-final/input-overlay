using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace InputOverlayPS5.Input;

internal readonly record struct HidDevice(string Path, uint ProductId);

internal readonly record struct HidConnection(Stream Stream, int ReportLength);

internal static class HidDevices
{
    public static HidConnection Open(HidDevice device)
    {
        // Non-exclusive access: games and other controller readers keep receiving input.
        var handle = Native.OpenHid(device.Path, 0x80000000, 3, 0, 3, 0x40000000, 0);
        try
        {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!Native.HidD_GetPreparsedData(handle, out nint preparsed))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Native.HidCaps caps;
            try
            {
                if (Native.HidP_GetCaps(preparsed, out caps) != 0x00110000)
                    throw new IOException("Could not read controller HID capabilities.");
            }
            finally { Native.HidD_FreePreparsedData(preparsed); }
            if (caps.InputReportByteLength is < 10 or > 128)
                throw new IOException("Unsupported controller report length.");
            return new(new FileStream(handle, FileAccess.Read, 1, isAsync: true), caps.InputReportByteLength);
        }
        catch { handle.Dispose(); throw; }
    }

    public static HidDevice[] Enumerate()
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf<Native.RawDeviceList>();
        if (Native.GetRawInputDeviceList(null, ref count, size) == uint.MaxValue)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate input devices.");
        if (count == 0) return [];
        // A device can arrive between the count query and enumeration.
        Native.RawDeviceList[] devices = [];
        uint result = uint.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            devices = new Native.RawDeviceList[count];
            result = Native.GetRawInputDeviceList(devices, ref count, size);
            if (result != uint.MaxValue) break;
            int error = Marshal.GetLastWin32Error();
            if (error != 122) throw new Win32Exception(error, "Could not enumerate input devices.");
        }
        if (result == uint.MaxValue) return []; // Retry on the next discovery signal.
        List<HidDevice> supported = [];
        foreach (Native.RawDeviceList device in devices.Take((int)result))
        {
            if (device.Type != 2) continue;
            uint infoSize = (uint)Marshal.SizeOf<Native.HidDeviceInfo>();
            var info = new Native.HidDeviceInfo { Size = infoSize };
            if (Native.GetRawInputDeviceInfo(device.Device, Native.RidiDeviceInfo, ref info, ref infoSize) == uint.MaxValue)
                continue; // Removed during enumeration.
            if (info.VendorId != 0x054c || info.ProductId != 0x0ce6
                || info.UsagePage != 1 || info.Usage is not (4 or 5)) continue;
            uint characters = 0;
            if (Native.GetRawInputDeviceName(device.Device, Native.RidiDeviceName, null, ref characters) == uint.MaxValue
                || characters == 0) continue;
            var name = new StringBuilder(checked((int)characters));
            if (Native.GetRawInputDeviceName(device.Device, Native.RidiDeviceName, name, ref characters) != uint.MaxValue)
                supported.Add(new(name.ToString(), info.ProductId));
        }
        return supported.OrderBy(device => device.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
