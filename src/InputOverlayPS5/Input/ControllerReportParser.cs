using System.Buffers.Binary;

namespace InputOverlayPS5.Input;

// DualSense layouts: https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c
// Basic Bluetooth reports use the older nine-byte controls layout. Full USB and
// extended Bluetooth reports put trigger pressure before the counter and buttons.
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
        bool basic;
        if (data[0] == 0x01)
        {
            // Windows pads basic Bluetooth's ten-byte report to 78 bytes.
            basic = data.Length is 10 or 78;
            if (!basic && data.Length is not (64 or 128)) return false;
            offset = 1;
        }
        else if (data[0] == 0x31)
        {
            if (data.Length is not (78 or 128)) return false;
            if (BluetoothCrc(data[..74]) != BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(74, 4)))
                return false;
            basic = false;
            offset = 2;
        }
        else return false;

        ReadOnlySpan<byte> controls = data.Slice(offset, basic ? 9 : 11);
        int buttonOffset = basic ? 4 : 7;
        int hat = controls[buttonOffset] & 0x0f;
        if (hat >= Hats.Length) return false;
        // In basic mode the upper bits are a packet counter, not Mic or Edge buttons.
        int systemMask = basic ? 3 : 7;
        ControllerButtons buttons = Hats[hat]
            | (ControllerButtons)(controls[buttonOffset] & 0xf0)
            | (ControllerButtons)(controls[buttonOffset + 1] << 8)
            | (ControllerButtons)((controls[buttonOffset + 2] & systemMask) << 16);
        int triggerOffset = basic ? 7 : 4;
        report = new(buttons, controls[0], controls[1], controls[2], controls[3],
            controls[triggerOffset], controls[triggerOffset + 1]);
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
