using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using InputOverlayPS5.Input;
using InputOverlayPS5.Overlay;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace InputOverlayPS5.Tests;

public sealed class WindowsIntegrationTests(ITestOutputHelper output)
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Native.Point point);
    private delegate bool EnumWindowsCallback(nint window, nint parameter);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    private static extern uint GetRegisteredRawInputDevices(nint devices, ref uint count, uint size);

    [Fact]
    public void NativeInputStructuresMatchWindowsX64Abi()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(24, Marshal.SizeOf<Native.RawHeader>());
        Assert.Equal(16, Marshal.SizeOf<Native.RawKeyboard>());
        Assert.Equal(16, Marshal.SizeOf<Native.RawInputDevice>());
    }

    [Fact]
    public void CaptureRegistersKeyboardAndControllerNotificationsAndUnregistersOnShutdown()
    {
        var store = new InputStore();
        var diagnostics = new Diagnostics();
        Exception? failure = null;
        using (var capture = new RawInputCapture(store, () => { }, exception => failure = exception))
        {
            Native.RawInputDevice[] registrations = Registrations();
            foreach (ushort usage in new ushort[] { 4, 5, 6 })
            {
                Native.RawInputDevice device = Assert.Single(registrations, item => item.UsagePage == 1 && item.Usage == usage);
                output.WriteLine($"Usage {usage}: flags 0x{device.Flags:x}; target {device.Target}");
                Assert.NotEqual(0, device.Target);
                // Windows readback omits DEVNOTIFY; require background reception and no legacy suppression.
                Assert.Equal(Native.RidevInputSink, device.Flags & Native.RidevInputSink);
                Assert.Equal(0u, device.Flags & 0x30);
            }
        }
        Assert.Null(failure);
        Assert.DoesNotContain(Registrations(), item => item.UsagePage == 1 && item.Usage is 4 or 5 or 6);
    }

    [Fact]
    public void OverlayStaysAboveBorderlessWindowWithoutTakingFocusAndAllowsClickThrough()
    {
        OnSta(() =>
        {
            Rectangle screen = Screen.PrimaryScreen!.Bounds;
            using var background = new Form
            {
                Text = "InputOverlayPS5 verification", FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(screen.Left, screen.Bottom - Math.Min(500, screen.Height), screen.Width, Math.Min(500, screen.Height)),
                BackColor = Color.FromArgb(45, 52, 60), ShowInTaskbar = false
            };
            background.Show();
            Application.DoEvents();
            var store = new InputStore();
            var diagnostics = new Diagnostics();
            Exception? failure = null;
            using var overlay = new OverlayWindow(new Options(), store, diagnostics, exception => failure = exception);
            int activations = 0;
            overlay.Activated += (_, _) => activations++;
            var point = new Native.Point(overlay.Left + 20, overlay.Top + 20);
            // Windows may keep the user's current app in front of the verification window.
            nint underlying = WindowFromPoint(point);
            overlay.Show();
            PumpUntil(() => diagnostics.FrameCount > 0);
            Assert.Null(failure);
            Assert.NotEqual(overlay.Handle, GetForegroundWindow());
            Assert.Equal(0, activations);
            long styles = GetWindowLongPtr(overlay.Handle, -20);
            foreach (int flag in new[] { Native.WsExLayered, Native.WsExTransparent, Native.WsExNoActivate, Native.WsExTopmost })
                Assert.Equal((long)flag, styles & flag);
            Assert.Equal(0, styles & Native.WsExToolWindow); // OBS excludes tool windows.
            Assert.Equal(0, styles & 0x40000); // WS_EX_APPWINDOW
            Assert.False(overlay.ShowInTaskbar);
            Assert.Contains(overlay.Handle, VisibleTopLevelWindows());
            nint hit = WindowFromPoint(point);
            Assert.NotEqual(0, underlying);
            Assert.NotEqual(overlay.Handle, hit);
            Assert.Equal(underlying, hit);
            Assert.InRange(Math.Abs(overlay.Left + overlay.Width / 2 - (screen.Left + screen.Width / 2)), 0, 1);
            Assert.True(overlay.Bottom <= screen.Bottom);
            // The top-row 0 and dedicated Insert key must not toggle the overlay.
            store.Keyboard(1, 0x0b, 0, Stopwatch.GetTimestamp());
            store.Keyboard(1, 0x0b, 1, Stopwatch.GetTimestamp());
            store.Keyboard(1, 0x52, 2, Stopwatch.GetTimestamp());
            store.Keyboard(1, 0x52, 3, Stopwatch.GetTimestamp());
            PumpFor(25);
            Assert.True(overlay.Visible);
            Rectangle originalBounds = overlay.Bounds;
            nint foregroundBeforeHide = GetForegroundWindow();
            // Numpad 0 uses the same physical scan code with Num Lock on or off.
            store.Keyboard(1, 0x52, 0, Stopwatch.GetTimestamp());
            PumpUntil(() => !overlay.Visible);
            Assert.True(IsIconic(overlay.Handle)); // OBS must stop drawing the captured window.
            Assert.Equal(foregroundBeforeHide, GetForegroundWindow());
            Assert.DoesNotContain(overlay.Handle, VisibleTopLevelWindows());
            for (int index = 0; index < 20; index++)
                store.Keyboard(1, 0x52, 0, Stopwatch.GetTimestamp());
            PumpFor(25);
            Assert.False(overlay.Visible);
            store.Keyboard(1, 0x52, 1, Stopwatch.GetTimestamp());
            PumpFor(25);
            Assert.False(overlay.Visible);
            long frames = diagnostics.FrameCount;
            store.Keyboard(1, 0x52, 0, Stopwatch.GetTimestamp());
            PumpUntil(() => overlay.Visible && diagnostics.FrameCount > frames);
            Assert.False(IsIconic(overlay.Handle));
            Assert.Equal(originalBounds, overlay.Bounds);
            Assert.Contains(overlay.Handle, VisibleTopLevelWindows());
            store.Keyboard(1, 0x52, 1, Stopwatch.GetTimestamp());
            Assert.NotEqual(overlay.Handle, GetForegroundWindow());
            Assert.Equal(0, activations);
            Assert.Null(failure);
            overlay.Close();
        });
    }

    [Fact]
    public void RenderingCoalescesFloodsUpdatesPromptlyAndStopsWhenIdle()
    {
        OnSta(() =>
        {
            var store = new InputStore();
            var diagnostics = new Diagnostics();
            Exception? failure = null;
            using var overlay = new OverlayWindow(new Options(), store, diagnostics, exception => failure = exception);
            overlay.Show();
            PumpUntil(() => diagnostics.FrameCount > 0);
            for (int index = 0; index < 64; index++)
            {
                long previous = diagnostics.ChangedStateSamples;
                store.Report(ControllerReport.Neutral with { Buttons = index % 2 == 0 ? ControllerButtons.Cross : ControllerButtons.None }, Stopwatch.GetTimestamp());
                PumpUntil(() => diagnostics.ChangedStateSamples > previous);
            }
            output.WriteLine(diagnostics.ToString());
            Assert.Null(failure);
            long version = store.Snapshot().Version;
            for (int index = 0; index < 100_000; index++) store.Report(ControllerReport.Neutral, Stopwatch.GetTimestamp());
            Assert.Equal(version, store.Snapshot().Version);
            // The active countdown expires even if no further input is received.
            PumpFor(750);
            long frames = diagnostics.FrameCount;
            PumpFor(60);
            Assert.Equal(frames, diagnostics.FrameCount);
            Assert.Null(failure);
            overlay.Close();
        });
    }

    private static HashSet<nint> VisibleTopLevelWindows()
    {
        var windows = new HashSet<nint>();
        Assert.True(EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window)) windows.Add(window);
            return true;
        }, 0));
        return windows;
    }

    private static Native.RawInputDevice[] Registrations()
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf<Native.RawInputDevice>();
        Assert.NotEqual(uint.MaxValue, GetRegisteredRawInputDevices(0, ref count, size));
        if (count == 0) return [];
        nint buffer = Marshal.AllocHGlobal(checked((int)(count * size)));
        try
        {
            Assert.NotEqual(uint.MaxValue, GetRegisteredRawInputDevices(buffer, ref count, size));
            return Enumerable.Range(0, (int)count)
                .Select(index => Marshal.PtrToStructure<Native.RawInputDevice>(buffer + index * (int)size)).ToArray();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); action(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Windows UI test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PumpUntil(Func<bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5), "Window did not process the expected update.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private static void PumpFor(int milliseconds)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }
}
