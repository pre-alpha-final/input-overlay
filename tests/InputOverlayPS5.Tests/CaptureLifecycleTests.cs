using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using InputOverlayPS5.Input;

namespace InputOverlayPS5.Tests;

public sealed class CaptureLifecycleTests
{
    [Fact]
    public async Task RetainsSelectedControllerThenFailsOverAndReconnectsWithoutLosingHistory()
    {
        var devices = new ConcurrentDictionary<string, ReportStream>();
        var first = new ReportStream();
        var second = new ReportStream();
        devices["A"] = first; devices["B"] = second;
        var opened = new ConcurrentQueue<string>();
        var store = new InputStore();
        Exception? failure = null;
        using var capture = new ControllerCapture(store, new Diagnostics(), exception => failure = exception,
            () => devices.Keys.Order().Select(path => new HidDevice(path, 0x0ce6)).ToArray(),
            device => { opened.Enqueue(device.Path); return new(devices[device.Path], 64); });
        first.Enqueue(ControllerReportParserTests.Usb(0x28)); // Cross.
        second.Enqueue(ControllerReportParserTests.Usb(0x88)); // Triangle.
        await Until(() => store.Snapshot().IsDown(ControllerButtons.Cross));
        capture.RefreshDevices();
        await Task.Delay(30);
        Assert.Equal(new[] { "A" }, opened.ToArray());
        devices.TryRemove("A", out _);
        first.Complete();
        await Until(() => store.Snapshot().IsDown(ControllerButtons.Triangle));
        Assert.Equal(new[] { "A", "B" }, opened.ToArray());
        Assert.Equal(new[] { ControllerButtons.Triangle, ControllerButtons.Cross },
            store.Snapshot().History.Select(entry => entry.Code));
        devices.TryRemove("B", out _);
        second.Complete();
        await Until(() => !store.Snapshot().Connected);
        Assert.Equal(ControllerReport.Neutral, store.Snapshot().Controls);
        Assert.Equal(2, store.Snapshot().History.Length);
        var reconnected = new ReportStream();
        devices["A"] = reconnected;
        reconnected.Enqueue(ControllerReportParserTests.Usb(8, 0, 1)); // PS.
        capture.RefreshDevices();
        await Until(() => store.Snapshot().IsDown(ControllerButtons.PS));
        Assert.Equal(new[] { ControllerButtons.PS, ControllerButtons.Triangle, ControllerButtons.Cross },
            store.Snapshot().History.Select(entry => entry.Code));
        Assert.Null(failure);
    }

    [Fact]
    public async Task DiscoverySignalWakesWaitingCaptureAndDisposalCancelsOutstandingRead()
    {
        bool available = false;
        var stream = new ReportStream();
        var store = new InputStore();
        Exception? failure = null;
        var capture = new ControllerCapture(store, new Diagnostics(), exception => failure = exception,
            () => Volatile.Read(ref available) ? [new("test", 0x0ce6)] : [], _ => new(stream, 64));
        try
        {
            Volatile.Write(ref available, true);
            stream.Enqueue(ControllerReportParserTests.Usb());
            capture.RefreshDevices();
            await Until(() => store.Snapshot().Connected);
            var elapsed = Stopwatch.StartNew();
            capture.Dispose();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1));
            Assert.True(stream.Disposed);
            Assert.False(store.Snapshot().Connected);
            Assert.Null(failure);
        }
        finally { capture.Dispose(); }
    }

    [Fact]
    public async Task InaccessibleControllerDoesNotPreventTryingAnotherDevice()
    {
        var stream = new ReportStream();
        var store = new InputStore();
        Exception? failure = null;
        using var capture = new ControllerCapture(store, new Diagnostics(), exception => failure = exception,
            () => [new("busy", 0x0ce6), new("available", 0x0ce6)],
            device => device.Path == "busy" ? throw new UnauthorizedAccessException() : new(stream, 64));
        stream.Enqueue(ControllerReportParserTests.Usb(0x18));
        await Until(() => store.Snapshot().IsDown(ControllerButtons.Square));
        Assert.Null(failure);
    }

    private static async Task Until(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Capture did not reach expected state.");
            await Task.Delay(5);
        }
    }

    private sealed class ReportStream : Stream
    {
        private readonly Channel<byte[]> _reports = Channel.CreateUnbounded<byte[]>();
        public bool Disposed { get; private set; }
        public void Enqueue(byte[] report) => _reports.Writer.TryWrite(report);
        public void Complete() => _reports.Writer.TryComplete();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] report = await _reports.Reader.ReadAsync(cancellationToken);
                report.CopyTo(buffer);
                return report.Length;
            }
            catch (ChannelClosedException) { return 0; }
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
