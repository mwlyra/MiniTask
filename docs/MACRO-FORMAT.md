# MiniTask file format · version 1

Extension: `.minitask`. Encoding: UTF-8 JSON. Case-sensitive camelCase property names, string enum values. Unknown members, unknown enum strings, numeric enums and unsupported versions are rejected. Opening only loads and validates data; it never executes playback.

Required root properties: `version`, `durationUs`, `coordinateMode`, `desktop`, `monitors`, `keyboardLayout`, `playback`, `events`. Optional properties: `name`, `target`. Versions are not inferred from missing version fields.

```json
{
  "version": 1,
  "name": "Physical A key",
  "durationUs": 1000000,
  "coordinateMode": "Screen",
  "desktop": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
  "monitors": [
    { "device": "\\\\.\\DISPLAY1", "bounds": { "x": 0, "y": 0, "width": 1920, "height": 1080 }, "dpiX": 96, "dpiY": 96 }
  ],
  "keyboardLayout": "4090409",
  "target": null,
  "playback": {
    "speed": 1,
    "repetitions": 1,
    "continuous": false,
    "gapMs": 0,
    "countdownSeconds": 3,
    "stopOnFocusLoss": false
  },
  "events": [
    { "atUs": 100000, "kind": "KeyDown", "virtualKey": 65, "scanCode": 30 },
    { "atUs": 200000, "kind": "KeyUp", "virtualKey": 65, "scanCode": 30 }
  ]
}
```

This example describes metadata for an illustrative desktop, not a universally playable file. Display device IDs, dimensions, scaling and layout identifiers must match the actual recording environment.

## Events

Every event has `atUs` (integer microseconds since recording started) and `kind`. Equal timestamps are allowed and retain array order. Duration includes the intentional trailing pause. Missing event payload fields take their type's zero/false defaults; validation rejects missing meaningful fields, such as an invalid key code.

| kind | Meaningful fields |
| --- | --- |
| `KeyDown`, `KeyUp` | `virtualKey` (1–255), `scanCode` (0–255), `extended` (boolean) |
| `Move` | `x`, `y` |
| `ButtonDown`, `ButtonUp` | `x`, `y`, `button`: 1 left, 2 right, 3 middle, 4 X1, 5 X2 |
| `Wheel`, `HorizontalWheel` | `x`, `y`, signed nonzero `delta` within ±32,767; one ordinary wheel notch is typically 120 |

Coordinates are signed physical pixels. In `Screen` mode they are virtual-desktop positions; in `Window` mode they are offsets from the target's client origin. Mouse presses/releases and wheel events include positions, so playback does not depend on a preceding move event being present.

Keyboard identity is virtual key + scan code + extended flag. Repeated `KeyDown` while held is allowed and preserves key repeat. Every release needs a preceding press, every final held input needs a release, and duplicate held mouse-button downs are invalid. When recording stops with an input held, the recorder appends releases at `durationUs` in reverse press order.

## Metadata and preferences

`desktop` is the bounding rectangle of the virtual desktop. `monitors` contains device identifiers, physical bounds and effective DPI, sorted by device name. Negative coordinates are supported. The writer records all available monitors. Files with an empty monitor list can be inspected/validated, but the desktop preflight will not match a live display configuration.

`keyboardLayout` is the hexadecimal Windows keyboard-layout handle value associated with the target/last external window at recording start. It is a comparison hint rather than a portable guarantee of text equivalence.

`Window` mode requires `target`: `processName`, `className`, exact `title`, `clientWidth`, `clientHeight`, `dpi`. No HWND is serialized. Resolution requires one matching visible top-level window, followed by size, DPI and minimized-state checks. During a run the resolved process/thread IDs and window identity are checked again.

Playback options are user preferences, not executable instructions. Speed range is 0.05–20; repetition range 1–1,000,000; gap range 0–3,600,000 ms; countdown range 0–60 s. Duration and event timestamps are scaled by speed; inter-cycle gaps and countdown are not.

## Validation and writes

File limit: **100 MiB**. Event limit: **500,000**, checked while deserializing the event list. Duration limit: **24 hours**. Limits apply even to hand-edited files. Root null, null events, unknown properties/types, out-of-order timestamps, mismatched key/button states, invalid numeric ranges, off-desktop screen coordinates and monitor gaps are rejected. Window-relative positions are checked against the resolved desktop before playback. Display changes during playback stop the run.

Files are written to a unique temporary sibling, flushed to disk, then replaced atomically on filesystems supporting Windows replacement semantics. A failed save leaves the original file intact and removes its temporary sibling when possible. Network shares and abrupt storage failure can weaken filesystem guarantees; this is not a backup system.

No field can contain a script, shell command, executable payload or automatic-open action.
