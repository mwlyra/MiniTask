using MiniTask.Core;

namespace MiniTask.Windows;

public sealed class AppController : IAsyncDisposable
{
    private readonly object sync = new();
    public InputService Input { get; } = new();
    public RunGate Gate { get; } = new();
    private readonly MonotonicClock clock = new();
    private RecordingSession? recording;
    private CancellationTokenSource? cancellation;
    private Task active = Task.CompletedTask;
    private string? stopReason;
    public Macro? Macro { get; set; }
    public string Status { get; private set; } = "Ready";
    public PlaybackProgress? Progress { get; private set; }
    public long RecordingElapsedUs => recording?.ElapsedUs ?? 0;
    public event Action? RecordRequested;
    public event Action? PlayRequested;
    public event Action<Macro>? Recorded;
    public event Action<string>? Failed;
    public event Action<Exception>? TechnicalFailure;
    public AppController()
    {
        Input.Hotkey += id =>
        {
            if (id == 3 || id == 1 && Gate.State == RunState.Recording || id == 2 && Gate.State is RunState.Playing or RunState.Countdown)
                Stop();
            else if (id == 1) RecordRequested?.Invoke();
            else if (id == 2) PlayRequested?.Invoke();
        };
        Input.SafetyStop += Stop;
    }
    public bool StartRecording(Macro metadata, nint target)
    {
        lock (sync)
        {
            if (!Gate.TryBegin(RunState.Recording)) return false;
            stopReason = null; Progress = null; Status = "Recording";
            recording = new(metadata, clock);
            active = Record(recording, Input.Begin(recording, target, metadata.Target));
            return true;
        }
    }
    private async Task Record(RecordingSession session, Task begin)
    {
        bool failed = false;
        try
        {
            await begin.ConfigureAwait(false);
            var result = await session.Completion.ConfigureAwait(false);
            Macro = result;
            Recorded?.Invoke(result);
            if (session.Overloaded) throw new InvalidOperationException("Recording stopped at a capture limit. Review the captured macro before using it.");
        }
        catch (Exception ex) { failed = true; Report(ex); }
        finally
        {
            await Input.End().ConfigureAwait(false);
            lock (sync) { recording = null; Status = stopReason ?? (failed ? Status : "Ready · recording captured"); Gate.Finish(failed); }
        }
    }
    public bool Play(PlaybackOptions options, nint target)
    {
        lock (sync)
        {
            if (Macro is null || !Gate.TryBegin(options.CountdownSeconds > 0 ? RunState.Countdown : RunState.Playing)) return false;
            stopReason = null; Progress = null;
            cancellation = new();
            var macro = Macro;
            var token = cancellation.Token;
            Status = "Preparing playback";
            active = Task.Run(async () =>
            {
                bool failed = false;
                try
                {
                    // Capture active target after countdown, so focus protection does not anchor to the toolbar.
                    var sink = new DeferredSink(() => new WindowsInputSink(Input, macro, options, target));
                    await new PlaybackEngine(clock, sink).Run(macro, options, p =>
                    {
                        Progress = p;
                        if (p.CountdownRemaining == 0) Gate.Playing();
                        Status = p.CountdownRemaining > 0 ? $"Starts in {p.CountdownRemaining}…" : $"Playing · repetition {p.Repetition}{(options.Continuous ? " · continuous" : $" of {options.Repetitions}")}";
                    }, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { failed = true; Report(ex); }
                finally
                {
                    lock (sync)
                    {
                        cancellation?.Dispose(); cancellation = null;
                        Status = stopReason ?? (failed ? Status : token.IsCancellationRequested ? "Stopped" : "Ready · playback finished");
                        Gate.Finish(failed);
                    }
                }
            });
            return true;
        }
    }
    private sealed class DeferredSink(Func<IInputSink> factory) : IInputSink
    {
        private IInputSink? instance;
        private IInputSink Value => instance ??= factory();
        public bool IsPhysicallyHeld(InputEvent e) => Value.IsPhysicallyHeld(e);
        public void Send(InputEvent e) => Value.Send(e);
        public void CheckEnvironment() => Value.CheckEnvironment();
        public void ReleaseHeld(InputEvent e) => Value.ReleaseHeld(e);
    }
    public void Stop() => Stop(null);
    public void Stop(string? reason)
    {
        lock (sync)
        {
            if (Gate.State is RunState.Idle or RunState.Error) return;
            stopReason = reason;
            Gate.Stopping(); Status = "Stopping…";
            cancellation?.Cancel();
            if (recording is not null) _ = Input.End();
        }
    }
    public Task WaitForStop() { lock (sync) return active; }
    private void Report(Exception ex) { Status = ex.Message; TechnicalFailure?.Invoke(ex); Failed?.Invoke(ex.Message); }
    public async ValueTask DisposeAsync()
    {
        Stop();
        await WaitForStop().ConfigureAwait(false);
        Input.Dispose();
    }
}
