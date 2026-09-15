using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MiniTask.Core;

namespace MiniTask.Windows;

public sealed record Hotkeys(int Record = 0x77, int Play = 0x78, int Emergency = 0x79)
{
    public static string Label(int key) => $"F{key - 0x6F}";
    public void Validate()
    {
        int[] keys = [Record, Play, Emergency];
        // Keep extended keys valid for saved profiles and isolated desktop tests.
        // The user-facing menu offers standard keyboard keys only.
        if (keys.Distinct().Count() != 3 || keys.Any(k => k is < 0x70 or > 0x87))
            throw new InvalidDataException("Choose three different function keys under Prefs → Hotkeys.");
    }
}
public sealed class InputService : IDisposable
{
    private readonly Thread thread;
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Native.HookProc keyboardDelegate, mouseDelegate;
    private readonly Native.WndProc windowDelegate;
    private readonly int[] physical = new int[256];
    private readonly bool[] excludedButtons = new bool[6];
    private readonly HashSet<string> recordedHeld = [];
    private nint keyboardHook, mouseHook, window;
    private uint threadId;
    private volatile bool disposed;
    private long keyboardObservations, mouseObservations;
    private long injectedKeyboardObservations, taggedKeyboardObservations;
    internal long KeyboardObservations => Interlocked.Read(ref keyboardObservations);
    internal long MouseObservations => Interlocked.Read(ref mouseObservations);
    internal long InjectedKeyboardObservations => Interlocked.Read(ref injectedKeyboardObservations);
    internal long TaggedKeyboardObservations => Interlocked.Read(ref taggedKeyboardObservations);
    private RecordingSession? recording;
    private Hotkeys hotkeys = new();
    private nint recordingTarget;
    private WindowTarget? targetInfo;
    public event Action<int>? Hotkey;
    public event Action<string>? SafetyStop;
    public string? SessionWarning { get; private set; }
    public nint LastExternalForeground { get; private set; }
    public InputService()
    {
        keyboardDelegate = Keyboard; mouseDelegate = Mouse; windowDelegate = WindowMessage;
        thread = new Thread(MessageLoop) { Name = "MiniTask input and emergency shortcuts", IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Task.GetAwaiter().GetResult();
    }
    public Task Configure(Hotkeys keys) => Invoke(() =>
    {
        keys.Validate();
        if (new[] { keys.Record, keys.Play, keys.Emergency }.Contains(0x7B))
            throw new InvalidDataException("Windows reserves F12 for debugging. Choose another key under Prefs → Hotkeys.");
        Hotkeys previous = hotkeys;
        for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(0, i);
        try { Register(keys); hotkeys = keys; }
        catch
        {
            for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(0, i);
            try { Register(previous); } catch { /* Caller gets a visible warning; no hidden success. */ }
            throw;
        }
    });
    private static void Register(Hotkeys keys)
    {
        int id = 0;
        foreach (int key in new[] { keys.Record, keys.Play, keys.Emergency })
            if (!Native.RegisterHotKey(0, ++id, 0x4000, (uint)key))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot register {Hotkeys.Label(key)}. Another application may use it. Choose different shortcuts in Settings. Global emergency stop may be unavailable.");
    }
    public Task Begin(RecordingSession session, nint target, WindowTarget? metadata) => Invoke(() =>
    {
        recordedHeld.Clear(); Array.Clear(excludedButtons);
        recordingTarget = target; targetInfo = metadata; recording = session;
    });
    public Task End() => Invoke(() => { recording?.Stop(); recording = null; recordedHeld.Clear(); });
    public bool IsPhysicallyHeld(InputEvent e)
    {
        int vk = e.Kind is InputKind.KeyDown or InputKind.KeyUp ? e.VirtualKey : e.Button switch { 1 => 1, 2 => 2, 3 => 4, 4 => 5, 5 => 6, _ => 0 };
        return vk != 0 && Volatile.Read(ref physical[vk]) != 0;
    }
    public Task Invoke(Action action)
    {
        if (disposed) return Task.FromException(new ObjectDisposedException(nameof(InputService)));
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.Enqueue(() => { try { action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } });
        if (!Native.PostThreadMessageW(threadId, 0x8001, 0, 0)) tcs.TrySetException(new Win32Exception(Marshal.GetLastWin32Error()));
        return tcs.Task;
    }
    private void MessageLoop()
    {
        string cls = "MiniTaskInput_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N");
        try
        {
            threadId = Native.GetCurrentThreadId();
            var wc = new Native.WindowClass { Size = (uint)Marshal.SizeOf<Native.WindowClass>(), Proc = windowDelegate, Class = cls, Instance = Native.GetModuleHandleW(null) };
            if (Native.RegisterClassExW(ref wc) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            window = Native.CreateWindowExW(0, cls, "MiniTask input service", 0, 0, 0, 0, 0, 0, 0, wc.Instance, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!Native.WTSRegisterSessionNotification(window, 0)) SessionWarning = "Session-lock notifications are unavailable. Stop playback before locking Windows.";
            if (Native.SetTimer(window, 1, 100, 0) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            for (int k = 1; k < 256; k++) physical[k] = (Native.GetAsyncKeyState(k) & 0x8000) != 0 ? 1 : 0;
            keyboardHook = Native.SetWindowsHookExW(13, keyboardDelegate, wc.Instance, 0);
            mouseHook = Native.SetWindowsHookExW(14, mouseDelegate, wc.Instance, 0);
            if (keyboardHook == 0 || mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot install Windows input hooks.");
            ready.SetResult();
            int result;
            while ((result = Native.GetMessageW(out var message, 0, 0, 0)) > 0)
            {
                if (message.Id == 0x8001) { while (commands.TryDequeue(out var command)) command(); }
                else if (message.Id == 0x0312) Hotkey?.Invoke((int)message.WParam);
                else { Native.TranslateMessage(ref message); Native.DispatchMessageW(ref message); }
            }
            if (result < 0) SafetyStop?.Invoke("The Windows input message loop failed. Restart MiniTask.");
        }
        catch (Exception ex) { ready.TrySetException(ex); SafetyStop?.Invoke("The Windows input service failed. Restart MiniTask."); }
        finally
        {
            recording?.Stop();
            for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(0, i);
            if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
            if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
            if (window != 0) { Native.KillTimer(window, 1); Native.WTSUnRegisterSessionNotification(window); Native.DestroyWindow(window); }
            Native.UnregisterClassW(cls, Native.GetModuleHandleW(null));
        }
    }
    private nint WindowMessage(nint hwnd, uint msg, nuint wp, nint lp)
    {
        if (msg == 0x0113)
        {
            nint foreground = Native.GetForegroundWindow();
            if (foreground != 0 && !DesktopEnvironment.IsOurs(foreground)) LastExternalForeground = foreground;
        }
        if (msg == 0x02B1 && wp == 7 || msg == 0x0218 && wp == 4) SafetyStop?.Invoke("Stopped for session lock or suspend. Playback will not resume automatically.");
        if (msg == 0x007E) SafetyStop?.Invoke("Display configuration changed. Recording/playback stopped.");
        if (msg == 0x0113 && recording is { } session)
        {
            if (session.Overloaded) { recording = null; SafetyStop?.Invoke("Recording could not keep up or reached its limit. Captured input was stopped; review before saving."); }
            else if (recordingTarget != 0)
            {
                try { DesktopEnvironment.Verify(recordingTarget, targetInfo!); }
                catch (Exception) { SafetyStop?.Invoke("Recording target changed, resized or disappeared. Recording stopped."); }
            }
        }
        return Native.DefWindowProcW(hwnd, msg, wp, lp);
    }
    private nint Keyboard(int code, nuint wp, nint lp)
    {
        if (code >= 0)
        {
            try
            {
                var k = Marshal.PtrToStructure<Native.KeyboardHook>(lp);
                if (ProcessKeyboard(k, DesktopEnvironment.IsOurs(Native.GetForegroundWindow()))) return 1;
            }
            catch { recording?.Stop(); SafetyStop?.Invoke("Keyboard capture failed. Recording stopped."); }
        }
        return Native.CallNextHookEx(0, code, wp, lp);
    }
    internal bool ProcessKeyboard(Native.KeyboardHook k, bool ownWindow)
    {
        Interlocked.Increment(ref keyboardObservations);
        if ((k.Flags & 0x10) != 0) Interlocked.Increment(ref injectedKeyboardObservations);
        if (k.Extra == Native.Tag) Interlocked.Increment(ref taggedKeyboardObservations);
        if ((k.Flags & 0x10) != 0 || k.Extra == Native.Tag || k.Vk >= 256) return false;
        bool up = (k.Flags & 0x80) != 0;
        bool alreadyDown = Volatile.Read(ref physical[k.Vk]) != 0;
        Volatile.Write(ref physical[k.Vk], up ? 0 : 1);
        int control = k.Vk == hotkeys.Record ? 1 : k.Vk == hotkeys.Play ? 2 : k.Vk == hotkeys.Emergency ? 3 : 0;
        if (control != 0)
        {
            // RegisterHotKey alone cannot stop a macro while an injected modifier is held.
            // Handle physical controls regardless of modifiers and suppress both edges/repeat.
            if (!up && !alreadyDown) Hotkey?.Invoke(control);
            return true;
        }
        if (recording is not null)
        {
            var e = new InputEvent(0, up ? InputKind.KeyUp : InputKind.KeyDown, VirtualKey: (int)k.Vk, ScanCode: (int)k.Scan, Extended: (k.Flags & 1) != 0);
            if (up ? recordedHeld.Remove(e.Identity) : !ownWindow)
            { if (!up) recordedHeld.Add(e.Identity); recording.Capture(e); }
        }
        return false;
    }
    private nint Mouse(int code, nuint wp, nint lp)
    {
        if (code >= 0)
        {
            try
            {
                var m = Marshal.PtrToStructure<Native.MouseHook>(lp);
                Interlocked.Increment(ref mouseObservations);
                if ((m.Flags & 1) == 0 && m.Extra != Native.Tag)
                {
                    int message = (int)wp;
                    int button = message switch { 0x201 or 0x202 => 1, 0x204 or 0x205 => 2, 0x207 or 0x208 => 3, 0x20B or 0x20C => (int)(m.Data >> 16) + 3, _ => 0 };
                    bool up = message is 0x202 or 0x205 or 0x208 or 0x20C;
                    if (button is >= 1 and <= 5) Volatile.Write(ref physical[button switch { 1 => 1, 2 => 2, 3 => 4, 4 => 5, _ => 6 }], up ? 0 : 1);
                    if (recording is not null)
                    {
                        bool own = DesktopEnvironment.IsOurs(Native.GetAncestor(Native.WindowFromPoint(m.Point), 2));
                        if (button != 0 && !up) excludedButtons[button] = own;
                        bool exclude = button != 0 ? excludedButtons[button] : own;
                        if (up && button != 0) excludedButtons[button] = false;
                        if (!exclude)
                        {
                            InputKind kind = button != 0 ? up ? InputKind.ButtonUp : InputKind.ButtonDown : message switch
                            { 0x20A => InputKind.Wheel, 0x20E => InputKind.HorizontalWheel, _ => InputKind.Move };
                            int x = m.Point.X, y = m.Point.Y;
                            if (recordingTarget != 0) { var origin = DesktopEnvironment.ClientOrigin(recordingTarget); x -= origin.X; y -= origin.Y; }
                            recording.Capture(new(0, kind, x, y, Button: button, Delta: kind is InputKind.Wheel or InputKind.HorizontalWheel ? (short)(m.Data >> 16) : 0));
                        }
                    }
                }
            }
            catch { recording?.Stop(); SafetyStop?.Invoke("Mouse capture failed. Recording stopped."); }
        }
        return Native.CallNextHookEx(0, code, wp, lp);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Native.PostThreadMessageW(threadId, 0x0012, 0, 0);
        if (Thread.CurrentThread != thread) thread.Join();
    }
}
