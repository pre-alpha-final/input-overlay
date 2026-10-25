using System.Diagnostics;
using System.Runtime.InteropServices;
using InputOverlayPS4.Input;
using Xunit.Abstractions;

namespace InputOverlayPS4.Tests;

public sealed class ControllerCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public void NativeHidStructuresMatchWindowsAbi()
    {
        Assert.Equal(32, Marshal.SizeOf<Native.HidDeviceInfo>());
        Assert.Equal(64, Marshal.SizeOf<Native.HidCaps>());
        Assert.Equal(16, Marshal.SizeOf<Native.RawDeviceList>());
        Assert.Equal(8, Marshal.OffsetOf<Native.HidDeviceInfo>(nameof(Native.HidDeviceInfo.VendorId)).ToInt32());
        Assert.Equal(20, Marshal.OffsetOf<Native.HidDeviceInfo>(nameof(Native.HidDeviceInfo.UsagePage)).ToInt32());
    }

    [Fact]
    public async Task ConnectedControllerStreamsReportsAndShutdownCancelsNativeRead()
    {
        HidDevice[] devices = HidDevices.Enumerate();
        if (devices.Length == 0)
        {
            output.WriteLine("No native DualShock 4 is currently available; hardware smoke test not exercised.");
            Assert.NotEqual("1", Environment.GetEnvironmentVariable("INPUT_OVERLAY_REQUIRE_CONTROLLER"));
            return;
        }
        output.WriteLine($"Discovered {devices.Length} native DualShock 4 device(s), first product 0x{devices[0].ProductId:x4}.");
        var store = new InputStore();
        var diagnostics = new Diagnostics();
        Exception? failure = null;
        var capture = new ControllerCapture(store, diagnostics, exception => failure = exception);
        try
        {
            var timeout = Stopwatch.StartNew();
            while (!store.Snapshot().Connected && timeout.Elapsed < TimeSpan.FromSeconds(5) && failure == null)
                await Task.Delay(10);
            Assert.Null(failure);
            Assert.True(store.Snapshot().Connected, "Native HID reports were not decoded within 5 seconds.");
            await Task.Delay(60);
            Assert.True(diagnostics.PacketCount > 1, "Expected continuous controller reports.");
            // A second reader verifies that capture really opens the HID collection with shared access.
            var otherStore = new InputStore();
            using var otherReader = new ControllerCapture(otherStore, new Diagnostics(), exception => failure = exception);
            var otherTimeout = Stopwatch.StartNew();
            while (!otherStore.Snapshot().Connected && otherTimeout.Elapsed < TimeSpan.FromSeconds(3) && failure == null)
                await Task.Delay(10);
            Assert.True(otherStore.Snapshot().Connected, "A second shared HID reader could not receive input.");
            Assert.True(store.Snapshot().Connected);
            Assert.Null(failure);
            ControllerReport report = store.Snapshot().Controls;
            output.WriteLine($"Received {diagnostics.PacketCount} reports; sticks {report.LeftX},{report.LeftY} / " +
                $"{report.RightX},{report.RightY}; triggers {report.LeftTrigger},{report.RightTrigger}; buttons {report.Buttons}.");
        }
        finally
        {
            var shutdown = Stopwatch.StartNew();
            capture.Dispose();
            Assert.True(shutdown.Elapsed < TimeSpan.FromSeconds(2), "Native read cancellation took too long.");
            Assert.False(store.Snapshot().Connected);
            capture.Dispose(); // Idempotent disposal.
            Assert.Null(failure);
        }
    }
}
