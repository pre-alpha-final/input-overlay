using System.Runtime.InteropServices;

namespace InputOverlayXbox.Input;

// Small x64 binding to Microsoft's versioned v4 interfaces. Vtable indices include IUnknown.
// ABI: https://github.com/microsoftconnect/GameInput/blob/main/include/v4/GameInput.h
internal sealed unsafe class GameInputController : IControllerInput
{
    private const uint GamepadKind = 0x00040000;
    private const uint SystemButtonKind = 0x00200000;
    private readonly object _gate = new();
    private readonly HashSet<nint> _devices = [];
    private readonly ReadingCallback _readingCallback;
    private readonly DeviceCallback _deviceCallback;
    private nint _input;
    private ulong _readingToken, _deviceToken;
    private int _disposed;
    private Action<nint, bool> _deviceChanged = null!;
    private Action<nint, GamepadState> _reading = null!;
    private Action<nint, uint> _systemButtons = null!;
    private Action<Exception> _failed = null!;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ReadingCallback(ulong token, nint context, nint reading);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DeviceCallback(ulong token, nint context, int deviceEvent, nint device);

    [DllImport("GameInputRedist.dll", CallingConvention = CallingConvention.StdCall)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    private static extern int GameInputInitialize(in Guid iid, out nint input);

    public GameInputController()
    {
        _readingCallback = OnReading;
        _deviceCallback = OnDevice;
        // Pin v4 explicitly; later GameInput versions have different interface layouts.
        Guid iid = new("F2DAAE4C-4D21-4250-8953-4F791E6279AB");
        Marshal.ThrowExceptionForHR(GameInputInitialize(in iid, out _input));
        ((delegate* unmanaged[Stdcall]<nint, byte, void>)Table(_input)[13])(_input, 1);
    }

    public void Start(Action<nint, bool> deviceChanged, Action<nint, GamepadState> reading,
        Action<nint, uint> systemButtons, Action<Exception> failed)
    {
        _deviceChanged = deviceChanged; _reading = reading; _systemButtons = systemButtons; _failed = failed;
        ulong token = 0;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, uint, nint, nint, ulong*, int>)Table(_input)[7])
            (_input, 0, GamepadKind | SystemButtonKind, 0, Marshal.GetFunctionPointerForDelegate(_readingCallback), &token));
        _readingToken = token;
        // v4 enumerates already-connected devices and subsequent changes asynchronously.
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, uint, nint, nint, ulong*, int>)Table(_input)[8])
            (_input, 0, GamepadKind, 0, Marshal.GetFunctionPointerForDelegate(_deviceCallback), &token));
        _deviceToken = token;
    }

    private void OnDevice(ulong token, nint context, int deviceEvent, nint device)
    {
        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                if (deviceEvent == 0)
                {
                    // Only Microsoft Xbox gamepads; do not select a DualSense mapped as a generic gamepad.
                    if (((delegate* unmanaged[Stdcall]<nint, ushort>)Table(device)[3])(device) != 0x045e
                        || !_devices.Add(device)) return;
                    Marshal.AddRef(device);
                    _deviceChanged(device, true);
                    // Fetch each kind separately so a controller held at startup is visible immediately.
                    foreach (uint kind in new[] { GamepadKind, SystemButtonKind })
                    {
                        nint reading = 0;
                        if (((delegate* unmanaged[Stdcall]<nint, uint, nint, nint*, int>)Table(_input)[4])
                            (_input, kind, device, &reading) < 0) continue;
                        try { Read(device, reading); }
                        finally { Marshal.Release(reading); }
                    }
                }
                else if (deviceEvent == 1 && _devices.Remove(device))
                {
                    try { _deviceChanged(device, false); }
                    finally { Marshal.Release(device); }
                }
            }
        }
        catch (Exception exception) { _failed(exception); }
    }

    private void OnReading(ulong token, nint context, nint reading)
    {
        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                nint device = 0;
                ((delegate* unmanaged[Stdcall]<nint, nint*, void>)Table(reading)[5])(reading, &device);
                try { if (_devices.Contains(device)) Read(device, reading); }
                finally { if (device != 0) Marshal.Release(device); }
            }
        }
        catch (Exception exception) { _failed(exception); }
    }

    private void Read(nint device, nint reading)
    {
        GamepadState state;
        if (((delegate* unmanaged[Stdcall]<nint, GamepadState*, byte>)Table(reading)[18])(reading, &state) != 0)
            _reading(device, state);
        uint systemButtons;
        if (((delegate* unmanaged[Stdcall]<nint, uint*, byte>)Table(reading)[21])(reading, &systemButtons) != 0)
            _systemButtons(device, systemButtons);
    }

    private static nint* Table(nint instance) => *(nint**)instance;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (ulong token in new[] { _deviceToken, _readingToken })
            if (token != 0) ((delegate* unmanaged[Stdcall]<nint, ulong, byte>)Table(_input)[9])(_input, token);
        lock (_gate)
        {
            foreach (nint device in _devices) Marshal.Release(device);
            _devices.Clear();
            if (_input != 0) { Marshal.Release(_input); _input = 0; }
        }
        GC.KeepAlive(_readingCallback); GC.KeepAlive(_deviceCallback);
    }
}
