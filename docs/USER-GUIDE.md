# MiniTask user guide

## Record and play

1. Open MiniTask. Open the application you want to automate.
2. Press **F8**, or click **Rec**. The toolbar says **Recording** and shows elapsed time. On first use, read the reminder that saved macros may contain sensitive input.
3. Perform mouse and keyboard actions normally. Pauses, repeated held keys, dragging, extra mouse buttons and wheel input are captured.
4. Press **F8** again, click **Stop**, or press **F10**. The delay between the last action and Stop is preserved. Any recorded input still held at Stop is given a matching release at the end.
5. Prepare the target, then press **F9** or click **Play**. A fresh 1.1 installation plays immediately; choose **Prefs → Start delay** for a countdown. Existing preferences and loaded macros retain their saved delay. Do not use the mouse or keyboard during playback.
6. Press **F9**, **F10**, or click **Stop** to interrupt. **F10 works through the physical keyboard hook even while a macro modifier is held.** The visible Stop and tray emergency action are also available.

The reserved control keys are excluded on both press and release and are not delivered to target applications. They remain reserved with modifiers held. If your target uses F8/F9/F10, choose different function keys under **Prefs → Hotkeys**. Registration failures are shown visibly; resolve them before relying on global shortcuts.

The shortcut menu offers **F1–F11**. **F12** is visibly disabled because Windows reserves it for debugging. A key already assigned to another MiniTask action is labeled and disabled; change that action first if you want to reuse it. Defaults remain F8 (record), F9 (play), and F10 (emergency stop). Windows also recognizes F13–F24 for specialist keyboards, but MiniTask no longer lists these uncommon keys. Existing assignments to them remain active until you choose a replacement. On some laptops you may need Fn with a function key.

Screen-position playback attempts to activate the most recently active external window once. If Windows refuses, MiniTask explains the problem. Activate the target yourself and use the global shortcut. The app does not repeatedly force foreground focus.

## Save, reopen and clear

Click **Save** to write a readable `.minitask` JSON file. The title and status show unsaved changes. Open, recording replacement, clearing and closing prompt before discarding them. Save again after changing playback preferences to include them in the file.

Click **Open** (Ctrl+O) to load a file and **Save** (Ctrl+S) to save. **Right-click Open**, or use **Prefs → Recent recordings**, for recent files. Loading never starts playback. Use **Prefs → Clear recording** to empty the current macro.

A recording stopped by overload or a capture limit keeps a valid captured prefix and displays an error. Review it before saving or replaying; the macro is incomplete. The limits are 500,000 events, 24 hours and 100 MiB per file. A heavily populated macro can reach the serialized file limit before the event limit.

## Playback settings

Click **Prefs** for **Playback speed**, **Repeat playback**, and **Start delay**. Choices take effect and are remembered immediately. Custom values open a small entry box. **Advanced** contains window-relative positions, delay between repetitions, focus protection and tray behavior.

- Speed presets: 0.25×, 0.5×, 1×, 2× and 4×; custom speed from 0.05× to 20×.
- Positive repetition count from 1 to 1,000,000, or continuous playback until stopped.
- Delay between complete cycles: 0–3,600,000 milliseconds. This gap is not speed-scaled.
- Countdown: 0–60 seconds. The countdown occurs once before the first cycle.
- Focus protection: stops if the target loses foreground focus, including during a recorded pause. Clicking the toolbar while this is enabled can itself stop playback.

Each cycle includes the original trailing pause scaled by playback speed. The gap follows that pause. Continuous playback never resumes automatically after cancellation, session unlock or resume.

## Coordinates and windows

**Screen positions** is the default. It uses physical positions across the virtual desktop, including monitors left of or above the primary monitor. Restore the original monitor arrangement and scaling before playback. MiniTask stops on detected display changes.

**Relative to a window** applies to new recordings. Select a visible target before recording. Mouse coordinates are stored relative to its client origin and anchored to that origin during playback. MiniTask matches process name, window class and exact title; requires exactly one match; and checks the client dimensions and DPI. It does not persist or trust a saved window handle. A moved window is supported within the recorded desktop, but renamed, resized, minimized or ambiguous targets are rejected. Targets with frequently changing titles may need a new recording. This mode still sends ordinary foreground input and can include positions outside the client area.

## Appearance, tray and privileges

Use **Prefs → Appearance** for Light, Dark or System; **Always on top** to pin the toolbar; and **Show button captions** for an icons-only bar. Hover over an icon to see its action and shortcut. Classic native menus and selection fields remain light in either toolbar theme. The status line shows elapsed time or repetition/progress while active, with the emergency shortcut at the right. Error text can be clicked for details.

Use **Prefs → Advanced** for minimize-to-tray behavior. Double-click the tray icon to restore the toolbar. Recording keeps the toolbar shown rather than hidden in the tray. Preferences and recording/playback starts are disabled while another operation or modal dialog is active; emergency stop remains independent.

If you explicitly need an elevated target, choose **Prefs → Tools → Restart as administrator**. MiniTask first saves unsaved work to your chosen file; canceling Save cancels the restart. Windows asks for elevation. The new process waits for the old process to exit, then reopens the saved macro without playing it. Canceling the Windows elevation prompt leaves the current app running.

## Local data

MiniTask has no telemetry or cloud dependency. Macros can contain passwords and other typed input; store and share them accordingly. Local settings, recent filenames and small technical diagnostics live in `%LOCALAPPDATA%\MiniTask`. Diagnostics contain error types and codes, not captured keystrokes or exception messages. No macro is automatically saved in the background.

The input-test window shows events in memory and never saves its typed content. It runs as a separate MiniTask process so its input can be recorded by the main utility.
