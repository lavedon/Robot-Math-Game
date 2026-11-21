using Godot;
using MathRobot.Core;
using MathRobot.Game.Screens;
using MathRobot.Game.Snes;

namespace MathRobot.Game;

/// <summary>
/// The root: draws the whole game into a 256×224 picture (the SNES screen), blows it up by a whole number to fill
/// the window, and runs it through the post-processing pass that does the SNES mosaic transition and the CRT
/// scanlines. It owns the pad, the save and the sound, and swaps screens.
/// </summary>
public partial class Main : Node
{
    public static Main I { get; private set; } = null!;

    public Pad Pad { get; } = new();
    public Progress Progress { get; private set; } = new();
    public Sound Sound { get; private set; } = null!;
    public Random Rng { get; } = new();

    private const string SavePath = "user://save.json";

    // Mosaic in SNES pixels (1 = off) like the hardware's mosaic register, plus darkening and scanline strength.
    private const string PostCode = """
        shader_type canvas_item;
        uniform float block = 1.0;
        uniform float dim = 0.0;
        uniform float scan = 0.0;
        uniform float scale = 4.0;
        const vec2 SRC = vec2(256.0, 224.0);
        void fragment() {
            vec2 px = floor(UV * SRC / block) * block + block * 0.5;
            vec3 c = texture(TEXTURE, px / SRC).rgb;
            // Darken the last physical row of every SNES row, like a CRT's gaps between scanlines.
            float row = mod(floor(FRAGCOORD.y), max(scale, 1.0));
            if (scale >= 3.0 && row >= scale - 1.0) c *= (1.0 - scan);
            COLOR = vec4(c * (1.0 - dim), 1.0);
        }
        """;

    private SubViewport _viewport = null!;
    private TextureRect _display = null!;
    private ShaderMaterial _post = null!;
    private Screen? _screen;
    private bool _scanlines = true;

    // Screenshot mode, for checking the art without playing: --shot out.png --screen play:hop --wait 1.5
    private string? _shotPath;
    private double _shotWait = 1.2;
    private double _clock;
    private bool _noSave;

    public bool Transitioning { get; private set; }

    public override void _Ready()
    {
        I = this;
        Gfx.Init();
        var args = OS.GetCmdlineUserArgs();
        string? screenArg = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--shot" when i + 1 < args.Length: _shotPath = args[++i]; break;
                case "--screen" when i + 1 < args.Length: screenArg = args[++i]; break;
                case "--wait" when i + 1 < args.Length: _shotWait = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--fresh": _noSave = true; break;
            }
        }
        if (_shotPath is not null) _noSave = true;
        Progress = _noSave ? new Progress() : Load();

        Sound = new Sound();
        AddChild(Sound);
        Sound.Muted = Progress.Muted;

        _viewport = new SubViewport
        {
            Size = new Vector2I(Gfx.W, Gfx.H),
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Disable3D = true,
            TransparentBg = false,
        };
        AddChild(_viewport);

        var layer = new CanvasLayer();
        AddChild(layer);
        var back = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(back);
        _post = new ShaderMaterial { Shader = new Shader { Code = PostCode } };
        _display = new TextureRect
        {
            Texture = _viewport.GetTexture(),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Material = _post,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_display);
        GetWindow().SizeChanged += Layout;
        GetWindow().Title = "Beep's Number Line Dash";
        Layout();
        SetMosaic(1, 0);

        Show(screenArg is null ? new TitleScreen() : DemoScreen(screenArg));
    }

    private Screen DemoScreen(string arg)
    {
        var parts = arg.Split(':');
        string mode = parts.Length > 1 ? parts[1] : "";
        // A save with something in it, so the screens have things to show.
        Progress.Grant(140, 420);
        for (int s = 1; s <= 6; s++) Progress.Record(new StageResult(s, 1 + s % 3, true, true, 60, 0, 0, Array.Empty<RewardLine>()));
        Progress.Buy(Progress.Item("treads"));
        Progress.Buy(Progress.Item("hat_propeller"));
        Progress.Grant(80);
        return parts[0] switch
        {
            "map" => new MapScreen(mode == "swoop"),
            "play" => new PlayScreen(Stages.Get(parts.Length > 2 ? int.Parse(parts[2]) : mode == "bighop" ? 8 : mode.StartsWith("boss") ? 10 : 4), demo: mode),
            "shop" => new ShopScreen(demo: mode),
            "results" => new ResultsScreen(Stages.Get(4), DemoResult(), levelsUp: 1),
            _ => new TitleScreen(),
        };
    }

    private static StageResult DemoResult()
    {
        var run = new StageRun(Stages.Get(4), ProblemDealer.Deal(Stages.Get(4), new Random(1)), new Perks(BoltBonus: 0.2, ComboLevel: 1));
        while (!run.Over)
        {
            run.Tick(6);
            run.Answer(run.Finished != 2, 6);
        }
        return run.Result();
    }

    private void Layout()
    {
        var size = GetWindow().Size;
        int scale = Math.Max(1, Math.Min(size.X / Gfx.W, size.Y / Gfx.H));
        var shown = new Vector2(Gfx.W * scale, Gfx.H * scale);
        _display.Size = shown;
        _display.Position = ((size - shown) / 2).Floor();
        _post.SetShaderParameter("scale", (float)scale);
    }

    private void SetMosaic(float block, float dim)
    {
        _post.SetShaderParameter("block", Mathf.Max(1, Mathf.Floor(block)));
        _post.SetShaderParameter("dim", dim);
        _post.SetShaderParameter("scan", _scanlines ? 0.22f : 0f);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        Pad.Poll(delta);
        if (_shotPath is not null && _clock >= _shotWait)
        {
            var img = _viewport.GetTexture().GetImage();
            img.Resize(Gfx.W * 3, Gfx.H * 3, Image.Interpolation.Nearest);
            img.SavePng(_shotPath);
            GD.Print($"SHOT saved {_shotPath}");
            _shotPath = null;
            GetTree().Quit();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.M:
                Sound.Muted = Progress.Muted = !Progress.Muted;
                Save();
                break;
            case Key.F11:
                var w = GetWindow();
                w.Mode = w.Mode == Window.ModeEnum.Fullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
                break;
            case Key.F2:
                _scanlines = !_scanlines;
                SetMosaic(1, 0);
                break;
        }
    }

    private void Show(Screen next)
    {
        if (_screen is not null)
        {
            _screen.Leave();
            _viewport.RemoveChild(_screen);
            _screen.QueueFree();
        }
        _screen = next;
        GD.Print($"SCREEN {next.GetType().Name}");
        _viewport.AddChild(next);
        Pad.Clear();
        next.Enter();
    }

    /// <summary>
    /// Changes screen with the SNES mosaic: the picture breaks into ever bigger blocks and darkens, the screens
    /// swap, then the new one resolves block by block.
    /// </summary>
    public async void Go(Screen next, double outSeconds = 0.35, double inSeconds = 0.4, bool whoosh = true)
    {
        if (Transitioning) return;
        Transitioning = true;
        Pad.Locked = true;
        if (whoosh) Sound.Play("whoosh");
        await Animate(0, 1, outSeconds);
        Show(next);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Animate(1, 0, inSeconds);
        Pad.Locked = false;
        Pad.Clear();
        Transitioning = false;
    }

    private async Task Animate(float from, float to, double seconds)
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(p => SetMosaic(1 + 15 * p * p, 0.7f * p)), from, to, seconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private static Progress Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath)) return new Progress();
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        return Progress.FromJson(f?.GetAsText());
    }

    public void Save()
    {
        if (_noSave) return;
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(Progress.ToJson());
    }
}
