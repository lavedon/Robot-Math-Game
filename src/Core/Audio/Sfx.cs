namespace MathRobot.Core.Audio;

/// <summary>Every sound effect: a recipe plus its mix level.</summary>
public static class Sfx
{
    // Loudness of each effect: the RMS of its loudest 100 ms, in dBFS (see AudioLevel.Loudest). Menu sounds and the
    // number-line ticks (which fire many times a second) sit low; hops and landings in the middle; rewards and
    // jingles at the top.
    private static readonly (string Name, double Level, Func<float[]> Make)[] Table =
    {
        // Menus
        ("cursor", -24, Cursor),
        ("confirm", -18, Confirm),
        ("cancel", -20, Cancel),
        ("error", -20, Error),
        // The number line
        ("tick", -27, Tick),
        ("tens", -20, Tens),
        ("lock", -17, Lock),
        ("hop", -18, Hop),
        ("bighop", -15, BigHop),
        ("land", -19, Land),
        // Answers
        ("correct", -14, () => Correct().RenderOnce(tail: 0.6)),
        ("wrong", -19, Wrong),
        // Rewards and the shop
        ("coin", -22, Coin),
        ("buy", -14, Buy),
        ("equip", -14, Equip),
        ("star", -15, () => StarChime().RenderOnce(tail: 1)),
        ("levelup", -13, () => LevelUp().RenderOnce(tail: 1)),
        ("victory", -13, () => Victory().RenderOnce(tail: 1.2)),
        // Screens and the boss
        ("whoosh", -19, Whoosh),
        ("boss_hit", -14, BossHit),
        ("boss_down", -13, BossDown),
    };

    public static readonly IReadOnlyList<string> Names = Table.Select(e => e.Name).ToArray();

    /// <summary>Renders an effect, normalised to its level.</summary>
    public static float[] Render(string name)
    {
        foreach (var (n, level, make) in Table)
        {
            if (n != name) continue;
            // Short low notes hold a little DC (an unfinished last cycle). The blocker's slow undershoot needs some
            // silence after the sound to die away in before the trim.
            var raw = make();
            var b = new float[raw.Length + Synth.Samples(0.25)];
            raw.CopyTo(b, 0);
            Synth.RemoveDc(b);
            b = Synth.Trim(b);
            AudioLevel.Normalize(b, level, Loudness.Loudest);
            return b;
        }
        throw new ArgumentException($"Unknown sound effect '{name}'", nameof(name));
    }

    /// <summary>The jingles as songs, for checking their harmony.</summary>
    internal static IEnumerable<(string Name, Song Song)> Jingles()
    {
        yield return ("correct", Correct());
        yield return ("star", StarChime());
        yield return ("levelup", LevelUp());
        yield return ("victory", Victory());
    }

    private static float[] Buffer(double seconds) => new float[Synth.Samples(seconds)];
    private static int At(double seconds) => Synth.Samples(seconds);

    // ---- Menus (the Kegel Quest blips) ----

    // Cursor: a tiny two-step blip up a fourth (E6, A6), 40 ms in all.
    private static float[] Cursor()
    {
        var b = Buffer(0.1);
        Synth.Note(b, 0, Voices.Blip, 88, 0.016);
        Synth.Note(b, At(0.018), Voices.Blip, 93, 0.022);
        return Synth.Trim(b);
    }

    // Confirm: a bright hop up a fifth (C6, G6) with a short echo.
    private static float[] Confirm()
    {
        var b = Buffer(0.5);
        Synth.Note(b, 0, Voices.ThinBlip, 84, 0.045);
        Synth.Note(b, At(0.05), Voices.ThinBlip, 91, 0.11);
        Synth.Echo(b, new EchoSettings(0.075, 0.25, 0.25, 3500));
        return Synth.Trim(b);
    }

    // Cancel: the hop falling instead (G5, C5), rounder and softer.
    private static float[] Cancel()
    {
        var b = Buffer(0.3);
        var soft = Voices.Blip with { Cutoff = 2200 };
        Synth.Note(b, 0, soft, 79, 0.04);
        Synth.Note(b, At(0.045), soft, 72, 0.09);
        return Synth.Trim(b);
    }

    // Error: two soft, low, slightly sour buzzes ("uh-uh") on A2.
    private static float[] Error()
    {
        var b = Buffer(0.4);
        Synth.Note(b, 0, Voices.Buzz, 45, 0.075);
        Synth.Note(b, At(0.13), Voices.Buzz, 45, 0.11);
        return Synth.Trim(b);
    }

    // ---- The number line ----

    // Tick: one notch of the number line's ratchet, fired for every step a handle moves (up to 20 a second while
    // held). A tiny, dry "tk": two short modes of a small plastic tooth and a pinch of noise, gone within 20 ms so
    // fast runs read as a purr rather than a buzz.
    private static float[] Tick()
    {
        var b = Buffer(0.06);
        Synth.Partial(b, 0, 2200, 0.5, 0.006);
        Synth.Partial(b, 0, 3500, 0.25, 0.004);
        Synth.Noise(b, 0, 0.012, 0.25, 0.0005, 0.003, 5000, 5000, 0.8, NoiseBand.Mid, seed: 41);
        return Synth.Trim(b);
    }

    // Tens: a jump of ten, so it must sound bigger than ten ticks: a ratchet "ka" (a noisy click) then a solid
    // "chunk" 45 ms later, a low knock with the ring of a steel plate, like a stapler or a gear snapping a notch.
    private static float[] Tens()
    {
        var b = Buffer(0.4);
        Synth.Noise(b, 0, 0.025, 0.5, 0.0005, 0.008, 3000, 2000, 1, NoiseBand.Mid, seed: 42);
        Synth.Partial(b, 0, 1500, 0.3, 0.01);
        int chunk = At(0.045);
        Synth.Note(b, chunk, Voices.Thunk, 43, 0.06);
        Synth.Partial(b, chunk, 310, 0.5, 0.05);
        Synth.Partial(b, chunk, 770, 0.35, 0.035);
        Synth.Partial(b, chunk, 1290, 0.2, 0.025);
        Synth.Noise(b, chunk, 0.04, 0.35, 0.0005, 0.012, 1400, 800, 0.9, NoiseBand.Low, seed: 43);
        return Synth.Trim(b);
    }

    // Lock: a handle snapping into place. A crisp latch click with a little knock under it, then a short, firm
    // two-note chime (G5 and D6 together, a bare fifth) that says "set".
    private static float[] Lock()
    {
        var b = Buffer(0.6);
        Synth.Noise(b, 0, 0.015, 0.6, 0.0003, 0.004, 6000, 4000, 0.9, NoiseBand.High, seed: 44);
        Synth.Partial(b, 0, 2600, 0.35, 0.012);
        Synth.Note(b, 0, Voices.Thunk, 52, 0.03);
        int chime = At(0.035);
        Synth.Note(b, chime, Voices.ThinBlip, 86, 0.06, 0.8);
        Synth.Note(b, chime, Voices.Blip, 79, 0.06, 0.7);
        Synth.Echo(b, new EchoSettings(0.06, 0.2, 0.18, 4000));
        return Synth.Trim(b);
    }

    // Hop: the robot springing back one step. A cute "boing" on D5: the pitch scoops up from well below, then
    // wobbles round the note like a spring settling. The game raises its pitch for each hop in a run, so it is kept
    // short and in the middle of the range.
    private static float[] Hop()
    {
        var b = Buffer(0.35);
        Synth.Noise(b, 0, 0.012, 0.2, 0.0005, 0.003, 3000, 3000, 0.8, NoiseBand.Mid, seed: 45); // the push-off
        Boing(b, 0, midi: 74, seconds: 0.22, scoop: 10, wobbleSemis: 1.0, wobbleHz: 18, amp: 1);
        return Synth.Trim(b);
    }

    // Big hop: a jump of ten. A deeper, longer, wider boing a fourth lower (A4) with a fainter one an octave above
    // for the spring's shimmer, over a whoosh of air sweeping up.
    private static float[] BigHop()
    {
        var b = Buffer(0.7);
        Synth.Noise(b, 0, 0.02, 0.3, 0.0005, 0.005, 2000, 2000, 0.8, NoiseBand.Mid, seed: 46);
        Boing(b, 0, midi: 69, seconds: 0.42, scoop: 14, wobbleSemis: 1.8, wobbleHz: 13, amp: 1);
        Boing(b, At(0.01), midi: 81, seconds: 0.3, scoop: 14, wobbleSemis: 1.2, wobbleHz: 13, amp: 0.25);
        Synth.Noise(b, 0, 0.32, 0.3, 0.08, 0.1, 600, 3200, 1.0, NoiseBand.Mid, seed: 47);
        return Synth.Trim(b);
    }

    // Land: two little metal feet touching down. A soft low "thup" with a puff of air, and a faint clink.
    private static float[] Land()
    {
        var b = Buffer(0.3);
        Synth.Note(b, 0, Voices.Thunk with { Decay = 0.05 }, 48, 0.05);
        Synth.Noise(b, 0, 0.08, 0.35, 0.001, 0.02, 900, 300, 0.8, NoiseBand.Low, seed: 48);
        Synth.Partial(b, At(0.004), 1800, 0.12, 0.03);
        Synth.Partial(b, At(0.004), 3150, 0.08, 0.02);
        return Synth.Trim(b);
    }

    /// <summary>
    /// A spring "boing": a sine (with a touch of its octave) whose pitch scoops up from <paramref name="scoop"/>
    /// semitones below, then wobbles round the note with a vibrato that dies away, under a quick decay.
    /// </summary>
    private static void Boing(float[] b, int start, double midi, double seconds, double scoop, double wobbleSemis, double wobbleHz, double amp)
    {
        int n = Math.Min(Synth.Samples(seconds), b.Length - start);
        int attack = Synth.Samples(0.002);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Synth.Rate;
            double semis = -scoop * Math.Exp(-t / 0.025)
                + wobbleSemis * Math.Exp(-t / (seconds * 0.35)) * Math.Sin(2 * Math.PI * wobbleHz * t);
            phase += Synth.Hz(midi + semis) / Synth.Rate;
            phase -= Math.Floor(phase);
            double env = i < attack ? (double)i / attack : Math.Exp(-t / (seconds * 0.3));
            env *= Math.Min(1, 8.0 * (n - i) / n); // the last eighth fades to exactly zero
            double s = (Synth.Sin(phase) + 0.3 * Synth.Sin(2 * phase)) / 1.3;
            b[start + i] += (float)(s * env * amp);
        }
    }

    // ---- Answers ----

    // Correct (about 0.8 s): "ta-da!" A quick run up a C major chord to a held G6 over a third below, a little bass
    // and a glockenspiel sparkle as it lands.
    private static Song Correct() =>
        new Song(bpm: 240, bars: 1) { Echo = new(Delay: 0.09, Feedback: 0.25, Wet: 0.2, Damping: 4500) }
            .Chords("C")
            .Line(Voices.Fanfare, "G5:s C6:s E6:s G6:s+q r:h |")
            .Line(Voices.FanfareLow, "E5:s G5:s C6:s E6:s+q r:h |", gain: 0.45)
            .Line(Voices.Glock, "r:e. C7:s E7:s G7:s r:h+e |", gain: 0.28)
            .Line(Voices.Bass, "C3:e G3:e C4:q r:h |", gain: 0.6);

    // Wrong: a gentle "uh-oh", never a buzzer. A soft, rounded falling minor third (C5 to A4), the second note
    // drooping in from a little sharp, like a puzzled shrug.
    private static float[] Wrong()
    {
        var b = Buffer(0.9);
        var soft = new Instrument
        {
            Name = "uh-oh", Wave = Waveform.Sine, Bright = 0.35, Attack = 0.012, Decay = 0.2, Sustain = 0.6, Release = 0.08,
            VibratoCents = 18, VibratoHz = 6, VibratoDelay = 0.08, Legato = 1,
        };
        Synth.Note(b, 0, soft with { Glide = -1, GlideTime = 0.02 }, 72, 0.12);
        Synth.Note(b, At(0.17), soft with { Glide = 1.5, GlideTime = 0.09 }, 69, 0.26);
        Synth.Echo(b, new EchoSettings(0.08, 0.2, 0.15, 3000));
        return Synth.Trim(b);
    }

    // ---- Rewards and the shop ----

    // Coin: one "tink" for count-up tallies, which may fire 15 times a second: a soft high sine (B6) with a fainter
    // octave, mostly gone within 60 ms so rapid ticks never pile up into a buzz.
    private static float[] Coin()
    {
        var b = Buffer(0.25);
        Synth.Partial(b, 0, Synth.Hz(95), 1, 0.028);
        Synth.Partial(b, 0, Synth.Hz(107), 0.25, 0.014);
        return Synth.Trim(b);
    }

    // Buy: "cha-ching". A noisy "cha" with a low thunk (the till drawer), two bell strikes a fifth apart (A6, E7),
    // then a few coins settling, all in a little echo.
    private static float[] Buy()
    {
        var b = Buffer(2.8);
        Synth.Noise(b, 0, 0.06, 0.6, 0.001, 0.018, 3800, 2200, 1.2, NoiseBand.Mid, seed: 11);
        Synth.Note(b, 0, Voices.Thunk, 50, 0.04);
        Bell(b, At(0.075), Synth.Hz(93), 0.9, 0.28);
        Bell(b, At(0.14), Synth.Hz(100), 0.7, 0.34);
        (double At, int Midi)[] coins = { (0.26, 100), (0.31, 97), (0.355, 98), (0.395, 95) };
        for (int i = 0; i < coins.Length; i++) Synth.Partial(b, At(coins[i].At), Synth.Hz(coins[i].Midi), 0.22 - 0.04 * i, 0.03);
        Synth.Echo(b, new EchoSettings(0.09, 0.3, 0.25, 4000));
        return Synth.Trim(b);
    }

    // Equip: a robot part fitted. A latch "click", a servo whirring up an octave (a buzzy square with a fast motor
    // vibrato), then a bright "ding" (A6 over E6).
    private static float[] Equip()
    {
        var b = Buffer(1.8);
        Synth.Noise(b, 0, 0.02, 0.5, 0.0005, 0.005, 5000, 3500, 0.9, NoiseBand.Mid, seed: 51);
        Synth.Partial(b, 0, 2400, 0.3, 0.012);
        var servo = new Instrument
        {
            Name = "servo", Duty = 0.3, Attack = 0.01, Decay = 0.3, Sustain = 0.8, Release = 0.04, Cutoff = 1400,
            Glide = -12, GlideTime = 0.07, VibratoCents = 30, VibratoHz = 30, VibratoDelay = 0, Legato = 1,
        };
        Synth.Note(b, At(0.03), servo, 62, 0.2, 0.5);
        Bell(b, At(0.25), Synth.Hz(93), 0.8, 0.3);
        Bell(b, At(0.25), Synth.Hz(88), 0.4, 0.25);
        Synth.Echo(b, new EchoSettings(0.08, 0.25, 0.2, 5000));
        return Synth.Trim(b);
    }

    /// <summary>A struck bell: partials at the ratios of a vibrating bar, the higher ones dying away faster.</summary>
    private static void Bell(float[] b, int at, double hz, double amp, double decay)
    {
        Synth.Partial(b, at, hz, amp, decay);
        Synth.Partial(b, at, hz * 2.76, amp * 0.45, decay * 0.45);
        Synth.Partial(b, at, hz * 5.40, amp * 0.2, decay * 0.25);
        Synth.Partial(b, at, hz * 8.93, amp * 0.08, decay * 0.15);
    }

    // Star: a star lighting up. A glockenspiel twinkle up an E major chord to E7 over a soft chime chord.
    private static Song StarChime() =>
        new Song(bpm: 280, bars: 1) { Echo = new(Delay: 0.1, Feedback: 0.35, Wet: 0.3, Damping: 5000) }
            .Chords("E")
            .Line(Voices.Glock, "B5:s E6:s G#6:s B6:s E7:q r:h |")
            .Line(Voices.Chime, "E5+G#5+B5:h r:h |", gain: 0.3);

    // Level up (about 1.2 s, from Kegel Quest): a bright pentatonic run up two octaves (C5 to E6), doubled a third
    // below, landing on a held C major chord over a rising bass, with a glockenspiel sparkle on top.
    private static Song LevelUp() =>
        new Song(bpm: 200, bars: 1) { Echo = new(Delay: 0.09, Feedback: 0.25, Wet: 0.2) }
            .Chords("C")
            .Line(Voices.Fanfare, "C5:s D5:s E5:s G5:s A5:s C6:s D6:s E6:s+h |")
            .Line(Voices.FanfareLow, "A4:s B4:s C5:s E5:s F5:s A5:s B5:s C6:s+h |", gain: 0.5)
            .Line(Voices.Bass, "C3:q G3:q C4:h |", gain: 0.8)
            .Line(Voices.Glock, "r:h G6:s C7:s E7:s G7:s r:q |", gain: 0.3);

    // Victory (about 2.5 s): an original fanfare in C major at 192 BPM. A sixteenth-note rocket up the C chord to C6,
    // a dotted step through F (A-G-A-B, the B leaning up), then "da-da-da-dum DUM": D-B-G-D over G landing on a long
    // C6 (V-I), harmony a third or sixth below, timpani and bass under it, a snare pickup into bar 2 and a cymbal and
    // glockenspiel sparkle on the final chord.
    private static Song Victory() =>
        new Song(bpm: 192, bars: 2) { Echo = new(Delay: 0.12, Feedback: 0.3, Wet: 0.22) }
            .Chords("C:h F:h | G:h C:h")
            .Line(Voices.Fanfare, "G4:s C5:s E5:s G5:s C6:q A5:e. G5:s A5:e B5:e | D6:e. B5:s G5:e D6:e C6:h |")
            .Line(Voices.FanfareLow, "E4:s G4:s C5:s E5:s G5:q F5:e. E5:s F5:e G5:e | B5:e. G5:s D5:e B5:e E5:h |", gain: 0.55)
            .Line(Voices.Bass, "C3:q E3:q F2:q A2:q | G2:q B2:q C3:h |", gain: 0.9)
            .Line(Voices.Timpani, "C2:q r:q F2:q r:q | G1:q r:q C2:h |", gain: 0.6)
            .Line(Voices.Glock, "r:w | r:h G6:s C7:s E7:s G7:s r:q |", gain: 0.3)
            .Drums("k . . . . . . . k . . . s . s s", 1, 1, 0.55)
            .Drums("k . . . s . . . C . . . . . . .", 2, 2, 0.6);

    // ---- Screens and the boss ----

    // Whoosh: air rushing past as the screen breaks up or the map camera swoops. A band of noise sweeping up, and a
    // second sweeping down as the first fades: a doppler "fwoo-osh".
    private static float[] Whoosh()
    {
        var b = Buffer(0.8);
        Synth.Noise(b, 0, 0.45, 0.7, 0.18, 0.12, 350, 3200, 1.3, NoiseBand.Mid, seed: 61);
        Synth.Noise(b, At(0.2), 0.45, 0.45, 0.05, 0.14, 2800, 500, 1.1, NoiseBand.Mid, seed: 62);
        return Synth.Trim(b);
    }

    // Boss hit: a zap and a crunch. Two pulse lasers diving two octaves in a blink, then a burst of crackling noise
    // and a low knock as it lands.
    private static float[] BossHit()
    {
        var b = Buffer(0.8);
        var zap = new Instrument
        {
            Name = "zap", Duty = 0.25, Attack = 0.001, Decay = 0.08, Sustain = 0.2, Release = 0.03, Cutoff = 6000,
            Glide = 26, GlideTime = 0.035, Legato = 1,
        };
        Synth.Note(b, 0, zap, 62, 0.14);
        Synth.Note(b, 0, zap with { Duty = 0.5, Glide = 20 }, 50, 0.16, 0.6);
        Synth.Noise(b, At(0.03), 0.18, 0.8, 0.001, 0.05, 2200, 500, 0.8, NoiseBand.Mid, seed: 71);
        Synth.Note(b, At(0.03), Voices.Thunk, 38, 0.07);
        Synth.Echo(b, new EchoSettings(0.07, 0.2, 0.18, 4000));
        return Synth.Trim(b);
    }

    // Boss down (about 3 s): the boss powers down and blows up. A whining tone slides down over two octaves while a
    // chain of four pops, each bigger and lower, goes off; then the big one, a long rumble with a crackle on top.
    private static float[] BossDown()
    {
        var b = Buffer(3.4);
        var whine = new Instrument
        {
            Name = "power down", Duty = 0.25, Attack = 0.01, Decay = 1.5, Sustain = 0.5, Release = 0.3, Cutoff = 2500,
            Glide = 30, GlideTime = 0.45, VibratoCents = 40, VibratoHz = 9, VibratoDelay = 0.1, Legato = 1,
        };
        Synth.Note(b, 0, whine, 40, 1.4, 0.5);
        double[] pops = { 0, 0.22, 0.45, 0.75 };
        for (int i = 0; i < pops.Length; i++)
        {
            Synth.Noise(b, At(pops[i]), 0.35 + 0.25 * i, 0.6 + 0.15 * i, 0.002, 0.08 + 0.06 * i, 2600 - 450 * i, 250, 0.7,
                NoiseBand.Low, seed: (uint)(81 + i));
            Synth.Note(b, At(pops[i]), Voices.Thunk with { Decay = 0.08 }, 36 - 2 * i, 0.1);
        }
        Synth.Noise(b, At(1.0), 1.6, 1.0, 0.004, 0.45, 1800, 120, 0.6, NoiseBand.Low, seed: 90);
        Synth.Noise(b, At(1.0), 0.8, 0.4, 0.002, 0.2, 5000, 1500, 0.7, NoiseBand.Mid, seed: 91);
        Synth.Echo(b, new EchoSettings(0.11, 0.3, 0.25, 2500));
        return Synth.Trim(b);
    }
}
