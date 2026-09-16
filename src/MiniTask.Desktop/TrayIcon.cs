using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MiniTask.Desktop;

internal sealed class TrayIcon : IDisposable
{
    private const int Callback = 0x8001;
    private readonly HwndSource source;
    private readonly uint taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    private readonly Action show, stop, exit;
    private NotifyIconData data;
    private bool disposed;
    public System.Drawing.Icon Icon { get; }
    public bool Added { get; private set; }
    public string Text
    {
        get => data.Tip;
        set { data.Tip = value.Length > 127 ? value[..127] : value; if (Added) Shell_NotifyIconW(1, ref data); }
    }

    public TrayIcon(System.Drawing.Icon icon, Action show, Action stop, Action exit)
    {
        Icon = icon; this.show = show; this.stop = stop; this.exit = exit;
        // An invisible top-level window receives Explorer's TaskbarCreated broadcast.
        source = new HwndSource(new HwndSourceParameters("MiniTask tray") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) });
        source.AddHook(Message);
        data = new NotifyIconData { Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = source.Handle, Id = 1,
            Flags = 1 | 2 | 4 | 0x80, CallbackMessage = Callback, Icon = icon.Handle, Tip = "MiniTask · Ready", Info = "", InfoTitle = "" };
        Add();
    }

    private void Add()
    {
        Added = Shell_NotifyIconW(0, ref data);
        if (Added) { data.Version = 4; Shell_NotifyIconW(4, ref data); }
    }

    private nint Message(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    {
        if (disposed) return 0;
        if (taskbarCreated != 0 && (uint)message == taskbarCreated) { Add(); if (!Added) show(); return 0; }
        if (message != Callback) return 0;
        handled = true;
        int notification = (int)((long)lp & 0xFFFF);
        if (notification is 0x0203 or 0x0401) show(); // Double-click or keyboard activation.
        else if (notification == 0x007B) ShowMenu(wp);
        return 0;
    }

    private void ShowMenu(nint position)
    {
        nint menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            AppendMenuW(menu, 0, 1, "Show MiniTask");
            AppendMenuW(menu, 0, 2, "Emergency stop");
            AppendMenuW(menu, 0x800, 0, null);
            AppendMenuW(menu, 0, 3, "Exit");
            int x = unchecked((short)((long)position & 0xFFFF));
            int y = unchecked((short)(((long)position >> 16) & 0xFFFF));
            if (x == -1 && y == -1 && GetCursorPos(out var cursor)) { x = cursor.X; y = cursor.Y; }
            SetForegroundWindow(source.Handle);
            int command = TrackPopupMenuEx(menu, 0x100 | 0x80 | 2, x, y, source.Handle, 0);
            PostMessageW(source.Handle, 0, 0, 0);
            if (command == 0) Shell_NotifyIconW(3, ref data); // Return keyboard focus when the menu is dismissed.
            ExecuteCommand(command);
        }
        finally { DestroyMenu(menu); }
    }

    internal void ExecuteCommand(int command)
    {
        if (disposed) return;
        switch (command) { case 1: show(); break; case 2: stop(); break; case 3: exit(); break; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (Added) Shell_NotifyIconW(2, ref data);
        Added = false; source.RemoveHook(Message); source.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public nint Window; public uint Id, Flags, CallbackMessage; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid GuidItem; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIconW(uint command, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string message);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessageW(nint window, uint message, nint wp, nint lp);
}
