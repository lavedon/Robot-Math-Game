using Godot;
using MathRobot.Core;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>
/// Gizmo's Garage, the RPG shop: three shelves (Parts, Gadgets, Style), a list with a pointing-hand cursor, Gizmo
/// talking at the counter, and Beep on a turntable wearing whatever the cursor is on, with his stats beside him
/// showing what a Part would add. Buying sends Bolts flying from the price to Beep.
/// </summary>
public partial class ShopScreen : Screen
{
    private const int Rows = 5;
    private const float ListY = 110;
    private const float RowH = 12;

    private readonly string _demo;
    private Shelf _shelf = Shelf.Parts;
    private int _cursor;
    private int _scroll;
    private string _talk = "";
    private double _talkStart;
    private double _boughtAt = -10;
    private double _deniedAt = -10;
    private bool _leaving;
    private readonly Random _random = new();
    private readonly List<(Vector2 From, Vector2 To, double Start)> _flying = new();

    public ShopScreen(string demo = "") => _demo = demo;

    private IReadOnlyList<ShopItem> Items => Progress.Catalog.Where(i => i.Shelf == _shelf).ToList();
    private ShopItem Selected => Items[Math.Clamp(_cursor, 0, Items.Count - 1)];

    public override void Enter()
    {
        M.Sound.PlayMusic("shop");
        Say(P.ShopVisits == 0 ? Keeper.FirstHello[0] : Keeper.Pick(Keeper.Hello, _random));
        P.ShopVisits++;
        M.Save();
        if (_demo == "style")
        {
            _shelf = Shelf.Style;
            _cursor = 2;
        }
    }

    private void Say(string text)
    {
        _talk = text;
        _talkStart = Time;
    }

    protected override void Update(double dt)
    {
        if (_leaving) return;
        // Gizmo's voice: a soft blip every few letters while he talks.
        int shown = Shown(), before = (int)((Time - dt - _talkStart) * 45);
        if (shown < _talk.Length && shown / 3 != before / 3 && _talk[Math.Min(shown, _talk.Length - 1)] != ' ')
            M.Sound.Play("tick", 0.7f + (shown % 5) * 0.06f, -14);

        if (Pad.Repeat(Btn.Down)) Move(1);
        else if (Pad.Repeat(Btn.Up)) Move(-1);
        else if (Pad.Pressed(Btn.RB) || Pad.Pressed(Btn.Right)) SwitchShelf(1);
        else if (Pad.Pressed(Btn.LB) || Pad.Pressed(Btn.Left)) SwitchShelf(-1);
        else if (Pad.Pressed(Btn.A)) Choose();
        else if (Pad.Pressed(Btn.B) || Pad.Pressed(Btn.Select) || Pad.Pressed(Btn.Start)) Leave_();

        for (int i = _flying.Count - 1; i >= 0; i--)
            if (Time - _flying[i].Start > 0.6) _flying.RemoveAt(i);
    }

    private int Shown() => (int)Math.Max(0, (Time - _talkStart) * 45);

    private void Move(int d)
    {
        int n = Items.Count;
        _cursor = (_cursor + d + n) % n;
        if (_cursor < _scroll) _scroll = _cursor;
        if (_cursor >= _scroll + Rows) _scroll = _cursor - Rows + 1;
        M.Sound.Play("cursor");
    }

    private void SwitchShelf(int d)
    {
        _shelf = (Shelf)(((int)_shelf + d + 3) % 3);
        _cursor = 0;
        _scroll = 0;
        M.Sound.Play("cursor", 0.8f);
    }

    private void Choose()
    {
        var item = Selected;
        // Owned Style: wear it, or take it off.
        if (item.Slot != Slot.None && P.Owns(item.Id))
        {
            if (P.Worn(item.Slot) == item.Id)
            {
                P.TakeOff(item.Slot);
                M.Sound.Play("cancel");
            }
            else
            {
                P.Wear(item);
                M.Sound.Play("equip");
                Fx.Burst(Particles.Kind.Sparkle, new Vector2(204, 70), 14, Gfx.White, 60, 30, 0.6);
            }
            M.Save();
            return;
        }
        switch (P.CanBuy(item))
        {
            case Progress.BuyCheck.MaxedOut:
                Deny(Keeper.Pick(Keeper.Maxed, _random));
                return;
            case Progress.BuyCheck.NeedLevel:
                Deny(Keeper.Pick(Keeper.TooLow, _random));
                return;
            case Progress.BuyCheck.NeedBolts:
                Deny(Keeper.Pick(Keeper.Broke, _random));
                return;
        }
        P.Buy(item);
        M.Save();
        M.Sound.Play("buy");
        if (item.Slot != Slot.None) M.Sound.Play("equip");
        _boughtAt = Time;
        Say(Keeper.Pick(Keeper.Bought, _random));
        var from = new Vector2(236, ListY + (_cursor - _scroll) * RowH + 6);
        for (int i = 0; i < 8; i++) _flying.Add((from + new Vector2(_random.Next(-4, 5), _random.Next(-3, 4)), new Vector2(204, 76), Time + i * 0.04));
        Fx.Confetti(new Vector2(204, 70), 30);
    }

    private void Deny(string line)
    {
        M.Sound.Play("error");
        _deniedAt = Time;
        Say(line);
    }

    private void Leave_()
    {
        _leaving = true;
        M.Sound.Play("cancel");
        Say(Keeper.Pick(Keeper.Bye, _random));
        M.Save();
        // The mosaic out of the Garage, then the camera swoops back down onto the island.
        M.Go(new MapScreen(swoop: true), 0.55, 0.35);
    }

    // ---------------------------------------------------------------- drawing

    protected override void Paint(CanvasItem ci)
    {
        DrawRoom(ci);
        DrawGizmo(ci);
        DrawBubble(ci);
        DrawBeepStage(ci);
        DrawStats(ci);
        DrawTabs(ci);
        DrawList(ci);
        DrawDetails(ci);
        DrawHeader(ci);
    }

    protected override void PaintTop(CanvasItem ci)
    {
        // Bolts flying from the price tag to Beep.
        foreach (var (from, to, start) in _flying)
        {
            float t = (float)((Time - start) / 0.5);
            if (t < 0 || t > 1) continue;
            var p = from.Lerp(to, EaseInOut(t)) - new Vector2(0, 30 * Mathf.Sin(t * Mathf.Pi));
            Sprites.DrawFoot(ci, Sprites.Get("bolt"), p);
        }
    }

    private void DrawHeader(CanvasItem ci)
    {
        Gfx.Rect(ci, 0, 0, Gfx.W, 13, new Color("1a0a10"));
        Gfx.Rect(ci, 0, 13, Gfx.W, 1, new Color("ff8a5a"));
        Gfx.Text(ci, "GIZMO'S GARAGE", new Vector2(4, 2), Gfx.Orange);
        Gfx.Text(ci, $"Lv{P.Level}", new Vector2(110, 2), Gfx.Cyan);
        Sprites.Draw(ci, Sprites.Get("bolt"), new Vector2(206, 3));
        bool flash = Time - _boughtAt < 0.6 && (Time * 12) % 2 < 1;
        Gfx.TextRight(ci, P.Bolts.ToString(), 252, 2, flash ? Gfx.White : Gfx.Gold);
    }

    private void DrawRoom(CanvasItem ci)
    {
        // Back wall: a pegboard with tools, a window, and a workbench counter.
        Gfx.Gradient(ci, new Rect2(0, 14, Gfx.W, 92), new Color("4a2a3a"), new Color("2a1622"));
        for (int y = 20; y < 92; y += 6)
            for (int x = 4 + (y / 6 % 2) * 3; x < Gfx.W; x += 6)
                Gfx.Rect(ci, x, y, 1, 1, new Color("1a0c14"));
        // Hanging tools on the pegboard.
        Gfx.Rect(ci, 152, 20, 2, 14, Gfx.Grey);
        Gfx.Rect(ci, 149, 20, 8, 3, Gfx.Grey);
        Gfx.Rect(ci, 162, 22, 1, 12, new Color("c08040"));
        Gfx.Rect(ci, 160, 32, 5, 4, Gfx.Grey);
        Sprites.Draw(ci, Sprites.Get("gear"), new Vector2(170, 22));
        // Counter.
        Gfx.Rect(ci, 0, 86, 80, 4, new Color("c88a48"));
        Gfx.Rect(ci, 0, 90, 80, 16, new Color("8a5a2a"));
        Gfx.Rect(ci, 0, 90, 80, 1, new Color("5a3418"));
        for (int x = 6; x < 80; x += 18) Gfx.Rect(ci, x, 94, 10, 8, new Color("6a4220"));
    }

    private void DrawGizmo(CanvasItem ci)
    {
        bool talking = Shown() < _talk.Length;
        float bob = Mathf.Round(Mathf.Sin((float)Time * 2) * 1);
        var foot = new Vector2(40, 88 + bob);
        if (Time - _boughtAt < 0.5) foot.Y -= Math.Abs(Mathf.Sin((float)(Time - _boughtAt) * 12)) * 4;
        var tex = Sprites.Get("gizmo");
        Sprites.DrawFoot(ci, tex, foot, 2);
        var top = foot - new Vector2(32, 60);
        // Antenna bulb blinks.
        if ((Time % 1.2) < 0.6) Gfx.Rect(ci, top.X + 28, top.Y + 2, 2, 2, Gfx.White);
        // Moustache-grille mouth chatters while he talks.
        if (talking && (Time * 10) % 2 < 1) Gfx.Rect(ci, top.X + 18, top.Y + 32, 28, 2, Gfx.Ink);
        // Monocle glint.
        if ((Time % 3) < 0.15) Gfx.Rect(ci, top.X + 38, top.Y + 18, 2, 2, Gfx.White);
        if (Time - _deniedAt < 0.4) Gfx.Text(ci, "!?", new Vector2(top.X + 54, top.Y + 4), Gfx.Red);
    }

    private void DrawBubble(CanvasItem ci)
    {
        var box = new Rect2(74, 17, 179, 30);
        Gfx.Window(ci, box, Gfx.Look.Dark);
        // Tail pointing at Gizmo.
        Gfx.Rect(ci, 70, 32, 5, 1, new Color("8a90b8"));
        Gfx.Rect(ci, 71, 33, 3, 1, new Color("8a90b8"));
        string text = _talk[..Math.Min(_talk.Length, Shown())];
        Gfx.Text(ci, text, new Vector2(80, 21), Gfx.White);
    }

    private Outfit Preview()
    {
        var outfit = Outfit.Of(P);
        var item = Selected;
        return item.Slot != Slot.None ? outfit.With(item) : outfit;
    }

    private void DrawBeepStage(CanvasItem ci)
    {
        // A turntable with a spotlight.
        var c = new Vector2(214, 100);
        var light = new Color(1, 1, 0.8f, 0.08f);
        ci.DrawPolygon(new[] { new Vector2(200, 48), new Vector2(224, 48), new Vector2(242, 100), new Vector2(182, 100) }, new[] { light, light, light, light });
        Gfx.Rect(ci, c.X - 26, c.Y - 4, 52, 6, new Color("3a3e5a"));
        Gfx.Rect(ci, c.X - 26, c.Y - 4, 52, 1, new Color("7a80a8"));
        for (int i = 0; i < 6; i++)
        {
            float x = c.X - 24 + ((float)(Time * 20) + i * 9) % 50;
            Gfx.Rect(ci, x, c.Y - 2, 2, 2, Gfx.Cyan.Darkened(0.4f));
        }
        float jump = Time - _boughtAt < 0.8 ? Math.Abs(Mathf.Sin((float)(Time - _boughtAt) * 8)) * 8 : 0;
        BeepArt.Draw(ci, new Vector2(c.X, c.Y - 4 - jump), Preview(), Time, 2, Time - _boughtAt < 1.2 ? "happy" : null);
        var trail = Preview().Trail;
        if (trail is not null && M.Rng.NextDouble() < 0.5) Fx.Trail(trail, new Vector2(c.X + M.Rng.Next(-12, 12), c.Y - 10), Time);
    }

    private void DrawStats(CanvasItem ci)
    {
        var box = new Rect2(74, 49, 104, 46);
        Gfx.Window(ci, box, Gfx.Look.Blue, 0.9f);
        var stats = P.Stats;
        var item = Selected;
        bool part = item.Shelf == Shelf.Parts && P.NextPrice(item) is not null;
        (string Name, int Value, int Max, string Id)[] rows =
        {
            ("SPD", stats.Speed, 6, "treads"),
            ("LUCK", stats.Luck, 6, "magnet"),
            ("TIME", stats.Time, 4, "clock"),
            ("COMBO", stats.Combo, 3, "combo"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, value, max, id) = rows[i];
            float y = 52 + i * 10;
            bool up = part && item.Id == id;
            Gfx.Text(ci, name, new Vector2(80, y), up ? Gfx.Green : Gfx.White);
            for (int k = 0; k < max; k++)
            {
                float x = 120 + k * 9;
                var col = k < value ? Gfx.Gold : Gfx.Ink;
                if (up && k == value && (Time % 0.6) < 0.4) col = Gfx.Green;
                Gfx.Rect(ci, x, y + 2, 7, 6, Gfx.Ink);
                Gfx.Rect(ci, x + 1, y + 3, 5, 4, col);
            }
        }
    }

    private void DrawTabs(CanvasItem ci)
    {
        string[] names = { "PARTS", "GADGETS", "STYLE" };
        float x = 4;
        for (int i = 0; i < 3; i++)
        {
            bool on = (int)_shelf == i;
            float w = Gfx.Measure(names[i]) + 12;
            var fill = on ? new Color("3a58c8") : new Color("1a1c30");
            Gfx.Rect(ci, x, 96 + (on ? 0 : 2), w, 12 - (on ? 0 : 2), Gfx.Ink);
            Gfx.Rect(ci, x + 1, 97 + (on ? 0 : 2), w - 2, 11 - (on ? 0 : 2), fill);
            if (on) Gfx.Rect(ci, x + 1, 97, w - 2, 1, new Color("f0f0ff"));
            Gfx.Text(ci, names[i], new Vector2(x + 6, 98 + (on ? 0 : 1)), on ? Gfx.White : Gfx.Grey);
            x += w + 2;
        }
        Gfx.Help(ci, "{LB}{RB}", x + 2, 97, Gfx.White);
    }

    private void DrawList(CanvasItem ci)
    {
        var box = new Rect2(2, 107, 252, RowH * Rows + 8);
        Gfx.Window(ci, box);
        var items = Items;
        for (int r = 0; r < Rows && _scroll + r < items.Count; r++)
        {
            int index = _scroll + r;
            var item = items[index];
            float y = ListY + r * RowH;
            bool on = index == _cursor;
            if (on) Gfx.Rect(ci, 6, y, 244, RowH, new Color(1, 1, 1, 0.12f));
            if (on)
            {
                float nudge = (Time % 0.5) < 0.25 ? 0 : 1;
                Sprites.Draw(ci, Sprites.Get("cursor"), new Vector2(7 + nudge, y + 2));
            }
            DrawIcon(ci, item, new Vector2(16, y));
            var check = P.CanBuy(item);
            bool owned = P.Owns(item.Id);
            var nameCol = check == Progress.BuyCheck.NeedLevel ? Gfx.Dim : Gfx.White;
            Gfx.Text(ci, item.Name, new Vector2(31, y + 1), nameCol);

            string status = "";
            Color statusCol = Gfx.Grey;
            if (item.Leveled) status = $"Lv{P.LevelOf(item.Id)}/{item.MaxLevel}";
            if (item.Slot != Slot.None && owned)
            {
                status = P.Worn(item.Slot) == item.Id ? "WORN" : "OWNED";
                statusCol = status == "WORN" ? Gfx.Green : Gfx.Cyan;
            }
            else if (!item.Leveled && owned)
            {
                status = "OWNED";
                statusCol = Gfx.Cyan;
            }
            Gfx.Text(ci, status, new Vector2(140, y + 1), statusCol);

            if (P.NextPrice(item) is { } price && !(item.Slot != Slot.None && owned))
            {
                if (check == Progress.BuyCheck.NeedLevel)
                {
                    Sprites.Draw(ci, Sprites.Get("lock"), new Vector2(206, y + 2));
                    Gfx.TextRight(ci, $"Lv{item.MinLevel}", 248, y + 1, Gfx.Red);
                }
                else
                {
                    Sprites.Draw(ci, Sprites.Get("bolt"), new Vector2(214, y + 2));
                    Gfx.TextRight(ci, price.ToString(), 248, y + 1, check == Progress.BuyCheck.Ok ? Gfx.Gold : Gfx.Red.Lightened(0.2f));
                }
            }
            else if (item.Leveled && owned) Gfx.TextRight(ci, "MAX", 248, y + 1, Gfx.Green);
        }
        // Scroll arrows.
        float bob = (Time % 0.6) < 0.3 ? 0 : 1;
        if (_scroll > 0) Gfx.TextCentered(ci, "^", 244, 106 - bob, Gfx.White);
        if (_scroll + Rows < items.Count) Gfx.TextCentered(ci, "v", 244, 166 + bob, Gfx.White);
    }

    private void DrawIcon(CanvasItem ci, ShopItem item, Vector2 at)
    {
        switch (item.Slot)
        {
            case Slot.Phones:
            {
                var (band, cups) = BeepArt.PhoneColors(item.Id, Time);
                var tex = Sprites.Get("phones");
                Sprites.Draw(ci, tex, at, 1, band);
                Gfx.Rect(ci, at.X + 1, at.Y + 5, 3, 4, cups);
                Gfx.Rect(ci, at.X + 8, at.Y + 5, 3, 4, cups);
                return;
            }
            case Slot.Eyes:
            {
                string style = item.Id.Replace("eyes_", "");
                var tex = Sprites.Eye(style);
                var col = BeepArt.EyeColor(item.Id);
                Gfx.Rect(ci, at.X, at.Y + 1, 12, 10, Gfx.Ink);
                Sprites.Draw(ci, tex, at + new Vector2(1, 3), 1, col);
                Sprites.Draw(ci, tex, at + new Vector2(7, 3), 1, col);
                return;
            }
            case Slot.Hat:
                Sprites.DrawFoot(ci, Sprites.Get(item.Id), at + new Vector2(6, 12), 0.85f);
                return;
            case Slot.Trail:
                for (int i = 0; i < 4; i++)
                {
                    var col = item.Id switch
                    {
                        "trail_rainbow" => Gfx.Rainbow(Time, i * 0.2f),
                        "trail_bubble" => new Color("a0e8ff"),
                        _ => new Color("fff6a0"),
                    };
                    Gfx.Rect(ci, at.X + i * 3, at.Y + 8 - i * 2, 2, 2, col);
                }
                return;
        }
        if (Sprites.Has(item.Id)) Sprites.Draw(ci, Sprites.Get(item.Id), at);
    }

    private void DrawDetails(CanvasItem ci)
    {
        var item = Selected;
        var box = new Rect2(2, 179, 252, 44);
        Gfx.Window(ci, box, Gfx.Look.Dark);
        Gfx.Text(ci, item.Name, new Vector2(8, 181), Gfx.Gold);
        if (item.Slot != Slot.None && !P.Owns(item.Id))
            Gfx.TextRight(ci, "Trying it on!", 248, 181, (Time % 0.8) < 0.5 ? Gfx.Cyan : Gfx.White);
        if (item.Leveled)
        {
            float x = 12 + Gfx.Measure(item.Name);
            for (int k = 0; k < item.MaxLevel; k++)
            {
                Gfx.Rect(ci, x + k * 6, 183, 5, 5, Gfx.Ink);
                Gfx.Rect(ci, x + k * 6 + 1, 184, 3, 3, k < P.LevelOf(item.Id) ? Gfx.Green : Gfx.Dim);
            }
        }
        var lines = Wrap(item.Blurb, 240);
        for (int i = 0; i < Math.Min(2, lines.Count); i++) Gfx.Text(ci, lines[i], new Vector2(8, 191 + i * 9), Gfx.White);

        string action;
        if (item.Slot != Slot.None && P.Owns(item.Id)) action = P.Worn(item.Slot) == item.Id ? "{A} Take off" : "{A} Wear";
        else if (P.NextPrice(item) is { } price) action = $"{{A}} Buy for {price}";
        else action = "Maxed out!";
        Gfx.Help(ci, action + "   {B} Leave", 8, 211, Gfx.Grey);
    }

    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var word in text.Split(' '))
        {
            string tryLine = line.Length == 0 ? word : line + " " + word;
            if (Gfx.Measure(tryLine) > width && line.Length > 0)
            {
                lines.Add(line);
                line = word;
            }
            else line = tryLine;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }
}
