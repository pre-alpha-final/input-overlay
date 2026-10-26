# InputOverlayXbox

Windows x64, .NET 10. Xbox Series X|S controller overlay using Microsoft GameInput.
Supports the standard controls over USB, Bluetooth or the Xbox Wireless Adapter through
Windows' controller drivers. Xbox One/Elite controllers can also use the standard controls;
the drawing follows the Series X|S layout. Elite paddles are not displayed.
The transparent overlay stays above windowed/borderless games and allows clicks through.

```powershell
dotnet run --project src/InputOverlayXbox
```

```text
--monitor N        Select a monitor from --list-monitors (default: primary)
--scale N          Size multiplier, 0.5 to 3 (default: 1)
--bottom-margin N  Distance from screen bottom in DIP, 0 to 1000 (default: 24)
--list-monitors    Print available monitors
--help            Show help
```

Numpad 0 hides/shows the overlay regardless of Num Lock. Ctrl+C exits.

Shows A/B/X/Y, the D-pad, LB/RB, LT/RT pressure, both offset sticks and their clicks,
View, Menu, Xbox and Share. Both sticks use a 10% circular deadzone to suppress resting
drift, without smoothing or rescaling movement beyond it. Trigger pressure has no deadzone.
Xbox triggers have no digital press bit; crossing 30/255 pressure counts as a press for
highlighting and history. Smaller pressure changes remain visible in the trigger bars.

The history contains the last ten button presses with independent timers in milliseconds
and frames at 60 fps. Stick movement, trigger pressure changes while held, releases and
keyboard input do not add history entries. Timers cap at 500 ms, then dim; entries remain
until replaced by newer presses.

The first accessible Microsoft Xbox gamepad stays selected until disconnected. The overlay
centers its controls on disconnect, retains history and switches to another connected
controller or reconnects automatically. Other controllers do not contribute history while
the selected controller is connected. Capture uses shared, event-driven GameInput access
and continues in the background. Xbox/Share are displayed when exposed by Windows and the
controller; older models have no Share button. Controller-hiding software or another app's
exclusive input policy can prevent input from reaching GameInput. Rumble and other output
features are not used, and the overlay does not change button mappings.

Microsoft.GameInput 4.0.302 supplies the native x64 runtime. Build and publish copy
`GameInputRedist.dll`, `GameInputRawInputProxy.exe` and their license/notice files alongside
the executable; keep these files together when distributing the overlay. No separate
GameInput installer is required. The binding explicitly requests the v4 interface, including
its system-button reading stream. See Microsoft's [versioned API header](https://github.com/microsoftconnect/GameInput/blob/main/include/v4/GameInput.h).

Build and run all tests from the repository root:

```powershell
dotnet build src/InputOverlay.slnx
dotnet test src/InputOverlay.slnx --maxcpucount:1
```

Tests cover button/axis mapping, trigger thresholds, multi-controller selection, system
buttons, reconnects, history, rendering and Windows overlay behavior. Native GameInput
initialization and shutdown are always checked. Hardware capture is smoke-tested when an
accessible controller is present. To require attached Xbox hardware during a test run:

```powershell
$env:INPUT_OVERLAY_REQUIRE_XBOX_CONTROLLER = '1'
dotnet test tests/InputOverlayXbox.Tests --filter FullyQualifiedName~ControllerCaptureTests
Remove-Item Env:INPUT_OVERLAY_REQUIRE_XBOX_CONTROLLER
```

Rendered review frames are written to `artifacts/visual-review-xbox/` by the renderer tests.
