using System.Buffers.Binary;

namespace InputOverlayPS4.Input;

// Native DS4 layouts: https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps4.c
// Only gamepad controls are decoded: packet counters, battery, touch coordinates and sensors
// must never cause state changes or appear in the input history.
internal static class ControllerReportParser
{
    private static readonly ControllerButtons[] Hats =
    [
        ControllerButtons.Up, ControllerButtons.Up | ControllerButtons.Right,
        ControllerButtons.Right, ControllerButtons.Right | ControllerButtons.Down,
        ControllerButtons.Down, ControllerButtons.Down | ControllerButtons.Left,
        ControllerButtons.Left, ControllerButtons.Left | ControllerButtons.Up,
        ControllerButtons.None
    ];

    public static bool TryParse(ReadOnlySpan<byte> data, out ControllerReport report)
    {
        report = ControllerReport.Neutral;
        if (data.IsEmpty || data.Length > 128) return false;
        int offset;
        if (data[0] == 0x01)
        {
            // Basic Bluetooth reports contain 9 control bytes. Windows may pad them
            // to the collection's report length; USB reports contain at least 64 bytes.
            if (data.Length != 10 && data.Length < 64) return false;
            offset = 1;
        }
        else if (data[0] is >= 0x11 and <= 0x19)
        {
            if (data.Length < 78 || (data[1] & 0x80) == 0) return false;
            if (BluetoothCrc(data[..74]) != BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(74, 4)))
                return false;
            offset = 3;
        }
        else return false;

        ReadOnlySpan<byte> controls = data.Slice(offset, 9);
        int hat = controls[4] & 0x0f;
        if (hat >= Hats.Length) return false;
        ControllerButtons buttons = Hats[hat]
            | (ControllerButtons)(controls[4] & 0xf0)
            | (ControllerButtons)(controls[5] << 8)
            | (ControllerButtons)((controls[6] & 3) << 16);
        report = new(buttons, controls[0], controls[1], controls[2], controls[3], controls[7], controls[8]);
        return true;
    }

    internal static uint BluetoothCrc(ReadOnlySpan<byte> data)
    {
        uint crc = Step(uint.MaxValue, 0xa1); // HID input transaction header participates in CRC.
        foreach (byte value in data) crc = Step(crc, value);
        return ~crc;
    }

    private static uint Step(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        return crc;
    }
}
