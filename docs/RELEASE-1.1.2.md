# MiniTask 1.1.2 · Classic recorder icon

MiniTask now uses an original cassette recorder icon with a beveled gray case, muted blue label, tape reels and a red record button. Its outlines, colors and small highlights match the existing toolbar icons and classic Windows utility style.

The icon is updated in the executable, title bars, taskbar and tray. Nine Windows icon sizes (16–256 px), the editable SVG and transparent PNG branding exports are included. The shortcuts and recording/playback behavior are unchanged.

## Verification

The Release build passed without warnings or errors. The WPF suite passed, including embedded icon sizes, transparent corners, exact tray-icon comparison with the source icon, shortcut choices and existing toolbar/menu behavior. Native-size icon previews were inspected on light and dark backgrounds. The 21 engine regression checks passed in 1.1.1; the engine is unchanged in this icon-only update.

This remains a release candidate with the physical desktop acceptance limits described in TEST-RESULTS.md.

To update, exit MiniTask (including its tray icon), extract `MiniTask-1.1.2-win-x64.zip`, and launch `MiniTask.exe`.
