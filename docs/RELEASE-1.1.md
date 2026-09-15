# MiniTask 1.1 · Classic utility interface

The toolbar now measures **284 × 117 device-independent pixels**, down from 432 × 207: about **63% less screen area** at the same scaling.

- Five small original colored icons: Open, Save, Rec, Play and Prefs.
- Neutral classic Windows styling; the large bordered cards and navy palette are removed.
- One-line HUD with recording time, repetition/progress and emergency shortcut; live state also appears in the native title bar.
- The active Rec or Play control becomes a red Stop square with the caption **Stop**.
- Prefs opens a standard menu. Speed, repeat count, continuous playback, countdown, hotkeys, always-on-top, captions and appearance are direct choices.
- Custom values use a small entry dialog. Advanced settings contain only positions, inter-repeat delay, focus protection and tray behavior.
- Optional icons-only mode; useful tooltips; Ctrl+O / Ctrl+S for open/save.
- New installations default to a light toolbar and immediate playback. Existing preferences remain intact, including previously selected countdowns.
- Save operations now block overlapping UI/hotkey starts while the save dialog/write is active.

The recording, playback, Windows input engines and `.minitask` format are unchanged. Artwork is original; TinyTask's compact interaction model is the reference, with no copied code or image assets. Reference: [TinyTask's own product page and toolbar images](https://www.tinytask.net/).

## Verification

Release build succeeded with zero warnings/errors. All **21 core regression checks passed**, with maximum observed cancellation of **0.897 ms** across 20 long-wait trials (not a hard real-time guarantee).

The WPF suite executed speed/repeat/caption menu actions, checked the resulting saved preference values through an isolated test store, verified compact dimensions, rendered the advanced dialog and both themes, and checked the recording/playback Stop states. Toolbar renders at 100%, 150% and 200% were inspected, including native-menu text contrast. HUD state renders test presentation, not physical input capture.

The self-contained 1.1 executable launched and closed successfully in **input-test mode** while the user's existing main instance was left alone. Main-window construction was exercised separately by the WPF suite. Real recording/replay and the wider Windows acceptance limits remain documented in TEST-RESULTS.md; this interface update does not turn those earlier unavailable checks into passes.

To update: exit the old MiniTask using its toolbar close button or **tray → Exit**, extract `MiniTask-1.1-win-x64.zip`, and launch its `MiniTask.exe`.
