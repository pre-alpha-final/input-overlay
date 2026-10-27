using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using InputOverlayPS5.Input;

namespace InputOverlayPS5.Overlay;

internal sealed class OverlayWindow : Form
{
    private const int RenderMessage = 0x8001, ToggleVisibilityMessage = 0x8002;
    private readonly Options _options;
    private readonly InputStore _store;
    private readonly Diagnostics _diagnostics;
    private readonly Action<Exception> _failed;
    private AnimationClock? _clock;
    private LayeredSurface? _surface;
    private OverlayRenderer? _renderer;
    private nint _windowHandle;
    private int _renderQueued, _visible;
    private int _refreshRate = 60;
    private bool _configuring, _sessionRegistered, _resourcesDisposed;
    private string? _monitorName;
    private Exception? _initializationError;
    private bool _initialized;

    public OverlayWindow(Options options, InputStore store, Diagnostics diagnostics, Action<Exception> failed)
    {
        _options = options;
        _store = store;
        _diagnostics = diagnostics;
        _failed = failed;
        Text = "InputOverlayPS5";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        // Keep topmost positioning native: Form.TopMost makes WinForms restore focus
        // during Show(), even when ShowWithoutActivation is true.
        try
        {
            _clock = new AnimationClock(RequestRender, failed);
            _store.Changed += RequestRender;
            _store.ToggleVisibilityRequested += RequestToggleVisibility;
            _ = Handle;
            if (_initializationError != null) throw new InvalidOperationException("Could not initialize overlay.", _initializationError);
            _initialized = true;
        }
        catch { Dispose(); throw; }
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= Native.WsExLayered | Native.WsExTransparent | Native.WsExNoActivate
                | Native.WsExTopmost;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Volatile.Write(ref _windowHandle, Handle);
        _sessionRegistered = Native.WTSRegisterSessionNotification(Handle, 0);
        if (!_sessionRegistered)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register session notifications.");
        ConfigureDisplay();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Volatile.Write(ref _windowHandle, 0);
        if (_sessionRegistered) Native.WTSUnRegisterSessionNotification(Handle);
        _sessionRegistered = false;
        base.OnHandleDestroyed(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Volatile.Write(ref _visible, 1);
        RequestRender();
    }

    private void ConfigureDisplay()
    {
        if (_configuring || !IsHandleCreated) return;
        _configuring = true;
        try
        {
            Screen[] screens = DisplayInfo.Screens();
            if (_monitorName == null && _options.Monitor is { } index)
            {
                if (index > screens.Length) throw new ArgumentException($"Monitor {index} does not exist. Use --list-monitors.");
                _monitorName = screens[index - 1].DeviceName;
            }
            Screen screen = screens.FirstOrDefault(item => item.DeviceName == _monitorName)
                ?? screens.First(item => item.Primary);
            Rectangle bounds = screen.Bounds;
            float dpi = DisplayInfo.DpiScale(screen);
            float scale = dpi * _options.Scale;
            // Keep even an explicitly enlarged overlay within the selected display.
            scale = Math.Min(scale, Math.Min(bounds.Width / (float)OverlayRenderer.LogicalWidth,
                bounds.Height / (float)OverlayRenderer.LogicalHeight));
            int width = (int)Math.Ceiling(OverlayRenderer.LogicalWidth * scale);
            int height = (int)Math.Ceiling(OverlayRenderer.LogicalHeight * scale);
            int margin = Math.Min((int)Math.Round(_options.BottomMargin * dpi), bounds.Height - height);
            var location = new Point(bounds.Left + (bounds.Width - width) / 2, bounds.Bottom - margin - height);
            var surface = new LayeredSurface(width, height);
            OverlayRenderer renderer;
            try { renderer = new OverlayRenderer(surface, scale); }
            catch { surface.Dispose(); throw; }
            _renderer?.Dispose();
            _surface?.Dispose();
            _surface = surface;
            _renderer = renderer;
            _refreshRate = DisplayInfo.RefreshRate(screen);
            if (!Native.SetWindowPos(Handle, -1, location.X, location.Y, width, height, 0x0010 | 0x0200))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not position overlay.");
            RequestRender();
        }
        finally { _configuring = false; }
    }

    public void RequestRender()
    {
        nint handle = Volatile.Read(ref _windowHandle);
        if (handle == 0 || Volatile.Read(ref _visible) == 0) return;
        if (Interlocked.Exchange(ref _renderQueued, 1) != 0) return;
        if (!Native.PostMessage(handle, RenderMessage, 0, 0)) Interlocked.Exchange(ref _renderQueued, 0);
    }

    private void RequestToggleVisibility() => Native.PostMessage(Volatile.Read(ref _windowHandle),
        ToggleVisibilityMessage, 0, 0);
    public void RequestExit() => Native.PostMessage(Volatile.Read(ref _windowHandle), Native.WmClose, 0, 0);

    private void RenderFrame()
    {
        Interlocked.Exchange(ref _renderQueued, 0);
        if (Volatile.Read(ref _visible) == 0 || _surface is null || _renderer is null) return;
        InputSnapshot snapshot = _store.Snapshot();
        long now = Stopwatch.GetTimestamp();
        _renderer.Draw(snapshot, now);
        _surface.Submit(Handle, Location);
        _diagnostics.Submitted(snapshot);
        _clock?.SetActive(snapshot.IsAnimating(now), _refreshRate);
    }

    protected override void WndProc(ref Message message)
    {
        try
        {
            switch (message.Msg)
            {
                case RenderMessage: RenderFrame(); return;
                case ToggleVisibilityMessage:
                    bool visible = Volatile.Read(ref _visible) == 0;
                    Volatile.Write(ref _visible, visible ? 1 : 0);
                    if (visible)
                    {
                        WindowState = FormWindowState.Normal;
                        Show();
                        RequestRender();
                    }
                    else
                    {
                        _clock?.SetActive(false, _refreshRate);
                        // OBS skips minimized windows; hiding alone leaves its last captured frame visible.
                        WindowState = FormWindowState.Minimized;
                        Hide();
                    }
                    return;
                case Native.WmDisplayChange:
                    ConfigureDisplay();
                    break;
                case Native.WmDpiChanged:
                    // Placement is monitor-centered; do not let Form apply the suggested drag rectangle.
                    ConfigureDisplay();
                    message.Result = 0;
                    return;
                case Native.WmSessionChange:
                    _store.Reset();
                    break;
                case Native.WmPowerBroadcast when message.WParam is 4 or 7 or 18:
                    _store.Reset();
                    break;
                case 0x0021: // WM_MOUSEACTIVATE: never take focus, including programmatic hit tests.
                    message.Result = 3; // MA_NOACTIVATE
                    return;
            }
            base.WndProc(ref message);
        }
        catch (Exception exception)
        {
            if (!_initialized) _initializationError ??= exception;
            else _failed(exception);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _store.Changed -= RequestRender;
            _store.ToggleVisibilityRequested -= RequestToggleVisibility;
            Volatile.Write(ref _visible, 0);
            _clock?.Dispose();
            _renderer?.Dispose();
            _surface?.Dispose();
        }
        base.Dispose(disposing);
    }
}
