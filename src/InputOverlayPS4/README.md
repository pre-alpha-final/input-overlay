# InputOverlayPS4

Windows x64, .NET 10. Shows native Sony DualShock 4 v1/v2 input over USB or Bluetooth.
The transparent overlay stays above windowed/borderless games and allows clicks through.

```powershell
dotnet run --project src/InputOverlayPS4
```

```text
--monitor N        Select a monitor from --list-monitors (default: primary)
--scale N          Size multiplier, 0.5 to 3 (default: 1)
--bottom-margin N  Distance from screen bottom in DIP, 0 to 1000 (default: 24)
--list-monitors    Print available monitors
--help            Show help
```

Numpad 0 hides/shows the overlay regardless of Num Lock. Ctrl+C exits.

Both sticks use a small 10% circular deadzone to suppress resting drift. Movement outside
it is shown directly without smoothing or rescaling. L2/R2 show pressure without a deadzone.
The history contains the last ten button presses, including L2/R2 and L3/R3,
with independent timers in milliseconds and frames at 60 fps. Stick movement, trigger
pressure changes, releases and keyboard input do not add history entries. Timers cap
at 500 ms, then dim; entries remain until replaced by newer presses.

The first accessible supported controller stays selected until disconnected. The overlay
centers its controls on disconnect, retains history and reconnects automatically.
Capture uses shared HID access. Devices hidden or held exclusively by another application
must be made accessible to this process. Gyroscope, touch gestures, rumble and lightbar
controls are not used. No additional driver or controller runtime package is required.

Build and run all tests from the repository root:

```powershell
dotnet build src/InputOverlay.slnx
dotnet test src/InputOverlay.slnx --maxcpucount:1
```

Tests replay USB and basic/extended Bluetooth reports and exercise capture reconnects,
rendering, history and window behavior. Native capture is smoke-tested when a controller
is present. To require an attached controller during a test run:

```powershell
$env:INPUT_OVERLAY_REQUIRE_CONTROLLER = '1'
dotnet test tests/InputOverlayPS4.Tests --filter FullyQualifiedName~ControllerCaptureTests
```

Rendered review frames are written to `artifacts/visual-review-ps4/` by the renderer tests.
