namespace MiniTask.Core;

public enum InputKind { KeyDown, KeyUp, Move, ButtonDown, ButtonUp, Wheel, HorizontalWheel }
public enum CoordinateMode { Screen, Window }
public enum RunState { Idle, Countdown, Recording, Playing, Stopping, Error }
public sealed record InputEvent(long AtUs, InputKind Kind, int X = 0, int Y = 0,
    int VirtualKey = 0, int ScanCode = 0, bool Extended = false, int Button = 0, int Delta = 0)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => Kind is InputKind.KeyDown or InputKind.KeyUp
        ? $"k:{VirtualKey}:{ScanCode}:{Extended}" : $"b:{Button}";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDown => Kind is InputKind.KeyDown or InputKind.ButtonDown;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUp => Kind is InputKind.KeyUp or InputKind.ButtonUp;
    public InputEvent Release(long at) => this with { AtUs = at, Kind = Kind == InputKind.KeyDown ? InputKind.KeyUp : InputKind.ButtonUp };
}
public sealed record DesktopRect(int X, int Y, int Width, int Height);
public sealed record MonitorInfo(string Device, DesktopRect Bounds, uint DpiX = 96, uint DpiY = 96);
public sealed record WindowTarget(string ProcessName, string ClassName, string Title, int ClientWidth, int ClientHeight, uint Dpi = 96);
public sealed record PlaybackOptions(double Speed = 1, int Repetitions = 1, bool Continuous = false,
    int GapMs = 0, int CountdownSeconds = 3, bool StopOnFocusLoss = false);
public sealed record Macro
{
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; init; } = 1;
    public string? Name { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public long DurationUs { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public CoordinateMode CoordinateMode { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public DesktopRect Desktop { get; init; } = new(0, 0, 1920, 1080);
    [System.Text.Json.Serialization.JsonRequired]
    public List<MonitorInfo> Monitors { get; init; } = [];
    [System.Text.Json.Serialization.JsonRequired]
    public string KeyboardLayout { get; init; } = "";
    public WindowTarget? Target { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public PlaybackOptions Playback { get; init; } = new();
    [System.Text.Json.Serialization.JsonRequired]
    public List<InputEvent> Events { get; init; } = [];
}
public static class Coordinates
{
    public static (int X, int Y) Normalize(int x, int y, DesktopRect desktop)
    {
        if (desktop.Width < 2 || desktop.Height < 2) throw new InvalidDataException("Desktop size is invalid.");
        if (x < desktop.X || y < desktop.Y || (long)x >= (long)desktop.X + desktop.Width || (long)y >= (long)desktop.Y + desktop.Height)
            throw new InvalidDataException("A recorded position is outside the current desktop. Restore the display arrangement.");
        return ((int)Math.Round((x - (double)desktop.X) * 65535 / (desktop.Width - 1)),
            (int)Math.Round((y - (double)desktop.Y) * 65535 / (desktop.Height - 1)));
    }
    public static (int X, int Y) Anchor(int x, int y, int clientX, int clientY) => (checked(x + clientX), checked(y + clientY));
}
