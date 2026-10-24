namespace InputOverlay.Input;

internal static class InputCodes
{
    public const int MouseLeft = 0x1000;
    public const int MouseRight = 0x1001;
    public const int MouseMiddle = 0x1002;
    public const int MouseBack = 0x1003;
    public const int MouseForward = 0x1004;
    public const int ScrollUp = 0x1005;
    public const int ScrollDown = 0x1006;

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

    public static string Label(int code) => code switch
    {
        MouseLeft => "LMB", MouseRight => "RMB", MouseMiddle => "MMB",
        MouseBack => "Mouse 4", MouseForward => "Mouse 5",
        ScrollUp => "Wheel up", ScrollDown => "Wheel down",
        0x01 => "Esc",
        >= 0x02 and <= 0x0b => "1234567890"[code - 0x02].ToString(),
        0x0c => "-", 0x0d => "=", 0x0e => "Backspace", 0x0f => "Tab",
        >= 0x10 and <= 0x19 => "QWERTYUIOP"[code - 0x10].ToString(),
        0x1a => "[", 0x1b => "]", 0x1c => "Enter", 0x1d => "Ctrl",
        >= 0x1e and <= 0x26 => "ASDFGHJKL"[code - 0x1e].ToString(),
        0x27 => ";", 0x28 => "'", 0x29 => "`", 0x2a => "Shift", 0x2b => "\\",
        >= 0x2c and <= 0x32 => "ZXCVBNM"[code - 0x2c].ToString(),
        0x33 => ",", 0x34 => ".", 0x35 => "/", 0x36 => "RShift", 0x37 => "Num *",
        0x38 => "Alt", 0x39 => "Space", 0x3a => "Caps",
        >= 0x3b and <= 0x44 => $"F{code - 0x3a}",
        0x45 => "Num Lock", 0x46 => "Scroll Lock",
        0x47 => "Num 7", 0x48 => "Num 8", 0x49 => "Num 9", 0x4a => "Num -",
        0x4b => "Num 4", 0x4c => "Num 5", 0x4d => "Num 6", 0x4e => "Num +",
        0x4f => "Num 1", 0x50 => "Num 2", 0x51 => "Num 3", 0x52 => "Num 0", 0x53 => "Num .",
        0x56 => "\\", 0x57 => "F11", 0x58 => "F12",
        0x11c => "Num Enter", 0x11d => "RCtrl", 0x135 => "Num /", 0x137 => "Print Scr",
        0x138 => "RAlt", 0x147 => "Home", 0x148 => "Up", 0x149 => "Page Up",
        0x14b => "Left", 0x14d => "Right", 0x14f => "End", 0x150 => "Down",
        0x151 => "Page Down", 0x152 => "Insert", 0x153 => "Delete",
        0x15b => "Win", 0x15c => "RWin", 0x15d => "Menu", 0x21d or 0x245 => "Pause",
        _ => $"0x{code:X3}"
    };

    public static ulong Mask(int code) => Masks.GetValueOrDefault(code);
}
