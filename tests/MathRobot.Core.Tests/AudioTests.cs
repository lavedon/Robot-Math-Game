using System.Diagnostics;
using MathRobot.Core.Audio;
using Xunit;
using Xunit.Abstractions;

namespace MathRobot.Core.Tests;

public class AudioTests
{
    // The names the game plays: Sound.Play and Sound.PlayMusic are called with these strings.
    private static readonly string[] GameEffects =
    {
        "cursor", "confirm", "cancel", "error", "tick", "tens", "hop", "bighop", "land", "correct", "wrong", "coin",
        "buy", "equip", "star", "levelup", "victory", "whoosh", "boss_hit", "boss_down", "lock",
    };

    private static readonly string[] GameTracks = { "map", "play", "shop", "boss", "title" };

    private readonly ITestOutputHelper _output;

    public AudioTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<string> Effects() => ToData(AudioBank.Effects);
    public static TheoryData<string> Tracks() => ToData(AudioBank.Tracks);

    private static TheoryData<string> ToData(IEnumerable<string> names)
    {
        var data = new TheoryData<string>();
        foreach (var n in names) data.Add(n);
        return data;
    }

    [Fact]
    public void EveryEffectTheGameUsesExists() => Assert.Equal(GameEffects.Order(), AudioBank.Effects.Order());

    [Fact]
    public void EveryTrackTheGameUsesExists() => Assert.Equal(GameTracks.Order(), AudioBank.Tracks.Order());

    [Theory]
    [MemberData(nameof(Effects))]
    public void EffectRendersCleanly(string name)
    {
        var clock = Stopwatch.StartNew();
        var samples = AudioBank.EffectSamples(name);
        _output.WriteLine($"{name}: {samples.Length / (double)Synth.Rate:0.000} s, rendered in {clock.Elapsed.TotalMilliseconds:0} ms");
        AssertClean(name, samples);
        Assert.True(samples.Length < Synth.Samples(4), $"{name} lasts {samples.Length / (double)Synth.Rate:0.0} s");
        Assert.Equal(samples.Length, AudioBank.Effect(name).Length);
    }

    [Theory]
    [MemberData(nameof(Tracks))]
    public void TrackRendersAsACleanLoop(string name)
    {
        var clock = Stopwatch.StartNew();
        var track = AudioBank.TrackSamples(name);
        double ms = clock.Elapsed.TotalMilliseconds;
        _output.WriteLine($"{name}: loop {track.LoopLength / (double)Synth.Rate:0.0} s, intro {track.LoopStart / (double)Synth.Rate:0.00} s, rendered in {ms:0} ms");
        AssertClean(name, track.Samples);
        Assert.Equal(Music.Get(name).Length, track.LoopLength);
        Assert.InRange(track.LoopStart, 0, track.Samples.Length - 1);
        // At the music level (quieter than the effects), well clear of the ceiling.
        Assert.InRange(AudioLevel.Db(AudioLevel.Rms(track.Samples.AsSpan(track.LoopStart))), AudioBank.MusicLevelDb - 1.5, AudioBank.MusicLevelDb + 0.5);
    }

    [Fact]
    public void AllEffectsRenderQuicklyEnoughForStartup()
    {
        var clock = Stopwatch.StartNew();
        foreach (var name in AudioBank.Effects) AudioBank.Effect(name);
        double ms = clock.Elapsed.TotalMilliseconds;
        _output.WriteLine($"All {AudioBank.Effects.Count} effects rendered in {ms:0} ms");
        Assert.True(ms < 2000, $"Rendering every effect took {ms:0} ms");
    }

    [Fact]
    public void EveryChordSoundsItsThird()
    {
        // A chord whose third nobody plays sounds hollow (or turns minor/major by accident): some part must sound it
        // for at least a beat (half of a two-beat chord).
        var songs = AudioBank.Tracks.Select(n => (Name: n, Song: Music.Get(n))).Concat(Sfx.Jingles());
        foreach (var (name, song) in songs)
            foreach (var chord in song.Harmony)
            {
                int third = chord.Tones[1];
                if (ChordChart.Mod12(third - chord.Tones[0]) is not (3 or 4)) continue; // sus chords have none
                double heard = song.AllNotes.Where(x => x.Note.Midi % 12 == third)
                    .Sum(x => Math.Max(0, Math.Min(x.Note.Beat + x.Note.Beats, chord.End) - Math.Max(x.Note.Beat, chord.Beat)));
                Assert.True(heard >= Math.Min(1, chord.Beats / 2) - 1e-9,
                    $"{name}: the chord at bar {chord.Beat / song.BeatsPerBar + 1:0.##} sounds its third for only {heard:0.##} beats");
            }
    }

    [Fact]
    public void UnknownNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Sfx.Render("kazoo"));
        Assert.Throws<ArgumentException>(() => Music.Get("polka"));
    }

    private static void AssertClean(string name, float[] samples)
    {
        Assert.NotEmpty(samples);
        int bad = Array.FindIndex(samples, s => !float.IsFinite(s));
        Assert.True(bad < 0, $"{name} has a NaN or infinite sample at {bad}");
        double peak = AudioLevel.Peak(samples);
        Assert.True(peak <= 1, $"{name} peaks at {peak:0.000}");
        Assert.True(peak > 0.01, $"{name} is silent (peak {peak:0.0000})");
    }
}
