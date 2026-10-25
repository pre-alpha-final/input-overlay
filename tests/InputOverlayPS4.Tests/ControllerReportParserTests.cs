using System.Buffers.Binary;
using InputOverlayPS4.Input;

namespace InputOverlayPS4.Tests;

public sealed class ControllerReportParserTests
{
    internal static byte[] Usb(byte hatAndFace = 8, byte shoulders = 0, byte system = 0)
    {
        byte[] data = new byte[64];
        data[0] = 1;
        data[1] = data[2] = data[3] = data[4] = 128;
        data[5] = hatAndFace;
        data[6] = shoulders;
        data[7] = system;
        return data;
    }

    // Golden extended report: CRC independently calculated with Python zlib.crc32,
    // over the HID transaction byte 0xa1 followed by the first 74 bytes.
    internal static byte[] Bluetooth()
    {
        byte[] data = new byte[78];
        data[0] = 0x11; data[1] = 0x80;
        new byte[] { 0, 255, 64, 192, 0xa1, 0x95, 0x03, 57, 200 }.CopyTo(data, 3);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(74), 0x5f779efc);
        return data;
    }

    [Fact]
    public void UsbAndExtendedBluetoothDecodeIdenticalControls()
    {
        byte[] bt = Bluetooth();
        byte[] usb = Usb();
        bt.AsSpan(3, 9).CopyTo(usb.AsSpan(1));
        Assert.True(ControllerReportParser.TryParse(usb, out ControllerReport wired));
        Assert.True(ControllerReportParser.TryParse(bt, out ControllerReport wireless));
        Assert.Equal(wired, wireless);
        Assert.Equal(new ControllerReport(ControllerButtons.Up | ControllerButtons.Right | ControllerButtons.Cross
            | ControllerButtons.Triangle | ControllerButtons.L1 | ControllerButtons.L2 | ControllerButtons.Share
            | ControllerButtons.R3 | ControllerButtons.PS | ControllerButtons.Touchpad, 0, 255, 64, 192, 57, 200), wired);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(64)]
    [InlineData(78)]
    [InlineData(128)]
    public void BasicReportsSupportNativeAndWindowsPaddedLengths(int length)
    {
        byte[] data = new byte[length];
        Usb(0x48, 0x0a, 0x03).AsSpan(0, 10).CopyTo(data);
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport report));
        Assert.Equal(ControllerButtons.Circle | ControllerButtons.R1 | ControllerButtons.R2
            | ControllerButtons.PS | ControllerButtons.Touchpad, report.Buttons);
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
    [InlineData(5, 0x10, 1u << 4)] [InlineData(5, 0x20, 1u << 5)]
    [InlineData(5, 0x40, 1u << 6)] [InlineData(5, 0x80, 1u << 7)]
    [InlineData(6, 0x01, 1u << 8)] [InlineData(6, 0x02, 1u << 9)]
    [InlineData(6, 0x04, 1u << 10)] [InlineData(6, 0x08, 1u << 11)]
    [InlineData(6, 0x10, 1u << 12)] [InlineData(6, 0x20, 1u << 13)]
    [InlineData(6, 0x40, 1u << 14)] [InlineData(6, 0x80, 1u << 15)]
    [InlineData(7, 0x01, 1u << 16)] [InlineData(7, 0x02, 1u << 17)]
    public void EveryDigitalButtonHasItsOwnBit(int index, byte mask, uint expected)
    {
        byte[] data = Usb();
        data[index] |= mask;
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport report));
        Assert.Equal((ControllerButtons)expected, report.Buttons);
    }

    [Fact]
    public void CountersSensorsBatteryAndTouchCoordinatesDoNotChangeDecodedState()
    {
        byte[] data = Usb(0x28, 0x40, 1);
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport before));
        data[7] |= 0xfc;
        Array.Fill(data, (byte)0xee, 10, data.Length - 10);
        Assert.True(ControllerReportParser.TryParse(data, out ControllerReport after));
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(9)] [InlineData(11)]
    [InlineData(63)] [InlineData(129)]
    public void RejectsTruncatedOrOversizeBasicReports(int length)
    {
        byte[] data = new byte[length];
        if (length != 0) data[0] = 1;
        Assert.False(ControllerReportParser.TryParse(data, out _));
    }

    [Fact]
    public void RejectsUnknownIdsInvalidHatsMissingHidFlagAndBadBluetoothCrc()
    {
        byte[] usb = Usb();
        usb[0] = 2;
        Assert.False(ControllerReportParser.TryParse(usb, out _));
        usb[0] = 1; usb[5] = 15;
        Assert.False(ControllerReportParser.TryParse(usb, out _));
        byte[] bt = Bluetooth();
        Assert.False(ControllerReportParser.TryParse(bt.AsSpan(0, 77), out _));
        bt[1] = 0;
        Assert.False(ControllerReportParser.TryParse(bt, out _));
        bt = Bluetooth(); bt[4] ^= 1;
        Assert.False(ControllerReportParser.TryParse(bt, out _));
    }

    [Fact]
    public void BluetoothCrcMatchesIndependentKnownVector() =>
        Assert.Equal(0x88ed2411u, ControllerReportParser.BluetoothCrc("123456789"u8));

    [Theory]
    [InlineData(0, -1)] [InlineData(128, 0)] [InlineData(255, 1)]
    public void AxesKeepCenterAndFullTravelWithoutDeadzone(byte raw, float expected) =>
        Assert.Equal(expected, ControllerReport.Axis(raw));
}
