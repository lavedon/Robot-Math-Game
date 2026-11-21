using System.Reflection;

namespace MathRobot.Core;

/// <summary>
/// The 16-bit menu font from Art/font.txt (borrowed from Kegel Quest): every glyph is <see cref="Height"/> rows,
/// '#' ink and '.' empty. Engine-free so it can be tested; the game bakes it into a texture atlas.
/// </summary>
public sealed class PixelFont
{
    public sealed record Glyph(char Char, int Width, bool[] Ink);

    private readonly Dictionary<char, Glyph> _glyphs;

    private PixelFont(int height, int ascent, int spacing, Dictionary<char, Glyph> glyphs)
    {
        Height = height;
        Ascent = ascent;
        Spacing = spacing;
        _glyphs = glyphs;
    }

    public int Height { get; }
    public int Ascent { get; }
    public int Spacing { get; }
    public IEnumerable<Glyph> Glyphs => _glyphs.Values;

    public Glyph? GlyphFor(char c) =>
        _glyphs.TryGetValue(c, out var g) ? g : c == '−' ? _glyphs.GetValueOrDefault('-') : _glyphs.GetValueOrDefault('?');

    /// <summary>Width of a line of text in pixels (no spacing after the last glyph).</summary>
    public int Measure(string text)
    {
        int w = 0, n = 0;
        foreach (char c in text)
        {
            if (GlyphFor(c) is not { } g) continue;
            w += g.Width;
            n++;
        }
        return n == 0 ? 0 : w + Spacing * (n - 1);
    }

    private static PixelFont? _default;

    /// <summary>The game font, loaded once from the embedded resource.</summary>
    public static PixelFont Default => _default ??= Load();

    private static PixelFont Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Art/font.txt")
            ?? throw new InvalidOperationException("Art/font.txt is not embedded");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static PixelFont Parse(string text)
    {
        int height = 10, ascent = 8, spacing = 1;
        var glyphs = new Dictionary<char, Glyph>();
        var lines = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("glyph "))
            {
                string name = line[6..].Trim();
                char c = name == "space" ? ' ' : name[0];
                var rows = new List<string>();
                while (++i < lines.Length && lines[i].Trim() != "end") rows.Add(lines[i].TrimEnd());
                int width = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
                var ink = new bool[width * height];
                for (int y = 0; y < Math.Min(height, rows.Count); y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        ink[y * width + x] = rows[y][x] == '#';
                glyphs[c] = new Glyph(c, width, ink);
                continue;
            }
            string bare = line.Split('#')[0].Trim();
            var parts = bare.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int v)) continue;
            switch (parts[0])
            {
                case "height": height = v; break;
                case "ascent": ascent = v; break;
                case "spacing": spacing = v; break;
            }
        }
        return new PixelFont(height, ascent, spacing, glyphs);
    }
}
