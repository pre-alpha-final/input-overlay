namespace InputOverlay.Input;

internal static class InputCodes
{
    public const int MouseLeft = 0x1000;
    public const int MouseRight = 0x1001;
    public const int MouseMiddle = 0x1002;
    public const int MouseBack = 0x1003;
    public const int MouseForward = 0x1004;

    // The low byte is the physical scan code; E0/E1 retain distinct identities.
    public static int Keyboard(ushort makeCode, ushort flags) =>
        (makeCode & 0x7f) | ((flags & 2) != 0 ? 0x100 : 0) | ((flags & 4) != 0 ? 0x200 : 0);

    public static readonly int[] Visible =
    [
        0x01, 0x29, 0x02, 0x03, 0x04, 0x05, 0x06,
        0x0f, 0x10, 0x11, 0x12, 0x13, 0x14,
        0x3a, 0x1e, 0x1f, 0x20, 0x21, 0x22,
        0x2a, 0x2c, 0x2d, 0x2e, 0x2f, 0x30,
        0x1d, 0x15b, 0x38, 0x39,
        MouseLeft, MouseRight, MouseMiddle, MouseBack, MouseForward
    ];

    private static readonly Dictionary<int, ulong> Masks = Visible
        .Select((code, index) => (code, mask: 1UL << index))
        .ToDictionary(item => item.code, item => item.mask);

    public static ulong Mask(int code) => Masks.GetValueOrDefault(code);
}
