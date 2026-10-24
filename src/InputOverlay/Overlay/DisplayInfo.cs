namespace InputOverlay.Overlay;

internal static class DisplayInfo
{
    public static Screen[] Screens() => Screen.AllScreens
        .OrderByDescending(screen => screen.Primary)
        .ThenBy(screen => screen.DeviceName, StringComparer.Ordinal).ToArray();

    public static float DpiScale(Screen screen)
    {
        Rectangle bounds = screen.Bounds;
        nint monitor = Native.MonitorFromPoint(new Native.Point(bounds.Left + bounds.Width / 2,
            bounds.Top + bounds.Height / 2), 2);
        return Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96f : 1;
    }

    public static int RefreshRate(Screen screen)
    {
        nint dc = Native.CreateDC("DISPLAY", screen.DeviceName, null, 0);
        if (dc == 0) return 60;
        try
        {
            int rate = Native.GetDeviceCaps(dc, 116);
            return rate > 1 ? rate : 60;
        }
        finally { Native.DeleteDC(dc); }
    }

    public static void Print()
    {
        Screen[] screens = Screens();
        for (int index = 0; index < screens.Length; index++)
        {
            Screen screen = screens[index];
            Console.WriteLine($"{index + 1}: {screen.DeviceName}{(screen.Primary ? " (primary)" : "")} " +
                $"{screen.Bounds.Width}x{screen.Bounds.Height}, {RefreshRate(screen)} Hz, " +
                $"DPI scale {DpiScale(screen):0.##}");
        }
    }
}
