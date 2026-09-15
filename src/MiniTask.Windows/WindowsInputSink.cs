using System.ComponentModel;
using System.Runtime.InteropServices;
using MiniTask.Core;

namespace MiniTask.Windows;

public sealed class WindowsInputSink : IInputSink
{
    private readonly InputService input;
    private readonly Macro macro;
    private readonly nint target;
    private readonly bool focusProtection;
    private readonly nint initialFocus;
    private readonly uint targetPid;
    private readonly uint targetThread;
    private readonly MonotonicClock clock = new();
    private long nextDisplayCheck;
    private long nextTargetCheck;
    public WindowsInputSink(InputService input, Macro macro, PlaybackOptions options, nint target)
    {
        this.input = input; this.macro = macro; this.target = target;
        focusProtection = options.StopOnFocusLoss;
        initialFocus = target != 0 ? target : DesktopEnvironment.Foreground;
        if (target != 0) targetThread = Native.GetWindowThreadProcessId(target, out targetPid);
    }
    public bool IsPhysicallyHeld(InputEvent e) => input.IsPhysicallyHeld(e);
    public void CheckEnvironment()
    {
        if (focusProtection && Native.GetForegroundWindow() != initialFocus)
            throw new InvalidOperationException("Target lost focus. Playback stopped.");
        if (target != 0 && clock.NowUs >= nextTargetCheck)
        {
            nextTargetCheck = clock.NowUs + 25_000;
            uint thread = Native.GetWindowThreadProcessId(target, out uint pid);
            if (thread != targetThread || pid != targetPid) throw new InvalidOperationException("Target window was replaced. Playback stopped.");
            DesktopEnvironment.Verify(target, macro.Target!);
        }
        if (clock.NowUs >= nextDisplayCheck)
        {
            nextDisplayCheck = clock.NowUs + 100_000;
            if (DesktopEnvironment.Bounds != macro.Desktop || !DesktopEnvironment.Monitors().SequenceEqual(macro.Monitors))
                throw new InvalidOperationException("Display arrangement changed. Restore the recorded monitor arrangement.");
        }
    }
    public void Send(InputEvent e)
    {
        Native.Input native = default;
        if (e.Kind is InputKind.KeyDown or InputKind.KeyUp)
        {
            native.Type = 1;
            bool useScan = e.ScanCode != 0 && e.VirtualKey != 0x13; // Pause has special scan-code semantics.
            native.Union.Keyboard = new()
            {
                Vk = useScan ? (ushort)0 : (ushort)e.VirtualKey, Scan = useScan ? (ushort)e.ScanCode : (ushort)0,
                Flags = (useScan ? 8u : 0) | (e.Extended ? 1u : 0) | (e.Kind == InputKind.KeyUp ? 2u : 0), Extra = Native.Tag
            };
        }
        else
        {
            int x = e.X, y = e.Y;
            if (macro.CoordinateMode == CoordinateMode.Window)
            {
                var origin = DesktopEnvironment.ClientOrigin(target);
                (x, y) = Coordinates.Anchor(x, y, origin.X, origin.Y);
            }
            var point = Coordinates.Normalize(x, y, DesktopEnvironment.Bounds);
            uint flags = 0x8000 | 0x4000 | 0x0001;
            uint data = 0;
            if (e.Kind is InputKind.ButtonDown or InputKind.ButtonUp)
            {
                bool down = e.Kind == InputKind.ButtonDown;
                flags |= e.Button switch { 1 => down ? 2u : 4u, 2 => down ? 8u : 16u, 3 => down ? 32u : 64u, _ => down ? 128u : 256u };
                if (e.Button >= 4) data = (uint)(e.Button - 3);
            }
            else if (e.Kind is InputKind.Wheel or InputKind.HorizontalWheel)
            { flags |= e.Kind == InputKind.Wheel ? 0x0800u : 0x1000u; data = unchecked((uint)e.Delta); }
            native.Union.Mouse = new() { X = point.X, Y = point.Y, Flags = flags, Data = data, Extra = Native.Tag };
        }
        Inject(native);
    }
    public void ReleaseHeld(InputEvent input)
    {
        if (input.Kind == InputKind.KeyDown) { Send(input.Release(0)); return; }
        // Cleanup releases must work without moving the cursor, even if the target/display disappears.
        Native.Input native = default;
        native.Union.Mouse = new() { Flags = input.Button switch { 1 => 4u, 2 => 16u, 3 => 64u, _ => 256u },
            Data = input.Button >= 4 ? (uint)(input.Button - 3) : 0, Extra = Native.Tag };
        Inject(native);
    }
    private static void Inject(Native.Input native)
    {
        uint accepted = Native.SendInput(1, [native], Marshal.SizeOf<Native.Input>());
        if (accepted != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected simulated input. Check target privilege level; input is not retried.");
    }
}
