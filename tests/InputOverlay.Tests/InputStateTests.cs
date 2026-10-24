using InputOverlay.Input;

namespace InputOverlay.Tests;

public sealed class InputStateTests
{
    [Theory]
    [InlineData(0x1d, 0, 0x1d)]
    [InlineData(0x1d, 2, 0x11d)]
    [InlineData(0x38, 0, 0x38)]
    [InlineData(0x38, 2, 0x138)]
    [InlineData(0x2a, 0, 0x2a)]
    [InlineData(0x36, 0, 0x36)]
    [InlineData(0x5b, 2, 0x15b)]
    [InlineData(0x1d, 4, 0x21d)]
    public void PhysicalScanCodesKeepExtendedIdentity(int scan, int flags, int expected) =>
        Assert.Equal(expected, InputCodes.Keyboard((ushort)scan, (ushort)flags));

    [Fact]
    public void OnlyLeftModifiersAreDisplayed()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x1d, 2, 10); // right Ctrl
        state.Keyboard(1, 0x38, 2, 11); // right Alt
        state.Keyboard(1, 0x36, 0, 12); // right Shift
        Assert.Equal(0UL, state.Snapshot.Pressed);
        Assert.Equal(12, state.Snapshot.LastActivity);
        state.Keyboard(1, 0x1d, 0, 13);
        Assert.True(state.Snapshot.IsDown(0x1d));
    }

    [Fact]
    public void KeysSupportChordsAndReleaseImmediately()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x11, 0, 10);
        state.Keyboard(1, 0x1d, 0, 11);
        Assert.True(state.Snapshot.IsDown(0x11));
        Assert.True(state.Snapshot.IsDown(0x1d));
        state.Keyboard(1, 0x11, 1, 12);
        Assert.False(state.Snapshot.IsDown(0x11));
        Assert.True(state.Snapshot.IsDown(0x1d));
        Assert.Equal(11, state.Snapshot.LastActivity);
    }

    [Fact]
    public void AutoRepeatDoesNotResetTimerOrPublishChanges()
    {
        var state = new InputState(1000);
        Assert.True(state.Keyboard(1, 0x12, 0, 0));
        long version = state.Snapshot.Version;
        for (int i = 1; i < 1000; i++) Assert.False(state.Keyboard(1, 0x12, 0, i));
        Assert.Equal(0, state.Snapshot.LastActivity);
        Assert.Equal(version, state.Snapshot.Version);
        InputHistoryEntry entry = Assert.Single(state.Snapshot.History);
        Assert.Equal(new InputHistoryEntry(0x12, 0), entry);
        Assert.False(state.Snapshot.IsAnimating(650));
    }

    [Fact]
    public void NonDisplayedKeyAddsHistoryWithoutAddingAKeycap()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x15, 0, 100); // Y
        Assert.Equal(0UL, state.Snapshot.Pressed);
        InputHistoryEntry entry = Assert.Single(state.Snapshot.History);
        Assert.Equal("Y", InputCodes.Label(entry.Code));
        Assert.Equal(new TimerVisual(25, 1), entry.TimerAt(125, state.Snapshot.Frequency));
    }

    [Fact]
    public void TwoKeyboardsKeepSharedKeyHeldUntilBothRelease()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 10);
        state.Keyboard(2, 0x21, 0, 20);
        state.Keyboard(1, 0x21, 1, 30);
        Assert.True(state.Snapshot.IsDown(0x21));
        state.Keyboard(2, 0x21, 1, 40);
        Assert.False(state.Snapshot.IsDown(0x21));
        Assert.Equal(20, state.Snapshot.LastActivity);
    }

    [Fact]
    public void DeviceRemovalReleasesOnlyThatDevicesContribution()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 10);
        state.Keyboard(2, 0x21, 0, 20);
        state.Keyboard(1, 0x12, 0, 30);
        Assert.True(state.RemoveDevice(1, 40));
        Assert.True(state.Snapshot.IsDown(0x21));
        Assert.False(state.Snapshot.IsDown(0x12));
        Assert.Equal(30, state.Snapshot.LastActivity);
        state.RemoveDevice(2, 50);
        Assert.Equal(0UL, state.Snapshot.Pressed);
    }

    [Theory]
    [InlineData(0x0001, 0x0002, InputCodes.MouseLeft)]
    [InlineData(0x0004, 0x0008, InputCodes.MouseRight)]
    [InlineData(0x0010, 0x0020, InputCodes.MouseMiddle)]
    [InlineData(0x0040, 0x0080, InputCodes.MouseBack)]
    [InlineData(0x0100, 0x0200, InputCodes.MouseForward)]
    public void AllFiveMouseButtonsHaveIndependentPressAndRelease(int down, int up, int code)
    {
        var state = new InputState(1000);
        Assert.True(state.Mouse(1, (ushort)down, 0, 10));
        Assert.True(state.Snapshot.IsDown(code));
        Assert.False(state.Mouse(1, (ushort)down, 0, 20));
        Assert.Equal(10, state.Snapshot.LastActivity);
        Assert.True(state.Mouse(1, (ushort)up, 0, 30));
        Assert.False(state.Snapshot.IsDown(code));
        Assert.Equal(10, state.Snapshot.LastActivity);
    }

    [Fact]
    public void MousePacketCanContainMultipleButtonTransitionsAndWheel()
    {
        var state = new InputState(1000);
        state.Mouse(1, 0x0415, 120, 10);
        Assert.True(state.Snapshot.IsDown(InputCodes.MouseLeft));
        Assert.True(state.Snapshot.IsDown(InputCodes.MouseRight));
        Assert.True(state.Snapshot.IsDown(InputCodes.MouseMiddle));
        Assert.True(state.Snapshot.ScrollUpAt(10));
        state.Mouse(1, 0x002a, 0, 20);
        Assert.Equal(0UL, state.Snapshot.Pressed);
    }

    [Fact]
    public void ScrollingSupportsSmallSignedDeltasAndIndependentPulses()
    {
        var state = new InputState(1000);
        state.Mouse(1, 0x0400, 1, 100);
        Assert.True(state.Snapshot.ScrollUpAt(179));
        Assert.False(state.Snapshot.ScrollUpAt(180));
        state.Mouse(1, 0x0400, -1, 150);
        Assert.True(state.Snapshot.ScrollUpAt(150));
        Assert.True(state.Snapshot.ScrollDownAt(229));
        Assert.False(state.Snapshot.ScrollDownAt(230));
        Assert.Equal(150, state.Snapshot.LastActivity);
        Assert.False(state.Mouse(1, 0x0400, 0, 500));
        Assert.False(state.Mouse(1, 0x0800, 120, 500));
        Assert.Equal(150, state.Snapshot.LastActivity);
    }

    [Fact]
    public void MotionFloodDoesNotPublishChangesOrResetTimer()
    {
        var store = new InputStore();
        int changes = 0;
        store.Changed += () => changes++;
        store.Keyboard(1, 0x21, 0, 100);
        InputSnapshot before = store.Snapshot();
        for (int i = 0; i < 100_000; i++) store.Mouse(1, 0, 0, i);
        Assert.Equal(1, changes);
        Assert.Equal(before, store.Snapshot());
    }

    [Fact]
    public void RapidTapsDoNotStickOrExtendHighlights()
    {
        var state = new InputState(1000);
        for (int i = 0; i < 10_000; i++)
        {
            state.Keyboard(1, 0x21, 0, i * 2);
            Assert.True(state.Snapshot.IsDown(0x21));
            state.Keyboard(1, 0x21, 1, i * 2 + 1);
            Assert.False(state.Snapshot.IsDown(0x21));
        }
        Assert.Equal(20_000, state.Snapshot.Version);
    }

    [Fact]
    public void ResetClearsKeysScrollAndTimerAndAllowsFreshPress()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 10);
        state.Mouse(2, 0x0401, 120, 20);
        state.Reset(30);
        Assert.Equal(0UL, state.Snapshot.Pressed);
        Assert.False(state.Snapshot.IsAnimating(30));
        Assert.Empty(state.Snapshot.History);
        Assert.True(state.Keyboard(1, 0x21, 0, 40));
        Assert.True(state.Snapshot.IsDown(0x21));
    }

    [Fact]
    public void HistoryKeepsOnlyLastTenInputsNewestFirstIncludingRepeatedTaps()
    {
        var state = new InputState(1000);
        for (int index = 0; index < 15; index++)
        {
            state.Keyboard(1, 0x21, 0, index * 100);
            state.Keyboard(1, 0x21, 1, index * 100 + 10);
        }
        InputSnapshot snapshot = state.Snapshot;
        Assert.Equal(10, snapshot.History.Length);
        Assert.Equal(Enumerable.Range(5, 10).Reverse().Select(index => (long)index * 100),
            snapshot.History.Select(entry => entry.Timestamp));
        Assert.All(snapshot.History, entry => Assert.Equal(0x21, entry.Code));
    }

    [Fact]
    public void HistorySnapshotsAreUnaffectedByNewInputsEvictionAndReset()
    {
        var state = new InputState(1000);
        for (int index = 0; index < 10; index++) state.Mouse(1, 0x0400, 120, index);
        InputSnapshot before = state.Snapshot;
        state.Mouse(1, 0x0400, -120, 10);
        state.Reset(11);
        Assert.Equal(10, before.History.Length);
        Assert.Equal(Enumerable.Range(0, 10).Reverse().Select(index => (long)index),
            before.History.Select(entry => entry.Timestamp));
        Assert.All(before.History, entry => Assert.Equal(InputCodes.ScrollUp, entry.Code));
        Assert.Empty(state.Snapshot.History);
    }

    [Fact]
    public void HistoryIncludesEveryMouseButtonAndEachWheelDirection()
    {
        var state = new InputState(1000);
        state.Mouse(1, 0x0555, 120, 100);
        state.Mouse(1, 0x0400, -1, 200);
        Assert.Equal(new[] { InputCodes.ScrollDown, InputCodes.ScrollUp, InputCodes.MouseForward,
            InputCodes.MouseBack, InputCodes.MouseMiddle, InputCodes.MouseRight, InputCodes.MouseLeft },
            state.Snapshot.History.Select(entry => entry.Code));
        state.Mouse(1, 0x02aa, 0, 210);
        Assert.Equal(7, state.Snapshot.History.Length);
        state.RemoveDevice(1, 220);
        Assert.Equal(7, state.Snapshot.History.Length);
    }

    [Theory]
    [InlineData(0x11d, "RCtrl")]
    [InlineData(0x36, "RShift")]
    [InlineData(0x148, "Up")]
    [InlineData(0x21d, "Pause")]
    [InlineData(0x57, "F11")]
    [InlineData(InputCodes.MouseBack, "Mouse 4")]
    [InlineData(InputCodes.ScrollDown, "Wheel down")]
    [InlineData(0x7f, "0x07F")]
    public void HistoryLabelsDistinguishExtendedKeysAndMouseInputs(int code, string label) =>
        Assert.Equal(label, InputCodes.Label(code));

    [Fact]
    public void UnknownReleasesAndInvalidScanCodesDoNothing()
    {
        var state = new InputState(1000);
        Assert.False(state.Keyboard(1, 0x21, 1, 10));
        Assert.False(state.Keyboard(1, 0xff, 0, 20));
        Assert.False(state.Keyboard(1, 0, 0, 30));
        Assert.False(state.RemoveDevice(1, 40));
        Assert.Null(state.Snapshot.LastActivity);
    }
}
