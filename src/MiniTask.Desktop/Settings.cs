using System.Text.Json;

namespace MiniTask.Desktop;

public sealed record Settings
{
    public Hotkeys Hotkeys { get; init; } = new();
    public PlaybackOptions Playback { get; init; } = new(CountdownSeconds: 0);
    public CoordinateMode CoordinateMode { get; init; }
    public string Theme { get; init; } = "Light";
    public bool ShowCaptions { get; init; } = true;
    public bool SoundCues { get; init; }
    public bool AlwaysOnTop { get; init; }
    public bool TrayOnMinimize { get; init; } = true;
    public bool PrivacyAccepted { get; init; }
    public List<string> Recent { get; init; } = [];
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniTask");
    public static Settings Load(out string? warning)
    {
        warning = null;
        try
        {
            string path = Path.Combine(Folder, "settings.json");
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException();
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), MacroStorage.Json) ?? new();
            s.Hotkeys.Validate(); MacroStorage.ValidateOptions(s.Playback);
            if (s.Theme is not ("Light" or "Dark" or "System") || !Enum.IsDefined(s.CoordinateMode) || s.Recent is null || s.Recent.Count > 10) throw new InvalidDataException();
            return s;
        }
        catch { warning = "Local settings could not be read. Defaults restored; check shortcuts in Settings."; return new(); }
    }
    public void Save() => MacroStorage.AtomicWrite(Path.Combine(Folder, "settings.json"), s => JsonSerializer.Serialize(s, this, MacroStorage.Json));
}
public static class Diagnostics
{
    public static void Failure(string component, Exception exception)
    {
        // No exception message, filenames or input payload: errors can include sensitive user data.
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            string path = Path.Combine(Settings.Folder, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.WriteAllText(path, "");
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {component} {exception.GetType().Name} HRESULT=0x{exception.HResult:X8}{Environment.NewLine}");
        }
        catch { }
    }
}
