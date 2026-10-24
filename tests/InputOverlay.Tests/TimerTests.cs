using InputOverlay.Input;

namespace InputOverlay.Tests;

public sealed class TimerTests
{
    [Fact]
    public void TimerStartsHidden() => Assert.False(new InputState(1000).Snapshot.TimerAt(10).Visible);

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(499, 499, 1)]
    [InlineData(500, 500, 1)]
    [InlineData(575, 500, 0.5)]
    [InlineData(649, 500, 1d / 150)]
    [InlineData(650, 0, 0)]
    [InlineData(1000, 0, 0)]
    public void TimerCapsAt500ThenFadesFor150Milliseconds(int elapsed, int milliseconds, double opacity)
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 1000);
        TimerVisual timer = state.Snapshot.TimerAt(1000 + elapsed);
        Assert.Equal(milliseconds, timer.Milliseconds);
        Assert.Equal(opacity, timer.Opacity, precision: 5);
    }

    [Fact]
    public void NewPressDuringFadeImmediatelyRestartsAtFullOpacity()
    {
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 0);
        Assert.True(state.Snapshot.TimerAt(600).Opacity < 1);
        state.Mouse(2, 1, 0, 600);
        Assert.Equal(new TimerVisual(0, 1), state.Snapshot.TimerAt(600));
        state.Mouse(2, 2, 0, 620);
        Assert.Equal(new TimerVisual(20, 1), state.Snapshot.TimerAt(620));
    }

    [Fact]
    public void TimingUsesActualStopwatchFrequencyAndTruncatesSubMilliseconds()
    {
        var state = new InputState(10_000_000);
        state.Keyboard(1, 0x21, 0, 123_000_000);
        Assert.Equal(new TimerVisual(123, 1), state.Snapshot.TimerAt(124_239_999));
        Assert.False(state.Snapshot.IsAnimating(129_500_000));
    }
}
