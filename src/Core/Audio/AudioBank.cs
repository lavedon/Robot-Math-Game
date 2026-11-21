using System.Diagnostics;
using System.Globalization;

namespace MathRobot.Core.Audio;

/// <summary>
/// The catalogue of game sounds, rendered to 16-bit mono at <see cref="Synth.Rate"/> and mixed to fixed levels,
/// ready for an engine to play or to export for auditioning. Ported from Kegel Quest.
/// </summary>
public static class AudioBank
{
    /// <summary>
    /// RMS level of every music loop in dBFS: about 14 dB under the main effects (whose loudest 100 ms sit near
    /// -14 dBFS), so music stays in the background.
    /// </summary>
    public const double MusicLevelDb = -28;

    public static IReadOnlyList<string> Effects => Sfx.Names;
    public static IReadOnlyList<string> Tracks => Music.Names;

    /// <summary>An effect at its mix level, as floats (-1..1).</summary>
    public static float[] EffectSamples(string name) => Sfx.Render(name);

    /// <summary>Renders an effect at its mix level.</summary>
    public static short[] Effect(string name) => Pcm.From(EffectSamples(name));

    /// <summary>A music track at the music level, as floats: a clean first pass, then one loop to repeat.</summary>
    public static LoopedSound TrackSamples(string name)
    {
        var track = Music.Get(name).RenderLoop();
        // Levelled on the loop, which is what plays nearly all the time.
        AudioLevel.Normalize(track.Samples, MusicLevelDb, Loudness.Rms, measureFrom: track.LoopStart);
        return track;
    }

    /// <summary>Renders a music track at the music level: a clean first pass, then one loop to repeat (see <see cref="Song.RenderLoop()"/>).</summary>
    public static PcmLoop Track(string name)
    {
        var track = TrackSamples(name);
        return new PcmLoop(Pcm.From(track.Samples), track.LoopStart);
    }

    /// <summary>
    /// Writes sfx_NAME.wav for every effect and music_NAME.wav for every track: the track as the game plays it, a clean
    /// first pass with 'smpl' loop points around one loop. For auditioning in any audio player.
    /// </summary>
    public static void ExportAll(string directory, Action<string>? log = null)
    {
        Directory.CreateDirectory(directory);
        log?.Invoke($"{"file",-22} {"seconds",8} {"loop at",8} {"peak dB",8} {"rms dB",8} {"loud dB",8} {"ms",6}");
        foreach (var name in Effects) Write($"sfx_{name}.wav", () => (Effect(name), -1));
        foreach (var name in Tracks)
            Write($"music_{name}.wav", () =>
            {
                var track = Track(name);
                return (track.Samples, track.LoopStart);
            });

        void Write(string file, Func<(short[] Pcm, int LoopStart)> render)
        {
            var clock = Stopwatch.StartNew();
            var (pcm, loopStart) = render();
            double ms = clock.Elapsed.TotalMilliseconds;
            File.WriteAllBytes(Path.Combine(directory, file), Pcm.Wav(pcm, loopStart));
            var f = pcm.Select(s => s / 32768f).ToArray();
            double rms = AudioLevel.Rms(f.AsSpan(Math.Max(loopStart, 0)));
            string loopAt = loopStart >= 0 ? string.Create(CultureInfo.InvariantCulture, $"{loopStart / (double)Synth.Rate:0.000}") : "-";
            log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                $"{file,-22} {pcm.Length / (double)Synth.Rate,8:0.000} {loopAt,8} {AudioLevel.Db(AudioLevel.Peak(f)),8:0.0} {AudioLevel.Db(rms),8:0.0} {AudioLevel.Db(AudioLevel.Loudest(f)),8:0.0} {ms,6:0}"));
        }
    }
}

/// <summary>A music track as 16-bit samples: play from the start, then repeat [<see cref="LoopStart"/>, end) forever.</summary>
public sealed record PcmLoop(short[] Samples, int LoopStart)
{
    public int LoopLength => Samples.Length - LoopStart;
}

/// <summary>How <see cref="AudioLevel.Normalize"/> measures loudness.</summary>
public enum Loudness
{
    /// <summary>RMS of the whole buffer: right for music, which plays continuously.</summary>
    Rms,
    /// <summary>RMS of the loudest 100 ms: right for short effects (a blip shorter than that counts as padded with silence, as the ear hears it).</summary>
    Loudest,
}

/// <summary>Level measurement and normalising for float sample buffers.</summary>
public static class AudioLevel
{
    /// <summary>No sample may exceed this after normalising (leaves headroom for mixing several sounds).</summary>
    public const double PeakCeiling = 0.89;

    public static double Peak(ReadOnlySpan<float> b)
    {
        double p = 0;
        foreach (var x in b) p = Math.Max(p, Math.Abs(x));
        return p;
    }

    public static double Rms(ReadOnlySpan<float> b)
    {
        double sum = 0;
        foreach (var x in b) sum += (double)x * x;
        return Math.Sqrt(sum / Math.Max(1, b.Length));
    }

    /// <summary>RMS of the loudest 100 ms window.</summary>
    public static double Loudest(ReadOnlySpan<float> b)
    {
        int w = Synth.Samples(0.1);
        double sum = 0, best = 0;
        for (int i = 0; i < b.Length; i++)
        {
            sum += (double)b[i] * b[i];
            if (i >= w) sum -= (double)b[i - w] * b[i - w];
            best = Math.Max(best, sum);
        }
        return Math.Sqrt(Math.Max(best, 0) / w);
    }

    public static double Db(double x) => 20 * Math.Log10(Math.Max(x, 1e-9));

    /// <summary>
    /// Scales a sound to a loudness in dBFS, measured from sample <paramref name="measureFrom"/> on, but never lets any
    /// sample pass <see cref="PeakCeiling"/>. Returns true if the ceiling held it back.
    /// </summary>
    public static bool Normalize(float[] b, double targetDb, Loudness mode, int measureFrom = 0)
    {
        var measure = b.AsSpan(measureFrom);
        double measured = mode == Loudness.Rms ? Rms(measure) : Loudest(measure);
        if (measured <= 0) return false;
        double gain = Math.Pow(10, targetDb / 20) / measured;
        double peak = Peak(b) * gain;
        bool limited = peak > PeakCeiling;
        if (limited) gain *= PeakCeiling / peak;
        for (int i = 0; i < b.Length; i++) b[i] = (float)(b[i] * gain);
        return limited;
    }
}

/// <summary>16-bit PCM and WAV files.</summary>
public static class Pcm
{
    public static short[] From(float[] b)
    {
        var pcm = new short[b.Length];
        for (int i = 0; i < b.Length; i++) pcm[i] = (short)Math.Clamp(Math.Round(b[i] * 32767.0), -32768, 32767);
        return pcm;
    }

    /// <summary>
    /// The samples as little-endian bytes, the layout of both WAV files and Godot's AudioStreamWav.
    /// (Buffer.BlockCopy keeps the machine's byte order, and every platform Godot ships on is little-endian.)
    /// </summary>
    public static byte[] Bytes(short[] pcm)
    {
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>A mono 16-bit WAV file. With <paramref name="loopStart"/> >= 0 it carries a 'smpl' chunk that loops samples [loopStart, end).</summary>
    public static byte[] Wav(short[] pcm, int loopStart = -1)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms); // BinaryWriter is always little-endian, as RIFF requires
        int data = pcm.Length * 2, smpl = loopStart >= 0 ? 60 : 0;
        w.Write("RIFF"u8);
        w.Write(4 + (8 + 16) + (smpl > 0 ? 8 + smpl : 0) + (8 + data));
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write((short)1); // mono
        w.Write(Synth.Rate);
        w.Write(Synth.Rate * 2); // bytes per second
        w.Write((short)2); // bytes per frame
        w.Write((short)16);
        if (smpl > 0)
        {
            w.Write("smpl"u8);
            w.Write(smpl);
            w.Write(0); // manufacturer
            w.Write(0); // product
            w.Write(1_000_000_000 / Synth.Rate); // sample period in ns
            w.Write(60); // unity note
            w.Write(0); // pitch fraction
            w.Write(0); // SMPTE format
            w.Write(0); // SMPTE offset
            w.Write(1); // one loop
            w.Write(0); // sampler data
            w.Write(0); // cue point id
            w.Write(0); // forward loop
            w.Write(loopStart); // start
            w.Write(pcm.Length - 1); // end (inclusive)
            w.Write(0); // fraction
            w.Write(0); // play forever
        }
        w.Write("data"u8);
        w.Write(data);
        w.Write(Bytes(pcm));
        w.Flush();
        return ms.ToArray();
    }
}
