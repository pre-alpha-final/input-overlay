using System.Diagnostics;

namespace InputOverlayXbox.Input;

internal sealed class InputStore
{
    private readonly object _gate = new();
    private readonly InputState _state = new(Stopwatch.Frequency);
    private readonly HashSet<nint> _toggleHeld = [];
    public event Action? Changed;
    public event Action? ToggleVisibilityRequested;
    public InputSnapshot Snapshot() { lock (_gate) return _state.Snapshot; }

    public void Report(ControllerReport report, long timestamp)
    {
        bool changed;
        lock (_gate) changed = _state.Apply(report, timestamp);
        if (changed) Changed?.Invoke();
    }

    public void Disconnect(long timestamp)
    {
        bool changed;
        lock (_gate) changed = _state.Disconnect(timestamp);
        if (changed) Changed?.Invoke();
    }

    public void Keyboard(nint device, ushort scanCode, ushort flags, long timestamp)
    {
        // Only physical Numpad 0, with either Num Lock setting. No keyboard history.
        if (scanCode != 0x52 || (flags & 6) != 0) return;
        bool toggle;
        lock (_gate)
        {
            if ((flags & 1) != 0) { _toggleHeld.Remove(device); return; }
            toggle = _toggleHeld.Add(device);
        }
        if (toggle) ToggleVisibilityRequested?.Invoke();
    }

    public void RemoveDevice(nint device) { lock (_gate) _toggleHeld.Remove(device); }

    public void Reset()
    {
        lock (_gate)
        {
            _state.Reset(Stopwatch.GetTimestamp());
            _toggleHeld.Clear();
        }
        Changed?.Invoke();
    }
}
