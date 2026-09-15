using System.Threading.Channels;

namespace MiniTask.Core;

public sealed class RecordingSession
{
    private readonly Channel<InputEvent> queue;
    private readonly IPlaybackClock clock;
    private readonly long start;
    private long duration;
    private int stopped;
    private int overflow;
    public bool Overloaded => Volatile.Read(ref overflow) != 0;
    public long ElapsedUs => Volatile.Read(ref stopped) == 0 ? clock.NowUs - start : duration;
    public Task<Macro> Completion { get; }
    public RecordingSession(Macro metadata, IPlaybackClock clock, int capacity = 32768)
    {
        this.clock = clock;
        start = clock.NowUs;
        queue = Channel.CreateBounded<InputEvent>(new BoundedChannelOptions(capacity)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
        Completion = Task.Run(() => Consume(metadata));
    }
    // One producer: the Windows hook/message thread. Never wait inside a hook.
    public bool Capture(InputEvent input)
    {
        if (Volatile.Read(ref stopped) != 0) return false;
        long at = clock.NowUs - start;
        if (at > MacroStorage.MaxDurationUs || !queue.Writer.TryWrite(input with { AtUs = at }))
        {
            Interlocked.Exchange(ref overflow, 1);
            Stop();
            return false;
        }
        return true;
    }
    public void Stop()
    {
        if (Interlocked.CompareExchange(ref stopped, 1, 0) != 0) return;
        duration = Math.Min(MacroStorage.MaxDurationUs, clock.NowUs - start);
        queue.Writer.TryComplete();
    }
    private async Task<Macro> Consume(Macro metadata)
    {
        List<InputEvent> events = [];
        Dictionary<string, InputEvent> held = [];
        await foreach (var e in queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (events.Count >= MacroStorage.MaxEvents - 512)
            {
                Interlocked.Exchange(ref overflow, 1);
                Stop();
                continue;
            }
            if (e.IsUp && !held.Remove(e.Identity)) continue; // A key held before Record is not ours.
            if (e.IsDown)
            {
                if (e.Kind == InputKind.ButtonDown && held.ContainsKey(e.Identity)) continue;
                held[e.Identity] = e;
            }
            events.Add(e);
        }
        duration = Math.Max(duration, events.Count == 0 ? 0 : events[^1].AtUs);
        foreach (var e in held.Values.Reverse()) events.Add(e.Release(duration));
        var result = metadata with { DurationUs = duration, Events = events };
        MacroStorage.Validate(result);
        return result;
    }
}
public sealed class RunGate
{
    private readonly object sync = new();
    private RunState state;
    public RunState State { get { lock (sync) return state; } }
    public bool TryBegin(RunState next)
    {
        if (next is not (RunState.Recording or RunState.Countdown or RunState.Playing)) throw new ArgumentException("Invalid start state.");
        lock (sync)
        {
            if (state is not (RunState.Idle or RunState.Error)) return false;
            state = next;
            return true;
        }
    }
    public void Playing() { lock (sync) { if (state == RunState.Countdown) state = RunState.Playing; } }
    public void Stopping() { lock (sync) { if (state is RunState.Countdown or RunState.Playing or RunState.Recording) state = RunState.Stopping; } }
    public void Finish(bool error = false) { lock (sync) state = error ? RunState.Error : RunState.Idle; }
}
