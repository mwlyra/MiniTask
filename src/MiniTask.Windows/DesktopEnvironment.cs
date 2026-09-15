using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MiniTask.Core;

namespace MiniTask.Windows;

public sealed record WindowChoice(nint Handle, WindowTarget Target)
{
    public override string ToString() => $"{Target.Title} — {Target.ProcessName}";
}
public static class DesktopEnvironment
{
    public static DesktopRect Bounds => new(Native.GetSystemMetrics(76), Native.GetSystemMetrics(77), Native.GetSystemMetrics(78), Native.GetSystemMetrics(79));
    public static string KeyboardLayout => Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _)).ToString("X");
    public static nint Foreground => Native.GetForegroundWindow();
    public static string LayoutFor(nint hwnd) => Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(hwnd, out _)).ToString("X");
    public static List<MonitorInfo> Monitors()
    {
        List<MonitorInfo> list = [];
        bool dpiFailed = false;
        bool enumerated = Native.EnumDisplayMonitors(0, 0, (nint h, nint _, ref Native.Rect r, nint d) =>
        {
            var info = new Native.MonitorData { Size = (uint)Marshal.SizeOf<Native.MonitorData>(), Device = "" };
            if (Native.GetMonitorInfoW(h, ref info))
            {
                if (Native.GetDpiForMonitor(h, 0, out uint x, out uint y) != 0) { dpiFailed = true; return false; }
                list.Add(new(info.Device, new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top), x, y));
            }
            return true;
        }, 0);
        if (!enumerated || dpiFailed || list.Count == 0) throw new InvalidOperationException("Cannot read monitor layout/scaling. Check Windows display settings.");
        return list.OrderBy(m => m.Device, StringComparer.Ordinal).ToList();
    }
    public static bool IsOurs(nint handle)
    {
        Native.GetWindowThreadProcessId(handle, out uint pid);
        return pid == Environment.ProcessId;
    }
    public static List<WindowChoice> Windows()
    {
        List<WindowChoice> list = [];
        Native.EnumWindows((hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd) || IsOurs(hwnd)) return true;
            try
            {
                var target = Describe(hwnd);
                if (target.Title.Length > 0 && target.ClientWidth > 0 && target.ClientHeight > 0) list.Add(new(hwnd, target));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        }, 0);
        return list;
    }
    public static WindowTarget Describe(nint hwnd)
    {
        if (!Native.IsWindow(hwnd)) throw new InvalidOperationException("The target window is no longer available.");
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        using var process = Process.GetProcessById((int)pid);
        var title = new StringBuilder(2001);
        var cls = new StringBuilder(501);
        Native.GetWindowTextW(hwnd, title, title.Capacity);
        Native.GetClassNameW(hwnd, cls, cls.Capacity);
        Native.GetClientRect(hwnd, out var rect);
        return new(process.ProcessName, cls.ToString(), title.ToString(), rect.Right, rect.Bottom, Native.GetDpiForWindow(hwnd));
    }
    public static (int X, int Y) ClientOrigin(nint hwnd)
    {
        Native.Point point = default;
        if (!Native.ClientToScreen(hwnd, ref point)) throw new InvalidOperationException("Cannot read target window coordinates.");
        return (point.X, point.Y);
    }
    public static nint Resolve(WindowTarget target)
    {
        var matches = Windows().Where(w => w.Target.ProcessName == target.ProcessName && w.Target.ClassName == target.ClassName && w.Target.Title == target.Title).ToList();
        if (matches.Count != 1) throw new InvalidOperationException(matches.Count == 0
            ? "Target window missing or renamed. Open the original window with the same title, or record again."
            : "Several windows match this macro. Close duplicate windows, then try again.");
        Verify(matches[0].Handle, target);
        return matches[0].Handle;
    }
    public static void Verify(nint hwnd, WindowTarget expected)
    {
        if (Native.IsIconic(hwnd)) throw new InvalidOperationException("Restore the minimized target window before playback.");
        var actual = Describe(hwnd);
        if (actual.ProcessName != expected.ProcessName || actual.ClassName != expected.ClassName || actual.Title != expected.Title)
            throw new InvalidOperationException("Target window identity changed. Playback stopped.");
        if (actual.ClientWidth != expected.ClientWidth || actual.ClientHeight != expected.ClientHeight)
            throw new InvalidOperationException($"Restore target client size to {expected.ClientWidth} × {expected.ClientHeight} pixels.");
        if (actual.Dpi != expected.Dpi) throw new InvalidOperationException("Target display scaling changed. Restore its original monitor/scaling or record again.");
    }
    public static bool Activate(nint hwnd) => Native.SetForegroundWindow(hwnd);
}
