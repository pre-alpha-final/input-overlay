using System.Diagnostics;
using InputOverlayXbox.Input;
using Xunit.Abstractions;

namespace InputOverlayXbox.Tests;

public sealed class ControllerCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public async Task BundledGameInputInitializesRegistersCallbacksAndShutsDown()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "GameInputRedist.dll")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "GameInputRawInputProxy.exe")));
        var store = new InputStore();
        Exception? failure = null;
        var capture = new ControllerCapture(store, new Diagnostics(), exception => failure = exception);
        try
        {
            var timeout = Stopwatch.StartNew();
            while (!store.Snapshot().Connected && timeout.Elapsed < TimeSpan.FromMilliseconds(750) && failure == null)
                await Task.Delay(10);
            Assert.Null(failure);
            if (!store.Snapshot().Connected)
            {
                output.WriteLine("GameInput initialized; no accessible Microsoft Xbox controller is attached. Hardware input not exercised.");
                Assert.NotEqual("1", Environment.GetEnvironmentVariable("INPUT_OVERLAY_REQUIRE_XBOX_CONTROLLER"));
                return;
            }
            output.WriteLine($"Xbox controller connected: {store.Snapshot().Controls}.");
            var secondStore = new InputStore();
            using var second = new ControllerCapture(secondStore, new Diagnostics(), exception => failure = exception);
            timeout.Restart();
            while (!secondStore.Snapshot().Connected && timeout.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(10);
            Assert.True(secondStore.Snapshot().Connected, "GameInput must allow another simultaneous reader.");
            Assert.Null(failure);
        }
        finally
        {
            var shutdown = Stopwatch.StartNew();
            capture.Dispose(); capture.Dispose();
            Assert.True(shutdown.Elapsed < TimeSpan.FromSeconds(2));
            Assert.False(store.Snapshot().Connected);
            Assert.Null(failure);
        }
    }
}
