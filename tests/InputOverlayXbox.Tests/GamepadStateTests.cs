using System.Runtime.InteropServices;
using InputOverlayXbox.Input;

namespace InputOverlayXbox.Tests;

public sealed class GamepadStateTests
{
    [Fact]
    public void NativeGamepadLayoutMatchesMicrosoftAbi()
    {
        Assert.Equal(28, Marshal.SizeOf<GamepadState>());
        Assert.Equal(4, Marshal.OffsetOf<GamepadState>(nameof(GamepadState.LeftTrigger)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<GamepadState>(nameof(GamepadState.LeftX)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<GamepadState>(nameof(GamepadState.RightY)).ToInt32());
    }

    [Theory]
    [InlineData(0x0001u, "Menu")] [InlineData(0x0002u, "View")]
    [InlineData(0x0004u, "A")] [InlineData(0x0008u, "B")]
    [InlineData(0x0010u, "X")] [InlineData(0x0020u, "Y")]
    [InlineData(0x0040u, "Up")] [InlineData(0x0080u, "Down")]
    [InlineData(0x0100u, "Left")] [InlineData(0x0200u, "Right")]
    [InlineData(0x0400u, "LB")] [InlineData(0x0800u, "RB")]
    [InlineData(0x1000u, "LS")] [InlineData(0x2000u, "RS")]
    public void MapsEachGamepadButtonIndependently(uint native, string expected) =>
        Assert.Equal(Enum.Parse<ControllerButtons>(expected), new GamepadState { Buttons = native }.ToReport().Buttons);

    [Fact]
    public void AxesAreCenteredAndYIsInvertedForScreenCoordinates()
    {
        Assert.Equal(ControllerReport.Neutral, new GamepadState().ToReport());
        ControllerReport report = new GamepadState { LeftX = -1, LeftY = 1, RightX = 1, RightY = -1 }.ToReport();
        Assert.Equal((byte)0, report.LeftX); Assert.Equal((byte)0, report.LeftY);
        Assert.Equal((byte)255, report.RightX); Assert.Equal((byte)255, report.RightY);
        Assert.Equal(ControllerButtons.None, report.Buttons);
    }

    [Fact]
    public void PressureIsUnfilteredAndTriggerPressesUseDocumentedThreshold()
    {
        var raw = new GamepadState { LeftTrigger = 1 / 255f, RightTrigger = GamepadState.TriggerThreshold };
        ControllerReport report = raw.ToReport();
        Assert.Equal((byte)1, report.LeftTrigger); Assert.Equal((byte)30, report.RightTrigger);
        Assert.Equal(ControllerButtons.None, report.Buttons);
        raw.LeftTrigger = 31 / 255f; raw.RightTrigger = 1;
        report = raw.ToReport();
        Assert.Equal(ControllerButtons.LT | ControllerButtons.RT, report.Buttons);
        Assert.Equal((byte)31, report.LeftTrigger); Assert.Equal((byte)255, report.RightTrigger);
    }

    [Fact]
    public void GuideAndShareCanBeCombinedWithEveryGamepadButtonAndBothTriggers()
    {
        var raw = new GamepadState { Buttons = 0x3fff, LeftTrigger = 1, RightTrigger = 1 };
        Assert.Equal(InputCodes.All.Aggregate(ControllerButtons.None, (all, next) => all | next), raw.ToReport(3).Buttons);
        Assert.Equal(ControllerButtons.Xbox, new GamepadState().ToReport(1).Buttons);
        Assert.Equal(ControllerButtons.Share, new GamepadState().ToReport(2).Buttons);
    }
}
