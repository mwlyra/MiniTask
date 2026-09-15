using System.Media;
using System.Text;

namespace MiniTask.Desktop;

internal static class SoundCues
{
    private static readonly Lazy<SoundPlayer> Start = new(() => Create(660, 880));
    private static readonly Lazy<SoundPlayer> Stop = new(() => Create(660, 440));

    public static void Play(bool starting)
    {
        // Audio is optional feedback; an unavailable sound device must not interrupt a macro.
        try { (starting ? Start : Stop).Value.Play(); }
        catch { }
    }

    private static SoundPlayer Create(double first, double second)
    {
        var player = new SoundPlayer(new MemoryStream(Wave(first, second), writable: false));
        player.Load();
        return player;
    }

    private static byte[] Wave(double first, double second)
    {
        const int rate = 22050, toneSamples = 1764, gapSamples = 441;
        const int samples = toneSamples * 2 + gapSamples;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
        writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            bool last = i >= toneSamples + gapSamples;
            int local = last ? i - toneSamples - gapSamples : i;
            double sample = 0;
            if (local < toneSamples)
            {
                double envelope = Math.Pow(Math.Sin(Math.PI * local / (toneSamples - 1)), 2);
                sample = .14 * short.MaxValue * envelope * Math.Sin(2 * Math.PI * (last ? second : first) * local / rate);
            }
            writer.Write((short)sample);
        }
        writer.Flush(); return stream.ToArray();
    }
}
