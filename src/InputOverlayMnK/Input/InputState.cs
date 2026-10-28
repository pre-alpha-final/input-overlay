using System.Collections.Immutable;

namespace InputOverlayMnK.Input;

internal readonly record struct TimerVisual(double ElapsedMilliseconds, float Opacity)
{
    public bool Visible => Opacity > 0;
    public int Milliseconds => (int)ElapsedMilliseconds;
    // One-based time equivalent only; no game or recording frame counter is available.
    public double EstimatedFramesAt60 => 1 + ElapsedMilliseconds / (1000d / 60);
}

internal readonly record struct InputHistoryEntry(int Code, long Timestamp)
{
    public TimerVisual TimerAt(long now, long frequency)
    {
        double elapsed = Math.Max(0, (now - Timestamp) * 1000d / frequency);
        // Completed timers remain readable until the entry leaves the history.
        float opacity = elapsed <= 500 ? 1 : (float)(1 - Math.Min(150, elapsed - 500) / 300);
        return new(Math.Min(500, elapsed), opacity);
    }
}

internal readonly record struct InputSnapshot(
    ulong Pressed, long? LastActivity, long? ScrollUp, long? ScrollDown,
    long Frequency, long Version, long LastChange, ImmutableArray<InputHistoryEntry> History)
{
    public bool IsDown(int code) => (Pressed & InputCodes.Mask(code)) != 0;
    private double Elapsed(long now, long then) => Math.Max(0, (now - then) * 1000d / Frequency);

    public bool ScrollUpAt(long now) => ScrollUp is { } last && Elapsed(now, last) < 80;
    public bool ScrollDownAt(long now) => ScrollDown is { } last && Elapsed(now, last) < 80;
    public bool IsAnimating(long now) => (LastActivity is { } last && Elapsed(now, last) < 650)
        || ScrollUpAt(now) || ScrollDownAt(now);
}

// Serialized by InputStore; never calls Windows APIs or performs rendering.
internal sealed class InputState(long frequency)
{
    public const int HistoryLimit = 10;
    private static readonly int[] MouseButtons =
        [InputCodes.MouseLeft, InputCodes.MouseRight, InputCodes.MouseMiddle, InputCodes.MouseBack, InputCodes.MouseForward];
    private readonly Dictionary<nint, HashSet<int>> _devices = [];
    private readonly Dictionary<int, int> _heldCounts = [];
    private ImmutableArray<InputHistoryEntry> _history = [];
    private ulong _pressed;
    private long? _lastActivity, _scrollUp, _scrollDown;
    private long _version, _lastChange;

    public InputSnapshot Snapshot => new(_pressed, _lastActivity, _scrollUp, _scrollDown,
        frequency, _version, _lastChange, _history);

    public bool Keyboard(nint device, ushort makeCode, ushort flags, long timestamp)
    {
        if (makeCode is 0 or 0xff) return false;
        bool changed = Button(device, InputCodes.Keyboard(makeCode, flags), (flags & 1) == 0, timestamp);
        if (changed) Changed(timestamp);
        return changed;
    }

    public bool Mouse(nint device, ushort flags, short wheelDelta, long timestamp)
    {
        bool changed = false;
        for (int index = 0; index < MouseButtons.Length; index++)
        {
            if ((flags & (1 << (index * 2))) != 0)
                changed |= Button(device, MouseButtons[index], true, timestamp);
            if ((flags & (2 << (index * 2))) != 0)
                changed |= Button(device, MouseButtons[index], false, timestamp);
        }
        if ((flags & 0x0400) != 0 && wheelDelta != 0)
        {
            if (wheelDelta > 0) _scrollUp = timestamp;
            else _scrollDown = timestamp;
            Record(wheelDelta > 0 ? InputCodes.ScrollUp : InputCodes.ScrollDown, timestamp);
            changed = true;
        }
        if (changed) Changed(timestamp);
        return changed;
    }

    private bool Button(nint device, int code, bool down, long timestamp)
    {
        if (!_devices.TryGetValue(device, out HashSet<int>? held))
        {
            if (!down) return false;
            _devices[device] = held = [];
        }
        if (down)
        {
            if (!held.Add(code)) return false;
            _heldCounts[code] = _heldCounts.GetValueOrDefault(code) + 1;
            _pressed |= InputCodes.Mask(code);
            Record(code, timestamp);
        }
        else
        {
            if (!held.Remove(code)) return false;
            Release(code);
        }
        return true;
    }

    private void Record(int code, long timestamp)
    {
        _lastActivity = timestamp;
        _history = _history.Insert(0, new InputHistoryEntry(code, timestamp));
        if (_history.Length > HistoryLimit) _history = _history.RemoveAt(HistoryLimit);
    }

    private void Release(int code)
    {
        int remaining = _heldCounts[code] - 1;
        if (remaining == 0)
        {
            _heldCounts.Remove(code);
            _pressed &= ~InputCodes.Mask(code);
        }
        else _heldCounts[code] = remaining;
    }

    public bool RemoveDevice(nint device, long timestamp)
    {
        if (!_devices.Remove(device, out HashSet<int>? held)) return false;
        foreach (int code in held) Release(code);
        if (held.Count == 0) return false;
        Changed(timestamp);
        return true;
    }

    public void Reset(long timestamp)
    {
        _devices.Clear();
        _heldCounts.Clear();
        _pressed = 0;
        _history = [];
        _lastActivity = _scrollUp = _scrollDown = null;
        Changed(timestamp);
    }

    private void Changed(long timestamp) { _version++; _lastChange = timestamp; }
}
