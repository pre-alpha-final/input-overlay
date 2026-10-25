using InputOverlayPS4.Input;

namespace InputOverlayPS4.Tests;

public sealed class InputStateTests
{
    [Fact]
    public void StartsDisconnectedWithCenteredSticksAndEmptyHistory()
    {
        var state = new InputState(1000);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
        Assert.False(state.Snapshot.Connected);
        Assert.Empty(state.Snapshot.History);
        Assert.False(state.Snapshot.IsAnimating(10));
    }

    [Fact]
    public void AnalogMovementAndPressureUpdateLiveStateWithoutAddingHistoryOrRestartingTimers()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross }, 0);
        InputSnapshot before = state.Snapshot;
        for (int i = 1; i < 10000; i++)
            state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross, LeftX = (byte)i,
                LeftY = (byte)(i + 1), RightX = (byte)(i + 2), RightY = (byte)(i + 3),
                LeftTrigger = (byte)(i + 4), RightTrigger = (byte)(i + 5) }, i);
        Assert.Equal(before.History, state.Snapshot.History);
        Assert.Equal(0, state.Snapshot.LastActivity);
        Assert.NotEqual(before.Controls, state.Snapshot.Controls);
        Assert.False(state.Snapshot.IsAnimating(9999));
    }

    [Fact]
    public void TriggerPressesAndStickClicksAreHistoryEventsButPressureChangesAreNot()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.L2 | ControllerButtons.R2
            | ControllerButtons.L3 | ControllerButtons.R3, LeftTrigger = 20, RightTrigger = 30 }, 100);
        Assert.Equal(new[] { ControllerButtons.R3, ControllerButtons.L3, ControllerButtons.R2, ControllerButtons.L2 },
            state.Snapshot.History.Select(entry => entry.Code));
        state.Apply(state.Snapshot.Controls with { LeftTrigger = 255, RightTrigger = 255 }, 200);
        state.Apply(ControllerReport.Neutral, 300);
        Assert.Equal(4, state.Snapshot.History.Length);
        Assert.Equal(100, state.Snapshot.LastActivity);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
    }

    [Fact]
    public void IdenticalReportsDoNotPublishChangesEvenWhileHeld()
    {
        var store = new InputStore();
        int changes = 0;
        store.Changed += () => changes++;
        var held = ControllerReport.Neutral with { Buttons = ControllerButtons.Square };
        store.Report(held, 1);
        for (int i = 0; i < 100000; i++) store.Report(held, i + 2);
        Assert.Equal(1, changes);
        Assert.Equal(1, store.Snapshot().Version);
        Assert.Single(store.Snapshot().History);
    }

    [Fact]
    public void RestingStickDriftStaysCenteredWithoutPublishingChangesOrRestartingHistory()
    {
        var store = new InputStore();
        int changes = 0;
        store.Changed += () => changes++;
        store.Report(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross }, 0);
        InputSnapshot before = store.Snapshot();
        (byte X, byte Y)[] drift = [(129, 125), (140, 128), (116, 128), (136, 136), (120, 120)];
        foreach (var (x, y) in drift)
            store.Report(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross,
                LeftX = x, LeftY = y, RightX = y, RightY = x }, 100);
        Assert.Equal(1, changes);
        Assert.Equal(before, store.Snapshot());
    }

    [Fact]
    public void StickDeadzoneIsCircularAndKeepsMovementBeyondItsEdgeUnchanged()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { LeftX = 136, LeftY = 136 }, 0);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
        // Ten raw counts on both axes lie outside the 10% circle; each axis alone is inside.
        var diagonal = ControllerReport.Neutral with { LeftX = 138, LeftY = 138, RightX = 115, RightY = 128 };
        state.Apply(diagonal, 10);
        Assert.Equal(diagonal, state.Snapshot.Controls);
        var fullTravel = ControllerReport.Neutral with { LeftX = 0, LeftY = 255, RightX = 255, RightY = 0 };
        state.Apply(fullTravel, 20);
        Assert.Equal(fullTravel, state.Snapshot.Controls);
        state.Apply(ControllerReport.Neutral with { LeftX = 129, LeftY = 125, RightX = 129 }, 30);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
        Assert.Empty(state.Snapshot.History);
    }

    [Fact]
    public void StickDeadzonePreservesStickClicksAndTriggerPressure()
    {
        var state = new InputState(1000);
        var controls = ControllerReport.Neutral with { Buttons = ControllerButtons.L3 | ControllerButtons.R3 | ControllerButtons.L2,
            LeftTrigger = 4, RightTrigger = 2 };
        state.Apply(controls with { LeftX = 129, LeftY = 125, RightX = 129 }, 10);
        Assert.Equal(controls, state.Snapshot.Controls);
        Assert.Equal(new[] { ControllerButtons.R3, ControllerButtons.L3, ControllerButtons.L2 },
            state.Snapshot.History.Select(entry => entry.Code));
    }

    [Fact]
    public void ChordsReleaseIndependentlyAndRepeatedTapsStayNewestFirst()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross | ControllerButtons.L1 }, 10);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.L1 }, 20);
        Assert.False(state.Snapshot.IsDown(ControllerButtons.Cross));
        Assert.True(state.Snapshot.IsDown(ControllerButtons.L1));
        for (int i = 0; i < 15; i++)
        {
            state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross }, 100 + i * 10);
            state.Apply(ControllerReport.Neutral, 101 + i * 10);
        }
        Assert.Equal(10, state.Snapshot.History.Length);
        Assert.Equal(Enumerable.Range(5, 10).Reverse().Select(i => 100L + i * 10),
            state.Snapshot.History.Select(entry => entry.Timestamp));
        Assert.All(state.Snapshot.History, entry => Assert.Equal(ControllerButtons.Cross, entry.Code));
        Assert.Equal(ControllerButtons.None, state.Snapshot.Controls.Buttons);
    }

    [Fact]
    public void DisconnectClearsControlsKeepsHistoryAndAllowsReconnect()
    {
        var state = new InputState(1000);
        var report = ControllerReport.Neutral with { Buttons = ControllerButtons.Touchpad, LeftX = 0, LeftTrigger = 255 };
        state.Apply(report, 10);
        InputSnapshot before = state.Snapshot;
        Assert.True(state.Disconnect(20));
        Assert.False(state.Snapshot.Connected);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
        Assert.Equal(before.History, state.Snapshot.History);
        Assert.False(state.Disconnect(30));
        Assert.True(state.Apply(report, 40));
        Assert.True(state.Snapshot.Connected);
        Assert.Equal(2, state.Snapshot.History.Length);
    }

    [Fact]
    public void ResetClearsControlsAndHistoryWithoutMutatingOldSnapshot()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.PS }, 100);
        InputSnapshot before = state.Snapshot;
        state.Reset(200);
        Assert.Empty(state.Snapshot.History);
        Assert.Null(state.Snapshot.LastActivity);
        Assert.Equal(ControllerReport.Neutral, state.Snapshot.Controls);
        Assert.Single(before.History);
        Assert.True(before.IsDown(ControllerButtons.PS));
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.PS }, 300);
        Assert.Single(state.Snapshot.History);
        Assert.Equal(300, state.Snapshot.History[0].Timestamp);
    }

    [Fact]
    public void OnlyPhysicalNumpadZeroTogglesAndKeyboardNeverEntersHistory()
    {
        var store = new InputStore();
        int toggles = 0;
        store.ToggleVisibilityRequested += () => toggles++;
        store.Keyboard(1, 0x0b, 0, 0); // Top-row 0.
        store.Keyboard(1, 0x52, 2, 0); // Insert.
        store.Keyboard(1, 0x21, 0, 0); // F.
        Assert.Equal(0, toggles);
        store.Keyboard(1, 0x52, 0, 0);
        store.Keyboard(1, 0x52, 0, 1);
        Assert.Equal(1, toggles);
        store.Keyboard(1, 0x52, 1, 2);
        store.Keyboard(1, 0x52, 0, 3);
        Assert.Equal(2, toggles);
        store.RemoveDevice(1);
        store.Keyboard(1, 0x52, 0, 4);
        Assert.Equal(3, toggles);
        Assert.Empty(store.Snapshot().History);
        Assert.Equal(0, store.Snapshot().Version);
    }

    [Theory]
    [InlineData(0, 0, 1)] [InlineData(499, 499, 1)] [InlineData(500, 500, 1)]
    [InlineData(575, 500, 0.75)] [InlineData(650, 500, 0.5)] [InlineData(1000, 500, 0.5)]
    public void HistoryTimersCapAt500ThenDimAndRemainReadable(int elapsed, int milliseconds, double opacity)
    {
        var entry = new InputHistoryEntry(ControllerButtons.Cross, 1000);
        TimerVisual timer = entry.TimerAt(1000 + elapsed, 1000);
        Assert.Equal(milliseconds, timer.Milliseconds);
        Assert.Equal(opacity, timer.Opacity, 5);
        Assert.Equal(milliseconds / (1000d / 60), timer.FramesAt60, 5);
    }

    [Fact]
    public void NewPressesAndReleasesNeverResetEarlierTimers()
    {
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross }, 0);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross | ControllerButtons.L1 }, 300);
        state.Apply(ControllerReport.Neutral, 320);
        Assert.Equal(new TimerVisual(20, 1), state.Snapshot.History[0].TimerAt(320, 1000));
        Assert.Equal(new TimerVisual(320, 1), state.Snapshot.History[1].TimerAt(320, 1000));
        Assert.False(state.Snapshot.IsAnimating(950));
        Assert.Equal(2, state.Snapshot.History.Length);
    }
}
