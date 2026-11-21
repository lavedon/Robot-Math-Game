using Godot;
using MathRobot.Core;

namespace MathRobot.Game.Snes;

/// <summary>
/// Drawing on the 256×224 SNES canvas: the pixel font, a tiny 3×5 digit font for number-line labels, the blue
/// gradient RPG windows, and button badges. Everything lands on whole pixels.
/// </summary>
public static class Gfx
{
    public const int W = 256, H = 224;

    public static readonly Color White = new(1, 1, 1);
    public static readonly Color Ink = new("10122a");
    public static readonly Color Shadow = new(0.02f, 0.03f, 0.1f, 0.85f);
    public static readonly Color Gold = new("ffd84a");
    public static readonly Color Cyan = new("5ef2ff");
    public static readonly Color Green = new("6cf06a");
    public static readonly Color Orange = new("ff9a3c");
    public static readonly Color Red = new("ff5a6a");
    public static readonly Color Pink = new("ff8ad8");
    public static readonly Color Grey = new("9aa0c0");
    public static readonly Color Dim = new("5a6088");

    private static ImageTexture _font = null!;
    private static readonly Dictionary<char, (Rect2 Src, int Width)> Glyphs = new();
    private static PixelFont _pf = null!;
    private static ImageTexture _tiny = null!;
    private static ImageTexture _pixel = null!;

    // 3×5 digits for the number-line labels, so two-digit numbers fit between ticks 11 pixels apart.
    private static readonly string[] TinyDigits =
    {
        "###,#.#,#.#,#.#,###", ".#.,##.,.#.,.#.,###", "###,..#,###,#..,###", "###,..#,.##,..#,###", "#.#,#.#,###,..#,..#",
        "###,#..,###,..#,###", "###,#..,###,#.#,###", "###,..#,.#.,.#.,.#.", "###,#.#,###,#.#,###", "###,#.#,###,..#,###",
        "...,...,###,...,...", // '-' at index 10
    };

    public static void Init()
    {
        _pf = PixelFont.Default;
        var glyphs = _pf.Glyphs.ToList();
        int width = glyphs.Sum(g => g.Width + 1);
        var img = Image.CreateEmpty(width, _pf.Height, false, Image.Format.Rgba8);
        int x = 0;
        foreach (var g in glyphs)
        {
            for (int gy = 0; gy < _pf.Height; gy++)
                for (int gx = 0; gx < g.Width; gx++)
                    if (g.Ink[gy * g.Width + gx]) img.SetPixel(x + gx, gy, Colors.White);
            Glyphs[g.Char] = (new Rect2(x, 0, g.Width, _pf.Height), g.Width);
            x += g.Width + 1;
        }
        _font = ImageTexture.CreateFromImage(img);

        var tiny = Image.CreateEmpty(4 * TinyDigits.Length, 5, false, Image.Format.Rgba8);
        for (int d = 0; d < TinyDigits.Length; d++)
        {
            var rows = TinyDigits[d].Split(',');
            for (int ry = 0; ry < 5; ry++)
                for (int rx = 0; rx < 3; rx++)
                    if (rows[ry][rx] == '#') tiny.SetPixel(d * 4 + rx, ry, Colors.White);
        }
        _tiny = ImageTexture.CreateFromImage(tiny);

        var px = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        px.SetPixel(0, 0, Colors.White);
        _pixel = ImageTexture.CreateFromImage(px);
    }

    public static int LineHeight => _pf.Height;

    /// <summary>Width of the widest line of <paramref name="text"/> in pixels at <paramref name="scale"/>.</summary>
    public static int Measure(string text, int scale = 1) =>
        text.Split('\n').Max(line => _pf.Measure(line)) * scale;

    /// <summary>Draws text with its top-left at <paramref name="pos"/>, with a drop shadow by default.</summary>
    public static void Text(CanvasItem ci, string text, Vector2 pos, Color color, int scale = 1, bool shadow = true)
    {
        pos = pos.Floor();
        if (shadow) Glyphs_(ci, text, pos + new Vector2(scale, scale), Shadow, scale);
        Glyphs_(ci, text, pos, color, scale);
    }

    /// <summary>Draws text centred on <paramref name="centerX"/>, each line centred on its own.</summary>
    public static void TextCentered(CanvasItem ci, string text, float centerX, float y, Color color, int scale = 1, bool shadow = true)
    {
        foreach (var line in text.Split('\n'))
        {
            Text(ci, line, new Vector2(Mathf.Floor(centerX - _pf.Measure(line) * scale / 2f), y), color, scale, shadow);
            y += _pf.Height * scale;
        }
    }

    public static void TextRight(CanvasItem ci, string text, float rightX, float y, Color color, int scale = 1, bool shadow = true) =>
        Text(ci, text, new Vector2(rightX - Measure(text, scale), y), color, scale, shadow);

    private static void Glyphs_(CanvasItem ci, string text, Vector2 pos, Color color, int scale)
    {
        float x = pos.X, y = pos.Y;
        foreach (char raw in text)
        {
            if (raw == '\n')
            {
                x = pos.X;
                y += _pf.Height * scale;
                continue;
            }
            char c = raw == '−' ? '-' : raw;
            if (!Glyphs.TryGetValue(c, out var g) && !Glyphs.TryGetValue('?', out g)) continue;
            ci.DrawTextureRectRegion(_font, new Rect2(x, y, g.Width * scale, _pf.Height * scale), g.Src, color);
            x += (g.Width + _pf.Spacing) * scale;
        }
    }

    /// <summary>Width of a number in the tiny font.</summary>
    public static int TinyWidth(string digits) => digits.Length * 4 - 1;

    /// <summary>Draws digits (and '-') in the 3×5 font, centred on <paramref name="centerX"/>.</summary>
    public static void Tiny(CanvasItem ci, string digits, float centerX, float y, Color color, int scale = 1)
    {
        float x = Mathf.Floor(centerX - TinyWidth(digits) * scale / 2f);
        foreach (char c in digits)
        {
            int i = c == '-' ? 10 : c - '0';
            if (i is >= 0 and <= 10)
                ci.DrawTextureRectRegion(_tiny, new Rect2(x, Mathf.Floor(y), 3 * scale, 5 * scale), new Rect2(i * 4, 0, 3, 5), color);
            x += 4 * scale;
        }
    }

    /// <summary>A solid rectangle on whole pixels.</summary>
    public static void Rect(CanvasItem ci, float x, float y, float w, float h, Color c) =>
        ci.DrawTextureRect(_pixel, new Rect2(Mathf.Floor(x), Mathf.Floor(y), Mathf.Floor(w), Mathf.Floor(h)), false, c);

    public static void Rect(CanvasItem ci, Rect2 r, Color c) => Rect(ci, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, c);

    /// <summary>A vertical gradient rectangle.</summary>
    public static void Gradient(CanvasItem ci, Rect2 r, Color top, Color bottom)
    {
        var p = r.Position.Floor();
        var s = r.Size.Floor();
        ci.DrawPolygon(new[] { p, p + new Vector2(s.X, 0), p + s, p + new Vector2(0, s.Y) }, new[] { top, top, bottom, bottom });
    }

    public enum Look { Blue, Gold, Dark, Green, Red }

    /// <summary>A 16-bit RPG menu window: rounded white frame, dark inner line, gradient fill.</summary>
    public static void Window(CanvasItem ci, Rect2 r, Look look = Look.Blue, float alpha = 1)
    {
        var (top, bottom, frame) = look switch
        {
            Look.Gold => (new Color("6a4a10"), new Color("2a1a04"), new Color("ffe08a")),
            Look.Dark => (new Color("1a1c30"), new Color("08080f"), new Color("8a90b8")),
            Look.Green => (new Color("1e6a3a"), new Color("0a2a16"), new Color("c8ffd0")),
            Look.Red => (new Color("7a2030"), new Color("2a0810"), new Color("ffc8d0")),
            _ => (new Color("3a58c8"), new Color("0c1450"), new Color("f0f0ff")),
        };
        top.A = bottom.A = alpha;
        float x = Mathf.Floor(r.Position.X), y = Mathf.Floor(r.Position.Y), w = Mathf.Floor(r.Size.X), h = Mathf.Floor(r.Size.Y);
        Gradient(ci, new Rect2(x + 2, y + 2, w - 4, h - 4), top, bottom);
        var edge = new Color(0.02f, 0.02f, 0.06f, alpha);
        // Outer dark line, then the bright frame with clipped corners, then a dim inner line.
        Rect(ci, x + 2, y, w - 4, 1, edge);
        Rect(ci, x + 2, y + h - 1, w - 4, 1, edge);
        Rect(ci, x, y + 2, 1, h - 4, edge);
        Rect(ci, x + w - 1, y + 2, 1, h - 4, edge);
        Rect(ci, x + 1, y + 1, 1, 1, edge);
        Rect(ci, x + w - 2, y + 1, 1, 1, edge);
        Rect(ci, x + 1, y + h - 2, 1, 1, edge);
        Rect(ci, x + w - 2, y + h - 2, 1, 1, edge);
        Rect(ci, x + 2, y + 1, w - 4, 1, frame);
        Rect(ci, x + 2, y + h - 2, w - 4, 1, frame.Darkened(0.25f));
        Rect(ci, x + 1, y + 2, 1, h - 4, frame);
        Rect(ci, x + w - 2, y + 2, 1, h - 4, frame.Darkened(0.25f));
        var inner = frame.Darkened(0.55f);
        inner.A = 0.6f * alpha;
        Rect(ci, x + 2, y + 2, w - 4, 1, inner);
        Rect(ci, x + 2, y + 2, 1, h - 4, inner);
    }

    /// <summary>
    /// A controller button badge (A green, B red, X blue, Y yellow, LB/RB grey, arrows) with its left edge at x.
    /// Returns its width. Shows keyboard keys when no controller is plugged in.
    /// </summary>
    public static int Button(CanvasItem ci, Btn b, float x, float y)
    {
        bool pad = Pad.ControllerConnected;
        string label = b switch
        {
            Btn.A => pad ? "A" : "Spc",
            Btn.B => pad ? "B" : "X",
            Btn.X => pad ? "X" : "C",
            Btn.Y => pad ? "Y" : "H",
            Btn.LB => pad ? "LB" : "Q",
            Btn.RB => pad ? "RB" : "E",
            Btn.Start => pad ? "Start" : "Enter",
            Btn.Select => pad ? "View" : "Tab",
            Btn.Left => "<",
            Btn.Right => ">",
            Btn.Up => "^",
            Btn.Down => "v",
            _ => "?",
        };
        var color = b switch
        {
            Btn.A => new Color("3cc85a"),
            Btn.B => new Color("e04050"),
            Btn.X => new Color("3c7ae8"),
            Btn.Y => new Color("e8c030"),
            Btn.Left or Btn.Right or Btn.Up or Btn.Down => new Color("4a5070"),
            _ => new Color("6a7090"),
        };
        if (!pad && b is Btn.A or Btn.B or Btn.X or Btn.Y) color = color.Darkened(0.15f);
        int w = Math.Max(9, _pf.Measure(label) + 5);
        Rect(ci, x + 1, y, w - 2, 11, color.Darkened(0.5f));
        Rect(ci, x, y + 1, w, 9, color.Darkened(0.5f));
        Rect(ci, x + 1, y + 1, w - 2, 9, color);
        Rect(ci, x + 2, y + 1, w - 4, 1, color.Lightened(0.4f));
        Glyphs_(ci, label, new Vector2(x + Mathf.Floor((w - _pf.Measure(label)) / 2f), y), White, 1);
        return w;
    }

    /// <summary>
    /// A line of help: text with button badges. Write "{A}" or "{LB}" for a badge. Returns the width.
    /// </summary>
    public static float Help(CanvasItem ci, string text, float x, float y, Color color, bool draw = true)
    {
        float start = x;
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('{', i);
            string chunk = open < 0 ? text[i..] : text[i..open];
            if (chunk.Length > 0)
            {
                if (draw) Text(ci, chunk, new Vector2(x, y), color);
                x += _pf.Measure(chunk) + _pf.Spacing;
            }
            if (open < 0) break;
            int close = text.IndexOf('}', open);
            var b = Enum.Parse<Btn>(text[(open + 1)..close]);
            x += draw ? Button(ci, b, x, y - 1) : Math.Max(9, _pf.Measure(ButtonLabel(b)) + 5);
            x += 2;
            i = close + 1;
        }
        return x - start;
    }

    private static string ButtonLabel(Btn b)
    {
        bool pad = Pad.ControllerConnected;
        return b switch
        {
            Btn.A => pad ? "A" : "Spc", Btn.B => pad ? "B" : "X", Btn.X => pad ? "X" : "C", Btn.Y => pad ? "Y" : "H",
            Btn.LB => pad ? "LB" : "Q", Btn.RB => pad ? "RB" : "E", Btn.Start => pad ? "Start" : "Enter",
            Btn.Select => pad ? "View" : "Tab", _ => "<",
        };
    }

    public static float HelpWidth(string text) => Help(null!, text, 0, 0, White, draw: false);

    /// <summary>Hue-cycling colour for rainbow things.</summary>
    public static Color Rainbow(double t, float offset = 0) => Color.FromHsv((float)((t * 0.5 + offset) % 1.0), 0.65f, 1f);
}
