using System.Diagnostics;

namespace InputOverlayXbox.Input;

internal interface IControllerInput : IDisposable
{
    void Start(Action<nint, bool> deviceChanged, Action<nint, GamepadState> reading,
        Action<nint, uint> systemButtons, Action<Exception> failed);
}

internal sealed class ControllerCapture : IDisposable
{
    private readonly object _gate = new();
    private readonly IControllerInput _input;
    private readonly InputStore _store;
    private readonly Diagnostics _diagnostics;
    private readonly Dictionary<nint, (GamepadState State, uint SystemButtons)> _devices = [];
    private nint _selected;
    private bool _disposed;

    public ControllerCapture(InputStore store, Diagnostics diagnostics, Action<Exception> failed)
        : this(store, diagnostics, failed, new GameInputController()) { }

    internal ControllerCapture(InputStore store, Diagnostics diagnostics, Action<Exception> failed, IControllerInput input)
    {
        _store = store;
        _diagnostics = diagnostics;
        _input = input;
        try { _input.Start(DeviceChanged, Reading, SystemButtons, failed); }
        catch { Dispose(); throw; }
    }

    private void DeviceChanged(nint device, bool connected)
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_disposed) return;
            if (connected)
            {
                _devices.TryAdd(device, default);
                if (_selected == 0) _selected = device;
            }
            else
            {
                _devices.Remove(device);
                if (_selected != device) return;
                _store.Disconnect(timestamp);
                _selected = _devices.Keys.FirstOrDefault();
                if (_selected != 0) Publish(timestamp);
            }
        }
    }

    private void Reading(nint device, GamepadState state)
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_disposed || !_devices.TryGetValue(device, out var previous)) return;
            _devices[device] = (state, previous.SystemButtons);
            if (_selected == device) { _diagnostics.PacketReceived(); Publish(timestamp); }
        }
    }

    private void SystemButtons(nint device, uint buttons)
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_disposed || !_devices.TryGetValue(device, out var previous)) return;
            _devices[device] = (previous.State, buttons);
            if (_selected == device) Publish(timestamp);
        }
    }

    private void Publish(long timestamp)
    {
        var current = _devices[_selected];
        _store.Report(current.State.ToReport(current.SystemButtons), timestamp);
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        // Unregister outside the lock: GameInput waits for in-flight callbacks to finish.
        _input.Dispose();
        lock (_gate)
        {
            _devices.Clear();
            _selected = 0;
            _store.Disconnect(Stopwatch.GetTimestamp());
        }
    }
}
