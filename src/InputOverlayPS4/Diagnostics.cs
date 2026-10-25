using System.Diagnostics;
using InputOverlayPS4.Input;

namespace InputOverlayPS4;

internal sealed class Diagnostics
{
    private long _packets, _frames, _samples, _totalTicks, _maxTicks, _lastVersion;
    public long PacketCount => Interlocked.Read(ref _packets);
    public long FrameCount => Interlocked.Read(ref _frames);
    public long ChangedStateSamples => Interlocked.Read(ref _samples);
    public void PacketReceived() => Interlocked.Increment(ref _packets);

    // This measures software receipt to UpdateLayeredWindow submission, not physical latency.
    public void Submitted(InputSnapshot snapshot)
    {
        Interlocked.Increment(ref _frames);
        if (snapshot.Version == 0 || snapshot.Version == _lastVersion) return;
        _lastVersion = snapshot.Version;
        long elapsed = Math.Max(0, Stopwatch.GetTimestamp() - snapshot.LastChange);
        Interlocked.Add(ref _totalTicks, elapsed);
        Interlocked.Exchange(ref _maxTicks, Math.Max(Interlocked.Read(ref _maxTicks), elapsed));
        Interlocked.Increment(ref _samples);
    }

    public override string ToString()
    {
        long count = Interlocked.Read(ref _samples);
        double average = count == 0 ? 0 : Interlocked.Read(ref _totalTicks) * 1000d / Stopwatch.Frequency / count;
        double maximum = Interlocked.Read(ref _maxTicks) * 1000d / Stopwatch.Frequency;
        return $"HID reports: {Interlocked.Read(ref _packets):N0}; submitted frames: {Interlocked.Read(ref _frames):N0}; " +
            $"changed-state samples: {count:N0}; receipt-to-submission avg {average:F3} ms, max {maximum:F3} ms.";
    }
}
