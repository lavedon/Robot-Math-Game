using Godot;
using MathRobot.Core.Audio;

namespace MathRobot.Game;

/// <summary>
/// The game's sound, synthesised in code so the game ships with no audio files: 16-bit console effects, jingles and
/// music loops (see MathRobot.Core.Audio). Every effect renders once in <see cref="_Ready"/>; every music track starts
/// rendering on a worker thread at the same time and plays as soon as it is ready. Ported from Kegel Quest's Tones.
/// </summary>
public partial class Sound : Node
{
    private const int EffectVoices = 8;
    private const int RapidVoices = 4;
    // Effects that may fire many times a second (number-line ticks, tally coins, menu cursor, a run of hops) play
    // round-robin on their own small pool, so they never steal a voice from a jingle or cut each other off.
    private static readonly HashSet<string> Rapid = new() { "tick", "coin", "cursor", "hop" };
    // Tiny pitch wobbles for the rapid repeats, so a long tally doesn't sound like a machine gun.
    private static readonly float[] Jitter = { 1f, 1.012f, 0.993f, 1.006f };
    // Fanfares duck the music, and a track asked for during one waits for it to end.
    private static readonly HashSet<string> Fanfares = new() { "victory", "levelup", "boss_down" };
    private const double DuckGain = 0.35; // about -9 dB
    private const double FanfareTail = 0.4; // a fanfare's last moments are only echo; music may start under them
    private const double MusicReturn = 0.12; // a track asked for again while it fades out comes back up this fast
    private const int LoopGuard = 64;

    private readonly Dictionary<string, AudioStreamWav> _effects = new();
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[EffectVoices];
    private readonly ulong[] _voiceStarted = new ulong[EffectVoices];
    private readonly AudioStreamPlayer[] _rapid = new AudioStreamPlayer[RapidVoices];
    private int _nextRapid;
    private readonly HashSet<string> _warned = new();

    private readonly Deck[] _decks = { new(), new() };
    private readonly Dictionary<string, AudioStreamWav> _tracks = new();
    private readonly Dictionary<string, Task<PcmLoop>> _rendering = new();
    private readonly HashSet<string> _failed = new();
    private string? _wanted;
    private double _fanfareEnds, _duck = 1;
    private bool _ready;

    /// <summary>Every name <see cref="Play"/> accepts.</summary>
    public static IReadOnlyList<string> EffectNames => AudioBank.Effects;

    /// <summary>Every track <see cref="PlayMusic"/> accepts.</summary>
    public static IReadOnlyList<string> TrackNames => AudioBank.Tracks;

    /// <summary>Mutes or unmutes everything (the Master bus). Music keeps its place while muted.</summary>
    public bool Muted
    {
        get => AudioServer.IsBusMute(MasterBus);
        set => AudioServer.SetBusMute(MasterBus, value);
    }

    /// <summary>The track last asked for with <see cref="PlayMusic"/> (null after <see cref="StopMusic"/>), audible yet or not.</summary>
    public string? CurrentTrack => _wanted;

    private static int MasterBus => Math.Max(0, AudioServer.GetBusIndex("Master"));

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // a paused tree still gets its menu blips and music
        GuardMasterBus();
        foreach (var name in AudioBank.Effects)
        {
            // One broken recipe must not silence the rest: skip it and say so.
            try
            {
                _effects[name] = ToStream(AudioBank.Effect(name));
            }
            catch (Exception e)
            {
                GD.PushError($"Sound effect '{name}' failed to render: {e.Message}");
            }
        }
        for (int i = 0; i < _voices.Length; i++) _voices[i] = AddPlayer();
        for (int i = 0; i < _rapid.Length; i++)
        {
            _rapid[i] = AddPlayer();
            _rapid[i].MaxPolyphony = 2; // a repeat that comes round again before its last one ends overlaps it
        }
        foreach (var deck in _decks) deck.Player = AddPlayer();
        _ready = true;
        // Every track renders in the background now, so screens find their music ready.
        foreach (var track in AudioBank.Tracks) MusicStream(track);
        UpdateMusic(fade: 0.25);
    }

    // Each sound peaks under -1 dBFS on its own, but two fanfares at once can sum past full scale, where the output
    // clips. A hard limiter on the master bus acts only on such overs, so a sound playing alone passes untouched.
    private static void GuardMasterBus()
    {
        int bus = MasterBus;
        for (int i = 0; i < AudioServer.GetBusEffectCount(bus); i++)
            if (AudioServer.GetBusEffect(bus, i) is AudioEffectHardLimiter) return;
        AudioServer.AddBusEffect(bus, new AudioEffectHardLimiter { CeilingDb = -0.3f });
    }

    private AudioStreamPlayer AddPlayer()
    {
        var p = new AudioStreamPlayer();
        AddChild(p);
        return p;
    }

    /// <summary>
    /// Plays an effect (see <see cref="EffectNames"/>) at a pitch (1 = as written; 2 = an octave up, and twice as
    /// fast) and a volume offset in dB. Unknown names are ignored with a one-time warning.
    /// </summary>
    public void Play(string name, float pitch = 1f, float volumeDb = 0f)
    {
        if (!_ready) return;
        if (!_effects.TryGetValue(name, out var stream))
        {
            Warn(name);
            return;
        }
        pitch = Math.Clamp(pitch, 0.1f, 4f);
        AudioStreamPlayer voice;
        if (Rapid.Contains(name))
        {
            int k = _nextRapid++ % _rapid.Length;
            voice = _rapid[k];
            if (name is "coin" or "tick") pitch *= Jitter[k % Jitter.Length];
        }
        else voice = FreeVoice();
        voice.Stream = stream;
        voice.PitchScale = pitch;
        voice.VolumeDb = volumeDb;
        voice.Play();
        if (Fanfares.Contains(name)) _fanfareEnds = Math.Max(_fanfareEnds, Now + stream.GetLength() / pitch - FanfareTail);
    }

    /// <summary>Length of an effect in seconds at pitch 1 (0 if unknown), for timing a screen around a jingle.</summary>
    public double EffectLength(string name) => _effects.TryGetValue(name, out var s) ? s.GetLength() : 0;

    /// <summary>
    /// Stops every sound and the music at once, with no fade. Call it a few frames before quitting: a playback still
    /// running when the engine shuts down is never released (Godot reports it as a leak).
    /// </summary>
    public void Silence()
    {
        _wanted = null;
        if (!_ready) return;
        foreach (var p in _voices) p.Stop();
        foreach (var p in _rapid) p.Stop();
        foreach (var deck in _decks)
        {
            deck.Player.Stop();
            deck.Track = null;
        }
    }

    // An idle voice if there is one, otherwise the one that started longest ago (its sound is the most likely to be fading).
    private AudioStreamPlayer FreeVoice()
    {
        int pick = 0;
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].Playing)
            {
                pick = i;
                break;
            }
            if (_voiceStarted[i] < _voiceStarted[pick]) pick = i;
        }
        _voiceStarted[pick] = Time.GetTicksUsec();
        return _voices[pick];
    }

    /// <summary>
    /// Starts a music loop (see <see cref="TrackNames"/>) from its first bar, while whatever was playing fades out.
    /// Asking for the track that is already playing changes nothing (it never restarts). A track still rendering
    /// starts a moment later, when it is ready; during a fanfare it waits until the fanfare is over.
    /// Unknown names are ignored with a one-time warning.
    /// </summary>
    public void PlayMusic(string track)
    {
        if (!AudioBank.Tracks.Contains(track))
        {
            Warn(track);
            return;
        }
        _wanted = track;
        UpdateMusic(fade: 0.6);
    }

    /// <summary>Fades the music out over <paramref name="fadeSeconds"/> (at least 30 ms, so it never clicks).</summary>
    public void StopMusic(double fadeSeconds = 0.6)
    {
        _wanted = null;
        UpdateMusic(fadeSeconds);
    }

    /// <summary>Renders a track in the background ahead of time (every track already starts rendering at startup).</summary>
    public void PreloadMusic(string track)
    {
        if (_ready && AudioBank.Tracks.Contains(track)) MusicStream(track);
    }

    // Sends every deck toward its target: the wanted track up, everything else down. A wanted track that is not
    // playing yet starts here, or from _Process once it has rendered or the fanfare is over.
    private void UpdateMusic(double fade)
    {
        if (!_ready) return;
        foreach (var deck in _decks)
        {
            if (deck.Track == null) continue;
            if (deck.Track == _wanted) deck.FadeTo(1, MusicReturn);
            else deck.FadeTo(0, fade);
        }
        if (_wanted != null) TryStartWanted();
    }

    private void TryStartWanted()
    {
        if (_wanted == null || Now < _fanfareEnds || _decks.Any(d => d.Track == _wanted)) return;
        var stream = MusicStream(_wanted);
        if (stream == null) return;
        var deck = _decks.FirstOrDefault(d => d.Track == null);
        if (deck == null)
        {
            // Both decks are still fading out other tracks (quick hops between screens): hurry the quieter one.
            _decks.MinBy(d => d.Gain)!.FadeTo(0, 0.05);
            return;
        }
        deck.Start(_wanted, stream, _duck);
    }

    // The cached stream, or null while the track renders on a worker thread (started by the first request).
    private AudioStreamWav? MusicStream(string track)
    {
        if (_tracks.TryGetValue(track, out var stream)) return stream;
        if (_failed.Contains(track)) return null;
        if (!_rendering.TryGetValue(track, out var task))
        {
            _rendering[track] = Task.Run(() => AudioBank.Track(track));
            return null;
        }
        if (!task.IsCompleted) return null;
        _rendering.Remove(track);
        if (!task.IsCompletedSuccessfully)
        {
            _failed.Add(track);
            GD.PushError($"Music '{track}' failed to render: {task.Exception?.GetBaseException().Message}");
            return null;
        }
        return _tracks[track] = LoopStream(task.Result);
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        // Collect finished renders on this (the main) thread, wanted or not, so they are ready when asked for.
        if (_rendering.Count > 0)
            foreach (var track in _rendering.Where(r => r.Value.IsCompleted).Select(r => r.Key).ToList())
                MusicStream(track);
        if (_wanted != null) TryStartWanted();
        double duckTarget = Now < _fanfareEnds ? DuckGain : 1;
        double duckSpeed = duckTarget < _duck ? 1 / 0.1 : 1 / 0.6; // duck fast, recover gently
        _duck = MoveToward(_duck, duckTarget, duckSpeed * delta);
        foreach (var deck in _decks) deck.Update(delta, _duck);
    }

    public override void _ExitTree()
    {
        if (_ready) Silence();
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private static double MoveToward(double from, double to, double step) =>
        from < to ? Math.Min(from + step, to) : Math.Max(from - step, to);

    private void Warn(string name)
    {
        if (_warned.Add(name)) GD.PushWarning($"Sound: no effect or music track named '{name}'");
    }

    private static AudioStreamWav ToStream(short[] pcm) =>
        new() { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Synth.Rate, Stereo = false, Data = Pcm.Bytes(pcm) };

    // A music track: its clean first pass plays once, then [LoopStart, end) repeats.
    private static AudioStreamWav LoopStream(PcmLoop track)
    {
        // The resampler reads a few samples past the loop end; padding with the samples at the loop start keeps the seam exact.
        var pcm = track.Samples;
        int guard = Math.Min(LoopGuard, track.LoopLength);
        var padded = new short[pcm.Length + guard];
        Array.Copy(pcm, padded, pcm.Length);
        Array.Copy(pcm, track.LoopStart, padded, pcm.Length, guard);
        var stream = ToStream(padded);
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = track.LoopStart;
        stream.LoopEnd = pcm.Length;
        return stream;
    }

    /// <summary>One music player with its own fade, so an old track can fade out while a new one starts.</summary>
    private sealed class Deck
    {
        public AudioStreamPlayer Player = null!;
        public string? Track;
        public double Gain, Target, Speed;

        public void Start(string track, AudioStreamWav stream, double duck)
        {
            Track = track;
            // A track's first pass starts from silence (no wrapped tails), so it comes straight in at full level.
            Gain = Target = 1;
            Player.Stream = stream;
            Apply(duck);
            Player.Play();
        }

        public void FadeTo(double target, double seconds)
        {
            Target = target;
            Speed = 1 / Math.Max(seconds, 0.03);
        }

        public void Update(double delta, double duck)
        {
            if (Track == null) return;
            Gain = MoveToward(Gain, Target, Speed * delta);
            if (Gain <= 0 && Target <= 0)
            {
                Player.Stop();
                Track = null;
                return;
            }
            Apply(Gain * duck);
        }

        private void Apply(double gain) => Player.VolumeDb = gain > 1e-4 ? Mathf.LinearToDb((float)gain) : -80;
    }
}
