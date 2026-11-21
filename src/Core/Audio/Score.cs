using System.Globalization;

namespace MathRobot.Core.Audio;

// The sequencer that turns written music into sound. Ported from Kegel Quest; the drum kit gained a tom and a woodblock.

/// <summary>A note in a part, placed in beats (quarter notes) from the start of the song.</summary>
public readonly record struct NoteEvent(double Beat, double Beats, int Midi);

/// <summary>A chord over a span of beats: the pitch classes of its tones (root first) and of its bass note.</summary>
public sealed record Chord(double Beat, double Beats, int[] Tones, int Bass)
{
    public double End => Beat + Beats;
}

/// <summary>
/// The melody notation. Tokens are separated by spaces, and every bar line '|' is checked against the meter, so a
/// miscounted bar fails loudly instead of shifting the rest of the tune.
/// <code>
///   C5:q   F#4:e.   Bb3:h+e   r:q   C4+E4+G4:w
/// </code>
/// A pitch is a letter, an optional # or b, and an octave (C4 is middle C). Lengths: w h q e s (whole note to
/// sixteenth); a '3' makes a triplet (e3 is a third of a beat), '.' dots it, and '+' ties lengths together.
/// Pitches joined with '+' sound together. 'r' is a rest.
/// </summary>
public static class Notation
{
    public static List<NoteEvent> Parse(string text, int beatsPerBar, double startBeat = 0) => Parse(text, beatsPerBar, startBeat, out _);

    /// <summary>Parses a part and also reports the beat where it ends, trailing rests included.</summary>
    public static List<NoteEvent> Parse(string text, int beatsPerBar, double startBeat, out double endBeat)
    {
        var notes = new List<NoteEvent>();
        double beat = startBeat, barStart = startBeat;
        int bar = 1;
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "|")
            {
                CheckBar(bar++, beat - barStart, beatsPerBar);
                barStart = beat;
                continue;
            }
            int colon = token.IndexOf(':');
            if (colon <= 0) throw new FormatException($"Bar {bar}: '{token}' needs a length, as in C5:q");
            double beats = Duration(token[(colon + 1)..]);
            string pitches = token[..colon];
            if (pitches != "r")
                foreach (var p in pitches.Split('+')) notes.Add(new NoteEvent(beat, beats, Pitch(p)));
            beat += beats;
        }
        if (beat - barStart > 1e-9) CheckBar(bar, beat - barStart, beatsPerBar);
        endBeat = beat;
        return notes;
    }

    private static void CheckBar(int bar, double beats, int beatsPerBar)
    {
        if (Math.Abs(beats - beatsPerBar) > 1e-6)
            throw new FormatException($"Bar {bar} lasts {beats:0.###} beats instead of {beatsPerBar}");
    }

    /// <summary>MIDI number of a pitch name: "C4" is 60, "F#4" 66, "Bb3" 58.</summary>
    public static int Pitch(string name)
    {
        int i = 0;
        int pc = PitchClass(name, ref i);
        if (!int.TryParse(name.AsSpan(i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int octave))
            throw new FormatException($"'{name}' is not a pitch like C5 or F#4");
        return 12 * (octave + 1) + pc;
    }

    /// <summary>Reads a letter and its accidentals from <paramref name="i"/> on; returns semitones above C (may be -1 or 12 for Cb, B#).</summary>
    internal static int PitchClass(string text, ref int i)
    {
        int pc = i < text.Length ? "C D EF G A B".IndexOf(text[i]) : -1; // the index is the semitone above C
        if (pc < 0 || text[i] == ' ') throw new FormatException($"'{text}' does not start with a note letter A-G");
        for (i++; i < text.Length && text[i] is '#' or 'b'; i++) pc += text[i] == '#' ? 1 : -1;
        return pc;
    }

    /// <summary>Length in beats of a duration code such as "q", "e.", "e3" or "h+e".</summary>
    public static double Duration(string code)
    {
        double total = 0;
        foreach (var part in code.Split('+'))
        {
            if (part.Length == 0) throw new FormatException($"Empty length in '{code}'");
            double d = part[0] switch
            {
                'w' => 4,
                'h' => 2,
                'q' => 1,
                'e' => 0.5,
                's' => 0.25,
                _ => throw new FormatException($"Unknown length '{part}' (use w h q e s)"),
            };
            int i = 1;
            if (i < part.Length && part[i] == '3')
            {
                d *= 2.0 / 3;
                i++;
            }
            for (double dot = d / 2; i < part.Length && part[i] == '.'; i++, dot /= 2) d += dot;
            if (i != part.Length) throw new FormatException($"Unknown length '{part}'");
            total += d;
        }
        return total;
    }
}

/// <summary>
/// Chord charts: bars separated by '|', one or more chord symbols per bar ("G", "Em", "D7", "Bbmaj7", "F/A").
/// A symbol can carry a length ("Em:q."); symbols without one share what is left of the bar equally.
/// </summary>
public static class ChordChart
{
    // Intervals above the root for each chord quality suffix.
    private static readonly Dictionary<string, int[]> Qualities = new()
    {
        [""] = new[] { 0, 4, 7 },
        ["m"] = new[] { 0, 3, 7 },
        ["7"] = new[] { 0, 4, 7, 10 },
        ["maj7"] = new[] { 0, 4, 7, 11 },
        ["m7"] = new[] { 0, 3, 7, 10 },
        ["6"] = new[] { 0, 4, 7, 9 },
        ["add9"] = new[] { 0, 4, 7, 2 },
        ["sus2"] = new[] { 0, 2, 7 },
        ["sus4"] = new[] { 0, 5, 7 },
        ["dim"] = new[] { 0, 3, 6 },
    };

    public static List<Chord> Parse(string chart, int beatsPerBar)
    {
        var chords = new List<Chord>();
        double barStart = 0;
        foreach (var bar in chart.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokens = bar.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            double given = 0;
            int open = 0;
            foreach (var t in tokens)
            {
                int c = t.IndexOf(':');
                if (c > 0) given += Notation.Duration(t[(c + 1)..]);
                else open++;
            }
            double share = open > 0 ? (beatsPerBar - given) / open : 0;
            if (share < 0 || (open == 0 && Math.Abs(given - beatsPerBar) > 1e-6))
                throw new FormatException($"Chord bar '{bar}' does not fill {beatsPerBar} beats");
            double beat = barStart;
            foreach (var t in tokens)
            {
                int c = t.IndexOf(':');
                double len = c > 0 ? Notation.Duration(t[(c + 1)..]) : share;
                chords.Add(Symbol(c > 0 ? t[..c] : t, beat, len));
                beat += len;
            }
            barStart += beatsPerBar;
        }
        return chords;
    }

    public static Chord Symbol(string symbol, double beat, double beats)
    {
        int slash = symbol.IndexOf('/');
        string main = slash >= 0 ? symbol[..slash] : symbol;
        int i = 0;
        int root = Mod12(Notation.PitchClass(main, ref i));
        if (!Qualities.TryGetValue(main[i..], out var intervals)) throw new FormatException($"Unknown chord '{symbol}'");
        int bass = root;
        if (slash >= 0)
        {
            int j = 0;
            bass = Mod12(Notation.PitchClass(symbol[(slash + 1)..], ref j));
        }
        return new Chord(beat, beats, intervals.Select(x => Mod12(root + x)).ToArray(), bass);
    }

    internal static int Mod12(int x) => ((x % 12) + 12) % 12;
}

/// <summary>A rendered music loop: play <see cref="Samples"/> from the start, then repeat [<see cref="LoopStart"/>, end) forever.</summary>
public sealed record LoopedSound(float[] Samples, int LoopStart)
{
    public int LoopLength => Samples.Length - LoopStart;
}

/// <summary>
/// A tiny sequencer: parts written in <see cref="Notation"/>, accompaniment generated from a chord chart (bass
/// lines, arpeggios, pads) and drum patterns. A song renders once (a jingle) or as a seamless loop. Every part must
/// fit inside the song's bars: one that runs past the end is rejected rather than wrapped onto bar 1.
/// </summary>
public sealed class Song
{
    private readonly List<(Instrument Ins, NoteEvent Note, double Gain, int Part)> _notes = new();
    private readonly List<(float[] Sound, double Beat, double Gain, int Part)> _hits = new();
    private readonly List<string> _parts = new();
    private List<Chord> _chords = new();

    public Song(double bpm, int bars, int beatsPerBar = 4)
    {
        Bpm = bpm;
        Bars = bars;
        BeatsPerBar = beatsPerBar;
    }

    public double Bpm { get; }
    public int Bars { get; }
    public int BeatsPerBar { get; }
    public double SecondsPerBeat => 60 / Bpm;
    /// <summary>Length of the song (one loop) in samples.</summary>
    public int Length => Pos(Bars * BeatsPerBar);
    public EchoSettings? Echo { get; init; }

    internal IReadOnlyList<string> PartNames => _parts;
    internal IEnumerable<(Instrument Ins, NoteEvent Note, int Part)> AllNotes => _notes.Select(n => (n.Ins, n.Note, n.Part));
    internal IReadOnlyList<Chord> Harmony => _chords;

    private int Pos(double beat) => (int)Math.Round(beat * SecondsPerBeat * Synth.Rate);
    private double BarBeat(int bar) => (bar - 1) * BeatsPerBar;

    private int AddPart(string name)
    {
        _parts.Add(name);
        return _parts.Count - 1;
    }

    // Rendering a loop folds anything past the end onto bar 1, so a part with a bar too many would quietly play its
    // extra bar over the opening. Refuse it instead.
    private void CheckBars(string part, int fromBar, int toBar)
    {
        if (fromBar < 1 || toBar > Bars || fromBar > toBar)
            throw new FormatException($"The {part} part covers bars {fromBar}-{toBar}, but the song has bars 1-{Bars}");
    }

    /// <summary>The harmony: a <see cref="ChordChart"/> covering every bar of the song.</summary>
    public Song Chords(string chart)
    {
        _chords = ChordChart.Parse(chart, BeatsPerBar);
        double end = _chords.Count > 0 ? _chords[^1].End : 0;
        if (Math.Abs(end - Bars * BeatsPerBar) > 1e-6)
            throw new FormatException($"The chord chart covers {end / BeatsPerBar:0.##} bars but the song has {Bars}");
        return this;
    }

    /// <summary>A written part (melody, counter-melody, timpani...) starting at the given bar and ending by the last bar line.</summary>
    public Song Line(Instrument ins, string notation, int bar = 1, double gain = 1)
    {
        CheckBars(ins.Name, bar, bar);
        var notes = Notation.Parse(notation, BeatsPerBar, BarBeat(bar), out double end);
        double over = (end - Bars * BeatsPerBar) / BeatsPerBar;
        if (over > 1e-9) throw new FormatException($"The {ins.Name} part runs {over:0.##} bar(s) past the end of the {Bars}-bar song");
        int part = AddPart(ins.Name);
        foreach (var n in notes) _notes.Add((ins, n, gain, part));
        return this;
    }

    /// <summary>
    /// A bass line from the chord chart; the pattern ("1:q 5:q 8:q 5:q") restarts on every chord. Degrees: 1 is the
    /// bass note (the root, or the note after '/'), 3, 5 and 7 are chord tones above it, 8 its octave.
    /// The bass note is the lowest one at or above <paramref name="low"/>.
    /// </summary>
    public Song Bass(Instrument ins, string pattern, int fromBar, int toBar, int low = 38, double gain = 1)
    {
        CheckBars(ins.Name, fromBar, toBar);
        var steps = Steps(pattern);
        int part = AddPart(ins.Name);
        foreach (var chord in ChordsIn(fromBar, toBar))
        {
            int bass = Above(chord.Bass, low);
            double beat = chord.Beat;
            for (int k = 0; beat < chord.End - 1e-9; k = (k + 1) % steps.Length)
            {
                var (degree, beats) = steps[k];
                double len = Math.Min(beats, chord.End - beat);
                if (degree != "r") _notes.Add((ins, new NoteEvent(beat, len, BassNote(chord, bass, degree)), gain, part));
                beat += len;
            }
        }
        return this;
    }

    private static int BassNote(Chord chord, int bass, string degree) => degree switch
    {
        "1" => bass,
        "8" => bass + 12,
        "3" => Above(chord.Tones[1], bass + 1),
        "5" => Above(chord.Tones[2], bass + 1),
        "7" => Above(chord.Tones.Length > 3 ? chord.Tones[3] : chord.Tones[0] + 10, bass + 1),
        _ => throw new FormatException($"Unknown bass degree '{degree}' (use 1 3 5 7 8 or r)"),
    };

    /// <summary>
    /// An arpeggio from the chord chart, running continuously across the bars. Each step names a chord tone: 0 is
    /// the lowest tone at or above <paramref name="low"/>, and numbers past the chord's size climb an octave; '-' rests.
    /// </summary>
    public Song Arp(Instrument ins, string steps, string stepLength, int fromBar, int toBar, int low = 55, double gain = 1)
    {
        CheckBars(ins.Name, fromBar, toBar);
        var pattern = steps.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        double len = Notation.Duration(stepLength), beat = BarBeat(fromBar), end = BarBeat(toBar + 1);
        int part = AddPart(ins.Name);
        for (int k = 0; beat < end - 1e-9; k++, beat += len)
        {
            string step = pattern[k % pattern.Length];
            if (step == "-") continue;
            var voicing = Voicing(ChordAt(beat), low);
            int i = int.Parse(step, CultureInfo.InvariantCulture);
            _notes.Add((ins, new NoteEvent(beat, len, voicing[i % voicing.Length] + 12 * (i / voicing.Length)), gain, part));
        }
        return this;
    }

    /// <summary>Block chords from the chord chart in a rhythm such as "x:h x:q r:q" ('x' plays the chord), restarting on every chord.</summary>
    public Song Pad(Instrument ins, string rhythm, int fromBar, int toBar, int low = 55, double gain = 1)
    {
        CheckBars(ins.Name, fromBar, toBar);
        var steps = Steps(rhythm);
        int part = AddPart(ins.Name);
        foreach (var chord in ChordsIn(fromBar, toBar))
        {
            var voicing = Voicing(chord, low);
            double beat = chord.Beat;
            for (int k = 0; beat < chord.End - 1e-9; k = (k + 1) % steps.Length)
            {
                var (what, beats) = steps[k];
                double len = Math.Min(beats, chord.End - beat);
                if (what != "r")
                    foreach (int m in voicing) _notes.Add((ins, new NoteEvent(beat, len, m), gain, part));
                beat += len;
            }
        }
        return this;
    }

    /// <summary>
    /// One bar of drum steps repeated over the bars: "k . t . k k t ." splits the bar evenly between its steps.
    /// k kick, s snare, h hat, t tambourine, c cymbal, o low tom, w woodblock, '.' rest; upper case is accented.
    /// </summary>
    public Song Drums(string steps, int fromBar, int toBar, double gain = 1)
    {
        CheckBars("drums", fromBar, toBar);
        var chars = steps.Where(c => !char.IsWhiteSpace(c)).ToArray();
        double step = (double)BeatsPerBar / chars.Length;
        int part = AddPart("drums");
        for (int bar = fromBar; bar <= toBar; bar++)
            for (int k = 0; k < chars.Length; k++)
            {
                if (chars[k] == '.') continue;
                var sound = Kit.For(char.ToLowerInvariant(chars[k])) ?? throw new FormatException($"Unknown drum '{chars[k]}'");
                _hits.Add((sound, BarBeat(bar) + k * step, gain * (char.IsUpper(chars[k]) ? 1 : 0.7), part));
            }
        return this;
    }

    /// <summary>Renders the song once and lets the last notes and echoes ring out (jingles).</summary>
    public float[] RenderOnce(double tail = 1.5) => RenderOnce(tail, solo: null);

    /// <summary>
    /// Renders the song as a seamless loop. Once a loop is running, the tails and echoes of its last notes ring on
    /// into bar 1, but the very first pass starts from silence and must not carry them. So the result is the first
    /// pass up to the point where those tails have died away (<see cref="LoopedSound.LoopStart"/>), then exactly one
    /// period of the running loop, rotated to carry on from that point: its end flows straight back into LoopStart.
    /// </summary>
    public LoopedSound RenderLoop() => RenderLoop(solo: null);

    /// <summary><see cref="RenderOnce(double)"/>, or with <paramref name="solo"/> a single part (for balancing).</summary>
    internal float[] RenderOnce(double tail, int? solo) => Synth.Trim(Pass(solo, Synth.Samples(tail)));

    /// <summary><see cref="RenderLoop()"/>, or with <paramref name="solo"/> a single part (for balancing).</summary>
    internal LoopedSound RenderLoop(int? solo)
    {
        // Played over and over from silence, the song is copies of one pass laid every n samples. So render one pass
        // with all its ring-out (release tails, then echoes): up to n it is exactly the first pass, and folded onto n
        // samples it is the loop as it runs, each repeat carrying the ring-out of the one before.
        int n = Length;
        var pass = Pass(solo, RingOut());
        var loop = Synth.Fold(pass, n);
        // The two differ only by that ring-out. Once it is 80 dB under the loop's level (below what 16 bits can
        // carry), the first pass can hand over to the running loop without a seam.
        double tolerance = AudioLevel.Rms(loop) * 1e-4;
        int start = Math.Min(n, pass.Length - n);
        while (start > 0 && Math.Abs(loop[start - 1] - pass[start - 1]) <= tolerance) start--;
        var samples = new float[n + start];
        Array.Copy(pass, samples, start);
        Array.Copy(loop, start, samples, start, n - start);
        Array.Copy(loop, 0, samples, n, start);
        return new LoopedSound(samples, start);
    }

    // One pass of the song from silence, with `extra` samples after the last release for it to ring on into.
    private float[] Pass(int? solo, int extra)
    {
        var buf = Mix(solo, extra);
        Synth.RemoveDc(buf);
        if (Echo is { } e) Synth.Echo(buf, e);
        return buf;
    }

    // How long a pass keeps sounding after its last release, until 100 dB down: the DC blocker settles (its time
    // constant is about 50 ms) and the echo's repeats fall by the feedback each time.
    private int RingOut()
    {
        double seconds = 0.6;
        if (Echo is { } e)
            seconds += e.Delay * (1 + (e.Feedback > 0 ? Math.Ceiling(Math.Log(1e-5) / Math.Log(Math.Min(e.Feedback, 0.9))) : 0));
        return Synth.Samples(seconds);
    }

    // Every note and drum hit, dry, in one buffer long enough for the last release, plus `extra` samples.
    private float[] Mix(int? solo, int extra)
    {
        double spb = SecondsPerBeat;
        int needed = Length;
        foreach (var (ins, n, _, _) in _notes)
            needed = Math.Max(needed, Pos(n.Beat) + Synth.NoteLength(ins, n.Beats * spb * ins.Legato));
        foreach (var (sound, beat, _, _) in _hits) needed = Math.Max(needed, Pos(beat) + sound.Length);

        var buf = new float[needed + extra];
        // Like a console replaying samples from sound RAM, each distinct note (voice, pitch, length) is synthesised
        // once and mixed in as often as it is played: arpeggios, basses and pads repeat the same notes constantly.
        var rendered = new Dictionary<(Instrument, int, double), float[]>();
        foreach (var (ins, n, gain, part) in _notes)
        {
            if (solo is not null && solo != part) continue;
            double seconds = n.Beats * spb * ins.Legato;
            if (!rendered.TryGetValue((ins, n.Midi, n.Beats), out var sound))
            {
                sound = new float[Synth.NoteLength(ins, seconds)];
                Synth.Note(sound, 0, ins, n.Midi, seconds);
                rendered[(ins, n.Midi, n.Beats)] = sound;
            }
            Synth.Add(buf, sound, Pos(n.Beat), gain * Accent(n.Beat));
        }
        foreach (var (sound, beat, gain, part) in _hits)
            if (solo is null || solo == part)
                Synth.Add(buf, sound, Pos(beat), gain);
        return buf;
    }

    // Gentle metric accents (the downbeat strongest, off-beat notes softest) so sequenced parts don't sound flat.
    private double Accent(double beat)
    {
        double inBar = beat - BeatsPerBar * Math.Floor(beat / BeatsPerBar + 1e-9);
        if (inBar < 1e-6) return 1.0;
        if (Math.Abs(inBar - Math.Round(inBar)) > 1e-6) return 0.84;
        return Math.Abs(inBar - BeatsPerBar / 2.0) < 1e-6 ? 0.95 : 0.9;
    }

    private IEnumerable<Chord> ChordsIn(int fromBar, int toBar)
    {
        RequireChords();
        double from = BarBeat(fromBar), to = BarBeat(toBar + 1);
        return _chords.Where(c => c.Beat >= from - 1e-9 && c.Beat < to - 1e-9);
    }

    internal Chord ChordAt(double beat)
    {
        RequireChords();
        var found = _chords[0];
        foreach (var c in _chords)
            if (c.Beat <= beat + 1e-9) found = c;
        return found;
    }

    private void RequireChords()
    {
        if (_chords.Count == 0) throw new InvalidOperationException("Set the chord chart before generating parts from it");
    }

    /// <summary>The chord's tones, each placed at the lowest pitch at or above <paramref name="low"/>: smooth voice leading for free.</summary>
    private static int[] Voicing(Chord chord, int low) => chord.Tones.Select(pc => Above(pc, low)).Distinct().Order().ToArray();

    private static int Above(int pitchClass, int low) => low + ChordChart.Mod12(pitchClass - low);

    private static (string What, double Beats)[] Steps(string pattern) =>
        pattern.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(t =>
        {
            int c = t.IndexOf(':');
            if (c <= 0) throw new FormatException($"'{t}' needs a length, as in 1:q");
            return (t[..c], Notation.Duration(t[(c + 1)..]));
        }).ToArray();
}

/// <summary>Drum one-shots, synthesised once and reused, like the samples a 16-bit console kept in sound RAM.</summary>
internal static class Kit
{
    public static readonly float[] Kick = MakeKick();
    public static readonly float[] Snare = MakeSnare();
    public static readonly float[] Hat = MakeHat();
    public static readonly float[] Tambourine = MakeTambourine();
    public static readonly float[] Cymbal = MakeCymbal();
    public static readonly float[] Tom = MakeTom();
    public static readonly float[] Woodblock = MakeWoodblock();

    public static float[]? For(char c) => c switch
    {
        'k' => Kick,
        's' => Snare,
        'h' => Hat,
        't' => Tambourine,
        'c' => Cymbal,
        'o' => Tom,
        'w' => Woodblock,
        _ => null,
    };

    // A soft, round thump (more frame drum than rock kick): a sine diving from about 120 Hz to 55 Hz.
    private static float[] MakeKick()
    {
        var b = new float[Synth.Samples(0.3)];
        var thump = new Instrument { Wave = Waveform.Sine, Bright = 0.1, Attack = 0.002, Decay = 0.09, Sustain = 0, Release = 0.1, Glide = 14, GlideTime = 0.025 };
        Synth.Note(b, 0, thump, 33, 0.14);
        Synth.Noise(b, 0, 0.03, 0.12, 0.0005, 0.008, 1200, 600, 0.7, NoiseBand.Low, seed: 1);
        return b;
    }

    private static float[] MakeSnare()
    {
        var b = new float[Synth.Samples(0.25)];
        Synth.Noise(b, 0, 0.22, 0.55, 0.001, 0.055, 2600, 1700, 0.9, NoiseBand.Mid, seed: 2);
        Synth.Noise(b, 0, 0.2, 0.2, 0.001, 0.04, 6000, 5000, 0.7, NoiseBand.High, seed: 3);
        var body = new Instrument { Wave = Waveform.Triangle, Attack = 0.001, Decay = 0.04, Sustain = 0, Release = 0.03, Glide = 3, GlideTime = 0.02 };
        Synth.Note(b, 0, body, 55, 0.05, 0.6);
        return b;
    }

    private static float[] MakeHat()
    {
        var b = new float[Synth.Samples(0.08)];
        Synth.Noise(b, 0, 0.08, 0.4, 0.0005, 0.016, 8000, 8000, 0.7, NoiseBand.High, seed: 4);
        return b;
    }

    // Shaken jingles: a high band of noise plus the ring of a few metal discs.
    private static float[] MakeTambourine()
    {
        var b = new float[Synth.Samples(0.2)];
        Synth.Noise(b, 0, 0.18, 0.4, 0.003, 0.045, 7500, 6500, 1.4, NoiseBand.Mid, seed: 5);
        Synth.Partial(b, 0, 5274, 0.07, 0.05);
        Synth.Partial(b, Synth.Samples(0.004), 6645, 0.06, 0.04);
        Synth.Partial(b, Synth.Samples(0.009), 8372, 0.05, 0.035);
        return b;
    }

    // A soft crash: long enough (about 6 time constants) to die away rather than stop.
    private static float[] MakeCymbal()
    {
        var b = new float[Synth.Samples(2.4)];
        Synth.Noise(b, 0, 2.4, 0.35, 0.002, 0.4, 9000, 6000, 0.6, NoiseBand.High, seed: 6);
        Synth.Noise(b, 0, 1.6, 0.15, 0.002, 0.25, 4200, 3200, 1.0, NoiseBand.Mid, seed: 7);
        return b;
    }

    // A low tom for fills: a sine skin that sags a few semitones as it rings, with a stick click on top.
    private static float[] MakeTom()
    {
        var b = new float[Synth.Samples(0.4)];
        var skin = new Instrument { Wave = Waveform.Sine, Bright = 0.2, Attack = 0.002, Decay = 0.12, Sustain = 0, Release = 0.12, Glide = 5, GlideTime = 0.05 };
        Synth.Note(b, 0, skin, 45, 0.2);
        Synth.Noise(b, 0, 0.04, 0.15, 0.0005, 0.01, 1500, 900, 0.8, NoiseBand.Mid, seed: 8);
        return b;
    }

    // A woodblock "tock": the two strongest modes of a struck hardwood block and a click of the stick.
    private static float[] MakeWoodblock()
    {
        var b = new float[Synth.Samples(0.15)];
        Synth.Partial(b, 0, 1150, 0.5, 0.022);
        Synth.Partial(b, 0, 2720, 0.2, 0.012);
        Synth.Noise(b, 0, 0.012, 0.15, 0.0003, 0.003, 4000, 4000, 0.8, NoiseBand.Mid, seed: 9);
        return b;
    }
}
