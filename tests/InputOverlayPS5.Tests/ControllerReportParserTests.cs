using System.Buffers.Binary;
using InputOverlayPS5.Input;

namespace InputOverlayPS5.Tests;

public sealed class ControllerReportParserTests
{
    internal static byte[] Usb(byte hatAndFace = 8, byte shoulders = 0, byte system = 0)
    {
        byte[] data = new byte[64];
        data[0] = 1;
        data[1] = data[2] = data[3] = data[4] = 128;
        data[8] = hatAndFace;
        data[9] = shoulders;
        data[10] = system;
        return data;
    }

    // CRC independently calculated with Python zlib.crc32 over the HID transaction
    // byte 0xa1 followed by the first 74 bytes. The second byte is a sequence tag.
    internal static byte[] Bluetooth()
    {
        byte[] data = new byte[78];
        data[0] = 0x31; data[1] = 0x10;
        new byte[] { 0, 255, 64, 192, 57, 200, 42, 0xa1, 0x95, 0x07, 0 }.CopyTo(data, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(74), 0x73bf8077);
        return data;
    }

    private static byte[] Basic(int length)
    {
        byte[] data = new byte[length];
        data[0] = 1;
        new byte[] { 0, 255, 64, 192, 0xa1, 0x95, 0xff, 57, 200 }.CopyTo(data, 1);
        return data;
    }

    [Fact]
    public void UsbAndExtendedBluetoothDecodeIdenticalControlsIncludingMic()
    {
        byte[] bt = Bluetooth();
        byte[] usb = Usb();
        bt.AsSpan(2, 11).CopyTo(usb.AsSpan(1));
        Assert.True(ControllerReportParser.TryParse(usb, out ControllerReport wired));
        Assert.True(ControllerReportParser.TryParse(bt, out ControllerReport wireless));
        Assert.Equal(wired, wireless);
        Assert.Equal(new ControllerReport(ControllerButtons.Up | ControllerButtons.Right | ControllerButtons.Cross
            | ControllerButtons.Triangle | ControllerButtons.L1 | ControllerButtons.L2 | ControllerButtons.Create
            | ControllerButtons.R3 | ControllerButtons.PS | ControllerButtons.Touchpad | ControllerButtons.Mic,
            0, 255, 64, 192, 57, 200), wired);
    }

    [Theory]
    [InlineData(10)] [InlineData(78)]
    public void BasicBluetoothUsesItsOwnTriggerLayoutAndNeverTreatsCounterAsMic(int length)
    {
        Assert.True(ControllerReportParser.TryParse(Basic(length), out ControllerReport basic));
        Assert.False(basic.IsDown(ControllerButtons.Mic));
        byte[] usb = Usb(0xa1, 0x95, 0x03);
        usb[1] = 0; usb[2] = 255; usb[3] = 64; usb[4] = 192;
        usb[5] = 57; usb[6] = 200; usb[7] = 0xff;
        Assert.True(ControllerReportParser.TryParse(usb, out ControllerReport full));
        Assert.Equal(full, basic);
    }

    [Fact]
    public void WindowsPaddingDoesNotChangeFullReportLayouts()
    {
        byte[] usb = Usb(0x28, 0x40, 4);
        byte[] paddedUsb = new byte[128];
        usb.CopyTo(paddedUsb, 0);
        Assert.True(ControllerReportParser.TryParse(usb, out ControllerReport original));
        Assert.True(ControllerReportParser.TryParse(paddedUsb, out ControllerReport padded));
        Assert.Equal(original, padded);
        byte[] paddedBt = new byte[128];
        Bluetooth().CopyTo(paddedBt, 0);
        Assert.True(ControllerReportParser.TryParse(paddedBt, out ControllerReport wireless));
        Assert.True(wireless.IsDown(ControllerButtons.Mic));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 3)] [InlineData(2, 2)] [InlineData(3, 6)]
    [InlineData(4, 4)] [InlineData(5, 12)] [InlineData(6, 8)] [InlineData(7, 9)] [InlineData(8, 0)]
    public void HatIncludesBothDirectionsForDiagonals(byte hat, uint expected)
    {
        Assert.True(ControllerReportParser.TryParse(Usb(hat), out ControllerReport report));
        Assert.Equal((ControllerButtons)expected, report.Buttons);
    }

    [Theory]
    [InlineData(8, 0x10, 1u << 4)] [InlineData(8, 0x20, 1u << 5)]
    [InlineData(8, 0x40, 1u << 6)] [InlineData(8, 0x80, 1u << 7)]
    [InlineData(9, 0x01, 1u << 8)] [InlineData(9, 0x02, 1u << 9)]
    [InlineData(9, 0x04, 1u << 10)] [InlineData(9, 0x08, 1u << 11)]
    [InlineData(9, 0x10, 1u << 12)] [InlineData(9, 0x20, 1u << 13)]
    [InlineData(9, 0x40, 1u << 14)] [InlineData(9, 0x80, 1u << 15)]
    [InlineData(10, 0x01, 1u << 16)] [InlineData(10, 0x02, 1u << 17)]
    [InlineData(10, 0x04, 1u << 18)]
    public void EveryDigitalButtonHasItsOwnBit(int index, byte mask, uint expected)
    {
        byte[] data = Usb();
        data[index] |= mask;
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport report));
        Assert.Equal((ControllerButtons)expected, report.Buttons);
    }

    [Fact]
    public void CountersSensorsBatteryTouchCoordinatesAndEdgeBitsAreIgnored()
    {
        byte[] data = Usb(0x28, 0x40, 1);
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport before));
        data[7] = 0xff;
        data[10] |= 0xf8;
        Array.Fill(data, (byte)0xee, 11, data.Length - 11);
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport after));
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(9)] [InlineData(11)] [InlineData(63)]
    [InlineData(65)] [InlineData(77)] [InlineData(79)] [InlineData(129)]
    public void RejectsTruncatedOrUnsupportedLengths(int length)
    {
        byte[] data = new byte[length];
        if (length != 0) data[0] = 1;
        Assert.False(ControllerReportParser.TryParse(data, out _));
    }

    [Fact]
    public void RejectsUnknownIdsInvalidHatsTruncatedBluetoothAndBadCrc()
    {
        byte[] usb = Usb();
        usb[0] = 2;
        Assert.False(ControllerReportParser.TryParse(usb, out _));
        usb[0] = 1; usb[8] = 15;
        Assert.False(ControllerReportParser.TryParse(usb, out _));
        byte[] bt = Bluetooth();
        Assert.False(ControllerReportParser.TryParse(bt.AsSpan(0, 77), out _));
        bt[0] = 0x11; // DualShock 4 report IDs are not DualSense inputs.
        Assert.False(ControllerReportParser.TryParse(bt, out _));
        bt = Bluetooth(); bt[4] ^= 1;
        Assert.False(ControllerReportParser.TryParse(bt, out _));
    }

    [Fact]
    public void BluetoothCrcMatchesIndependentKnownVector() =>
        Assert.Equal(0x88ed2411u, ControllerReportParser.BluetoothCrc("123456789"u8));

    [Theory]
    [InlineData(0, -1)] [InlineData(128, 0)] [InlineData(255, 1)]
    public void RawAxesKeepCenterAndFullTravel(byte raw, float expected) =>
        Assert.Equal(expected, ControllerReport.Axis(raw));
}
