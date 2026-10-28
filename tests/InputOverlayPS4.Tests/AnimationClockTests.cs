using InputOverlayPS4.Overlay;

namespace InputOverlayPS4.Tests;

public sealed class AnimationClockTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    [InlineData(1000)]
    public void RepeatedLateWakeupsDoNotAccumulateDrift(int refreshRate)
    {
        const long frequency = 10_000_003, origin = 200_000_000;
        long now = origin, deadline = origin;
        for (int frame = 0; frame < refreshRate * 10; frame++)
        {
            deadline = AnimationClock.NextFrameTimestamp(origin, now, frequency, refreshRate);
            Assert.True(deadline > now);
            now = deadline + 4_000; // 0.4 ms late on every wake-up.
        }
        Assert.Equal(origin + frequency * 10, deadline);
    }

    [Fact]
    public void AStallSkipsMissedTicksAndReturnsToTheOriginalSchedule()
    {
        const long origin = 1_000_000, frequency = 1_000_000;
        long next = AnimationClock.NextFrameTimestamp(origin, origin + 40_000, frequency, 120);
        Assert.Equal(origin + 41_667, next);
        Assert.Equal(origin + 50_000,
            AnimationClock.NextFrameTimestamp(origin, next + 400, frequency, 120));
    }

    [Fact]
    public void RestartingUsesAFreshOriginInsteadOfCatchingUpAfterIdle()
    {
        const long frequency = 1_000_000, resumed = 90_000_000;
        Assert.Equal(resumed + 16_667,
            AnimationClock.NextFrameTimestamp(resumed, resumed, frequency, 60));
    }

    [Theory]
    [InlineData(1, 3_000_000_000, -1)]
    [InlineData(301, 3_000_000_000, -2)]
    [InlineData(166_667, 10_000_000, -166_667)]
    public void NativeWaitRoundsUpToAvoidWakingBeforeTheDeadline(long remaining, long frequency, long due)
    {
        Assert.Equal(due, AnimationClock.TimerDueTime(remaining, frequency));
    }

    [Theory]
    [InlineData(990_000, 994_000)]
    [InlineData(998_000, 1_002_000)]
    [InlineData(1_002_000, 1_010_000)]
    [InlineData(1_002_400, 1_010_000)]
    [InlineData(1_030_000, 1_034_000)]
    public void CompositionDeadlinesFollowTheReportedRefreshPhase(long now, long expected)
    {
        Assert.Equal(expected, AnimationClock.NextCompositionTimestamp(1_000_000, 8_000, now));
    }

    [Fact]
    public void CompositionPacingKeepsTheActualPeriodInsteadOfRoundingToIntegerHertz()
    {
        const long vblank = 1_000_000, period = 83_332;
        long first = AnimationClock.NextCompositionTimestamp(vblank, period, vblank);
        long next = AnimationClock.NextCompositionTimestamp(vblank, period, first + 4_000);
        Assert.Equal(period, next - first);
    }
}
