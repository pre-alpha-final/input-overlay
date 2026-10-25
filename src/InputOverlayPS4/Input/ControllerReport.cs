namespace InputOverlayPS4.Input;

[Flags]
internal enum ControllerButtons : uint
{
    None = 0,
    Up = 1 << 0, Right = 1 << 1, Down = 1 << 2, Left = 1 << 3,
    Square = 1 << 4, Cross = 1 << 5, Circle = 1 << 6, Triangle = 1 << 7,
    L1 = 1 << 8, R1 = 1 << 9, L2 = 1 << 10, R2 = 1 << 11,
    Share = 1 << 12, Options = 1 << 13, L3 = 1 << 14, R3 = 1 << 15,
    PS = 1 << 16, Touchpad = 1 << 17
}

internal readonly record struct ControllerReport(ControllerButtons Buttons,
    byte LeftX, byte LeftY, byte RightX, byte RightY, byte LeftTrigger, byte RightTrigger)
{
    public static ControllerReport Neutral => new(ControllerButtons.None, 128, 128, 128, 128, 0, 0);
    public bool IsDown(ControllerButtons button) => (Buttons & button) != 0;
    public static float Axis(byte value) => value < 128 ? (value - 128) / 128f : (value - 128) / 127f;

    public const float StickDeadzone = 0.10f;

    public ControllerReport WithStickDeadzone()
    {
        var left = CenterStick(LeftX, LeftY);
        var right = CenterStick(RightX, RightY);
        return this with { LeftX = left.X, LeftY = left.Y, RightX = right.X, RightY = right.Y };
    }

    private static (byte X, byte Y) CenterStick(byte rawX, byte rawY)
    {
        float x = Axis(rawX), y = Axis(rawY);
        // A circular deadzone centers resting drift without changing positions outside it.
        return x * x + y * y <= StickDeadzone * StickDeadzone ? ((byte)128, (byte)128) : (rawX, rawY);
    }
}

internal static class InputCodes
{
    public static readonly ControllerButtons[] All = Enum.GetValues<ControllerButtons>()
        .Where(button => button != ControllerButtons.None).ToArray();

    public static string Label(ControllerButtons button) => button switch
    {
        ControllerButtons.Up => "D-pad up", ControllerButtons.Right => "D-pad right",
        ControllerButtons.Down => "D-pad down", ControllerButtons.Left => "D-pad left",
        ControllerButtons.Touchpad => "Touchpad",
        _ => button.ToString()
    };
}
