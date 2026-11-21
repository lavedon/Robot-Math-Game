namespace MathRobot.Core.Audio;

/// <summary>
/// The music loops: original compositions in the style of a 16-bit console game, written for <see cref="Song"/>.
/// Every loop's last bar leads back into its first. The renderer wraps note tails and echoes across the seam, except
/// into the very first bar 1, which starts from silence.
/// </summary>
public static class Music
{
    public static readonly IReadOnlyList<string> Names = new[] { "title", "map", "play", "shop", "boss" };

    public static Song Get(string name) => name switch
    {
        "title" => Title(),
        "map" => Map(),
        "play" => Play(),
        "shop" => Shop(),
        "boss" => Boss(),
        _ => throw new ArgumentException($"Unknown music track '{name}'", nameof(name)),
    };

    // TITLE: a short, proud-but-cute fanfare loop for the title screen. C major, 100 BPM, 8 bars.
    //   A trumpet theme in dotted "da-dum" rhythms climbs the C chord (bar 1), steps down through G/B (bar 2), lifts
    //   to A minor and rests on a D over G (bar 4, half cadence); bars 5-8 climb to C6, fall back through F and
    //   cadence ii7-V7 (Dm7, G7) so bar 8's D5 steps down to bar 1's C5. A glockenspiel doubles the tune at pitch for
    //   a music-box shine, a horn holds long counter-notes, harp arpeggios and strings fill in, timpani mark the
    //   phrases and a snare roll in bar 8 lifts the loop back to the top.
    private static Song Title()
    {
        const string Melody =
            "C5:q. G4:e C5:q E5:q | D5:q. B4:e G4:h | A4:q. C5:e E5:q A5:q | G5:q. F5:e D5:h | " +
            "E5:q. G5:e C6:q G5:q | A5:q. G5:e F5:q A5:q | C6:q. B5:e A5:q F5:q | F5:q D5:q B4:q D5:q |";
        const string Counter =
            "E4:w | D4:w | C4:h E4:h | C4:h B3:h | G4:w | A4:w | E4:h C4:h | D4:h B3:h |";
        const string Timpani =
            "C2:q r:q r:h | G1:q r:q r:h | A1:q r:q r:h | F1:q r:q G1:q G1:e G1:e | " +
            "C2:q r:q r:h | F1:q r:q r:h | A1:q r:q F1:q r:q | D2:q r:q G1:q G1:e G1:e |";
        return new Song(bpm: 100, bars: 8) { Echo = new(Delay: 0.2, Feedback: 0.35, Wet: 0.25, Damping: 2400) }
            .Chords("C | G/B | Am | F G | C | F | Am F | Dm7 G7")
            .Line(Voices.Trumpet, Melody)
            .Line(Voices.Glock, Melody, gain: 0.18)
            .Line(Voices.Horn, Counter, gain: 0.25)
            .Arp(Voices.Harp, "0 1 2 3 4 3 2 1", "e", 1, 8, low: 48, gain: 0.3)
            .Pad(Voices.Strings, "x:w", 1, 8, low: 55, gain: 0.12)
            .Bass(Voices.Bass, "1:h 5:q 8:q", 1, 8, low: 36, gain: 0.8)
            .Line(Voices.Timpani, Timpani, gain: 0.5)
            .Drums("k . . . . . . . s . . . . . . .", 1, 7, 0.4)
            .Drums("k . . . s . . . s . s . s s S S", 8, 8, 0.45);
    }

    // MAP: the island overworld, bright and adventurous. G major, 116 BPM, 24 bars in A B A form.
    //   A  (bars 1-8)   a trumpet tune that bounds up the G chord in eighths and floats down in dotted quarters,
    //                   over a bouncing bass and a plucked lute; bar 8 spells D7 (F#-A-C-A) to lead on.
    //   B  (bars 9-16)  the recorder takes over through C, D, B minor and E minor with a soft string pad, climbs to
    //                   E6 and turns back over D7 (bar 16) to the top of A.
    //   A' (bars 17-24) the trumpet tune again with a reed counter-melody of long guide tones; bar 24 (D7) leads
    //                   back into bar 1's G.
    // The drums fill at the end of every section.
    private static Song Map()
    {
        const string A =
            "D5:e G5:e B5:e D6:e B5:q. G5:e | A5:q. F#5:e D5:q A4:q | B4:e E5:e G5:e B5:e A5:e G5:e E5:q | E5:q. G5:e F#5:h | " +
            "D5:e G5:e B5:e D6:e E6:q. D6:e | B5:q. G5:e E5:q G5:q | C6:q. B5:e A5:e G5:e E5:q | F#5:q A5:q C6:q A5:q | ";
        const string B =
            "E5:q G5:q C6:q. B5:e | A5:q F#5:q D5:h | D5:q F#5:q B5:q. A5:e | G5:q E5:q B4:h | " +
            "C5:e E5:e G5:e C6:e E6:q. D6:e | D6:q. A5:e F#5:q A5:q | B5:q G5:q E6:q C6:q | D6:q C6:q A5:q F#5:q | ";
        const string Counter = "B4:w | A4:w | G4:w | G4:h A4:h | B4:w | G4:h B4:h | E5:h C5:h | D5:h C5:h |";
        const string ChordsA = "G | D/F# | Em | C D | G | Em | C | D7 | ";
        const string ChordsB = "C | D | Bm | Em | C | D | Em C | D7 | ";
        return new Song(bpm: 116, bars: 24) { Echo = new(Delay: 0.16, Feedback: 0.3, Wet: 0.2) }
            .Chords(ChordsA + ChordsB + ChordsA)
            .Line(Voices.Trumpet, A, bar: 1)
            .Line(Voices.Recorder, B, bar: 9)
            .Line(Voices.Trumpet, A, bar: 17)
            .Line(Voices.Reed, Counter, bar: 17, gain: 0.45)
            .Arp(Voices.Lute, "0 1 2 1", "e", 1, 24, low: 55, gain: 0.35)
            .Bass(Voices.Bass, "1:q 5:e 1:e 8:q 5:q", 1, 24, low: 40, gain: 0.85)
            .Pad(Voices.Strings, "x:w", 9, 16, low: 55, gain: 0.12)
            .Drums("k h s h k k s h", 1, 7, 0.45)
            .Drums("k h s h k h S S", 8, 8, 0.45)
            .Drums("k t s t k k s t", 9, 15, 0.45)
            .Drums("k t s t s s S S", 16, 16, 0.45)
            .Drums("k h s h k k s h", 17, 23, 0.45)
            .Drums("k h s h O o O o", 24, 24, 0.45);
    }

    // PLAY: the number-line groove, upbeat but out of the way of thinking. F major, 128 BPM, 16 bars.
    // Offbeat electric-piano stabs and a syncopated pulse bass carry it; a soft flute plays short answering phrases
    // that start after a rest, so the tune breathes. Bars 1-8 go round Fmaj7-Gm7-Am7-Bbmaj7 and rest on C (bar 8);
    // bars 9-16 step down Bbmaj7-Am7-Gm7 twice with a marimba joining, and C7 in bar 16 leads home to Fmaj7.
    private static Song Play()
    {
        const string Melody =
            "r:q C5:e F5:e A5:q G5:e F5:e | G5:q. F5:e D5:h | r:q E5:e G5:e C6:q A5:e G5:e | F5:h E5:h | " +
            "r:q C5:e F5:e A5:q C6:q | D6:q. C6:e A5:h | Bb5:e A5:e G5:e F5:e D5:q F5:q | E5:h. r:q | " +
            "r:q D5:e F5:e A5:q. Bb5:e | C6:q A5:q E5:h | r:q D5:e G5:e Bb5:q. A5:e | A5:q F5:q C5:h | " +
            "r:q F5:e Bb5:e D6:q C6:e Bb5:e | A5:q. G5:e E5:q C5:q | D5:q F5:q G5:q Bb5:q | C6:q. Bb5:e G5:e E5:e C5:q |";
        return new Song(bpm: 128, bars: 16) { Echo = new(Delay: 0.14, Feedback: 0.22, Wet: 0.15) }
            .Chords("Fmaj7 | Gm7 | Am7 | Bbmaj7:h C:h | Fmaj7 | Dm7 | Gm7 | C | " +
                    "Bbmaj7 | Am7 | Gm7 | Fmaj7 | Bbmaj7 | Am7 | Gm7 | C7")
            .Line(Voices.Flute, Melody, gain: 0.7)
            .Pad(Voices.EPiano, "r:e x:e", 1, 16, low: 57, gain: 0.26)
            .Arp(Voices.Marimba, "0 1 2 3", "e", 9, 16, low: 60, gain: 0.28)
            .Bass(Voices.PulseBass, "1:q. 1:e r:e 5:e 8:e 5:e", 1, 16, low: 40, gain: 0.6)
            .Drums("k . h . s . h . k k h . s . h .", 1, 7, 0.4)
            .Drums("k . h . s . h . k . s s S s S S", 8, 8, 0.4)
            .Drums("k . h . s . h . k k h . s . h .", 9, 15, 0.4)
            .Drums("k . h . s . h . k . s s S s S S", 16, 16, 0.4);
    }

    // SHOP: the robot workshop, cosy and a little jazzy. F major, 100 BPM swung, 16 bars.
    // Vibraphone over a hand-written walking bass that approaches every chord by a half step, Charleston-rhythm
    // electric piano, a swung ride and a woodblock clock ticking on 2 and 4. The changes are jazz staples: I-vi-ii-V,
    // iii-VI7-ii-V, then I-I7-IV-iv (Bb to B flat minor, a wistful turn) and iii-VI7-ii-V; C7 in bar 16 walks down
    // to bar 1's F.
    private static Song Shop()
    {
        const string Melody =
            "A4:q C5:q3 A4:e3 E5:h | D5:q3 F5:e3 A5:q3 F5:e3 C5:h | Bb4:q D5:q3 F5:e3 G5:q3 F5:e3 D5:q | E5:h G5:q3 E5:e3 C5:q | " +
            "r:q3 A4:e3 C5:q3 E5:e3 G5:h | F#5:q3 A5:e3 F#5:q3 D5:e3 C5:h | Bb4:q D5:q F5:q A5:q | G5:h r:q3 C5:e3 E5:q3 F5:e3 | " +
            "A5:q G5:q3 F5:e3 E5:h | Eb5:q3 C5:e3 A4:q3 C5:e3 Eb5:h | D5:q F5:q3 A5:e3 D6:h | Db6:q Bb5:q3 F5:e3 Db5:h | " +
            "C5:q3 E5:e3 G5:q3 E5:e3 C5:h | F#5:q A5:q C6:q A5:q | Bb5:q3 A5:e3 G5:q3 F5:e3 D5:h | E5:q3 G5:e3 Bb5:q3 G5:e3 E5:q C5:q |";
        const string Walk =
            "F2:q A2:q C3:q C#3:q | D3:q A2:q F2:q F#2:q | G2:q Bb2:q D3:q Db3:q | C3:q G2:q E2:q G#2:q | " +
            "A2:q C3:q E3:q Eb3:q | D3:q F#2:q A2:q Ab2:q | G2:q Bb2:q D3:q B2:q | C3:q E3:q G2:q E2:q | " +
            "F2:q G2:q A2:q E2:q | F2:q A2:q C3:q A2:q | Bb2:q D3:q F3:q B2:q | Bb2:q Db3:q F2:q Bb2:q | " +
            "A2:q C3:q E3:q Eb3:q | D3:q C3:q A2:q Ab2:q | G2:q D3:q Bb2:q Db3:q | C3:q Bb2:q G2:q E2:q |";
        return new Song(bpm: 100, bars: 16) { Echo = new(Delay: 0.18, Feedback: 0.25, Wet: 0.18, Damping: 2200) }
            .Chords("Fmaj7 | Dm7 | Gm7 | C7 | Am7 | D7 | Gm7 | C7 | Fmaj7 | F7 | Bbmaj7 | Bbm | Am7 | D7 | Gm7 | C7")
            .Line(Voices.Vibes, Melody, gain: 0.8)
            .Line(Voices.Upright, Walk, gain: 0.75)
            .Pad(Voices.EPiano, "x:q+q3 x:e3 r:h", 1, 16, low: 53, gain: 0.2)
            .Drums("h . . h . h h . . h . h", 1, 16, 0.45)
            .Drums("k . . . . . k . . . . .", 1, 16, 0.25)
            .Drums(". . . w . . . . . w . .", 1, 16, 0.3);
    }

    // BOSS: the boss battle, tense but kid-friendly. A minor, 150 BPM, 16 bars.
    // A square-wave hero theme over driving octave eighths in the bass and a sixteenth-note pulse ostinato. Bars 1-8
    // state the theme (Am-F-G) and end on E major (the raised G# of harmonic minor) with a sly C-B-G# turn; bars 9-16
    // climb through D minor, gain a harmony line a third below, push up F-G to the top E6 and fall E-D-B-G# over E
    // so the loop snaps back to A minor. Timpani hit the phrase starts; toms and a snare roll fill bars 8 and 16.
    private static Song Boss()
    {
        const string Melody =
            "E5:q. E5:e A5:q G5:e E5:e | C5:e D5:e E5:e C5:e A4:h | F5:q. E5:e C5:q A4:q | B4:q. D5:e G5:h | " +
            "E5:q. E5:e A5:q B5:e C6:e | B5:e A5:e G5:e E5:e A5:h | C6:q. A5:e F5:q C5:q | B4:e C5:e B4:e G#4:e E4:h | " +
            "D5:q F5:q A5:q. G5:e | E5:q C5:q A4:h | F5:q A5:q D6:q. C6:e | B5:q G#5:q E5:h | " +
            "A5:e G5:e F5:e G5:e A5:q C6:q | B5:e A5:e G5:e A5:e B5:q D6:q | C6:q A5:q B5:q D6:q | E6:q. D6:e B5:q G#5:q |";
        const string Harmony =
            "A4:q D5:q F5:q. E5:e | C5:q A4:q E4:h | D5:q F5:q A5:q. G5:e | G#5:q E5:q B4:h | " +
            "F5:e E5:e D5:e E5:e F5:q A5:q | G5:e F5:e E5:e F5:e G5:q B5:q | A5:q F5:q G5:q B5:q | B5:q. B5:e G#5:q E5:q |";
        const string Timpani =
            "A2:q r:q r:h | r:w | F2:q r:q r:h | G2:q r:q r:h | A2:q r:q r:h | r:w | F2:q r:q r:h | E2:q r:q E2:e E2:e E2:q | " +
            "D2:q r:q r:h | A2:q r:q r:h | D2:q r:q r:h | E2:q r:q E2:e E2:e E2:q | F2:q r:q r:h | G2:q r:q r:h | F2:q r:q G2:q r:q | E2:q E2:e E2:e E2:q E2:q |";
        return new Song(bpm: 150, bars: 16) { Echo = new(Delay: 0.15, Feedback: 0.25, Wet: 0.16) }
            .Chords("Am | Am | F | G | Am | Am | F | E | Dm | Am | Dm | E | F | G | F:h G:h | E")
            .Line(Voices.SquareLead, Melody, gain: 0.8)
            .Line(Voices.FanfareLow, Harmony, bar: 9, gain: 0.4)
            .Bass(Voices.PulseBass, "1:e 1:e 8:e 1:e 1:e 8:e 1:e 5:e", 1, 16, low: 40, gain: 0.6)
            .Arp(Voices.Lute, "0 1 2 3", "s", 1, 16, low: 57, gain: 0.18)
            .Pad(Voices.Strings, "x:w", 1, 16, low: 52, gain: 0.1)
            .Line(Voices.Timpani, Timpani, gain: 0.5)
            .Drums("k h s h k k s h", 1, 7, 0.45)
            .Drums("k h s h O o O o", 8, 8, 0.45)
            .Drums("k h s h k k s h", 9, 15, 0.45)
            .Drums("k h s s S S S S", 16, 16, 0.45);
    }
}
