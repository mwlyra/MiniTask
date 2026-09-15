# Engineering notes

## Input path

A dedicated STA Windows thread creates an invisible notification window, installs `WH_KEYBOARD_LL` and `WH_MOUSE_LL`, registers hotkeys, registers session notifications, and pumps native messages. Rooted delegates live for the service lifetime. The hooks update a physical-held bitmap, filter reserved controls and MiniTask windows, transform window coordinates if applicable, and call `Channel.TryWrite`. There is one hook producer and one recording consumer. Hook callbacks never wait for the recording consumer or WPF.

The queue capacity is 32,768. Failure to enqueue stops the recording and surfaces an overload error; there is no silent movement coalescing or dropping. The consumer preserves order and repeats, filters orphan releases, limits total events and seals remaining input states at Stop. Captured timestamps use `Stopwatch`, independent of wall-clock changes.

Function-key controls are fully suppressed on both physical edges. Press/repeat state ensures one toggle per press. Registration detects conflicts; direct handling in the same dedicated hook makes emergency stop work logically even if a macro holds a modifier. Injected input and the application marker are filtered before physical-state tracking or recording. Tests exercise this pipeline using supplied hook payloads without labeling that as physical hardware testing.

## Playback and races

The core engine depends only on `IPlaybackClock` and `IInputSink`. It schedules each event against an absolute cycle start. Deadline waits use cancellation-aware delays rather than busy spinning. A cycle includes trailing duration, and the unscaled inter-cycle gap follows it. If the OS delays an entire cycle, future cycle starts are clamped to the current time instead of bursting through overdue repetitions. Timing is best effort and limited by Windows scheduling.

`RunGate` models Idle, Countdown, Recording, Playing, Stopping and Error. `AppController` serializes start/stop resource assignment under a short lock and queues hook-thread begin/end commands in order. Cancellation is assigned before a playback task is dispatched. Stopping cannot be turned back into Playing by a late countdown notification. A new run cannot begin until the old run completes cleanup.

The WPF timer only displays state. It is not responsible for capture, scheduling, environment guards or emergency cancellation. Modal dialogs disable new UI/hotkey starts. Shutdown cancels and awaits the active operation before disposing hooks and prompting about captured unsaved work.

## Injection ownership

The Win32 `INPUT` union is 40 bytes on x64, with its union at offset 8. Keyboard input prefers scan codes, preserves extended flags and uses a documented Pause fallback. Mouse positions map virtual physical coordinates onto 0–65,535 with `MOUSEEVENTF_VIRTUALDESK | ABSOLUTE`; mouse actions include absolute movement to the recorded position.

Each `SendInput` call submits exactly one event. Any return other than one is failure; an already accepted input is never retried. Accepted presses enter an ownership ledger, accepted releases remove them, and remaining owned inputs are released in reverse order in `finally`. Cleanup mouse releases deliberately do not resolve target coordinates or move the pointer, so they can run after display/window failure. Physical-held overlap is checked before normal injection and cleanup.

## Environment

The WPF process declares PerMonitorV2 DPI awareness. Display bounds, per-monitor physical rectangles/effective DPI and target client size/DPI are recorded. Win32 positions remain physical; WPF layout stays in device-independent units. Screen playback requires the original arrangement and rejects positions in monitor gaps. Relative playback resolves a unique process/class/title match and anchors client offsets; it still requires the recorded display metadata for conservative reproducibility.

Environment checks run between input events and during waits. Expensive window identity checks are throttled to 25 ms, display enumeration to 100 ms. Session lock/suspend/display messages independently request cancellation. Window activation is attempted once, never forced repeatedly.

## Testing and distribution

The test programs intentionally use only the .NET SDK/BCL, avoiding a test-framework dependency for offline portability. `build.ps1` runs them explicitly; `dotnet test` is not the test entry point. Core tests use a controllable clock/sink. Windows tests separate supplied hook payload tests from actual native injection. The UI suite instantiates WPF controls and writes render artifacts; pixel scaling in that suite does not replace physical mixed-DPI monitor testing.

Windows Forms supplies only `NotifyIcon` and its menu. The WPF application manifest owns DPI configuration; WFO0003 is suppressed for this documented hybrid case. WPF trimming is disabled. The executable bundles .NET and WindowsDesktop; native library extraction may create runtime cache files under the user's temporary `.net` folder. Settings remain in LocalAppData.
