namespace MathRobot.Core.Audio;

// The synthesiser behind every game sound: oscillators, envelopes, filters and an echo, all plain C# (no engine
// types), so sounds can be rendered on any thread and inspected outside Godot. Ported from Kegel Quest.

/// <summary>Oscillator shapes, after the voices of 16-bit console sound chips.</summary>
public enum Waveform
{
    /// <summary>Band-limited pulse. <see cref="Instrument.Duty"/> 0.5 is a square; 0.25 and 0.125 sound thinner and brighter.</summary>
    Pulse,
    /// <summary>Soft and hollow: basses and flutes.</summary>
    Triangle,
    /// <summary>A pure tone; <see cref="Instrument.Bright"/> mixes in the octave above.</summary>
    Sine,
    /// <summary>Two-operator FM (one sine bending the phase of another): bells, glockenspiel, electric piano.</summary>
    Fm,
}

/// <summary>Which part of the spectrum <see cref="Synth.Noise"/> keeps.</summary>
public enum NoiseBand { Low, Mid, High }

/// <summary>A synth voice: waveform, envelope and expression. Times are in seconds.</summary>
public sealed record Instrument
{
    public string Name { get; init; } = "";
    public Waveform Wave { get; init; } = Waveform.Pulse;
    /// <summary>Pulse width, 0..1.</summary>
    public double Duty { get; init; } = 0.5;
    /// <summary>Pulse-width modulation depth and rate: the slow shimmer of 16-bit string pads.</summary>
    public double Pwm { get; init; }
    public double PwmHz { get; init; } = 0.4;
    /// <summary>Sine only: level of the octave partial.</summary>
    public double Bright { get; init; }
    /// <summary>FM only: modulator/carrier frequency ratio.</summary>
    public double FmRatio { get; init; } = 1;
    /// <summary>FM only: modulation index (radians) at the attack; it falls with time constant <see cref="FmDecay"/>, so the tone mellows.</summary>
    public double FmIndex { get; init; }
    public double FmDecay { get; init; } = 0.2;
    public double Attack { get; init; } = 0.005;
    /// <summary>Time constant of the fall from the attack peak toward <see cref="Sustain"/>.</summary>
    public double Decay { get; init; } = 0.3;
    public double Sustain { get; init; } = 0.7;
    public double Release { get; init; } = 0.06;
    /// <summary>How much of the written length a note is held: under 1 detaches notes, over 1 lets plucked notes ring on.</summary>
    public double Legato { get; init; } = 0.92;
    public double VibratoCents { get; init; }
    public double VibratoHz { get; init; } = 5.5;
    /// <summary>The vibrato fades in after this long, as a player holding a note would do.</summary>
    public double VibratoDelay { get; init; } = 0.2;
    /// <summary>One-pole low-pass in Hz (0 = off): takes the fizz off pulse waves.</summary>
    public double Cutoff { get; init; }
    /// <summary>Cents of a second, detuned oscillator (0 = off): a chorus for pads. Not used by FM.</summary>
    public double Detune { get; init; }
    /// <summary>The note starts this many semitones off pitch and slides home with time constant <see cref="GlideTime"/>.</summary>
    public double Glide { get; init; }
    public double GlideTime { get; init; } = 0.03;
    public double Gain { get; init; } = 1;
}

/// <summary>Echo settings: SNES games ran most of their music through the sound chip's feedback echo.</summary>
public sealed record EchoSettings(double Delay, double Feedback, double Wet, double Damping = 2800);

/// <summary>Sound generators and processors working on mono float buffers at <see cref="Rate"/>.</summary>
public static class Synth
{
    /// <summary>Output sample rate: the SNES sound chip's own.</summary>
    public const int Rate = 32000;

    // Pitch, vibrato, glide and FM depth update once per block (1 ms), like a console sound driver's tick.
    private const int Block = 32;

    private const int SineSize = 4096;
    private static readonly float[] SineTable = BuildSineTable();

    public static int Samples(double seconds) => (int)Math.Round(seconds * Rate);

    /// <summary>Frequency of a MIDI note (69 = A4 = 440 Hz; 60 = middle C).</summary>
    public static double Hz(double midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>sin(2π·cycles) from a table: Math.Sin per sample would dominate the render time.</summary>
    public static double Sin(double cycles)
    {
        double x = (cycles - Math.Floor(cycles)) * SineSize;
        int i = (int)x;
        double f = x - i;
        return SineTable[i] + (SineTable[i + 1] - SineTable[i]) * f;
    }

    private static float[] BuildSineTable()
    {
        var t = new float[SineSize + 2]; // two guard entries: index SineSize can occur through rounding
        for (int i = 0; i < t.Length; i++) t[i] = (float)Math.Sin(2 * Math.PI * i / SineSize);
        return t;
    }

    /// <summary>Samples a note occupies: held for <paramref name="seconds"/>, then released.</summary>
    public static int NoteLength(Instrument ins, double seconds) => HoldSamples(seconds) + ReleaseSamples(ins);

    private static int HoldSamples(double seconds) => Math.Max(1, (int)(seconds * Rate));
    private static int ReleaseSamples(Instrument ins) => Math.Max(64, (int)(ins.Release * Rate));

    /// <summary>
    /// Adds one note to the buffer. The envelope rises from zero over the attack (at least 0.5 ms) and the release
    /// (at least 2 ms) curves back to exactly zero, so a note can never click.
    /// </summary>
    public static void Note(float[] buf, int start, Instrument ins, double midi, double seconds, double velocity = 1)
    {
        int hold = HoldSamples(seconds);
        int release = ReleaseSamples(ins);
        int attack = Math.Max(16, (int)(ins.Attack * Rate));
        int end = Math.Min(hold + release, buf.Length - start);
        double hz = Hz(midi), amp = ins.Gain * velocity;
        double decayStep = Math.Exp(-1 / (Math.Max(ins.Decay, 1e-3) * Rate));
        double lpCoef = ins.Cutoff > 0 ? 1 - Math.Exp(-2 * Math.PI * ins.Cutoff / Rate) : 0;
        double detune = ins.Detune > 0 && ins.Wave != Waveform.Fm ? Math.Pow(2, ins.Detune / 1200) : 0;

        double phase = 0, phase2 = 0, modPhase = 0, lp = 0;
        double dt = 0, duty = ins.Duty, index = 0;
        double excess = 1, level = 0, releaseFrom = 0;
        for (int i = 0; i < end; i++)
        {
            if ((i & (Block - 1)) == 0)
            {
                double t = (double)i / Rate, semis = 0;
                if (ins.Glide != 0) semis += ins.Glide * Math.Exp(-t / ins.GlideTime);
                if (ins.VibratoCents > 0 && t > ins.VibratoDelay)
                    semis += ins.VibratoCents / 100 * Math.Min(1, (t - ins.VibratoDelay) / 0.15) * Math.Sin(2 * Math.PI * ins.VibratoHz * t);
                dt = hz * Math.Pow(2, semis / 12) / Rate;
                if (ins.Pwm > 0) duty = Math.Clamp(ins.Duty + ins.Pwm * Math.Sin(2 * Math.PI * ins.PwmHz * t), 0.06, 0.94);
                if (ins.Wave == Waveform.Fm) index = ins.FmIndex * Math.Exp(-t / ins.FmDecay) / (2 * Math.PI);
            }

            if (i < hold)
            {
                if (i < attack) level = (double)i / attack;
                else
                {
                    excess *= decayStep;
                    level = ins.Sustain + (1 - ins.Sustain) * excess;
                }
                releaseFrom = level;
            }
            else
            {
                double r = 1 - (double)(i - hold) / release;
                level = releaseFrom * r * r;
            }

            phase += dt;
            if (phase >= 1) phase -= 1;
            double s;
            if (ins.Wave == Waveform.Fm)
            {
                modPhase += dt * ins.FmRatio;
                modPhase -= Math.Floor(modPhase);
                s = Sin(phase + index * Sin(modPhase));
            }
            else
            {
                s = Osc(ins, phase, dt, duty);
                if (detune > 0)
                {
                    phase2 += dt * detune;
                    if (phase2 >= 1) phase2 -= 1;
                    s = 0.5 * (s + Osc(ins, phase2, dt * detune, duty));
                }
            }
            if (lpCoef > 0)
            {
                lp += lpCoef * (s - lp);
                s = lp;
            }
            buf[start + i] += (float)(s * level * amp);
        }
    }

    private static double Osc(Instrument ins, double phase, double dt, double duty) => ins.Wave switch
    {
        Waveform.Pulse => Pulse(phase, dt, duty),
        Waveform.Triangle => Triangle(phase),
        _ => (Sin(phase) + ins.Bright * Sin(2 * phase)) / (1 + ins.Bright),
    };

    /// <summary>A pulse wave with PolyBLEP-smoothed edges (far less aliasing than a raw square) and its DC removed.</summary>
    private static double Pulse(double phase, double dt, double duty)
    {
        double v = phase < duty ? 1 : -1;
        v += PolyBlep(phase, dt);
        double fall = phase - duty;
        if (fall < 0) fall += 1;
        v -= PolyBlep(fall, dt);
        return v - (2 * duty - 1); // a narrow pulse spends most of its time low: re-centre it
    }

    private static double PolyBlep(double t, double dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1;
        }
        if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return t * t + t + t + 1;
        }
        return 0;
    }

    /// <summary>Triangle starting at zero and rising, so a note's first sample is silent.</summary>
    private static double Triangle(double phase)
    {
        double x = phase + 0.25;
        if (x >= 1) x -= 1;
        return 1 - 4 * Math.Abs(x - 0.5);
    }

    /// <summary>An exponentially decaying sine: one mode of a struck bar, bell or plate (modal synthesis).</summary>
    public static void Partial(float[] buf, int start, double hz, double amp, double decay)
    {
        if (hz >= Rate * 0.45) return; // above what this rate can carry: it would alias
        int n = Math.Min(Samples(decay * 7), buf.Length - start); // e^-7: about -60 dB
        int attack = Samples(0.0008), fade = Math.Min(n / 2, Samples(0.003));
        // A two-pole resonator rings as amp·r^i·sin(w·i): the decaying sine costs one multiply-add per sample.
        double w = 2 * Math.PI * hz / Rate, r = Math.Exp(-1 / (decay * Rate));
        double c1 = 2 * r * Math.Cos(w), c2 = -r * r;
        double prev = -amp * Math.Sin(w) / r, cur = 0; // y[-1] and y[0]
        for (int i = 0; i < n; i++)
        {
            double env = 1;
            if (i < attack) env = (double)i / attack;
            if (i >= n - fade) env *= (double)(n - i) / fade;
            buf[start + i] += (float)(cur * env);
            double next = c1 * cur + c2 * prev;
            prev = cur;
            cur = next;
        }
    }

    /// <summary>
    /// A burst of white noise through a filter whose frequency sweeps exponentially from <paramref name="fromHz"/>
    /// to <paramref name="toHz"/>: swishes, drum skins, the "ch" of a till. The envelope rises over
    /// <paramref name="attack"/>, decays with time constant <paramref name="decay"/>, and its last 15% fades to zero
    /// (so a burst cut short still dies away naturally).
    /// </summary>
    public static void Noise(float[] buf, int start, double seconds, double amp, double attack, double decay,
        double fromHz, double toHz, double q, NoiseBand band, uint seed)
    {
        int n = Math.Min(Samples(seconds), buf.Length - start);
        int a = Math.Max(8, Samples(attack)), fade = Math.Max(Math.Min(n / 4, Samples(0.004)), (int)(n * 0.15));
        double step = Math.Exp(-1 / (decay * Rate)), g = 1;
        uint rng = seed * 2654435761u | 1; // xorshift must not start at zero
        var filter = new Svf();
        for (int i = 0; i < n; i++)
        {
            if ((i & (Block - 1)) == 0) filter.Set(fromHz * Math.Pow(toHz / fromHz, (double)i / n), q);
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            var (low, mid, high) = filter.Process(rng / 2147483648.0 - 1);
            double s = band switch { NoiseBand.Low => low, NoiseBand.Mid => mid, _ => high };
            double env;
            if (i < a) env = (double)i / a;
            else env = g *= step;
            if (i >= n - fade)
            {
                double f = (double)(n - i) / fade;
                env *= f * f; // reaches zero with zero slope
            }
            buf[start + i] += (float)(s * env * amp);
        }
    }

    /// <summary>Mixes a prepared sound (a drum hit) into the buffer.</summary>
    public static void Add(float[] buf, float[] sound, int start, double gain)
    {
        int n = Math.Min(sound.Length, buf.Length - start);
        for (int i = 0; i < n; i++) buf[start + i] += (float)(sound[i] * gain);
    }

    /// <summary>
    /// SNES-style echo: a feedback delay whose repeats come back darker each time. It is linear, so a loop gets its
    /// echoes by echoing one pass with room to ring out, then folding (see <see cref="Song.RenderLoop()"/>).
    /// </summary>
    public static void Echo(float[] buf, EchoSettings e)
    {
        var line = new float[Math.Max(1, Samples(e.Delay))];
        double coef = 1 - Math.Exp(-2 * Math.PI * e.Damping / Rate), lp = 0;
        int pos = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            double x = buf[i];
            lp += coef * (line[pos] - lp);
            line[pos] = (float)(x + lp * e.Feedback);
            if (++pos == line.Length) pos = 0;
            buf[i] = (float)(x + lp * e.Wet);
        }
    }

    /// <summary>A gentle DC-blocking high-pass (about 3 Hz).</summary>
    public static void RemoveDc(float[] buf)
    {
        const double R = 0.9994;
        double prev = 0, y = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            double x = buf[i];
            y = x - prev + R * y;
            prev = x;
            buf[i] = (float)y;
        }
    }

    /// <summary>Wraps everything past <paramref name="length"/> back onto the start: the last notes ring on into bar 1.</summary>
    public static float[] Fold(float[] buf, int length)
    {
        var loop = new float[length];
        for (int i = 0; i < buf.Length; i++) loop[i % length] += buf[i];
        return loop;
    }

    /// <summary>Cuts the near-silent end of a one-shot sound (60 dB under its peak) and curves the last 5 ms down to exactly zero.</summary>
    public static float[] Trim(float[] buf)
    {
        double floor = AudioLevel.Peak(buf) * 0.001;
        int end = buf.Length;
        while (end > 1 && Math.Abs(buf[end - 1]) <= floor) end--;
        var b = buf[..end];
        int fade = Math.Min(Samples(0.005), end);
        for (int i = 0; i < fade; i++)
        {
            double f = (double)i / fade;
            b[end - 1 - i] *= (float)(f * f);
        }
        return b;
    }
}

/// <summary>
/// Topology-preserving state-variable filter (after Zavalishin): stays stable while its frequency sweeps.
/// The band-pass output is scaled to unity gain at its centre.
/// </summary>
public struct Svf
{
    private double _ic1, _ic2, _k, _a1, _a2, _a3;

    public void Set(double hz, double q)
    {
        double g = Math.Tan(Math.PI * Math.Min(hz, Synth.Rate * 0.45) / Synth.Rate);
        _k = 1 / q;
        _a1 = 1 / (1 + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    public (double Low, double Mid, double High) Process(double x)
    {
        double v3 = x - _ic2;
        double v1 = _a1 * _ic1 + _a2 * v3;
        double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
        _ic1 = 2 * v1 - _ic1;
        _ic2 = 2 * v2 - _ic2;
        return (v2, _k * v1, x - _k * v1 - v2);
    }
}
