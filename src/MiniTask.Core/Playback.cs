using System.Diagnostics;

namespace MiniTask.Core;

public interface IPlaybackClock
{
    long NowUs { get; }
    ValueTask WaitUntil(long deadlineUs, CancellationToken token);
}
public sealed class MonotonicClock : IPlaybackClock
{
    public long NowUs => (long)(Stopwatch.GetTimestamp() * (1_000_000d / Stopwatch.Frequency));
    public async ValueTask WaitUntil(long deadlineUs, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            long left = deadlineUs - NowUs;
            if (left <= 0) return;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(25, left / 1000d))), token).ConfigureAwait(false);
        }
    }
}
public interface IInputSink
{
    bool IsPhysicallyHeld(InputEvent input);
    void Send(InputEvent input);
    void CheckEnvironment();
    void ReleaseHeld(InputEvent input) => Send(input.Release(0));
}
public sealed record PlaybackProgress(int Repetition, double Fraction, int CountdownRemaining = 0);
public sealed class PlaybackEngine(IPlaybackClock clock, IInputSink sink)
{
    public static long Scaled(long timeUs, double speed) => checked((long)Math.Round(timeUs / speed));
    public async Task Run(Macro macro, PlaybackOptions options, Action<PlaybackProgress>? progress, CancellationToken token)
    {
        MacroStorage.Validate(macro);
        MacroStorage.ValidateOptions(options);
        if (macro.Events.Count == 0 || macro.DurationUs <= 0) throw new InvalidDataException("Record some input before playback.");
        var held = new Dictionary<string, InputEvent>();
        Exception? failure = null;
        try
        {
            long start = clock.NowUs;
            for (int remaining = options.CountdownSeconds; remaining > 0; remaining--)
            {
                progress?.Invoke(new(0, 0, remaining));
                await clock.WaitUntil(start + (options.CountdownSeconds - remaining + 1) * 1_000_000L, token).ConfigureAwait(false);
            }
            sink.CheckEnvironment();
            long cycle = clock.NowUs;
            long length = Scaled(macro.DurationUs, options.Speed);
            for (int repetition = 1; options.Continuous || repetition <= options.Repetitions; repetition++)
            {
                progress?.Invoke(new(repetition, 0));
                long nextProgress = clock.NowUs + 100_000;
                void ReportProgress()
                {
                    if (clock.NowUs < nextProgress) return;
                    nextProgress = clock.NowUs + 100_000;
                    progress?.Invoke(new(repetition, Math.Clamp((clock.NowUs - cycle) / (double)length, 0, 1)));
                }
                foreach (var input in macro.Events)
                {
                    await WaitChecked(cycle + Scaled(input.AtUs, options.Speed), token, ReportProgress).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    sink.CheckEnvironment();
                    if ((input.IsDown || input.IsUp) && sink.IsPhysicallyHeld(input))
                        throw new InvalidOperationException("A macro key or button is physically held. Release it, then try again.");
                    sink.Send(input);
                    if (input.IsDown) held[input.Identity] = input;
                    else if (input.IsUp) held.Remove(input.Identity);
                }
                await WaitChecked(cycle + length, token, ReportProgress).ConfigureAwait(false);
                progress?.Invoke(new(repetition, 1));
                if (!options.Continuous && repetition == options.Repetitions) break;
                cycle += length + options.GapMs * 1000L;
                await WaitChecked(cycle, token).ConfigureAwait(false);
                // An overloaded cycle never causes an unbounded burst of catch-up repetitions.
                cycle = Math.Max(cycle, clock.NowUs);
                if (repetition == int.MaxValue) repetition = 0;
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            List<Exception> cleanupErrors = [];
            foreach (var input in held.Values.Reverse())
            {
                try { if (!sink.IsPhysicallyHeld(input)) sink.ReleaseHeld(input); }
                catch (Exception ex) { cleanupErrors.Add(ex); }
            }
            if (cleanupErrors.Count != 0)
                failure = new InvalidOperationException("Windows rejected input cleanup. Release any held macro keys/buttons manually.", new AggregateException(cleanupErrors));
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private async ValueTask WaitChecked(long deadline, CancellationToken token, Action? progress = null)
    {
        while (clock.NowUs < deadline)
        {
            sink.CheckEnvironment();
            await clock.WaitUntil(Math.Min(deadline, clock.NowUs + 25_000), token).ConfigureAwait(false);
            progress?.Invoke();
        }
        token.ThrowIfCancellationRequested();
    }
}
