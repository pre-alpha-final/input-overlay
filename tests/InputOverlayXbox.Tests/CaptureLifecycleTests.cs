using InputOverlayXbox.Input;

namespace InputOverlayXbox.Tests;

public sealed class CaptureLifecycleTests
{
    [Fact]
    public void SelectionStaysStableAndDisconnectUsesTheOtherControllersLatestState()
    {
        var source = new FakeInput();
        var store = new InputStore();
        using var capture = new ControllerCapture(store, new Diagnostics(), exception => Assert.Fail(exception.ToString()), source);
        source.Device(1, true); source.Read(1, new() { Buttons = 4 });
        source.Device(2, true); source.Read(2, new() { Buttons = 0x20 });
        Assert.True(store.Snapshot().IsDown(ControllerButtons.A));
        Assert.Single(store.Snapshot().History);
        source.Device(1, false);
        Assert.True(store.Snapshot().IsDown(ControllerButtons.Y));
        Assert.Equal(new[] { ControllerButtons.Y, ControllerButtons.A }, store.Snapshot().History.Select(entry => entry.Code));
        source.Device(2, false);
        Assert.False(store.Snapshot().Connected);
        Assert.Equal(ControllerReport.Neutral, store.Snapshot().Controls);
        Assert.Equal(2, store.Snapshot().History.Length);
        source.Device(3, true); source.Read(3, new()); source.System(3, 1);
        Assert.True(store.Snapshot().IsDown(ControllerButtons.Xbox));
        Assert.Equal(3, store.Snapshot().History.Length);
    }

    [Fact]
    public void SystemButtonsMergeWithAnalogReadingsAndReleaseIndependently()
    {
        var source = new FakeInput();
        var store = new InputStore();
        using var capture = new ControllerCapture(store, new Diagnostics(), exception => Assert.Fail(exception.ToString()), source);
        source.Device(1, true);
        source.Read(1, new() { Buttons = 4, LeftTrigger = 0.5f, LeftX = 1 });
        source.System(1, 3);
        source.Read(1, new() { Buttons = 4, LeftTrigger = 1, LeftX = 1 });
        Assert.Equal(ControllerButtons.A | ControllerButtons.LT | ControllerButtons.Share | ControllerButtons.Xbox, store.Snapshot().Controls.Buttons);
        Assert.Equal((byte)255, store.Snapshot().Controls.LeftTrigger);
        Assert.Equal((byte)255, store.Snapshot().Controls.LeftX);
        Assert.Equal(4, store.Snapshot().History.Length);
        source.System(1, 2);
        Assert.False(store.Snapshot().IsDown(ControllerButtons.Xbox));
        Assert.True(store.Snapshot().IsDown(ControllerButtons.Share));
        source.Read(1, new());
        Assert.Equal(ControllerButtons.Share, store.Snapshot().Controls.Buttons);
        source.System(1, 0);
        Assert.Equal(ControllerReport.Neutral, store.Snapshot().Controls);
        Assert.Equal(4, store.Snapshot().History.Length);
    }

    [Fact]
    public void OtherControllersAndLateCallbacksCannotAlterSelectedStateOrHistory()
    {
        var source = new FakeInput();
        var store = new InputStore();
        var capture = new ControllerCapture(store, new Diagnostics(), exception => Assert.Fail(exception.ToString()), source);
        source.Read(99, new() { Buttons = 4 }); source.System(99, 3);
        Assert.False(store.Snapshot().Connected);
        source.Device(1, true); source.Read(1, new() { Buttons = 4 });
        source.Device(2, true); source.System(2, 3);
        Assert.Equal(ControllerButtons.A, store.Snapshot().Controls.Buttons);
        capture.Dispose(); capture.Dispose();
        Assert.Equal(1, source.DisposeCount);
        InputSnapshot disconnected = store.Snapshot();
        source.Device(3, true); source.Read(1, new()); source.System(1, 0);
        Assert.Equal(disconnected, store.Snapshot());
        Assert.False(disconnected.Connected);
        Assert.Single(disconnected.History);
    }

    [Fact]
    public void StartupFailureDisposesInputAndClearsAnyPartialState()
    {
        var source = new FakeInput { FailStart = true };
        var store = new InputStore();
        Assert.Throws<InvalidOperationException>(() => new ControllerCapture(store, new Diagnostics(), exception => Assert.Fail(exception.ToString()), source));
        Assert.Equal(1, source.DisposeCount);
        Assert.False(store.Snapshot().Connected);
    }

    private sealed class FakeInput : IControllerInput
    {
        public Action<nint, bool> Device = null!;
        public Action<nint, GamepadState> Read = null!;
        public Action<nint, uint> System = null!;
        public int DisposeCount;
        public bool FailStart;
        public void Start(Action<nint, bool> deviceChanged, Action<nint, GamepadState> reading,
            Action<nint, uint> systemButtons, Action<Exception> failed)
        {
            Device = deviceChanged; Read = reading; System = systemButtons;
            if (FailStart)
            {
                Device(1, true); Read(1, new() { Buttons = 4 });
                throw new InvalidOperationException("Test initialization failure.");
            }
        }
        public void Dispose() => DisposeCount++;
    }
}
