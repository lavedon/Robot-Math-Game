using Godot;

namespace MathRobot.Game.Snes;

/// <summary>
/// All the pixel art, drawn as text: one character per pixel, '.' clear, other characters looked up in a palette.
/// Symmetric sprites are written as their left half and mirrored. Textures are built once and cached.
/// </summary>
public static class Sprites
{
    private static readonly Dictionary<string, ImageTexture> Cache = new();

    // The shared 16-bit palette.
    private static readonly Dictionary<char, Color> Base = new()
    {
        ['K'] = new("0b0d1c"), // outline
        ['D'] = new("252a4a"), // Beep's shell
        ['d'] = new("3d4578"), // shell highlight
        ['F'] = new("04050c"), // face screen
        ['G'] = new("7e859e"), // bezel
        ['g'] = new("b8bdd0"), // bezel shine
        ['L'] = new("1c2038"), // legs
        ['P'] = new("6b5bd6"), // feet
        ['p'] = new("9a8cff"),
        ['W'] = new("ffffff"),
        ['w'] = new("d8dcf0"),
        ['E'] = new("ffffff"), // eyes: drawn white, tinted when drawn
        ['e'] = new("b0b0b0"),
        ['H'] = new("9a9a9a"), // phone band: white-ish, tinted
        ['h'] = new("ffffff"),
        ['C'] = new("ffffff"), // ear cups, tinted with the accent
        ['c'] = new("a8a8a8"),
        ['Y'] = new("ffd84a"), // gold
        ['y'] = new("c8901c"),
        ['O'] = new("ff9a3c"), // orange
        ['o'] = new("b85a18"),
        ['R'] = new("ff4a5a"), // red
        ['r'] = new("a02030"),
        ['B'] = new("4a8aff"), // blue
        ['b'] = new("2a4aa8"),
        ['V'] = new("3ce0f0"), // cyan
        ['v'] = new("1a8aa0"),
        ['N'] = new("6cf06a"), // green
        ['n'] = new("2a9a3a"),
        ['M'] = new("b060ff"), // purple
        ['m'] = new("6a2ab0"),
        ['T'] = new("8a5a30"), // brown
        ['t'] = new("5a3418"),
        ['S'] = new("c0c4d8"), // steel
        ['s'] = new("70768e"),
        ['Q'] = new("ff8ad8"), // pink
        ['q'] = new("c04a98"),
        ['Z'] = new("40445e"), // dark steel
    };

    /// <summary>Beep, left half (mirrored). Eyes are left as screen ('F'): they are drawn on top.</summary>
    private static readonly string[] BeepHalf =
    {
        "......KKKKKK",
        ".....Khhhhhh",
        "....KhHKKKKK",
        "...KhHKDdddd",
        "..KKHKDDDDDD",
        ".KCCKDDGgggg",
        "KCccKDGFFFFF",
        "KCcCKDGFFFFF",
        "KCcCKDGFFFFF",
        "KCcCKDGFFFFF",
        "KCcCKDGFFFFF",
        "KCccKDGFFFFF",
        ".KCCKDGFFFFF",
        "..KKKDDGGGGG",
        "....KdDDDDDD",
        ".....KKKKKKK",
        ".......KLLLL",
        "......KLLKKK",
        "......KLLK..",
        "......KLLK..",
        ".....KPpPPK.",
        ".....KKKKKK.",
    };

    /// <summary>Beep's size in pixels.</summary>
    public static readonly Vector2I BeepSize = new(24, 22);

    /// <summary>Where Beep's eyes go (top-left of the left eye; the right eye is 5 pixels further).</summary>
    public static readonly Vector2I EyeAt = new(8, 7);

    public static readonly Dictionary<string, string[]> Eyes = new()
    {
        ["square"] = new[] { "EEE", "EEE", "EEE", "EEE" },
        ["blink"] = new[] { "...", "...", "EEE", "..." },
        ["round"] = new[] { ".E.", "EEE", "EEE", ".E." },
        ["heart"] = new[] { "E.E", "EEE", "EEE", ".E." },
        ["star"] = new[] { ".E.", "EEE", ".E.", "E.E" },
        ["happy"] = new[] { "...", ".E.", "E.E", "..." },
        ["sad"] = new[] { "...", "EEE", ".E.", "..." },
        ["wide"] = new[] { "EEE", "E.E", "E.E", "EEE" },
    };

    // Hats sit with their bottom row on Beep's head top (y = 2), centred on x = 12.
    public static readonly Dictionary<string, string[]> Hats = new()
    {
        ["hat_propeller"] = new[]
        {
            "......KK......",
            ".......K......",
            ".....KRRBK....",
            "....KRRRBBK...",
            "...KRRRRBBBK..",
            "..KKKKKKKKKKK.",
        },
        ["hat_party"] = new[]
        {
            "....W....",
            "...KQK...",
            "...KMK...",
            "..KYQMK..",
            "..KQMYK..",
            ".KMYQMQK.",
            ".KQMYQMK.",
            "KKKKKKKKK",
        },
        ["hat_wizard"] = new[]
        {
            "........KK....",
            ".......KMK....",
            "......KMMK....",
            ".....KMYMK....",
            ".....KMMMMK...",
            "....KMMMYMK...",
            "...KMMYMMMMK..",
            "..KKKKKKKKKKK.",
            ".KmmmmmmmmmmmK",
        },
        ["hat_crown"] = new[]
        {
            ".K...K...K.",
            "KYK.KYK.KYK",
            "KYYKYRYKYYK",
            "KYYYYYYYYYK",
            "KyYByYRyYBK",
            "KKKKKKKKKKK",
        },
    };

    /// <summary>Gizmo, the shopkeeper: a rusty old repair robot with a monocle and a moustache grille.</summary>
    private static readonly string[] Gizmo =
    {
        "..............Y.................",
        ".............YRY................",
        "..............K.................",
        "..............K.................",
        ".......KKKKKKKKKKKKKKKKKK.......",
        "......KOOOOOOOOOOOOOOOOOOK......",
        ".....KOooooooooooooooooooOK.....",
        ".....KOoKKKKKoooooKKKKKKoOK.....",
        "..KKKKOKYYYYYKoooKSSSSSSKOKKKK..",
        "..KssKOKYWVVYKoooKSWVVSSKOKssK..",
        "..KsSKOKYVVVYKoooKSVVVSSKOKSsK..",
        "..KssKOKYVVVYKoooKSVVVSSKOKssK..",
        "..KKKKOKYYYYYKoooKSSSSSSKOKKKK..",
        ".....KOoKKKKKoooooKKYKKKKoOK....",
        ".....KOooooooooooooooYooooOK....",
        ".....KOoSSSSSSSSSSSSSSSSoOK.....",
        ".....KOoSKsKsKsKsKsKsKsSoOK.....",
        ".....KOoSSSSSSSSSSSSSSSSoOK.....",
        ".....KOooooooooooooooooooOK.....",
        "......KKKKKKKKKKKKKKKKKKKK......",
        "...........KZZZZZZZZK...........",
        "......KKKKKKKKKKKKKKKKKKKK......",
        ".....KTTTTTTTTTTTTTTTTTTTTK.....",
        "....KTtttttttttttttttttttTK.....",
        "...KKTtYtttttttVVtttttttYtTKK...",
        "..KSSKtttttttttVVtttttttttKSSK..",
        "..KSsKTTTTTTTTTTTTTTTTTTTTKsSK..",
        "..KSsKtttttttttttttttttttTKsSK..",
        "..KSSKTtttttttttttttttttttKSSK..",
        "..KKKKKKKKKKKKKKKKKKKKKKKKKKKK..",
    };

    /// <summary>The Glitch: the boss, a jagged purple bug-bot.</summary>
    private static readonly string[] GlitchHalf =
    {
        "..K.........",
        "..KK........",
        "...KK...KKKK",
        "....KKKKMMMM",
        "...KMMMMMMMM",
        "..KMMmmmmmmm",
        ".KMMmKKKKmmm",
        ".KMmKRRRRKmm",
        "KMMmKRWRRKmm",
        "KMmmKRRRRKmm",
        "KMmmmKKKKmmm",
        "KMmmmmmmmmmm",
        ".KMmmKKKKKKK",
        ".KMmKYKYKYKY",
        "..KMmKKKKKKK",
        "..KKMmmmmmmm",
        ".KM.KKKmmKKK",
        "KM....KK..KK",
        "K......K....",
    };

    private static readonly Dictionary<string, string[]> Misc = new()
    {
        ["bolt"] = new[]
        {
            "..KKKK..",
            ".KYYYYK.",
            "KYyKKyYK",
            "KYK..KYK",
            "KYK..KYK",
            "KyYKKYyK",
            ".KyyyyK.",
            "..KKKK..",
        },
        ["star"] = new[]
        {
            "....K....",
            "...KYK...",
            "...KYK...",
            "KKKYWYKKK",
            "KYYYYYYYK",
            ".KYYYYYK.",
            ".KYYyYYK.",
            "KYYKKKYYK",
            "KKK...KKK",
        },
        ["star_empty"] = new[]
        {
            "....K....",
            "...KZK...",
            "...KZK...",
            "KKKZZZKKK",
            "KZZZZZZZK",
            ".KZZZZZK.",
            ".KZZZZZK.",
            "KZZKKKZZK",
            "KKK...KKK",
        },
        ["heart"] = new[]
        {
            ".KK.KK.",
            "KRRKRRK",
            "KRWRRRK",
            "KRRRRRK",
            ".KRRRK.",
            "..KRK..",
            "...K...",
        },
        ["flag_start"] = new[]
        {
            "KK.....",
            "KNNK...",
            "KNNNNK.",
            "KNNNNNK",
            "KnnnK..",
            "KnK....",
            "KK.....",
            "KK.....",
            "KK.....",
            "KK.....",
        },
        ["flag_land"] = new[]
        {
            "KK.....",
            "KOOK...",
            "KOOOOK.",
            "KOOOOOK",
            "KoooK..",
            "KoK....",
            "KK.....",
            "KK.....",
            "KK.....",
            "KK.....",
        },
        ["tree"] = new[]
        {
            ".....KKKKK......",
            "...KKNNNNNKK....",
            "..KNNNNNNNNNK...",
            ".KNNWNNNNNNNNK..",
            ".KNNNNNNNnNNNK..",
            "KNNNNNNNNNNnNNK.",
            "KNNNnNNNNNNNNNK.",
            "KnNNNNNNNnNNNnK.",
            ".KnNNNNnNNNNnK..",
            ".KnnNNNNNNNnnK..",
            "..KKnnnnnnnKK...",
            "....KKTTKKK.....",
            "......KTK.......",
            "......KTK.......",
            "......KTK.......",
            ".....KTTtK......",
            "....KKKKKKK.....",
        },
        ["pine"] = new[]
        {
            ".....K......",
            "....KNK.....",
            "...KNNNK....",
            "...KNnNK....",
            "..KNNNNNK...",
            "..KnNNNnK...",
            ".KNNNNNNNK..",
            ".KnNNnNNnK..",
            "KNNNNNNNNNK.",
            "KKnnnnnnnKK.",
            "....KTK.....",
            "....KTK.....",
            "...KKKKK....",
        },
        ["rock"] = new[]
        {
            "...KKKKK...",
            "..KSSSSsK..",
            ".KSWSSSSsK.",
            "KSSSSSSSssK",
            "KsSSSSSsssK",
            ".KKKKKKKKK.",
        },
        ["gear"] = new[]
        {
            "....KK....",
            ".KK.KK.KK.",
            ".KSKSSKSK.",
            "..KSSSSK..",
            "KKSSKKSSKK",
            "KKSSKKSSKK",
            "..KSSSSK..",
            ".KSKSSKSK.",
            ".KK.KK.KK.",
            "....KK....",
        },
        ["lock"] = new[]
        {
            "..KKK..",
            ".KK.KK.",
            ".K...K.",
            "KKKKKKK",
            "KYYYYYK",
            "KYYKYYK",
            "KYYKYYK",
            "KKKKKKK",
        },
        ["garage"] = new[]
        {
            "........KKKKKKKKKKKKKKKK........",
            "......KKRRRRRRRRRRRRRRRRKK......",
            "....KKRRRRRRRRRRRRRRRRRRRRKK....",
            "..KKRRRRrrrrrrrrrrrrrrrrRRRRKK..",
            "KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK",
            ".KWWWWWWWWWWWWWWWWWWWWWWWWWWWWK.",
            ".KWYYKYYKYYKYYKYYKYYKYYKYYKYYWK.",
            ".KWKKKKKKKKKKKKKKKKKKKKKKKKKKWK.",
            ".KWKSSSSSSSSSSSSSSSSSSSSSSSSKWK.",
            ".KWKsssssssssssssssssssssssKKWK.",
            ".KWKSSSSSSSSSSSSSSSSSSSSSSSSKWK.",
            ".KWKsssssssssssssssssssssssKKWK.",
            ".KWKSSSSSSSSSSSSSSSSSSSSSSSSKWK.",
            ".KWKsssssssssssssssssssssssKKWK.",
            ".KWKSSSSSSSSSSSSSSSSSSSSSSSSKWK.",
            ".KWKsssssssssssssssssssssssKKWK.",
            ".KWKSSSSSSSSSSSSSSSSSSSSSSSSKWK.",
            ".KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK.",
        },
        ["pad"] = new[]
        {
            "....KKKKKKKK....",
            "..KKVVVVVVVVKK..",
            ".KVVvvvvvvvvVVK.",
            "KVvvVVVVVVVVvvVK",
            ".KVVvvvvvvvvVVK.",
            "..KKVVVVVVVVKK..",
            "....KKKKKKKK....",
        },
        ["hand"] = new[]
        {
            "..KK.....",
            ".KWWK....",
            ".KWWKKKK.",
            "KKWWKWWKK",
            "KWWWWWWWK",
            "KWWWWWWWK",
            ".KWWWWWK.",
            "..KKKKK..",
        },
        ["cursor"] = new[]
        {
            "KK....",
            "KWK...",
            "KWWK..",
            "KWWWK.",
            "KWWK..",
            "KWK...",
            "KK....",
        },
        // Shop icons, 12×12
        ["treads"] = new[]
        {
            "............",
            "..KKKKKKKK..",
            ".KZZZZZZZZK.",
            "KZKKZKKZKKZK",
            "KZKSKSSKSKZK",
            "KKSsSKKSsSKK",
            "KKSsSKKSsSKK",
            "KZKSKSSKSKZK",
            "KZKKZKKZKKZK",
            ".KZZZZZZZZK.",
            "..KKKKKKKK..",
            "............",
        },
        ["magnet"] = new[]
        {
            "..KKK..KKK..",
            "..KSK..KSK..",
            "..KSK..KSK..",
            "..KRK..KRK..",
            "..KRK..KRK..",
            "..KRK..KRK..",
            "..KRRKKRRK..",
            "..KRRRRRRK..",
            "...KRRRRK...",
            "....KKKK....",
            "Y.........Y.",
            ".Y.......Y..",
        },
        ["clock"] = new[]
        {
            "...KKKKKK...",
            "..KYYYYYYK..",
            ".KYWWWWWWYK.",
            "KYWWWKWWWWYK",
            "KYWWWKWWWWYK",
            "KYWWWKWWWWYK",
            "KYWWWKKKWWYK",
            "KYWWWWWWWWYK",
            "KYWWWWWWWWYK",
            ".KYWWWWWWYK.",
            "..KYYYYYYK..",
            "...KKKKKK...",
        },
        ["combo"] = new[]
        {
            "......KK....",
            ".....KYK....",
            "....KYYK....",
            "...KYYK.....",
            "..KYYYKKKK..",
            "..KYYYYYYK..",
            "..KKKKYYYK..",
            ".....KYYK...",
            "....KYYK....",
            "....KYK.....",
            "....KK......",
            "............",
        },
        ["specs"] = new[]
        {
            "............",
            "............",
            "............",
            "KKKKK..KKKKK",
            "KVVWKKKKVVWK",
            "KVVVK..KVVVK",
            "KVVVK..KVVVK",
            ".KKK....KKK.",
            "............",
            "............",
            "............",
            "............",
        },
        ["lamp"] = new[]
        {
            "...KKKKKK...",
            "..KYYYYYYK..",
            ".KYYWWYYYYK.",
            ".KYWYYYYYYK.",
            ".KYYYYYYYYK.",
            ".KYYYYYYYYK.",
            "..KYYYYYYK..",
            "...KYYYYK...",
            "...KSSSSK...",
            "...KssssK...",
            "...KSSSSK...",
            "....KKKK....",
        },
        ["shield"] = new[]
        {
            ".KKKKKKKKKK.",
            "KBBBBBBBBBBK",
            "KBWBBBBBBBBK",
            "KBBBBYYBBBBK",
            "KBBBBYYBBBBK",
            "KBBYYYYYYBBK",
            "KBBBBYYBBBBK",
            ".KBBBYYBBBK.",
            ".KBBBBBBBBK.",
            "..KBBBBBBK..",
            "...KBBBBK...",
            "....KKKK....",
        },
        ["phones"] = new[]
        {
            "...KKKKKK...",
            "..KhhhhhhK..",
            ".KhKKKKKKhK.",
            ".KhK....KhK.",
            "KKhK....KhKK",
            "KCCK....KCCK",
            "KCcK....KcCK",
            "KCcK....KcCK",
            "KCCK....KCCK",
            ".KK......KK.",
            "............",
            "............",
        },
        ["sparkle"] = new[]
        {
            "...........",
            ".....W.....",
            ".....W.....",
            "....WWW....",
            ".WWWWWWWWW.",
            "....WWW....",
            ".....W.....",
            ".....W.....",
            "...........",
        },
    };

    public static ImageTexture Get(string name)
    {
        if (Cache.TryGetValue(name, out var t)) return t;
        string[] rows = name switch
        {
            "beep" => Mirror(BeepHalf),
            "gizmo" => Gizmo,
            "glitch" => Mirror(GlitchHalf),
            _ when Hats.TryGetValue(name, out var hat) => hat,
            _ when Misc.TryGetValue(name, out var m) => m,
            _ => throw new ArgumentException($"No sprite named {name}"),
        };
        return Cache[name] = Build(rows, Base);
    }

    /// <summary>Beep split into three layers so the headphones can be any colour: shell, band, ear cups.</summary>
    public static ImageTexture BeepLayer(string layer)
    {
        string key = "beep_" + layer;
        if (Cache.TryGetValue(key, out var t)) return t;
        var rows = Mirror(BeepHalf);
        string keep = layer switch { "band" => "Hh", "cups" => "Cc", _ => "" };
        var filtered = rows.Select(r => new string(r.Select(c =>
            layer == "body" ? ("HhCc".Contains(c) ? '.' : c) : (keep.Contains(c) ? c : '.')).ToArray())).ToArray();
        return Cache[key] = Build(filtered, Base);
    }

    public static ImageTexture Eye(string style)
    {
        string key = "eye_" + style;
        if (Cache.TryGetValue(key, out var t)) return t;
        return Cache[key] = Build(Eyes.GetValueOrDefault(style) ?? Eyes["square"], Base);
    }

    public static bool Has(string name) => Hats.ContainsKey(name) || Misc.ContainsKey(name) || name is "beep" or "gizmo" or "glitch";

    private static string[] Mirror(string[] half) =>
        half.Select(r => r + new string(r.Reverse().ToArray())).ToArray();

    private static ImageTexture Build(string[] rows, Dictionary<char, Color> palette)
    {
        int w = rows.Max(r => r.Length), h = rows.Length;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] != '.' && palette.TryGetValue(rows[y][x], out var c)) img.SetPixel(x, y, c);
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Draws a texture with its bottom centre at <paramref name="foot"/>.</summary>
    public static void DrawFoot(CanvasItem ci, Texture2D tex, Vector2 foot, float scale = 1, bool flip = false, Color? tint = null)
    {
        var size = tex.GetSize() * scale;
        var pos = new Vector2(Mathf.Floor(foot.X - size.X / 2), Mathf.Floor(foot.Y - size.Y));
        var rect = new Rect2(pos, size);
        if (flip) rect = new Rect2(pos.X + size.X, pos.Y, -size.X, size.Y);
        ci.DrawTextureRect(tex, rect, false, tint ?? Colors.White);
    }

    /// <summary>Draws a texture with its top-left at <paramref name="at"/>.</summary>
    public static void Draw(CanvasItem ci, Texture2D tex, Vector2 at, float scale = 1, Color? tint = null) =>
        ci.DrawTextureRect(tex, new Rect2(at.Floor(), tex.GetSize() * scale), false, tint ?? Colors.White);
}
