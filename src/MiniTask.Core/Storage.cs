using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniTask.Core;

public static class MacroStorage
{
    public const int MaxEvents = 500_000;
    public const long MaxBytes = 100 * 1024 * 1024;
    public const long MaxDurationUs = 24L * 60 * 60 * 1_000_000;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new EventListConverter() }
    };
    public static void ValidateOptions(PlaybackOptions p)
    {
        if (p is null || !double.IsFinite(p.Speed) || p.Speed is < 0.05 or > 20 || p.Repetitions is < 1 or > 1_000_000 ||
            p.GapMs is < 0 or > 3_600_000 || p.CountdownSeconds is < 0 or > 60)
            throw new InvalidDataException("Use speed 0.05–20×, repetitions 1–1,000,000, gap 0–3,600,000 ms and countdown 0–60 s.");
    }
    public static void Validate(Macro m)
    {
        if (m.Version != 1) throw new InvalidDataException("Unsupported MiniTask format version.");
        if (m.Events is null || m.Events.Count > MaxEvents) throw new InvalidDataException("Macro exceeds 500,000 events.");
        if (m.DurationUs is < 0 or > MaxDurationUs) throw new InvalidDataException("Macro duration must be between 0 and 24 hours.");
        if (!Enum.IsDefined(m.CoordinateMode) || m.Desktop is null || m.Desktop.Width is < 2 or > 100000 || m.Desktop.Height is < 2 or > 100000 ||
            Math.Abs((long)m.Desktop.X) > 100000 || Math.Abs((long)m.Desktop.Y) > 100000 || m.Name?.Length > 500 || m.KeyboardLayout is null || m.KeyboardLayout.Length > 100 ||
            m.Monitors is null || m.Monitors.Count > 64 || m.Monitors.Any(d => d is null || d.Bounds is null || d.Device is null || d.Device.Length > 256 || d.Bounds.Width is < 1 or > 100000 || d.Bounds.Height is < 1 or > 100000 || Math.Abs((long)d.Bounds.X) > 100000 || Math.Abs((long)d.Bounds.Y) > 100000 || d.DpiX is < 48 or > 960 || d.DpiY is < 48 or > 960))
            throw new InvalidDataException("Invalid desktop or macro metadata.");
        if (m.CoordinateMode == CoordinateMode.Window && m.Target is null) throw new InvalidDataException("Window mode needs target metadata.");
        if (m.Target is { } t && (string.IsNullOrWhiteSpace(t.ProcessName) || string.IsNullOrWhiteSpace(t.ClassName) || t.Title is null ||
            t.ProcessName.Length > 500 || t.ClassName.Length > 500 || t.Title.Length > 2000 || t.ClientWidth is < 1 or > 100000 || t.ClientHeight is < 1 or > 100000 || t.Dpi is < 48 or > 960))
            throw new InvalidDataException("Invalid window target.");
        ValidateOptions(m.Playback);
        var held = new Dictionary<string, InputEvent>();
        long previous = 0;
        foreach (var e in m.Events)
        {
            if (e is null || !Enum.IsDefined(e.Kind) || e.AtUs < previous || e.AtUs > m.DurationUs || e.AtUs < 0 ||
                Math.Abs((long)e.X) > 200000 || Math.Abs((long)e.Y) > 200000)
                throw new InvalidDataException("Events must have known types, valid coordinates and ordered timestamps within the duration.");
            if (e.Kind is InputKind.KeyDown or InputKind.KeyUp && (e.VirtualKey is < 1 or > 255 || e.ScanCode is < 0 or > 255))
                throw new InvalidDataException("Invalid keyboard event.");
            if (e.Kind is InputKind.ButtonDown or InputKind.ButtonUp && e.Button is < 1 or > 5)
                throw new InvalidDataException("Invalid mouse button.");
            if (e.Kind is InputKind.Wheel or InputKind.HorizontalWheel && (e.Delta == 0 || Math.Abs((long)e.Delta) > 32767))
                throw new InvalidDataException("Invalid wheel delta.");
            if (m.CoordinateMode == CoordinateMode.Screen && e.Kind is not (InputKind.KeyDown or InputKind.KeyUp))
            {
                Coordinates.Normalize(e.X, e.Y, m.Desktop);
                if (m.Monitors.Count > 0 && !m.Monitors.Any(d => e.X >= d.Bounds.X && e.Y >= d.Bounds.Y && (long)e.X < (long)d.Bounds.X + d.Bounds.Width && (long)e.Y < (long)d.Bounds.Y + d.Bounds.Height))
                    throw new InvalidDataException("A mouse position falls in a gap between monitors.");
            }
            if (e.IsDown)
            {
                if (e.Kind == InputKind.ButtonDown && held.ContainsKey(e.Identity)) throw new InvalidDataException("Duplicate mouse button down.");
                held[e.Identity] = e; // Repeated key downs preserve Windows key repeat.
            }
            if (e.IsUp && !held.Remove(e.Identity)) throw new InvalidDataException("Release without a matching press.");
            previous = e.AtUs;
        }
        if (held.Count != 0) throw new InvalidDataException("Macro contains inputs without matching releases.");
    }
    public static Macro Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Macro exceeds the 100 MiB file limit.");
        var m = JsonSerializer.Deserialize<Macro>(stream, Json) ?? throw new InvalidDataException("Empty macro file.");
        Validate(m);
        return m;
    }
    public static void Save(string path, Macro macro)
    {
        Validate(macro);
        AtomicWrite(path, stream => JsonSerializer.Serialize(stream, macro, Json));
    }
    public static void AtomicWrite(string path, Action<Stream> writer)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writer(f);
                if (f.Length > MaxBytes) throw new InvalidDataException("File exceeds the 100 MiB limit.");
                f.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
internal sealed class EventListConverter : JsonConverter<List<InputEvent>>
{
    public override List<InputEvent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Events must be an array.");
        List<InputEvent> events = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (events.Count >= MacroStorage.MaxEvents) throw new JsonException("Macro exceeds 500,000 events.");
            events.Add(JsonSerializer.Deserialize<InputEvent>(ref reader, options) ?? throw new JsonException("Null event."));
        }
        if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Incomplete events array.");
        return events;
    }
    public override void Write(Utf8JsonWriter writer, List<InputEvent> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray(); foreach (var e in value) JsonSerializer.Serialize(writer, e, options); writer.WriteEndArray();
    }
}
