# Input Overlay

![Keyboard and mouse overlay showing live inputs during gameplay](docs/valor.gif)

Input Overlay displays your keyboard, mouse, PlayStation, or Xbox controller inputs over your game. Pressed buttons light up, and a history of your last 10 inputs helps you follow a sequence and see when each press happened.

The overlay is transparent, stays above windowed or borderless games, and lets mouse clicks pass through without taking focus from your game.

**Tap <kbd>Num 0</kbd> (Numpad 0) to hide or show the overlay at any time.** It works while you're in a game and with Num Lock either on or off, so you can clear the screen and bring it back without switching windows. This shortcut works in all four overlays.

## Requirements

Windows x64 and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
are required to run the overlays. On the download page, choose the **Windows x64**
installer under **.NET Desktop Runtime** and install it before running an overlay.

## Overlays

### Keyboard and mouse

A compact keyboard layout, five mouse buttons, and scroll-wheel activity. The input history also shows keyboard presses outside the visible layout.

![Keyboard and mouse overlay with active keys, mouse buttons, and input history](docs/overlay-keyboard-mouse.png)

### PS4 / DualShock 4

Supports Sony DualShock 4 v1 and v2 controllers over USB or Bluetooth. Shows buttons, the D-pad, stick movement and clicks, and L2/R2 trigger pressure.

![PS4 DualShock 4 overlay with active buttons, stick positions, trigger pressure, and input history](docs/overlay-ps4.png)

### PS5 / DualSense

Supports the standard Sony DualSense controller over USB or Bluetooth. Shows buttons, the D-pad, stick movement and clicks, trigger pressure, Create, and Mic presses.

![PS5 DualSense overlay with active buttons, stick positions, trigger pressure, and input history](docs/overlay-ps5.png)

### Xbox Series X|S

Shows A/B/X/Y, the D-pad, offset sticks and clicks, LT/RT pressure, LB/RB, View, Menu,
Xbox and Share using Microsoft GameInput. Supports USB, Bluetooth and the Xbox Wireless
Adapter through Windows' controller drivers.

![Xbox Series X|S overlay with active buttons, stick positions, trigger pressure, and input history](docs/overlay-xbox.png)

## Capture in OBS

1. Start the overlay and leave it visible.
2. In OBS, choose **Sources > + > Window Capture** and select `InputOverlayMnK`,
   `InputOverlayPS4`, `InputOverlayPS5`, or `InputOverlayXbox` from the **Window** list.
3. Set **Capture Method** to **Modern (WGC)** (called **Windows 10 (1903 and up)**
   in older OBS versions) and turn off **Capture Cursor**.
4. Place the Window Capture source above your game source in the **Sources** list.

The overlay stays click-through on your desktop. If it is missing from the Window
list, press Numpad 0 to show it before opening the source properties.
Numpad 0 hides the overlay on both your desktop and in OBS. Press it again to
restore the overlay in the same position without switching away from your game.

## Adjust size and position

| Option | What it does | Default |
| --- | --- | --- |
| `--scale N` | Resize the overlay, from `0.5` to `3`. | `1` |
| `--bottom-margin N` | Distance from the bottom of the screen, from `0` to `1000` display-independent pixels. | `24` |
| `--monitor N` | Use a monitor number shown by `--list-monitors`. | Primary monitor |
| `--list-monitors` | List available monitors and exit. | |
| `--help` | Show command-line help and exit. | |

## Read the input history

The newest press appears at the top, with up to 10 entries visible. Each row shows the time since that press in milliseconds and a frame count at 60 fps (`f@60`) that starts at 1 for a new press. This is a timing reference, not a measurement of your game's frame rate.

Timers stop at `500+ ms` / `31+ f@60` and dim, but entries stay visible until newer presses replace them. Releasing a button does not add a row. Controller stick movement and changes in trigger pressure do not add rows either; trigger button presses do.

## Controller notes

- The first accessible supported controller is selected. If it disconnects, the controls return to neutral, the history stays visible, and the overlay reconnects automatically when a supported controller becomes available.
- Both sticks have a 10% circular deadzone to suppress resting drift. Trigger pressure is displayed without a deadzone.
- If a controller is missing, check whether another application has hidden it or opened it exclusively. The PlayStation overlays need shared access to the physical controller; Xbox input comes through GameInput.

For more controller-specific details, see the [PS4 README](src/InputOverlayPS4/README.md), [PS5 README](src/InputOverlayPS5/README.md), or [Xbox README](src/InputOverlayXbox/README.md).

## Build GitHub release assets

On Windows with the .NET 10 SDK or newer, run from the repository root:

```powershell
.\build-release.ps1 -Version 1.0.0
```

This creates one Windows x64 Release ZIP per overlay and a SHA-256 checksum file
in `artifacts/releases/`, ready to upload to a GitHub release. The ZIPs use the
installed .NET 10 Desktop Runtime listed above. Extract an overlay's ZIP and run
its executable. Keep the extracted files together, including the Xbox GameInput files.

Omit `-Version` to use the project versions without a version in the asset names.
Use `-OutputDirectory <path>` to change the destination; relative paths start at the
repository root. Re-running replaces assets with matching names. The script does
not create or publish a GitHub release.

## License

[MIT](LICENSE).
