using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace InputOverlay.Overlay;

internal sealed class AnimationClock : IDisposable
{
    private readonly AutoResetEvent _changed = new(false);
    private readonly ManualResetEvent _stop = new(false);
    private readonly EventWaitHandle _timer;
    private readonly Thread _thread;
    private int _active, _refreshRate = 60;

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
                while (true)
                {
                    if (Volatile.Read(ref _active) == 0)
                    {
                        if (WaitHandle.WaitAny(idle) == 0) break;
                        continue;
                    }
                    long due = -Math.Max(1, TimeSpan.TicksPerSecond / Volatile.Read(ref _refreshRate));
                    if (!Native.SetWaitableTimerEx(handle, in due, 0, 0, 0, 0, 0))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not arm animation timer.");
                    int signaled = WaitHandle.WaitAny(active);
                    if (signaled == 0) break;
                    if (signaled == 2 && Volatile.Read(ref _active) != 0) tick();
                }
            }
            catch (Exception exception) { failed(exception); }
        }) { IsBackground = true, Name = "Overlay animation clock" };
        _thread.Start();
    }

    public void SetActive(bool active, int refreshRate)
    {
        int rate = Math.Clamp(refreshRate, 1, 1000);
        int previousRate = Interlocked.Exchange(ref _refreshRate, rate);
        int previousActive = Interlocked.Exchange(ref _active, active ? 1 : 0);
        if (previousActive != (active ? 1 : 0) || previousRate != rate) _changed.Set();
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
