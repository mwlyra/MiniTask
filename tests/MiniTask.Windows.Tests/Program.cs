using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MiniTask.Core;
using MiniTask.Windows;

// Intentionally interacts only with this harness's own foreground test window.
// Actual physical keyboard/mouse recording still requires a human acceptance check.
internal static class Program
{
    private static readonly ConcurrentQueue<(uint Message, nuint Value)> Received = new();
    private static readonly Native.WndProc Proc = WindowProc;
    private static nint hwnd;
    private static uint thread;
    private static int failed, passed, skipped;
    [STAThread]
    private static int Main()
    {
        Console.WriteLine($"Windows: {Environment.OSVersion.VersionString}; x64={Environment.Is64BitProcess}; runtime={Environment.Version}");
        thread = Native.GetCurrentThreadId();
        var wc = new Native.WindowClass { Size = (uint)Marshal.SizeOf<Native.WindowClass>(), Proc = Proc, Class = "MiniTaskWindowsTest", Instance = Native.GetModuleHandleW(null) };
        if (Native.RegisterClassExW(ref wc) == 0) throw new Exception("Register test window failed");
        hwnd = Native.CreateWindowExW(0, wc.Class, "MiniTask · Windows input validation", 0x10CF0000, 80, 80, 600, 350, 0, 0, wc.Instance, 0);
        if (hwnd == 0) throw new Exception("Create test window failed");
        DesktopEnvironment.Activate(hwnd);
        var task = Task.Run(Run);
        while (Native.GetMessageW(out var message, 0, 0, 0) > 0) { Native.TranslateMessage(ref message); Native.DispatchMessageW(ref message); }
        task.GetAwaiter().GetResult();
        Native.DestroyWindow(hwnd); Native.UnregisterClassW(wc.Class, wc.Instance);
        Console.WriteLine($"WINDOWS RESULT: {passed} passed, {failed} failed, {skipped} unavailable");
        return failed != 0 ? 1 : skipped != 0 ? 2 : 0;
    }
    private static nint WindowProc(nint window, uint msg, nuint wp, nint lp)
    {
        if (msg is 0x100 or 0x101 or 0x102 or 0x200 or 0x201 or 0x202 or 0x20A or 0x20E) Received.Enqueue((msg, wp));
        return Native.DefWindowProcW(window, msg, wp, lp);
    }
    private static async Task Run()
    {
        try
        {
            using var input = new InputService();
            await input.Configure(new());
            await Test("x64 Win32 INPUT and hook layouts", () =>
            {
                Check(Marshal.SizeOf<Native.Input>() == 40, "INPUT must be 40 bytes");
                Check(Marshal.OffsetOf<Native.Input>(nameof(Native.Input.Union)).ToInt32() == 8, "Union alignment");
                Check(Marshal.SizeOf<Native.KeyboardHook>() == 24, "Keyboard hook size"); Check(Marshal.SizeOf<Native.MouseHook>() == 32, "Mouse hook size");
                return Task.CompletedTask;
            });
            await Test("Hotkey conflict detection and registration recovery", async () =>
            {
                using var second = new InputService();
                bool conflict = false;
                try { await second.Configure(new()); } catch (System.ComponentModel.Win32Exception) { conflict = true; }
                Check(conflict, "Expected F8/F9/F10 conflict");
                await second.Configure(new(0x80, 0x81, 0x82));
            });
            await Test("Keyboard capture pipeline: controls, repeat, injected exclusion and own UI", async () =>
            {
                var session = new RecordingSession(new(), new MonotonicClock()); await input.Begin(session, 0, null);
                int emergencyCalls = 0;
                void OnHotkey(int id) { if (id == 3) emergencyCalls++; }
                input.Hotkey += OnHotkey;
                try
                {
                    await input.Invoke(() =>
                    {
                        input.ProcessKeyboard(new() { Vk = 0xA2, Scan = 29 }, false);
                        input.ProcessKeyboard(new() { Vk = 0x79, Flags = 0x80 }, false);
                        Check(input.ProcessKeyboard(new() { Vk = 0x79 }, false), "Control down not suppressed");
                        input.ProcessKeyboard(new() { Vk = 0x79 }, false);
                        Check(input.ProcessKeyboard(new() { Vk = 0x79, Flags = 0x80 }, false), "Control up not suppressed");
                        input.ProcessKeyboard(new() { Vk = 65, Scan = 30, Extra = Native.Tag }, false);
                        input.ProcessKeyboard(new() { Vk = 65, Scan = 30, Flags = 0x10 }, false);
                        input.ProcessKeyboard(new() { Vk = 66, Scan = 48 }, true);
                        input.ProcessKeyboard(new() { Vk = 66, Scan = 48, Flags = 0x80 }, false);
                        input.ProcessKeyboard(new() { Vk = 65, Scan = 30 }, false);
                        input.ProcessKeyboard(new() { Vk = 65, Scan = 30 }, false);
                        input.ProcessKeyboard(new() { Vk = 65, Scan = 30, Flags = 0x80 }, true);
                        input.ProcessKeyboard(new() { Vk = 0xA2, Scan = 29, Flags = 0x80 }, false);
                    });
                    await input.End(); var result = await session.Completion;
                    Check(emergencyCalls == 1, "Emergency shortcut did not fire exactly once while Control was held");
                    Check(result.Events.Count == 5 && result.Events.All(e => e.VirtualKey is 65 or 0xA2), "Recording filter leaked control, injected or UI keys");
                    MacroStorage.Validate(result);
                }
                finally { input.Hotkey -= OnHotkey; await input.End(); }
            });
            if (DesktopEnvironment.Foreground != hwnd && !DesktopEnvironment.Activate(hwnd))
            {
                skipped = 6;
                Console.WriteLine("UNAVAILABLE: Windows refused foreground activation. Live injection checks were not run. Launch this harness from a foreground terminal for desktop acceptance.");
                return;
            }
            await Task.Delay(120);
            var bounds = DesktopEnvironment.Bounds;
            var origin = DesktopEnvironment.ClientOrigin(hwnd);
            int x = origin.X + 100, y = origin.Y + 100;
            var macro = new Macro { Desktop = bounds, Monitors = DesktopEnvironment.Monitors(), KeyboardLayout = DesktopEnvironment.KeyboardLayout,
                DurationUs = 500_000, Events = [Key(10_000, 0xA0, 42, true), Key(50_000, 65, 30, true), Key(80_000, 65, 30, false), Key(100_000, 0xA0, 42, false),
                new(140_000, InputKind.Move, x, y), new(180_000, InputKind.ButtonDown, x, y, Button: 1),
                new(220_000, InputKind.Move, x + 20, y + 10), new(250_000, InputKind.ButtonUp, x + 20, y + 10, Button: 1), new(300_000, InputKind.Wheel, x, y, Delta: -120), new(330_000, InputKind.HorizontalWheel, x, y, Delta: 120)] };
            await Test("Real SendInput delivery: scan codes, modifier, click-drag and both wheel axes", async () =>
            {
                Received.Clear();
                Check(DesktopEnvironment.Foreground == hwnd, "Test window lost focus before input");
                await new PlaybackEngine(new MonotonicClock(), new WindowsInputSink(input, macro, new(CountdownSeconds: 0, StopOnFocusLoss: true), 0)).Run(macro, new(CountdownSeconds: 0), null, default);
                await Task.Delay(100);
                var received = Received.ToArray();
                Console.WriteLine($"  Received: key-down={received.Count(e => e.Message == 0x100)}, key-up={received.Count(e => e.Message == 0x101)}, chars={received.Count(e => e.Message == 0x102)}, mouse={received.Count(e => e.Message >= 0x200)}; foreground={Native.GetForegroundWindow() == hwnd}; hooks={input.KeyboardObservations}, injected={input.InjectedKeyboardObservations}, own-tag={input.TaggedKeyboardObservations}");
                Check(received.Any(e => e.Message == 0x102 && (e.Value == 'A' || e.Value == 'a')), "Expected A/a received by test window (Caps Lock may invert case)");
                Check(received.Count(e => e.Message == 0x100) == 2 && received.Count(e => e.Message == 0x101) == 2, "Expected paired keys");
                foreach (uint id in new uint[] { 0x201, 0x202, 0x20A, 0x20E }) Check(received.Any(e => e.Message == id), $"Missing mouse message {id:X}");
                Check(input.KeyboardObservations >= 4 && input.MouseObservations >= 6, "Low-level hooks did not observe injected input");
            });
            await Test("Real injected playback excluded from active recorder", async () =>
            {
                Check(DesktopEnvironment.Foreground == hwnd, "Test window lost focus before input");
                var session = new RecordingSession(macro with { Events = [], DurationUs = 0 }, new MonotonicClock());
                await input.Begin(session, 0, null);
                try { await new PlaybackEngine(new MonotonicClock(), new WindowsInputSink(input, macro, new(CountdownSeconds: 0, StopOnFocusLoss: true), 0)).Run(macro, new(CountdownSeconds: 0), null, default); }
                finally { await input.End(); }
                var result = await session.Completion;
                Console.WriteLine($"  Recorder event kinds: {string.Join(", ", result.Events.GroupBy(e => e.Kind).Select(g => $"{g.Key}={g.Count()}"))}");
                Check(result.Events.Count == 0, "Input was captured during injected-only test (physical interference or injection leak)");
            });
            await Test("Real repeat boundaries and trailing delay", async () =>
            {
                Check(DesktopEnvironment.Foreground == hwnd, "Test window lost focus before input");
                var watch = Stopwatch.StartNew();
                await new PlaybackEngine(new MonotonicClock(), new WindowsInputSink(input, macro, new(CountdownSeconds: 0, StopOnFocusLoss: true), 0)).Run(macro, new(2, 3, false, 50, 0), null, default);
                watch.Stop(); Console.WriteLine($"  Three 2× cycles + two 50ms gaps: expected 850ms; actual {watch.Elapsed.TotalMilliseconds:F3}ms");
                Check(watch.Elapsed.TotalMilliseconds is >= 845 and < 1200, "Cycle duration outside tolerance");
            });
            await Test("Native message-thread stop during held modifier and long pause", async () =>
            {
                Check(DesktopEnvironment.Foreground == hwnd, "Test window lost focus before input");
                var held = macro with { DurationUs = 3_600_000_000, Events = [Key(0, 0xA2, 29, true), Key(3_600_000_000, 0xA2, 29, false)] };
                using var cts = new CancellationTokenSource();
                var task = new PlaybackEngine(new MonotonicClock(), new WindowsInputSink(input, held, new(CountdownSeconds: 0, StopOnFocusLoss: true), 0)).Run(held, new(CountdownSeconds: 0), null, cts.Token);
                await Task.Delay(80);
                Check((Native.GetAsyncKeyState(0xA2) & 0x8000) != 0, "Injected control not held");
                var watch = Stopwatch.StartNew(); await input.Invoke(cts.Cancel);
                try { await task; throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
                watch.Stop(); await Task.Delay(50);
                Console.WriteLine($"  Native message dispatch → cancellation + cleanup: {watch.Elapsed.TotalMilliseconds:F3}ms");
                Check(watch.Elapsed.TotalMilliseconds < 100, "Missed 100ms ordinary-load target");
                Check((Native.GetAsyncKeyState(0xA2) & 0x8000) == 0, "Control was not released");
            });
            await Test("Cleanup releases mouse without consulting invalid recorded coordinates", () =>
            {
                Check(DesktopEnvironment.Foreground == hwnd, "Test window lost focus before input");
                var sink = new WindowsInputSink(input, macro, new(CountdownSeconds: 0), 0);
                sink.Send(new(0, InputKind.ButtonDown, x, y, Button: 1));
                sink.ReleaseHeld(new(0, InputKind.ButtonDown, int.MaxValue, int.MaxValue, Button: 1));
                Check((Native.GetAsyncKeyState(1) & 0x8000) == 0, "Mouse button remained held"); return Task.CompletedTask;
            });
            await Test("Focus-loss protection stops injection", async () =>
            {
                var guarded = new WindowsInputSink(input, macro, new(StopOnFocusLoss: true), 0);
                guarded.CheckEnvironment();
                nint other = 0;
                try
                {
                    await input.Invoke(() =>
                    {
                        other = Native.CreateWindowExW(0, "STATIC", "MiniTask focus-loss test", 0x10CF0000, 720, 80, 200, 150, 0, 0, Native.GetModuleHandleW(null), 0);
                        Check(other != 0 && Native.SetForegroundWindow(other), "Could not activate second test window");
                    });
                    bool focusStopped = false;
                    try { guarded.CheckEnvironment(); } catch (InvalidOperationException) { focusStopped = true; }
                    Check(focusStopped, "Focus loss was not detected");
                }
                finally { if (other != 0) await input.Invoke(() => Native.DestroyWindow(other)); DesktopEnvironment.Activate(hwnd); }
                bool mismatch = false;
                try { DesktopEnvironment.Verify(hwnd, DesktopEnvironment.Describe(hwnd) with { ClientWidth = 1 }); } catch (InvalidOperationException) { mismatch = true; }
                Check(mismatch, "Resized target was accepted");
            });
        }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL harness: " + ex); }
        finally { Native.PostThreadMessageW(thread, 0x12, 0, 0); }
    }
    private static InputEvent Key(long at, int vk, int scan, bool down) => new(at, down ? InputKind.KeyDown : InputKind.KeyUp, VirtualKey: vk, ScanCode: scan);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Test(string name, Func<Task> action)
    {
        try { await action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }
}
