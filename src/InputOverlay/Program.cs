using InputOverlay.Input;
using InputOverlay.Overlay;

namespace InputOverlay;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.Help) { Console.WriteLine(Options.Usage); return 0; }
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            if (options.ListMonitors) { DisplayInfo.Print(); return 0; }
            if (options.Monitor > DisplayInfo.Screens().Length)
                throw new ArgumentException($"Monitor {options.Monitor} does not exist. Use --list-monitors.");

            var store = new InputStore();
            var diagnostics = new Diagnostics();
            OverlayWindow? window = null;
            int exitCode = 0;
            void Failed(Exception exception)
            {
                if (Interlocked.Exchange(ref exitCode, 1) == 0)
                    Console.Error.WriteLine($"Overlay stopped: {exception.GetBaseException().Message}");
                window?.RequestExit();
            }
            using (window = new OverlayWindow(options, store, diagnostics, Failed))
            using (var capture = new RawInputCapture(store, diagnostics, Failed))
            {
                Console.WriteLine("InputOverlay running. Use a windowed or borderless game.");
                Console.WriteLine("Numpad 0: hide/show. Ctrl+C exits.");
                Console.CancelKeyPress += Cancel;
                try
                {
                    Application.Run(window);
                }
                finally { Console.CancelKeyPress -= Cancel; }
                void Cancel(object? sender, ConsoleCancelEventArgs e)
                { e.Cancel = true; window.RequestExit(); }
            }
            return exitCode;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"InputOverlay failed: {exception.GetBaseException().Message}");
            return 1;
        }
    }
}
