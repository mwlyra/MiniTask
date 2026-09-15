# Validation report

**Version 1.1 UI update:** see [RELEASE-1.1.md](RELEASE-1.1.md) and the `*-v1.1.txt` logs for the latest compact UI, menu-action, regression and portable input-test checks. The original baseline results below remain for traceability.

Date: **2026-09-14**. Host: **Windows 11 Pro 25H2 x64, build 26200.9445**. Build: **.NET SDK 10.0.401**, bundled runtime **10.0.12**, Release/x64. The SDK was downloaded locally into the workspace; the globally installed runtime was .NET 9.0.19.

**Release status: functional implementation and portable build delivered; desktop acceptance incomplete.** Do not interpret a successful build or mocked engine checks as proof of reliable execution in every target app.

## Final executed checks

| Suite | Result | What it proves |
| --- | --- | --- |
| Full solution Release build | Passed, 0 warnings / 0 errors | All six projects compile |
| Core engine tests | **21 passed, 0 failed** | Ordered events/repeats, speed math, countdown, absolute cycles, trailing delay, cancellation, ownership cleanup, failure handling, physical overlap logic, capture overload, file limits, streaming event-count limit, round trips, coordinates and concurrent state gating |
| Windows integration checks | **3 passed, 0 failed, 6 unavailable** | Actual x64 structure layout; native hotkey registration conflict/recovery; capture/control pipeline using supplied hook payloads on the actual input thread |
| WPF UI checks | Passed | Actual WPF toolbar/settings/input-test construction, five accessible toolbar actions, light/dark rendering, toolbar renders at 100%, 150%, 200% |
| Portable executable startup/shutdown | Passed, exit code 0 | Bundled MiniTask.exe starts and closes through the normal WPF shutdown path without a global .NET 10 installation |

The 21 core tests include a fake clock and fake input sink, plus real cancellation waits. These tests do not generate desktop input. The supplied hook-payload Windows test checks reserved press/release suppression, auto-repeat, emergency dispatch while Control is logically held, own-window exclusion and injected/tagged exclusion. It is **not a hardware keyboard recording test**.

The UI render artifacts were visually inspected. Initial dark-theme contrast faults were fixed and re-rendered, including dropdown selection text. Scaling a render target is not equivalent to moving the application between physical monitors with different scaling factors. Native title-bar theme appearance follows Windows behavior and was not separately certified.

## Timing measurements

- **Final core run:** 20 cancellations during one-hour waits; maximum **0.979 ms**, mean **0.102 ms** from `Cancel()` until the wait task completed.
- An earlier exploratory native run measured **1.310 ms** from a command arriving on the native input message thread through cancellation and cleanup while an injected Control key was held. The assertion that Control was released passed in that run. This was a single exploratory sample, not a physical emergency-key latency test or final full-suite certification.
- Repeat timing is deterministically checked against absolute expected deadlines, including all trailing delays and gaps. The final real-desktop repeat-drift measurement was unavailable.

These are observations on this host, **not hard real-time guarantees**. Physical F10 press-to-cleanup latency still needs interactive measurement.

## Desktop restrictions and failed exploratory checks

The final guarded Windows harness could not acquire foreground focus: `SetForegroundWindow` was refused. It stopped before sending live input and reported six live checks as unavailable. No focus-forcing workaround was used.

An earlier exploratory run, before the stricter harness focus guard, failed its expected text-delivery and injected-only recording assertions and then encountered a physical-held condition. A duplicate test-service window class was separately fixed and its registration test now passes. The earlier input-delivery/recording failures are **not counted as passes**; their cause remains unconfirmed in this desktop environment. The final implementation additionally checks MiniTask's injection marker independently of Windows' injected flag. The complete live suite must be rerun interactively before calling the app production-validated.

## Manual acceptance still required

| Requested scenario | Status |
| --- | --- |
| Record physical typing in Notepad, stop and replay | Not executed end to end |
| Record shortcuts, left/right modifiers, repeats and held keys | Pipeline/engine logic tested; physical end-to-end check not executed |
| Browser clicks and scrolling | Not executed end to end |
| Record and replay a drag | Engine and injection implementation present; final interactive check unavailable |
| Several real repetitions and drift | Deterministic cycles pass; final real drift check unavailable |
| Stop during a long recorded pause | Real-clock cancellation passes; physical global shortcut path unverified |
| Stop while modifier/button is held | Engine cleanup passes; one exploratory native modifier-cleanup sample passed; final physical shortcut check unverified |
| Save, close, reopen, replay | Atomic round trip and portable startup/shutdown pass separately; combined desktop workflow not executed |
| Multiple monitors / negative positions / mixed scaling / hot-plug | Coordinate math and render scaling pass; physical configuration checks not executed |
| Privilege mismatch and administrator restart | Implemented; interactive UAC/privilege checks not executed |
| Focus loss, session lock and suspend | Guard logic plus an exploratory focus-loss check passed; final interactive focus/lock/suspend acceptance not executed |
| A compatible game | No game tested |
| Single-instance collision, tray restoration, unsaved prompts and shutdown during recording | Implemented; final manual acceptance not executed |
| Windows 10 x64 and older supported Windows 11 builds | Not available here |

## Rerun

From the source folder, with a .NET 10 SDK:

```powershell
./build.ps1
./build.ps1 -WindowsChecks
./tests/Smoke-Portable.ps1
```

Run desktop checks from an interactive foreground terminal with other MiniTask instances closed. Leave physical input untouched during live injection checks. The Windows test executable returns 0 for full success, 1 for a failed assertion, and 2 for an unavailable desktop. Some shells expose a generic nonzero exit status for the latter. Test logs are under `artifacts/`; copies from this delivery are included in `docs/test-logs/`.

For physical recording acceptance, launch `MiniTask.exe --input-test` in a separate process, or open it from Settings, then use the main recorder's normal controls. Follow with Notepad, browser, drag, focus, privilege and multi-monitor scenarios from the table above.
