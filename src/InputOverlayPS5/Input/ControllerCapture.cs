using System.ComponentModel;
using System.Diagnostics;

namespace InputOverlayPS5.Input;

internal sealed class ControllerCapture : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _deviceChanged = new(0, 1);
    private readonly Task _worker;
    private readonly Func<HidDevice[]> _enumerate;
    private readonly Func<HidDevice, HidConnection> _open;
    private int _disposed;

    public ControllerCapture(InputStore store, Diagnostics diagnostics, Action<Exception> failed)
        : this(store, diagnostics, failed, HidDevices.Enumerate, HidDevices.Open) { }

    internal ControllerCapture(InputStore store, Diagnostics diagnostics, Action<Exception> failed,
        Func<HidDevice[]> enumerate, Func<HidDevice, HidConnection> open)
    {
        _enumerate = enumerate;
        _open = open;
        _worker = Task.Run(async () =>
        {
            try { await Run(store, diagnostics, _stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception exception) { failed(exception); }
            finally { store.Disconnect(Stopwatch.GetTimestamp()); }
        });
    }

    public void RefreshDevices()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        try { _deviceChanged.Release(); }
        catch (SemaphoreFullException) { } // Coalesce arrivals/removals.
        catch (ObjectDisposedException) { }
    }

    private async Task Run(InputStore store, Diagnostics diagnostics, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            foreach (HidDevice device in _enumerate())
            {
                stop.ThrowIfCancellationRequested();
                try
                {
                    HidConnection connection = _open(device);
                    using Stream stream = connection.Stream;
                    byte[] buffer = new byte[connection.ReportLength];
                    using var readStop = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    while (!stop.IsCancellationRequested)
                    {
                        // DualSense sends continuously even at rest. Bound a lost Bluetooth link and
                        // cancel native I/O on shutdown instead of leaving a thread blocked in ReadFile.
                        readStop.CancelAfter(TimeSpan.FromSeconds(2));
                        int length = await stream.ReadAsync(buffer, readStop.Token).ConfigureAwait(false);
                        long timestamp = Stopwatch.GetTimestamp();
                        if (length == 0) break;
                        diagnostics.PacketReceived();
                        if (ControllerReportParser.TryParse(buffer.AsSpan(0, length), out ControllerReport report))
                            store.Report(report, timestamp);
                    }
                }
                catch (OperationCanceledException) when (!stop.IsCancellationRequested) { }
                catch (IOException) { } // Unplugged or disconnected.
                catch (UnauthorizedAccessException) { } // Another application may temporarily own the device.
                catch (Win32Exception) { } // Removed or inaccessible during discovery/open.
                finally { store.Disconnect(Stopwatch.GetTimestamp()); }
            }
            await _deviceChanged.WaitAsync(TimeSpan.FromSeconds(1), stop).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _deviceChanged.Dispose();
        _stop.Dispose();
    }
}
