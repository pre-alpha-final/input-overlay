namespace InputOverlayXbox.Input;

[Flags]
internal enum ControllerButtons : uint
{
    None = 0,
    Up = 1 << 0, Right = 1 << 1, Down = 1 << 2, Left = 1 << 3,
    X = 1 << 4, A = 1 << 5, B = 1 << 6, Y = 1 << 7,
    LB = 1 << 8, RB = 1 << 9, LT = 1 << 10, RT = 1 << 11,
    View = 1 << 12, Menu = 1 << 13, LS = 1 << 14, RS = 1 << 15,
    Xbox = 1 << 16, Share = 1 << 17
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
        ControllerButtons.LS => "LS click", ControllerButtons.RS => "RS click",
        _ => button.ToString()
    };
}
