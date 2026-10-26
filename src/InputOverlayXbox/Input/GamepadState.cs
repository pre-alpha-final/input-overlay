using System.Runtime.InteropServices;

namespace InputOverlayXbox.Input;

// Microsoft GameInput v4 GameInputGamepadState (28 bytes, floats in native axis order).
[StructLayout(LayoutKind.Sequential)]
internal struct GamepadState
{
    public uint Buttons;
    public float LeftTrigger, RightTrigger, LeftX, LeftY, RightX, RightY;

    // Xbox triggers have no digital bit. Use the standard XInput 30/255 threshold for history.
    internal const float TriggerThreshold = 30 / 255f;
    public readonly ControllerReport ToReport(uint systemButtons = 0)
    {
        ControllerButtons buttons = ControllerButtons.None;
        if ((Buttons & 0x0001) != 0) buttons |= ControllerButtons.Menu;
        if ((Buttons & 0x0002) != 0) buttons |= ControllerButtons.View;
        if ((Buttons & 0x0004) != 0) buttons |= ControllerButtons.A;
        if ((Buttons & 0x0008) != 0) buttons |= ControllerButtons.B;
        if ((Buttons & 0x0010) != 0) buttons |= ControllerButtons.X;
        if ((Buttons & 0x0020) != 0) buttons |= ControllerButtons.Y;
        if ((Buttons & 0x0040) != 0) buttons |= ControllerButtons.Up;
        if ((Buttons & 0x0080) != 0) buttons |= ControllerButtons.Down;
        if ((Buttons & 0x0100) != 0) buttons |= ControllerButtons.Left;
        if ((Buttons & 0x0200) != 0) buttons |= ControllerButtons.Right;
        if ((Buttons & 0x0400) != 0) buttons |= ControllerButtons.LB;
        if ((Buttons & 0x0800) != 0) buttons |= ControllerButtons.RB;
        if ((Buttons & 0x1000) != 0) buttons |= ControllerButtons.LS;
        if ((Buttons & 0x2000) != 0) buttons |= ControllerButtons.RS;
        if ((systemButtons & 1) != 0) buttons |= ControllerButtons.Xbox;
        if ((systemButtons & 2) != 0) buttons |= ControllerButtons.Share;
        if (LeftTrigger > TriggerThreshold) buttons |= ControllerButtons.LT;
        if (RightTrigger > TriggerThreshold) buttons |= ControllerButtons.RT;
        return new(buttons, Axis(LeftX), Axis(-LeftY), Axis(RightX), Axis(-RightY),
            Pressure(LeftTrigger), Pressure(RightTrigger));
    }

    private static byte Axis(float value) => (byte)Math.Round(128 + Math.Clamp(value, -1, 1) * (value < 0 ? 128 : 127));
    private static byte Pressure(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
