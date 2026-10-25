# Input Overlay

![Keyboard and mouse overlay showing live inputs during gameplay](docs/valor.gif)

Input Overlay displays your keyboard, mouse, or PlayStation controller inputs over your game. Pressed buttons light up, and a history of your last 10 inputs helps you follow a sequence and see when each press happened.

The overlay is transparent, stays above windowed or borderless games, and lets mouse clicks pass through without taking focus from your game.

**Tap <kbd>Num 0</kbd> (Numpad 0) to hide or show the overlay at any time.** It works while you're in a game and with Num Lock either on or off, so you can clear the screen and bring it back without switching windows. This shortcut works in all three overlays.

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

## Adjust size and position

| Option | What it does | Default |
| --- | --- | --- |
| `--scale N` | Resize the overlay, from `0.5` to `3`. | `1` |
| `--bottom-margin N` | Distance from the bottom of the screen, from `0` to `1000` display-independent pixels. | `24` |
| `--monitor N` | Use a monitor number shown by `--list-monitors`. | Primary monitor |
| `--list-monitors` | List available monitors and exit. | |
| `--help` | Show command-line help and exit. | |

## Read the input history

The newest press appears at the top, with up to 10 entries visible. Each row shows the time since that press in milliseconds and the equivalent number of frames at 60 fps (`f@60`). This is a timing reference, not a measurement of your game's frame rate.

Timers stop at `500+ ms` / `30+ f@60` and dim, but entries stay visible until newer presses replace them. Releasing a button does not add a row. Controller stick movement and changes in trigger pressure do not add rows either; trigger button presses do.

## Controller notes

- The first accessible supported controller is selected. If it disconnects, the controls return to neutral, the history stays visible, and the overlay reconnects automatically when a supported controller becomes available.
- Both sticks have a 10% circular deadzone to suppress resting drift. Trigger pressure is displayed without a deadzone.
- If a controller is missing, check whether another application has hidden it or opened it exclusively. The overlay needs access to the physical controller.

For more controller-specific details, see the [PS4 README](src/InputOverlayPS4/README.md) or [PS5 README](src/InputOverlayPS5/README.md).

## License

[MIT](LICENSE).
