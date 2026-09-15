using System.Diagnostics;
using System.Text.Json;
using MiniTask.Core;

int passed = 0, failed = 0;
List<double> cancellationTimes = [];
await Test("Absolute scheduling, ordering, repeated key down, hold and trailing delay", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock);
    var m = Sample() with { Events = [Key(100_000, true), Key(120_000, true), Key(200_000, false)], DurationUs = 500_000 };
    await new PlaybackEngine(clock, sink).Run(m, new(2, 3, false, 100, 0), null, default);
    Equal(string.Join(",", sink.Sent.Select(e => e.Time)), "50000,60000,100000,400000,410000,450000,750000,760000,800000");
    Equal(clock.NowUs, 950_000L);
    Equal(sink.Sent[0].Event.Kind, InputKind.KeyDown); Equal(sink.Sent[2].Event.Kind, InputKind.KeyUp);
});
await Test("Speed calculations at all presets and validated custom speed", () =>
{
    foreach (double speed in new[] { .25, .5, 1, 2, 4, 1.25 }) Equal(PlaybackEngine.Scaled(1_000_000, speed), (long)(1_000_000 / speed));
    Throws(() => MacroStorage.ValidateOptions(new(double.NaN))); Throws(() => MacroStorage.ValidateOptions(new(0)));
    Throws(() => MacroStorage.ValidateOptions(new(1, 0))); Throws(() => MacroStorage.ValidateOptions(new(GapMs: -1)));
    return Task.CompletedTask;
});
await Test("Countdown precedes events and does not consume macro duration", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock); List<int> countdowns = [];
    await new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 3), p => { if (p.CountdownRemaining > 0) countdowns.Add(p.CountdownRemaining); }, default);
    Equal(string.Join(",", countdowns), "3,2,1"); Equal(sink.Sent[0].Time, 3_100_000L); Equal(clock.NowUs, 3_500_000L);
});
await Test("Cancellation releases owned key and mouse button in reverse order", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock); using var cts = new CancellationTokenSource();
    var m = Sample() with { Events = [Key(0, true), new(10, InputKind.ButtonDown, 20, 30, Button: 1), new(999_999, InputKind.ButtonUp, 20, 30, Button: 1), Key(1_000_000, false)], DurationUs = 1_000_000 };
    sink.After = e => { if (e.Kind == InputKind.ButtonDown) cts.Cancel(); };
    await Cancelled(() => new PlaybackEngine(clock, sink).Run(m, new(CountdownSeconds: 0), null, cts.Token));
    Equal(string.Join(",", sink.Sent.Select(e => e.Event.Kind)), "KeyDown,ButtonDown,ButtonUp,KeyUp");
});
await Test("Physical hold collision aborts without injecting or releasing it", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock) { Physical = true };
    await ThrowsAsync(() => new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 0), null, default)); Equal(sink.Sent.Count, 0);
});
await Test("Physical press during playback is not released by cleanup", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock); using var cts = new CancellationTokenSource();
    sink.After = _ => { sink.Physical = true; cts.Cancel(); };
    await Cancelled(() => new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 0), null, cts.Token)); Equal(sink.Sent.Count, 1);
});
await Test("Failed injection stops, cleans up and does not replay accepted down", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock) { FailSendNumber = 2 };
    await ThrowsAsync(() => new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 0), null, default));
    Equal(sink.Attempts, 3); Equal(string.Join(",", sink.Sent.Select(e => e.Event.Kind)), "KeyDown,KeyUp");
});
await Test("Cleanup failure is reported", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock) { FailSendNumber = 2 }; using var cts = new CancellationTokenSource();
    sink.After = _ => cts.Cancel();
    try { await new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 0), null, cts.Token); throw new Exception("Expected cleanup error"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains("cleanup"), "Cleanup message missing"); }
});
await Test("Environment failure during a pause releases owned inputs", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock) { FailEnvironmentAt = 150_000 };
    await ThrowsAsync(() => new PlaybackEngine(clock, sink).Run(Sample(), new(CountdownSeconds: 0), null, default));
    Equal(sink.Sent[^1].Event.Kind, InputKind.KeyUp); Check(clock.NowUs <= 175_000, "Guard was not checked promptly");
});
await Test("Continuous playback cancels between complete cycles", async () =>
{
    var clock = new FakeClock(); var sink = new FakeSink(clock); using var cts = new CancellationTokenSource();
    await Cancelled(() => new PlaybackEngine(clock, sink).Run(Sample(), new(Continuous: true, CountdownSeconds: 0), p => { if (p.Repetition == 3 && p.Fraction == 1) cts.Cancel(); }, cts.Token));
    Equal(sink.Sent.Count, 6); Equal(clock.NowUs, 1_500_000L);
});
await Test("Real cancellation during a 1-hour wait (20 trials)", async () =>
{
    for (int i = 0; i < 20; i++)
    {
        var clock = new MonotonicClock(); using var cts = new CancellationTokenSource();
        var waiting = clock.WaitUntil(clock.NowUs + 3_600_000_000, cts.Token).AsTask();
        await Task.Delay(5);
        var watch = Stopwatch.StartNew(); cts.Cancel(); await Cancelled(() => waiting); watch.Stop();
        cancellationTimes.Add(watch.Elapsed.TotalMilliseconds);
        Check(watch.ElapsedMilliseconds < 1000, "Cancellation exceeded 1 second");
    }
});
await Test("Recording preserves order, repeat and intentional trailing delay", async () =>
{
    var clock = new FakeClock(); var recording = new RecordingSession(new(), clock);
    clock.NowUs = 100; recording.Capture(Key(0, true)); clock.NowUs = 200; recording.Capture(Key(0, true));
    clock.NowUs = 400; recording.Capture(Key(0, false)); clock.NowUs = 1000; recording.Stop();
    var m = await recording.Completion; Equal(string.Join(",", m.Events.Select(e => e.AtUs)), "100,200,400"); Equal(m.DurationUs, 1000L);
});
await Test("Recording ignores orphan releases and seals held input on stop", async () =>
{
    var clock = new FakeClock(); var recording = new RecordingSession(new(), clock);
    recording.Capture(Key(0, false)); clock.NowUs = 10; recording.Capture(Key(0, true)); clock.NowUs = 100; recording.Stop();
    var m = await recording.Completion; Equal(m.Events.Count, 2); Equal(m.Events[^1].Kind, InputKind.KeyUp); Equal(m.Events[^1].AtUs, 100L);
});
await Test("Bounded queue overload stops visibly and preserves valid captured prefix", async () =>
{
    var clock = new FakeClock(); var recording = new RecordingSession(new(), clock, 1);
    for (int i = 0; i < 100000 && !recording.Overloaded; i++) { clock.NowUs++; recording.Capture(new(0, InputKind.Move, i % 100, 0)); }
    recording.Stop(); var m = await recording.Completion; Check(recording.Overloaded, "Queue did not overload"); MacroStorage.Validate(m);
});
await Test("Invalid versions, ordering, durations, enums and unmatched states rejected", () =>
{
    foreach (var m in new[] { Sample() with { Version = 2 }, Sample() with { DurationUs = -1 }, Sample() with { Events = [Key(20, true), Key(10, false)] },
        Sample() with { Events = [Key(10, false)] }, Sample() with { Events = [Key(10, true)] }, Sample() with { Events = [new(10, (InputKind)123)] },
        Sample() with { Events = [new(10, InputKind.Move, int.MaxValue)] }, Sample() with { CoordinateMode = CoordinateMode.Window },
        Sample() with { Events = [new(10, InputKind.Wheel, Delta: int.MinValue)] } }) Throws(() => MacroStorage.Validate(m));
    return Task.CompletedTask;
});
await Test("Duplicate button down rejected, repeated key down accepted", () =>
{
    Throws(() => MacroStorage.Validate(Sample() with { Events = [new(0, InputKind.ButtonDown, Button: 1), new(1, InputKind.ButtonDown, Button: 1), new(2, InputKind.ButtonUp, Button: 1)] }));
    MacroStorage.Validate(Sample() with { Events = [Key(0, true), Key(1, true), Key(2, false)] }); return Task.CompletedTask;
});
await Test("Save/load round trip and atomic replacement", () =>
{
    string dir = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-temp"); Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, "roundtrip.minitask");
    try
    {
        MacroStorage.Save(path, Sample()); var loaded = MacroStorage.Load(path); Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(Sample()));
        MacroStorage.Save(path, Sample() with { Name = "replacement" }); Equal(MacroStorage.Load(path).Name, "replacement");
        Check(!Directory.GetFiles(dir, "*.tmp").Any(), "Temporary file leaked");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
    return Task.CompletedTask;
});
await Test("Oversized file/event list and malicious JSON rejected", () =>
{
    string path = Path.Combine(Environment.CurrentDirectory, "artifacts", "oversize.minitask"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    try
    {
        using (var file = File.Create(path)) file.SetLength(MacroStorage.MaxBytes + 1);
        Throws(() => MacroStorage.Load(path));
        foreach (string json in new[] { "null", "{\"version\":999}", "{\"script\":\"anything\"}", "{\"events\":[{\"kind\":\"Execute\"}]}" })
        { File.WriteAllText(path, json); Throws(() => MacroStorage.Load(path)); }
        Throws(() => MacroStorage.Validate(Sample() with { Events = Enumerable.Repeat(new InputEvent(0, InputKind.Move), MacroStorage.MaxEvents + 1).ToList() }));
    }
    finally { File.Delete(path); } return Task.CompletedTask;
});
await Test("Negative desktop coordinates, normalization edges and window anchoring", () =>
{
    var d = new DesktopRect(-1920, -500, 3840, 1580);
    Equal(Coordinates.Normalize(-1920, -500, d), (0, 0)); Equal(Coordinates.Normalize(1919, 1079, d), (65535, 65535));
    Equal(Coordinates.Anchor(-20, 50, -1000, 250), (-1020, 300)); Throws(() => Coordinates.Normalize(1920, 0, d)); return Task.CompletedTask;
});
await Test("Required format header and streaming event-count bound", () =>
{
    string path = Path.Combine(Environment.CurrentDirectory, "artifacts", "bounded-json.minitask"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    try
    {
        File.WriteAllText(path, "{}"); Throws(() => MacroStorage.Load(path));
        using (var writer = new StreamWriter(path))
        {
            writer.Write("{\"events\":[");
            for (int i = 0; i <= MacroStorage.MaxEvents; i++) { if (i != 0) writer.Write(','); writer.Write("{\"kind\":\"Move\"}"); }
            writer.Write("]}");
        }
        try { MacroStorage.Load(path); throw new Exception("Expected bounded read rejection"); }
        catch (JsonException ex) { Check(ex.Message.Contains("500,000"), "Event cap did not reject while reading"); }
    }
    finally { File.Delete(path); } return Task.CompletedTask;
});
await Test("State transitions exclude overlapping/rapid runs", () =>
{
    var gate = new RunGate(); Equal(gate.State, RunState.Idle); Check(gate.TryBegin(RunState.Recording), "Start failed");
    Check(!gate.TryBegin(RunState.Playing), "Overlap allowed"); gate.Stopping(); Check(!gate.TryBegin(RunState.Recording), "Restart while stopping");
    gate.Finish(); Check(gate.TryBegin(RunState.Countdown), "Countdown failed"); gate.Playing(); Equal(gate.State, RunState.Playing);
    gate.Stopping(); gate.Playing(); Equal(gate.State, RunState.Stopping); gate.Finish(true); Check(gate.TryBegin(RunState.Playing), "Error cannot recover");
    gate.Finish(); int winners = 0;
    Parallel.For(0, 1000, _ => { if (gate.TryBegin(RunState.Playing)) Interlocked.Increment(ref winners); }); Equal(winners, 1);
    return Task.CompletedTask;
});
Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
if (cancellationTimes.Count > 0) Console.WriteLine($"Cancellation detection, 20 real-clock trials: max {cancellationTimes.Max():F3} ms; average {cancellationTimes.Average():F3} ms. Not a hard real-time guarantee.");
return failed == 0 ? 0 : 1;

async Task Test(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex}"); }
}
static Macro Sample() => new() { DurationUs = 500_000, Events = [Key(100_000, true), Key(200_000, false)] };
static InputEvent Key(long at, bool down) => new(at, down ? InputKind.KeyDown : InputKind.KeyUp, VirtualKey: 65, ScanCode: 30);
static void Equal<T>(T actual, T expected) { if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new Exception($"Expected {expected}; actual {actual}"); }
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
static async Task ThrowsAsync(Func<Task> action) { try { await action(); } catch (Exception) { return; } throw new Exception("Expected failure"); }
static async Task Cancelled(Func<Task> action) { try { await action(); } catch (OperationCanceledException) { return; } throw new Exception("Expected cancellation"); }
sealed class FakeClock : IPlaybackClock
{
    public long NowUs { get; set; }
    public ValueTask WaitUntil(long deadlineUs, CancellationToken token) { token.ThrowIfCancellationRequested(); NowUs = Math.Max(NowUs, deadlineUs); return ValueTask.CompletedTask; }
}
sealed class FakeSink(IPlaybackClock clock) : IInputSink
{
    public List<(long Time, InputEvent Event)> Sent { get; } = [];
    public bool Physical { get; set; }
    public int FailSendNumber { get; init; }
    public long FailEnvironmentAt { get; init; } = long.MaxValue;
    public int Attempts { get; private set; }
    public Action<InputEvent>? After { get; set; }
    public bool IsPhysicallyHeld(InputEvent input) => Physical;
    public void Send(InputEvent input) { if (++Attempts == FailSendNumber) throw new InvalidOperationException("Rejected input"); Sent.Add((clock.NowUs, input)); After?.Invoke(input); }
    public void CheckEnvironment() { if (clock.NowUs >= FailEnvironmentAt) throw new InvalidOperationException("Focus changed"); }
}
