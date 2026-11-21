using Godot;
using MathRobot.Core;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>
/// The World Map: Gear Island in Mode 7. Beep walks the path from stop to stop with the camera chasing behind him,
/// turning as the path turns. Trees, rocks and stop signs stand up out of the plane as scaled sprites. Coming in
/// from the title or the Garage, the camera swoops down from the sky.
/// </summary>
public partial class MapScreen : Screen
{
    private const float ChaseDistance = 84;
    private const float WalkSpeed = 90;

    private readonly bool _swoop;
    private int _stop;
    private int _target;
    private Vector2 _pos;
    private float _angle;
    private float _walkT;
    private double _swoopT;
    private Outfit _outfit = new();
    private readonly List<(Vector2 Pos, string Sprite)> _props = new();

    public MapScreen(bool swoop = false) => _swoop = swoop;

    public override void Enter()
    {
        _outfit = Outfit.Of(P);
        var plane = AddPlane();
        plane.SetMap(WorldArt.Map, tiled: false);
        plane.SetSea(WorldArt.Sea);
        plane.SetSky(WorldArt.SkyDay);
        plane.SetColors(new Color("2848b8"), new Color("a8c8ff"), new Color("b8d0ff"), 700);
        plane.Focal = 120;
        plane.Horizon = 68;
        plane.CamHeight = 34;

        _stop = Math.Clamp(P.LastStop, 0, WorldArt.Stops.Length - 1);
        if (!Reachable(_stop)) _stop = 0;
        _target = _stop;
        _pos = WorldArt.Stops[_stop];
        _angle = FacingAngle(_stop);
        _swoopT = _swoop ? 0 : 10;
        PlaceProps();
        Place();
        M.Sound.PlayMusic("map");
    }

    private void PlaceProps()
    {
        var r = new Random(7);
        int tries = 0;
        while (_props.Count < 90 && tries++ < 4000)
        {
            var p = new Vector2(r.Next(20, 492), r.Next(20, 492));
            if (WorldArt.Inland(p) < 14) continue;
            if (WorldArt.PathDistance(p) < 16) continue;
            if (WorldArt.Stops.Any(s => s.DistanceTo(p) < 22)) continue;
            if (_props.Any(q => q.Pos.DistanceTo(p) < 14)) continue;
            if (p.DistanceTo(new Vector2(300, 268)) < 34) continue; // the pond
            string sprite = WorldArt.InCircuit(p) ? "gear" : WorldArt.InRocks(p) ? (r.Next(3) == 0 ? "pine" : "rock") : r.Next(3) == 0 ? "pine" : "tree";
            _props.Add((p, sprite));
        }
    }

    /// <summary>A stop can be walked to when every Stage before it is Cleared; the Garage never blocks.</summary>
    private bool Reachable(int stop)
    {
        for (int s = 0; s < stop; s++)
        {
            int stage = WorldArt.StageAt(s);
            if (stage > 0 && !P.Cleared(stage)) return false;
        }
        return true;
    }

    private static float FacingAngle(int stop)
    {
        var stops = WorldArt.Stops;
        var dir = stop + 1 < stops.Length ? stops[stop + 1] - stops[stop] : stops[stop] - stops[stop - 1];
        return AngleOf(dir);
    }

    // Mode 7 angle 0 looks "up" the map (-y); the angle turns clockwise.
    private static float AngleOf(Vector2 dir) => Mathf.Atan2(dir.X, -dir.Y);

    protected override void Update(double dt)
    {
        _swoopT += dt;
        bool walking = _target != _stop;
        if (walking)
        {
            var from = WorldArt.Stops[_stop];
            var to = WorldArt.Stops[_target];
            float len = from.DistanceTo(to);
            _walkT += (float)dt * WalkSpeed / len;
            _pos = from.Lerp(to, Math.Min(1, _walkT));
            float want = AngleOf(to - from);
            _angle = TurnToward(_angle, want, (float)dt * 4);
            if ((int)(_walkT * len / 10) != (int)((_walkT - dt * WalkSpeed / len) * len / 10)) M.Sound.Play("tick", 0.8f, -10);
            if (_walkT >= 1)
            {
                _stop = _target;
                _walkT = 0;
                _pos = to;
                M.Sound.Play("land", 1.2f);
                P.LastStop = _stop;
            }
            Fx.Trail(_outfit.Trail, new Vector2(128, 160), Time);
        }
        else
        {
            _angle = TurnToward(_angle, FacingAngle(_stop), (float)dt * 1.5f);
            if (_swoopT > 1.0) Input_();
        }
        Place();
    }

    private void Input_()
    {
        if (Pad.Repeat(Btn.Right) || Pad.Repeat(Btn.Up)) Walk(+1);
        else if (Pad.Repeat(Btn.Left) || Pad.Repeat(Btn.Down)) Walk(-1);
        else if (Pad.Pressed(Btn.A) || Pad.Pressed(Btn.Start)) EnterStop();
        else if (Pad.Pressed(Btn.Y) || Pad.Pressed(Btn.X)) OpenShop();
        else if (Pad.Pressed(Btn.B) || Pad.Pressed(Btn.Select))
        {
            M.Sound.Play("cancel");
            M.Save();
            M.Go(new TitleScreen());
        }
    }

    private void Walk(int dir)
    {
        int to = _stop + dir;
        if (to < 0 || to >= WorldArt.Stops.Length) return;
        if (!Reachable(to))
        {
            M.Sound.Play("error");
            return;
        }
        _target = to;
        _walkT = 0;
        M.Sound.Play("cursor");
    }

    private void EnterStop()
    {
        int stage = WorldArt.StageAt(_stop);
        M.Save();
        if (stage == 0)
        {
            OpenShop();
            return;
        }
        M.Sound.Play("confirm");
        M.Go(new PlayScreen(Stages.Get(stage)), 0.5, 0.4);
    }

    private void OpenShop()
    {
        M.Sound.Play("confirm");
        M.Save();
        M.Go(new ShopScreen(), 0.45, 0.4);
    }

    private static float TurnToward(float from, float to, float rate)
    {
        float d = Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);
        return from + d * Math.Min(1, rate);
    }

    /// <summary>Puts the camera behind Beep; during the swoop it spins down out of the sky.</summary>
    private void Place()
    {
        if (Plane is not { } plane) return;
        float s = EaseInOut((float)Math.Min(1, _swoopT / 1.6));
        float height = Mathf.Lerp(420, 42, s);
        float dist = Mathf.Lerp(30, ChaseDistance, s);
        float spin = (1 - s) * Mathf.Tau * 0.75f;
        plane.CamHeight = height;
        plane.Horizon = Mathf.Lerp(20, 68, s);
        plane.Chase(_pos, _angle + spin, dist);
    }

    protected override void Paint(CanvasItem ci)
    {
        if (Plane is not { } plane) return;
        // Everything standing on the plane, far to near.
        var things = new List<(float Depth, Action Draw)>();
        foreach (var (pos, sprite) in _props)
        {
            if (!plane.Project(pos, out var at, out float scale, out float depth) || depth < 62) continue;
            things.Add((depth, () => Sprites.DrawFoot(ci, Sprites.Get(sprite), at, scale)));
        }
        for (int i = 0; i < WorldArt.Stops.Length; i++)
        {
            int stop = i;
            if (!plane.Project(WorldArt.Stops[i], out var at, out float scale, out float depth)) continue;
            things.Add((depth + 0.01f, () => DrawStop(ci, stop, at, scale)));
        }
        if (plane.Project(_pos, out var beepAt, out float beepScale, out float beepDepth))
        {
            things.Add((beepDepth - 0.01f, () =>
            {
                bool walking = _target != _stop;
                float bob = walking ? Math.Abs(Mathf.Sin((float)Time * 12)) * 3 : 0;
                BeepArt.Draw(ci, beepAt - new Vector2(0, bob * beepScale), _outfit, Time, Math.Max(0.5f, beepScale * 0.8f),
                    squash: walking ? new Vector2(1, 1 + 0.06f * Mathf.Sin((float)Time * 24)) : null);
            }));
        }
        foreach (var t in things.OrderByDescending(t => t.Depth)) t.Draw();
    }

    private void DrawStop(CanvasItem ci, int stop, Vector2 at, float scale)
    {
        int stage = WorldArt.StageAt(stop);
        if (stage == 0)
        {
            Sprites.DrawFoot(ci, Sprites.Get("garage"), at, scale);
            return;
        }
        bool open = Reachable(stop);
        bool cleared = P.Cleared(stage);
        bool boss = Stages.Get(stage).Boss;
        // A floating sign with the stage number, bobbing over the pad.
        float bob = Mathf.Sin((float)Time * 3 + stop) * 2 * scale;
        var sign = at - new Vector2(0, 22 * scale + bob);
        float s = Math.Clamp(scale, 0.4f, 1.6f);
        int w = (int)(14 * s), h = (int)(12 * s);
        var color = !open ? new Color("50546a") : boss ? new Color("a02a6a") : cleared ? new Color("2a8a3a") : new Color("3a58c8");
        Gfx.Rect(ci, sign.X - 0.5f, sign.Y, Math.Max(1, s), 22 * scale + bob - 2, new Color("2a2030"));
        Gfx.Rect(ci, sign.X - w / 2f - 1, sign.Y - h - 1, w + 2, h + 2, Gfx.Ink);
        Gfx.Rect(ci, sign.X - w / 2f, sign.Y - h, w, h, color);
        if (s >= 0.7f)
        {
            if (!open) Sprites.DrawFoot(ci, Sprites.Get("lock"), sign - new Vector2(0, h / 2f - 4), 1);
            else Gfx.TextCentered(ci, boss ? "!" : stage.ToString(), sign.X, sign.Y - h / 2f - 5, Gfx.White, 1, shadow: false);
            if (cleared)
                for (int k = 0; k < 3; k++)
                    Gfx.Rect(ci, sign.X - 5 + k * 4, sign.Y - h - 4, 3, 3, k < P.BestStars(stage) ? Gfx.Gold : Gfx.Dim);
        }
        else Gfx.Rect(ci, sign.X - 1, sign.Y - h / 2f - 1, 2, 2, Gfx.White);
    }

    protected override void PaintTop(CanvasItem ci)
    {
        float show = EaseOut((float)(_swoopT - 1.2) * 3);
        if (show <= 0) return;
        // Top bar: Beep's level, title and Bolts.
        float topY = -16 + 16 * show;
        Gfx.Rect(ci, 0, topY, Gfx.W, 14, new Color(0.02f, 0.02f, 0.12f, 0.75f));
        Gfx.Text(ci, $"Lv{P.Level} {P.Title}", new Vector2(4, topY + 2), Gfx.Cyan);
        Sprites.Draw(ci, Sprites.Get("star"), new Vector2(160, topY + 2));
        Gfx.Text(ci, P.TotalStars.ToString(), new Vector2(171, topY + 2), Gfx.Gold);
        Sprites.Draw(ci, Sprites.Get("bolt"), new Vector2(206, topY + 3));
        Gfx.TextRight(ci, P.Bolts.ToString(), 252, topY + 2, Gfx.Gold);
        // XP bar.
        int lv = P.Level;
        float xpShare = (P.Xp - Progress.XpFor(lv)) / (float)(Progress.XpFor(lv + 1) - Progress.XpFor(lv));
        Gfx.Rect(ci, 0, topY + 13, Gfx.W, 1, Gfx.Ink);
        Gfx.Rect(ci, 0, topY + 13, Gfx.W * xpShare, 1, Gfx.Green);

        // The stop's card at the bottom.
        int stage = WorldArt.StageAt(_target);
        float y = 224 - 44 * show;
        var box = new Rect2(4, y, 248, 40);
        if (stage == 0)
        {
            Gfx.Window(ci, box, Gfx.Look.Red);
            Gfx.Text(ci, "GIZMO'S GARAGE", new Vector2(12, y + 5), Gfx.Gold);
            Gfx.Text(ci, "Upgrades, gadgets and style for Beep!", new Vector2(12, y + 16), Gfx.White);
            Gfx.Help(ci, "{A} Go in   {Left}{Right} Walk", 12, y + 27, Gfx.Grey);
        }
        else
        {
            var st = Stages.Get(stage);
            Gfx.Window(ci, box, st.Boss ? Gfx.Look.Red : Gfx.Look.Blue);
            Gfx.Text(ci, $"STAGE {stage}: {st.Name.ToUpperInvariant()}", new Vector2(12, y + 5), st.Boss ? Gfx.Pink : Gfx.Gold);
            Gfx.Text(ci, st.Blurb, new Vector2(12, y + 16), Gfx.White);
            for (int k = 0; k < 3; k++)
                Sprites.Draw(ci, Sprites.Get(k < P.BestStars(stage) ? "star" : "star_empty"), new Vector2(214 + k * 11, y + 5));
            Gfx.Help(ci, "{A} Play  {Left}{Right} Walk  {Y} Garage", 12, y + 27, Gfx.Grey);
            if (st.Boss && !P.Cleared(st.Number))
                Gfx.TextRight(ci, "BOSS!", 246, y + 16, (Time % 0.5) < 0.3 ? Gfx.Red : Gfx.Pink);
        }
        if (_target != _stop) return;
        // Arrows showing where Beep can walk.
        float pulse = (Time % 0.8) < 0.5 ? 0 : 1;
        if (_stop + 1 < WorldArt.Stops.Length && Reachable(_stop + 1)) Gfx.Text(ci, ">", new Vector2(240 + pulse, 120), Gfx.White, 2);
        if (_stop > 0) Gfx.Text(ci, "<", new Vector2(4 - pulse, 120), Gfx.White, 2);
    }
}
