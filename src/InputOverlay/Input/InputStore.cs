using System.Diagnostics;

namespace InputOverlay.Input;

internal sealed class InputStore
{
    private readonly object _gate = new();
    private readonly InputState _state = new(Stopwatch.Frequency);
    public event Action? Changed;

    public InputSnapshot Snapshot() { lock (_gate) return _state.Snapshot; }

    public void Keyboard(nint device, ushort scanCode, ushort flags, long timestamp)
    {
        bool changed;
        lock (_gate) changed = _state.Keyboard(device, scanCode, flags, timestamp);
        if (changed) Changed?.Invoke();
    }

    public void Mouse(nint device, ushort flags, short delta, long timestamp)
    {
        // Motion is overwhelmingly the most frequent packet; discard it before locking.
        if ((flags & 0x07ff) == 0) return;
        bool changed;
        lock (_gate) changed = _state.Mouse(device, flags, delta, timestamp);
        if (changed) Changed?.Invoke();
    }

    public void RemoveDevice(nint device, long timestamp)
    {
        bool changed;
        lock (_gate) changed = _state.RemoveDevice(device, timestamp);
        if (changed) Changed?.Invoke();
    }

    public void Reset()
    {
        lock (_gate) _state.Reset(Stopwatch.GetTimestamp());
        Changed?.Invoke();
    }
}
