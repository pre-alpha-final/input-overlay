using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace InputOverlayXbox.Overlay;

internal sealed class AnimationClock : IDisposable
{
    private readonly AutoResetEvent _changed = new(false);
    private readonly ManualResetEvent _stop = new(false);
    private readonly EventWaitHandle _timer;
    private readonly Thread _thread;
    private int _active, _refreshRate = 60, _syncToComposition;

    public AnimationClock(Action tick, Action<Exception> failed)
    {
        nint handle = Native.CreateWaitableTimerEx(0, null, 2, 0x001f0003);
        if (handle == 0) handle = Native.CreateWaitableTimerEx(0, null, 0, 0x001f0003);
        if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create animation timer.");
        _timer = new EventWaitHandle(false, EventResetMode.AutoReset);
        _timer.SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: true);
        _thread = new Thread(() =>
        {
            try
            {
                WaitHandle[] idle = [_stop, _changed];
                WaitHandle[] active = [_stop, _changed, _timer];
                long origin = 0;
                bool resetSchedule = true;
                while (true)
                {
                    if (Volatile.Read(ref _active) == 0)
                    {
                        if (WaitHandle.WaitAny(idle) == 0) break;
                        resetSchedule = true;
                        continue;
                    }
                    long now = Stopwatch.GetTimestamp();
                    if (resetSchedule)
                    {
                        origin = now;
                        resetSchedule = false;
                    }
                    int refreshRate = Volatile.Read(ref _refreshRate);
                    long deadline;
                    if (Volatile.Read(ref _syncToComposition) == 0
                        || !TryCompositionDeadline(refreshRate, out deadline))
                        deadline = NextFrameTimestamp(origin, now, Stopwatch.Frequency, refreshRate);
                    // Account for time spent querying DWM before arming the relative wait.
                    long due = TimerDueTime(Math.Max(1, deadline - Stopwatch.GetTimestamp()), Stopwatch.Frequency);
                    if (!Native.SetWaitableTimerEx(handle, in due, 0, 0, 0, 0, 0))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not arm animation timer.");
                    int signaled = WaitHandle.WaitAny(active);
                    if (signaled == 0) break;
                    if (signaled == 1)
                    {
                        resetSchedule = true;
                        continue;
                    }
                    if (signaled == 2 && Volatile.Read(ref _active) != 0) tick();
                }
            }
            catch (Exception exception) { failed(exception); }
        }) { IsBackground = true, Name = "Overlay animation clock" };
        _thread.Start();
    }

    // Choose the next future deadline, skipping missed ticks instead of issuing a burst.
    // Integer arithmetic preserves fractional periods at rates such as 60 and 144 Hz.
    internal static long NextFrameTimestamp(long origin, long now, long frequency, int refreshRate)
    {
        Int128 elapsed = Math.Max(0, now - origin);
        Int128 frame = elapsed * refreshRate / frequency + 1;
        return checked(origin + (long)((frame * frequency + refreshRate - 1) / refreshRate));
    }

    internal static long TimerDueTime(long remaining, long frequency) =>
        -Math.Max(1, (long)(((Int128)remaining * TimeSpan.TicksPerSecond + frequency - 1) / frequency));

    private static bool TryCompositionDeadline(int refreshRate, out long deadline)
    {
        deadline = 0;
        var timing = new Native.CompositionTiming { Size = (uint)Marshal.SizeOf<Native.CompositionTiming>() };
        // Windows 8.1+ requires a null HWND. These are desktop-wide statistics,
        // so OverlayWindow enables this path only on the primary display.
        if (Native.DwmGetCompositionTimingInfo(0, ref timing) != 0
            || timing.RefreshPeriod == 0 || timing.RefreshPeriod > (ulong)Stopwatch.Frequency
            || timing.VBlank > long.MaxValue) return false;
        long now = Stopwatch.GetTimestamp();
        long period = (long)timing.RefreshPeriod;
        double expected = Stopwatch.Frequency / (double)refreshRate;
        if (Math.Abs(period - expected) > expected * 0.01
            || Math.Abs((double)timing.VBlank - now) > Stopwatch.Frequency) return false;
        deadline = NextCompositionTimestamp((long)timing.VBlank, period, now);
        return true;
    }

    internal static long NextCompositionTimestamp(long vblank, long period, long now)
    {
        // Draw at a stable phase after vblank, away from the composition boundary.
        // The result works whether DWM reports a past or upcoming vblank.
        Int128 phase = (Int128)vblank + period / 4;
        Int128 elapsed = (Int128)now - phase;
        Int128 cycle = elapsed >= 0 ? elapsed / period + 1 : -((-elapsed - 1) / period);
        return checked((long)(phase + cycle * period));
    }

    public void SetActive(bool active, int refreshRate, bool syncToComposition = false)
    {
        int rate = Math.Clamp(refreshRate, 1, 1000);
        int previousRate = Interlocked.Exchange(ref _refreshRate, rate);
        int previousSync = Interlocked.Exchange(ref _syncToComposition, syncToComposition ? 1 : 0);
        int previousActive = Interlocked.Exchange(ref _active, active ? 1 : 0);
        if (previousActive != (active ? 1 : 0) || previousRate != rate
            || previousSync != (syncToComposition ? 1 : 0)) _changed.Set();
    }

    public void Dispose()
    {
        _stop.Set();
        _thread.Join();
        _timer.Dispose();
        _changed.Dispose();
        _stop.Dispose();
    }
}
