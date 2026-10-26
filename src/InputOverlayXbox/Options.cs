using System.Globalization;

namespace InputOverlayXbox;

internal sealed record Options(int? Monitor = null, float Scale = 1, int BottomMargin = 24,
    bool ListMonitors = false, bool Help = false)
{
    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            string Value() => ++index < args.Length ? args[index]
                : throw new ArgumentException($"Missing value for {argument}.");
            switch (argument)
            {
                case "--monitor":
                    if (!int.TryParse(Value(), out int monitor) || monitor < 1)
                        throw new ArgumentException("--monitor must be a positive monitor number.");
                    options = options with { Monitor = monitor };
                    break;
                case "--scale":
                    if (!float.TryParse(Value(), NumberStyles.Float, CultureInfo.InvariantCulture, out float scale)
                        || !float.IsFinite(scale) || scale is < 0.5f or > 3)
                        throw new ArgumentException("--scale must be between 0.5 and 3 (use a decimal point).");
                    options = options with { Scale = scale };
                    break;
                case "--bottom-margin":
                    if (!int.TryParse(Value(), out int margin) || margin is < 0 or > 1000)
                        throw new ArgumentException("--bottom-margin must be between 0 and 1000 DIP.");
                    options = options with { BottomMargin = margin };
                    break;
                case "--list-monitors": options = options with { ListMonitors = true }; break;
                case "--help" or "-h": options = options with { Help = true }; break;
                default: throw new ArgumentException($"Unknown option: {argument}. Use --help.");
            }
        }
        return options;
    }

    public const string Usage = """
        InputOverlayXbox - Xbox Series X|S controller overlay for windowed/borderless games

        InputOverlayXbox.exe [--monitor N] [--scale N] [--bottom-margin N]
        InputOverlayXbox.exe --list-monitors

          --monitor N        Monitor number from --list-monitors (default: primary)
          --scale N          Size multiplier, 0.5 to 3 (default: 1)
          --bottom-margin N  Distance from screen bottom in DIP, 0 to 1000 (default: 24)
          --help, -h         Show this help

        Numpad 0: hide/show. Ctrl+C exits.
        """;
}
