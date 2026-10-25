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
    [InlineData(0, 0)]
    [InlineData(1, 0.06)]
    [InlineData(16, 0.96)]
    [InlineData(17, 1.02)]
    [InlineData(25, 1.5)]
    [InlineData(100, 6)]
    [InlineData(250, 15)]
    [InlineData(499, 29.94)]
    [InlineData(500, 30)]
    public void FrameReadoutDividesMillisecondsByThe60FpsFrameDuration(int milliseconds, double frames)
    {
        var timer = new TimerVisual(milliseconds, 1);
        Assert.Equal(frames, timer.FramesAt60, precision: 10);
    }

    [Fact]
    public void TimingUsesActualStopwatchFrequencyAndTruncatesSubMilliseconds()
    {
        var state = new InputState(10_000_000);
        state.Keyboard(1, 0x21, 0, 123_000_000);
        InputSnapshot snapshot = state.Snapshot;
        InputHistoryEntry entry = Assert.Single(snapshot.History);
        Assert.Equal(new TimerVisual(123, 1), entry.TimerAt(124_239_999, snapshot.Frequency));
        Assert.Equal(new TimerVisual(0, 1), entry.TimerAt(122_999_999, snapshot.Frequency));
        Assert.False(snapshot.IsAnimating(129_500_000));
    }
}
