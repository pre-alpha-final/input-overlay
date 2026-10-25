using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InputOverlayMnK.Input;

internal sealed class RawInputCapture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Exception? _startupError;
    private nint _windowHandle;
    private bool _disposed;

    public RawInputCapture(InputStore store, Diagnostics diagnostics, Action<Exception> failed)
    {
        _thread = new Thread(() => Run(store, diagnostics, failed))
        { IsBackground = true, Name = "Raw input capture" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            _thread.Join();
            _ready.Dispose();
            throw new InvalidOperationException("Could not start Raw Input capture.", _startupError);
        }
    }

    private void Run(InputStore store, Diagnostics diagnostics, Action<Exception> failed)
    {
        bool started = false;
        try
        {
            using var context = new ApplicationContext();
            using var window = new CaptureWindow(store, diagnostics, context.ExitThread);
            Volatile.Write(ref _windowHandle, window.Handle);
            started = true;
            _ready.Set();
            Application.Run(context);
        }
        catch (Exception exception)
        {
            if (!started) _startupError = exception;
            else failed(exception);
        }
        finally
        {
            Volatile.Write(ref _windowHandle, 0);
            if (!started) _ready.Set();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        nint handle = Volatile.Read(ref _windowHandle);
        if (handle != 0) Native.PostMessage(handle, Native.WmClose, 0, 0);
        _thread.Join();
        _ready.Dispose();
    }

    private sealed class CaptureWindow : NativeWindow, IDisposable
    {
        private readonly InputStore _store;
        private readonly Diagnostics _diagnostics;
        private readonly Action _quit;
        private nint _buffer;
        private uint _capacity = 256;
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf<Native.RawHeader>();

        public CaptureWindow(InputStore store, Diagnostics diagnostics, Action quit)
        {
            _store = store;
            _diagnostics = diagnostics;
            _quit = quit;
            try
            {
                _buffer = Marshal.AllocHGlobal((int)_capacity);
                CreateHandle(new CreateParams { Caption = "InputOverlayMnK.RawInput", Parent = Native.HwndMessage });
                Native.RawInputDevice[] devices =
                [
                    new() { UsagePage = 1, Usage = 6, Flags = Native.RidevInputSink | Native.RidevDevNotify, Target = Handle },
                    new() { UsagePage = 1, Usage = 2, Flags = Native.RidevInputSink | Native.RidevDevNotify, Target = Handle }
                ];
                if (!Native.RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<Native.RawInputDevice>()))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterRawInputDevices failed.");
            }
            catch { Dispose(); throw; }
        }

        protected override void WndProc(ref Message message)
        {
            // Timestamp before marshalling or acquiring the state lock.
            long timestamp = Stopwatch.GetTimestamp();
            switch (message.Msg)
            {
                case Native.WmInput: Read(message.LParam, timestamp); break;
                case Native.WmInputDeviceChange when message.WParam == 2:
                    _store.RemoveDevice(message.LParam, timestamp);
                    break;
                case Native.WmClose: _quit(); return;
            }
            // DefWindowProc must run for foreground WM_INPUT cleanup.
            base.WndProc(ref message);
        }

        private void Read(nint handle, long timestamp)
        {
            uint size = _capacity;
            uint read = Native.GetRawInputData(handle, Native.RidInput, _buffer, ref size, HeaderSize);
            if (read == uint.MaxValue && Marshal.GetLastWin32Error() == 122)
            {
                size = 0;
                if (Native.GetRawInputData(handle, Native.RidInput, 0, ref size, HeaderSize) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                _buffer = Marshal.ReAllocHGlobal(_buffer, (nint)size);
                _capacity = size;
                read = Native.GetRawInputData(handle, Native.RidInput, _buffer, ref size, HeaderSize);
            }
            if (read == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error(), "GetRawInputData failed.");
            if (read < HeaderSize) return;
            _diagnostics.PacketReceived();
            Native.RawHeader header = Marshal.PtrToStructure<Native.RawHeader>(_buffer);
            nint payload = _buffer + (int)HeaderSize;
            if (header.Type == 1 && read >= HeaderSize + Marshal.SizeOf<Native.RawKeyboard>())
            {
                Native.RawKeyboard keyboard = Marshal.PtrToStructure<Native.RawKeyboard>(payload);
                if (keyboard.VKey >= 0xff || keyboard.MakeCode == 0xff) return;
                if (keyboard.MakeCode == 0)
                {
                    uint mapped = Native.MapVirtualKey(keyboard.VKey, 4);
                    keyboard.MakeCode = (ushort)(mapped & 0xff);
                    if ((mapped & 0xff00) == 0xe000) keyboard.Flags |= 2;
                    if ((mapped & 0xff00) == 0xe100) keyboard.Flags |= 4;
                }
                _store.Keyboard(header.Device, keyboard.MakeCode, keyboard.Flags, timestamp);
            }
            else if (header.Type == 0 && read >= HeaderSize + Marshal.SizeOf<Native.RawMouse>())
            {
                Native.RawMouse mouse = Marshal.PtrToStructure<Native.RawMouse>(payload);
                _store.Mouse(header.Device, mouse.ButtonFlags, mouse.ButtonData, timestamp);
            }
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                Native.RawInputDevice[] devices =
                [
                    new() { UsagePage = 1, Usage = 6, Flags = Native.RidevRemove },
                    new() { UsagePage = 1, Usage = 2, Flags = Native.RidevRemove }
                ];
                Native.RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<Native.RawInputDevice>());
                DestroyHandle();
            }
            if (_buffer != 0) { Marshal.FreeHGlobal(_buffer); _buffer = 0; }
        }
    }
}
