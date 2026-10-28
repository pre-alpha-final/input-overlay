using System.Collections.Immutable;

namespace InputOverlayXbox.Input;

internal readonly record struct TimerVisual(double ElapsedMilliseconds, float Opacity)
{
    public int Milliseconds => (int)ElapsedMilliseconds;
    // One-based time equivalent only; no game or recording frame counter is available.
    public double EstimatedFramesAt60 => 1 + ElapsedMilliseconds / (1000d / 60);
}

internal readonly record struct InputHistoryEntry(ControllerButtons Code, long Timestamp)
{
    public TimerVisual TimerAt(long now, long frequency)
    {
        double elapsed = Math.Max(0, (now - Timestamp) * 1000d / frequency);
        float opacity = elapsed <= 500 ? 1 : (float)(1 - Math.Min(150, elapsed - 500) / 300);
        return new(Math.Min(500, elapsed), opacity);
    }
}

internal readonly record struct InputSnapshot(ControllerReport Controls, bool Connected,
    long? LastActivity, long Frequency, long Version, long LastChange, ImmutableArray<InputHistoryEntry> History)
{
    public bool IsDown(ControllerButtons button) => Controls.IsDown(button);
    public bool IsAnimating(long now) => LastActivity is { } last
        && Math.Max(0, (now - last) * 1000d / Frequency) < 650;
}

// Serialized by InputStore; independent of Windows, rendering and wall-clock time.
internal sealed class InputState(long frequency)
{
    public const int HistoryLimit = 10;
    private ControllerReport _controls = ControllerReport.Neutral;
    private bool _connected;
    private long? _lastActivity;
    private long _version, _lastChange;
    private ImmutableArray<InputHistoryEntry> _history = [];
    public InputSnapshot Snapshot => new(_controls, _connected, _lastActivity, frequency, _version, _lastChange, _history);

    public bool Apply(ControllerReport report, long timestamp)
    {
        report = report.WithStickDeadzone();
        if (_connected && report == _controls) return false;
        ControllerButtons pressed = report.Buttons & ~_controls.Buttons;
        foreach (ControllerButtons button in InputCodes.All)
        {
            if ((pressed & button) == 0) continue;
            _history = _history.Insert(0, new(button, timestamp));
            if (_history.Length > HistoryLimit) _history = _history.RemoveAt(HistoryLimit);
            _lastActivity = timestamp;
        }
        _controls = report;
        _connected = true;
        Changed(timestamp);
        return true;
    }

    public bool Disconnect(long timestamp)
    {
        if (!_connected) return false;
        _controls = ControllerReport.Neutral;
        _connected = false;
        Changed(timestamp);
        return true;
    }

    public void Reset(long timestamp)
    {
        _controls = ControllerReport.Neutral;
        _history = [];
        _lastActivity = null;
        Changed(timestamp);
    }

    private void Changed(long timestamp) { _version++; _lastChange = timestamp; }
}
