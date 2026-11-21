namespace MathRobot.Core.Audio;

/// <summary>The instrument palette shared by the music, the jingles and the effects.</summary>
public static class Voices
{
    // Effects (from Kegel Quest)

    /// <summary>Menu blips: a square softened by a low-pass.</summary>
    public static readonly Instrument Blip = new() { Name = "blip", Attack = 0.001, Decay = 0.035, Sustain = 0.4, Release = 0.015, Cutoff = 3500, Legato = 1 };
    public static readonly Instrument ThinBlip = Blip with { Name = "thin blip", Duty = 0.25, Cutoff = 4500 };
    /// <summary>A low, slightly sour buzz (two detuned pulses beating) that droops in pitch.</summary>
    public static readonly Instrument Buzz = new() { Name = "buzz", Duty = 0.25, Attack = 0.004, Decay = 0.2, Sustain = 0.8, Release = 0.025, Cutoff = 900, Detune = 35, Glide = 0.7, GlideTime = 0.06, Legato = 1 };
    /// <summary>A short low knock that dives in pitch: a drawer, a boot, the body of a clank.</summary>
    public static readonly Instrument Thunk = new() { Name = "thunk", Wave = Waveform.Sine, Attack = 0.001, Decay = 0.04, Sustain = 0, Release = 0.02, Glide = 12, GlideTime = 0.015, Legato = 1 };

    // Melody voices (from Kegel Quest)

    public static readonly Instrument Recorder = new() { Name = "recorder", Attack = 0.012, Decay = 0.35, Sustain = 0.75, Release = 0.06, VibratoCents = 14, VibratoHz = 5.2, VibratoDelay = 0.18, Cutoff = 2400, Legato = 0.9 };
    public static readonly Instrument Reed = new() { Name = "reed", Duty = 0.25, Attack = 0.03, Decay = 0.5, Sustain = 0.8, Release = 0.1, VibratoCents = 10, VibratoHz = 5, VibratoDelay = 0.25, Cutoff = 1800, Legato = 0.95 };
    public static readonly Instrument Horn = new() { Name = "horn", Attack = 0.05, Decay = 0.8, Sustain = 0.85, Release = 0.14, VibratoCents = 16, VibratoHz = 4.8, VibratoDelay = 0.3, Cutoff = 1500, Legato = 0.96 };
    public static readonly Instrument Trumpet = new() { Name = "trumpet", Duty = 0.25, Attack = 0.008, Decay = 0.3, Sustain = 0.7, Release = 0.07, VibratoCents = 15, VibratoHz = 5.6, VibratoDelay = 0.16, Cutoff = 3600, Legato = 0.88 };
    /// <summary>Jingle lead: the trumpet with a longer release, so a final note rings out.</summary>
    public static readonly Instrument Fanfare = Trumpet with { Name = "fanfare", Release = 0.25, Legato = 0.92 };
    /// <summary>Jingle harmony: a rounder square under the fanfare lead.</summary>
    public static readonly Instrument FanfareLow = new() { Name = "fanfare low", Attack = 0.006, Decay = 0.3, Sustain = 0.7, Release = 0.25, VibratoCents = 12, VibratoHz = 5.6, VibratoDelay = 0.16, Cutoff = 2600, Legato = 0.92 };

    // Melody voices (new for the robot)

    /// <summary>A soft, breathy lead (sine plus a little octave) for tunes that must not distract.</summary>
    public static readonly Instrument Flute = new() { Name = "flute", Wave = Waveform.Sine, Bright = 0.15, Attack = 0.025, Decay = 0.4, Sustain = 0.75, Release = 0.08, VibratoCents = 12, VibratoHz = 5, VibratoDelay = 0.2, Legato = 0.9 };
    /// <summary>The classic 16-bit square lead, with a delayed vibrato.</summary>
    public static readonly Instrument SquareLead = new() { Name = "square lead", Attack = 0.005, Decay = 0.25, Sustain = 0.75, Release = 0.05, VibratoCents = 16, VibratoHz = 6, VibratoDelay = 0.15, Cutoff = 3000, Legato = 0.85 };
    /// <summary>Vibraphone: a soft FM bell that rings on a little.</summary>
    public static readonly Instrument Vibes = new() { Name = "vibes", Wave = Waveform.Fm, FmRatio = 1, FmIndex = 0.9, FmDecay = 0.25, Attack = 0.002, Decay = 0.7, Sustain = 0.2, Release = 0.25, VibratoCents = 8, VibratoHz = 5.5, VibratoDelay = 0.12, Legato = 1.05 };

    // Accompaniment (from Kegel Quest)

    public static readonly Instrument Lute = new() { Name = "lute", Duty = 0.25, Attack = 0.002, Decay = 0.15, Sustain = 0, Release = 0.05, Cutoff = 2600, Legato = 1.5 };
    public static readonly Instrument Harp = new() { Name = "harp", Wave = Waveform.Sine, Bright = 0.3, Attack = 0.003, Decay = 0.5, Sustain = 0, Release = 0.1, Legato = 2 };
    public static readonly Instrument Chime = new() { Name = "chime", Wave = Waveform.Fm, FmRatio = 1, FmIndex = 1.6, FmDecay = 0.15, Attack = 0.002, Decay = 0.4, Sustain = 0, Release = 0.08, Legato = 1.4 };
    public static readonly Instrument Strings = new() { Name = "strings", Pwm = 0.18, PwmHz = 0.35, Detune = 7, Attack = 0.4, Decay = 1, Sustain = 0.9, Release = 0.5, Cutoff = 1200, Legato = 1 };
    public static readonly Instrument Bass = new() { Name = "bass", Wave = Waveform.Triangle, Attack = 0.004, Decay = 0.35, Sustain = 0.6, Release = 0.05, Legato = 0.85 };
    public static readonly Instrument Timpani = new() { Name = "timpani", Wave = Waveform.Sine, Bright = 0.15, Attack = 0.004, Decay = 0.45, Sustain = 0, Release = 0.25, Glide = 0.7, GlideTime = 0.05, Legato = 1.5 };
    public static readonly Instrument Glock = new() { Name = "glockenspiel", Wave = Waveform.Fm, FmRatio = 3.5, FmIndex = 2.4, FmDecay = 0.06, Attack = 0.001, Decay = 0.4, Sustain = 0, Release = 0.2, Legato = 2 };

    // Accompaniment (new for the robot)

    /// <summary>Electric piano (FM, ratio 1): offbeat stabs and jazzy comping.</summary>
    public static readonly Instrument EPiano = new() { Name = "e-piano", Wave = Waveform.Fm, FmRatio = 1, FmIndex = 1.8, FmDecay = 0.12, Attack = 0.002, Decay = 0.35, Sustain = 0.25, Release = 0.08, Legato = 0.7 };
    /// <summary>A wooden mallet: FM with a fourth-partial bite that dies at once.</summary>
    public static readonly Instrument Marimba = new() { Name = "marimba", Wave = Waveform.Fm, FmRatio = 4, FmIndex = 0.9, FmDecay = 0.025, Attack = 0.001, Decay = 0.22, Sustain = 0, Release = 0.06, Legato = 1.3 };
    /// <summary>A plucked upright bass for walking lines: a triangle that thumps slightly sharp, then settles.</summary>
    public static readonly Instrument Upright = new() { Name = "upright bass", Wave = Waveform.Triangle, Attack = 0.004, Decay = 0.25, Sustain = 0.4, Release = 0.05, Glide = 0.3, GlideTime = 0.02, Legato = 0.8 };
    /// <summary>A punchy, filtered square bass for driving grooves.</summary>
    public static readonly Instrument PulseBass = new() { Name = "pulse bass", Attack = 0.003, Decay = 0.15, Sustain = 0.55, Release = 0.03, Cutoff = 800, Legato = 0.8 };
}
