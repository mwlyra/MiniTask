# MiniTask 1.1.1 · Replay icon and simpler shortcuts

- New original red replay mark with an ivory play symbol, used by the executable, title bars and system tray. Nine Windows icon sizes cover 16–256 px; SVG and transparent PNG exports are included in the source for branding.
- The shortcut menu now offers F1–F11. F12 is disabled with an explanation because Windows reserves it for debugging. F13–F24 no longer clutter the menu; existing saved assignments to them are preserved.
- Keys already used by another action are labeled and disabled to avoid duplicate assignments. Defaults remain F8 / F9 / F10.
- The tray loads MiniTask's embedded icon directly, so it also displays correctly when launching through the development runtime.

Microsoft documents both the [extended function-key codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes) and [F12's reserved status](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).

## Verification

Release build: zero warnings/errors. All 21 engine regression tests passed. The WPF suite checked ordinary-key choices, duplicate assignment prevention, F1–F3 validation, reserved F12 rejection, preservation of saved extended keys, all nine embedded icon sizes, transparent corners and tray branding. The existing compact toolbar/menu and theme checks also passed. Actual-size icon renders were visually inspected on light and dark backgrounds.

This remains a release candidate with the external-app recording/replay and physical desktop acceptance limits described in TEST-RESULTS.md. These changes do not certify those unavailable scenarios.

To update, exit MiniTask (including its tray icon), extract `MiniTask-1.1.1-win-x64.zip`, and launch `MiniTask.exe`.
