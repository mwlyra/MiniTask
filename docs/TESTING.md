# Testing

MiniTask has separate test programs for the recording/playback engine, Windows integration, and WPF interface. They run directly; `dotnet test` is not the entry point.

## Engine tests

From the repository root, after building:

```powershell
dotnet run --project tests/MiniTask.Core.Tests -c Release --no-build
```

These cover event ordering, playback speed, cancellation, held-input cleanup, recording limits, file validation, and save/load round trips. They use controlled input sinks and do not send input to the desktop.

## Desktop tests

Run these in an unlocked Windows desktop session. Close other MiniTask instances and leave the mouse and keyboard alone during the Windows integration checks; the tests may move the pointer and type into their test window.

```powershell
dotnet run --project tests/MiniTask.Ui.Tests -c Release --no-build -- .
dotnet run --project tests/MiniTask.Windows.Tests -c Release --no-build
```

The UI suite checks controls, preferences, icon resources, and rendered layouts. The Windows suite covers native structures, hotkey registration, capture filtering, and live input. A desktop test that cannot acquire foreground focus reports the scenario as unavailable, not passed.

To check a packaged build:

```powershell
./tests/Smoke-Portable.ps1 -Executable ./artifacts/portable/MiniTask.exe
```

The `-InputTest` option checks startup and shutdown through the separate input-test window.

## Manual checks

Before a release, record and replay typing, clicks, scrolling, and dragging in ordinary desktop apps. Check repeated playback, stopping during a long pause, stopping while an input is held, saving and reopening a recording, and restoring the tray window. Test different display scaling and monitor arrangements separately.

Automated checks do not establish compatibility with every app. Full physical desktop acceptance remains incomplete, including mixed-DPI displays, elevated targets, and Windows 10. See [compatibility](COMPATIBILITY.md) for supported behavior and limitations.
