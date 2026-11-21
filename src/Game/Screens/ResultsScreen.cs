using Godot;
using MathRobot.Core;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>
/// After a Stage: the Stars pop in one by one, then the Bolts are counted up line by line like a shop receipt,
/// then the XP bar fills (with a LEVEL UP! if Beep went up a level). The island spins slowly underneath.
/// </summary>
public partial class ResultsScreen : Screen
{
    private readonly Stage _stage;
    private readonly StageResult _result;
    private readonly int _levelsUp;
    private double _t;
    private int _starsShown;
    private int _linesShown;
    private int _boltsShown;
    private double _nextAt = 0.6;
    private bool _done;
    private bool _leveled;
    private readonly int _boltsBefore;
    private readonly int _xpBefore;
    private float _xpShown;

    public ResultsScreen(Stage stage, StageResult result, int levelsUp)
    {
        _stage = stage;
        _result = result;
        _levelsUp = levelsUp;
        _boltsBefore = Main.I.Progress.Bolts - result.Bolts;
        _xpBefore = Main.I.Progress.Xp - result.Xp;
        _xpShown = _xpBefore;
    }

    public override void Enter()
    {
        var plane = AddPlane();
        plane.SetMap(WorldArt.Map, tiled: false);
        plane.SetSea(WorldArt.Sea);
        plane.SetSky(WorldArt.SkyDay);
        plane.SetColors(new Color("2848b8"), new Color("a8c8ff"), new Color("b8d0ff"), 800);
        plane.Focal = 120;
        plane.Horizon = 40;
        plane.CamHeight = 90;
        M.Sound.PlayMusic("map");
    }

    protected override void Update(double dt)
    {
        _t += dt;
        if (Plane is { } plane)
        {
            var stop = WorldArt.Stops[WorldArt.StopOf(_stage.Number)];
            plane.Angle = (float)Time * 0.25f;
            plane.Cam = stop - plane.Forward * 120;
        }

        bool skip = Pad.Pressed(Btn.A);
        if (!_done && (_t >= _nextAt || skip)) Advance(skip);
        else if (_done && skip)
        {
            M.Sound.Play("confirm");
            M.Go(new MapScreen());
        }
        // The XP bar fills smoothly once the Bolts are in.
        if (_linesShown >= _result.Lines.Count && _starsShown >= _result.Stars)
        {
            float target = Main.I.Progress.Xp;
            _xpShown = Math.Min(target, _xpShown + (float)dt * Math.Max(60, _result.Xp * 1.2f));
            if (!_leveled && _levelsUp > 0 && _xpShown >= Progress.XpFor(LevelAt(_xpBefore) + 1))
            {
                _leveled = true;
                M.Sound.Play("levelup");
                Fx.Confetti(new Vector2(128, 170), 50);
            }
        }
    }

    private static int LevelAt(int xp)
    {
        int level = 1;
        while (xp >= Progress.XpFor(level + 1)) level++;
        return level;
    }

    private void Advance(bool skipAll)
    {
        do
        {
            if (_starsShown < _result.Stars)
            {
                _starsShown++;
                M.Sound.Play("star", 1f + _starsShown * 0.12f);
                Fx.Burst(Particles.Kind.Sparkle, new Vector2(128 + (_starsShown - 2) * 26, 50), 12, Gfx.Gold, 70, 40, 0.6);
                _nextAt = _t + 0.45;
            }
            else if (_linesShown < _result.Lines.Count)
            {
                var line = _result.Lines[_linesShown++];
                _boltsShown += line.Bolts;
                if (line.Bolts > 0)
                {
                    M.Sound.Play("coin", 1f + _linesShown * 0.05f);
                    Fx.Add(Particles.Kind.Bolt, new Vector2(214, 86 + _linesShown * 11), new Vector2(0, -30), Colors.White, 0.5);
                }
                _nextAt = _t + 0.32;
            }
            else
            {
                _done = true;
                if (!skipAll) M.Sound.Play("buy");
                _nextAt = double.MaxValue;
            }
        } while (skipAll && !_done);
    }

    protected override void Paint(CanvasItem ci)
    {
        Gfx.Rect(ci, 0, 0, Gfx.W, Gfx.H, new Color(0, 0, 0.1f, 0.35f));
        WavyText(ci, _stage.Boss ? "GLITCH BEATEN!" : "STAGE CLEAR!", 128, 8, Gfx.Rainbow(Time), 2, Time, 2, Gfx.Ink);

        // Stars, each with what earned it: clearing, no slips, beating the clock.
        string[] why = { "Clear", "No slips", "Speedy" };
        bool[] got = { true, _result.Accurate, _result.Fast };
        int rank = 0;
        for (int i = 0; i < 3; i++)
        {
            if (got[i]) rank++;
            bool lit = got[i] && rank <= _starsShown;
            Sprites.DrawFoot(ci, Sprites.Get(lit ? "star" : "star_empty"), new Vector2(76 + i * 52, 50), 2);
            var col = got[i] ? (lit ? Gfx.Gold : Gfx.Dim) : Gfx.Dim;
            Gfx.TextCentered(ci, why[i], 76 + i * 52, 52, col);
        }

        // The receipt.
        var box = new Rect2(16, 70, 224, 18 + 11 * _result.Lines.Count + 14);
        Gfx.Window(ci, box);
        Gfx.Text(ci, "BOLTS EARNED", new Vector2(26, 76), Gfx.Cyan);
        for (int i = 0; i < _linesShown; i++)
        {
            var line = _result.Lines[i];
            float y = 88 + i * 11;
            var col = line.Bolts > 0 ? Gfx.White : Gfx.Grey;
            Gfx.Text(ci, line.Label, new Vector2(26, y), col);
            if (line.Detail.Length > 0) Gfx.TextRight(ci, line.Detail, 176, y, Gfx.Grey);
            Gfx.TextRight(ci, line.Bolts > 0 ? $"+{line.Bolts}" : "-", 230, y, line.Bolts > 0 ? Gfx.Gold : Gfx.Dim);
        }
        float ty = box.End.Y - 14;
        Gfx.Rect(ci, 26, ty - 1, 204, 1, new Color(1, 1, 1, 0.4f));
        Sprites.Draw(ci, Sprites.Get("bolt"), new Vector2(26, ty + 2));
        Gfx.Text(ci, "TOTAL", new Vector2(38, ty + 1), Gfx.White);
        Gfx.TextRight(ci, $"{_boltsShown}", 230, ty + 1, Gfx.Gold);
    }

    protected override void PaintTop(CanvasItem ci)
    {
        // Beep, cheering, with the XP bar.
        var box = new Rect2(16, 180, 224, 40);
        Gfx.Window(ci, box, Gfx.Look.Dark, 0.95f);
        float jump = _done ? Math.Abs(Mathf.Sin((float)Time * 6)) * 4 : 0;
        BeepArt.Draw(ci, new Vector2(36, 214 - jump), Outfit.Of(P), Time, 1, _done ? "happy" : null);
        int lv = LevelAt((int)_xpShown);
        float share = ((int)_xpShown - Progress.XpFor(lv)) / (float)(Progress.XpFor(lv + 1) - Progress.XpFor(lv));
        Gfx.Text(ci, $"Lv {lv}", new Vector2(56, 186), _leveled ? Gfx.Rainbow(Time) : Gfx.White);
        Gfx.Text(ci, $"+{_result.Xp} XP", new Vector2(96, 186), Gfx.Green);
        Gfx.Rect(ci, 56, 200, 172, 6, Gfx.Ink);
        Gfx.Rect(ci, 57, 201, 170 * share, 4, Gfx.Green);
        Gfx.Rect(ci, 57, 201, 170 * share, 1, Gfx.Green.Lightened(0.5f));
        if (_leveled) WavyText(ci, "LEVEL UP!", 172, 185, Gfx.Gold, 1, Time, 1);
        if (_done && (Time % 1) < 0.7) Gfx.Help(ci, "{A} Map", 190, 208, Gfx.White);
        else Gfx.Text(ci, $"Bolts {_boltsBefore + _boltsShown}", new Vector2(56, 208), Gfx.Gold);
    }
}
