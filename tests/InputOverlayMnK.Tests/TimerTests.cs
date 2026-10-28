using InputOverlayMnK.Input;

namespace InputOverlayMnK.Tests;

public sealed class TimerTests
{
    [Fact]
    public void HistoryStartsEmptyAndIdle()
    {
        var state = new InputState(1000);
        Assert.Empty(state.Snapshot.History);
        Assert.False(state.Snapshot.IsAnimating(10));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(499, 499, 1)]
    [InlineData(500, 500, 1)]
    [InlineData(575, 500, 0.75)]
    [InlineData(649, 500, 0.5 + 1d / 300)]
    [InlineData(650, 500, 0.5)]
    [InlineData(1000, 500, 0.5)]
    public void EachTimerCapsAt500ThenDimsButRemainsReadable(int elapsed, int milliseconds, double opacity)
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 1000);
        InputSnapshot snapshot = state.Snapshot;
        TimerVisual timer = Assert.Single(snapshot.History).TimerAt(1000 + elapsed, snapshot.Frequency);
        Assert.Equal(milliseconds, timer.Milliseconds);
        Assert.Equal(opacity, timer.Opacity, precision: 5);
        Assert.Equal(elapsed < 650, snapshot.IsAnimating(1000 + elapsed));
    }

    [Fact]
    public void NewInputAndReleasesDoNotResetEarlierTimers()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 0);
        state.Mouse(2, 1, 0, 300);
        state.Mouse(2, 2, 0, 320);
        InputSnapshot snapshot = state.Snapshot;
        Assert.Equal(2, snapshot.History.Length);
        Assert.Equal(new TimerVisual(20, 1), snapshot.History[0].TimerAt(320, snapshot.Frequency));
        Assert.Equal(new TimerVisual(320, 1), snapshot.History[1].TimerAt(320, snapshot.Frequency));
        state.Mouse(2, 0x0400, 120, 600);
        snapshot = state.Snapshot;
        Assert.Equal(new TimerVisual(0, 1), snapshot.History[0].TimerAt(600, snapshot.Frequency));
        Assert.Equal(new TimerVisual(300, 1), snapshot.History[1].TimerAt(600, snapshot.Frequency));
        Assert.Equal(new TimerVisual(500, 2f / 3), snapshot.History[2].TimerAt(600, snapshot.Frequency));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1.06)]
    [InlineData(16, 1.96)]
    [InlineData(17, 2.02)]
    [InlineData(25, 2.5)]
    [InlineData(100, 7)]
    [InlineData(250, 16)]
    [InlineData(499, 30.94)]
    [InlineData(500, 31)]
    public void EstimatedFrameReadoutStartsAtOneAndAdvancesAt60Fps(int milliseconds, double frames)
    {
        var timer = new TimerVisual(milliseconds, 1);
        Assert.Equal(frames, timer.EstimatedFramesAt60, precision: 10);
    }

    [Fact]
    public void TimingUsesActualStopwatchFrequencyAndTruncatesSubMilliseconds()
    {
        var state = new InputState(10_000_000);
        state.Keyboard(1, 0x21, 0, 123_000_000);
        InputSnapshot snapshot = state.Snapshot;
        InputHistoryEntry entry = Assert.Single(snapshot.History);
        TimerVisual timer = entry.TimerAt(124_239_999, snapshot.Frequency);
        Assert.Equal(123, timer.Milliseconds);
        Assert.Equal(123.9999, timer.ElapsedMilliseconds, precision: 6);
        Assert.Equal(8.439994, timer.EstimatedFramesAt60, precision: 6);
        Assert.Equal(new TimerVisual(0, 1), entry.TimerAt(122_999_999, snapshot.Frequency));
        Assert.False(snapshot.IsAnimating(129_500_000));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(166_666, 16, 1.999996)]
    [InlineData(166_667, 16, 2.000002)]
    [InlineData(1_239_999, 123, 8.439994)]
    [InlineData(5_000_001, 500, 31)]
    public void EstimatedFramesPreserveSubMillisecondTiming(long ticks, int milliseconds, double frames)
    {
        var entry = new InputHistoryEntry(0x21, 10_000_000);
        TimerVisual timer = entry.TimerAt(10_000_000 + ticks, 10_000_000);
        Assert.Equal(milliseconds, timer.Milliseconds);
        Assert.Equal(frames, timer.EstimatedFramesAt60, precision: 6);
    }
}
