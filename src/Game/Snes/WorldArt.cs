using Godot;

namespace MathRobot.Game.Snes;

/// <summary>
/// The big Mode 7 textures, painted by code at start-up: Gear Island with its winding path, the sea tile, the sky
/// panoramas and the circuit floor under the number line.
/// </summary>
public static class WorldArt
{
    public const int MapSize = 512;

    /// <summary>The World Map's stops in path order: Stages 1-3, the Garage, Stages 4-10.</summary>
    public static readonly Vector2[] Stops =
    {
        new(250, 452), new(168, 424), new(108, 362), new(150, 296), new(232, 318), new(322, 352),
        new(394, 300), new(382, 218), new(300, 186), new(206, 170), new(268, 96),
    };

    /// <summary>Which stop is the Garage.</summary>
    public const int ShopStop = 3;

    /// <summary>The stage at a stop, or 0 for the Garage.</summary>
    public static int StageAt(int stop) => stop == ShopStop ? 0 : stop < ShopStop ? stop + 1 : stop;

    public static int StopOf(int stage) => stage <= ShopStop ? stage - 1 : stage;

    private static readonly Vector2 Center = new(252, 278);

    private static Image? _mapImage;
    private static ImageTexture? _map, _sea, _skyDay, _skyTech, _floor, _skyDusk;

    public static ImageTexture Map => _map ??= ImageTexture.CreateFromImage(_mapImage ??= PaintIsland());
    public static ImageTexture Sea => _sea ??= ImageTexture.CreateFromImage(PaintSea());
    public static ImageTexture SkyDay => _skyDay ??= ImageTexture.CreateFromImage(PaintSky(new Color("6a78c8"), new Color("8a96e0"), clouds: true, city: false));
    public static ImageTexture SkyDusk => _skyDusk ??= ImageTexture.CreateFromImage(PaintSky(new Color("5a2a78"), new Color("8a3a8a"), clouds: true, city: false));
    public static ImageTexture SkyTech => _skyTech ??= ImageTexture.CreateFromImage(PaintSky(new Color("1a1a4a"), new Color("2a2a6a"), clouds: false, city: true));
    public static ImageTexture Floor => _floor ??= ImageTexture.CreateFromImage(PaintFloor());

    // ---- tiny deterministic noise ----

    private static float Hash(int x, int y, int seed = 0)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    private static float Smooth(float x, float y, int seed)
    {
        int ix = (int)Mathf.Floor(x), iy = (int)Mathf.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float x, float y, int seed) =>
        Smooth(x, y, seed) * 0.6f + Smooth(x * 2.1f, y * 2.1f, seed + 1) * 0.3f + Smooth(x * 4.3f, y * 4.3f, seed + 2) * 0.1f;

    /// <summary>Distance from p to the path between the stops.</summary>
    public static float PathDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Stops.Length; i++)
            best = Math.Min(best, SegmentDistance(p, Stops[i], Stops[i + 1]));
        return best;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>How far inside the island a point is (negative: in the sea).</summary>
    public static float Inland(Vector2 p)
    {
        var d = p - Center;
        float angle = Mathf.Atan2(d.Y, d.X);
        float radius = 212 + 22 * (Fbm(Mathf.Cos(angle) * 2 + 5, Mathf.Sin(angle) * 2 + 5, 7) - 0.5f) * 2
                       + 10 * Mathf.Sin(angle * 3 + 1);
        return radius - d.Length();
    }

    // Areas of the island, so each part of the path has its own look.
    private static readonly Vector2 Pond = new(300, 268);
    private static readonly Vector2 Peak = new(220, 176);
    private static readonly Vector2 Core = new(268, 92);

    public static bool InCircuit(Vector2 p) => p.DistanceTo(Core) < 62;
    public static bool InRocks(Vector2 p) => p.DistanceTo(Peak) < 58 && !InCircuit(p);

    private static Image PaintIsland()
    {
        int n = MapSize;
        var data = new byte[n * n * 4];
        var grassA = new Color("3a9a3a");
        var grassB = new Color("47ab43");
        var sand = new Color("e8d48a");
        var sandDark = new Color("d0b870");
        var shallow = new Color("48b0e0");
        var dirt = new Color("c88a48");
        var dirtEdge = new Color("8a5a2a");
        var dirtLine = new Color("f0c070");
        var rockA = new Color("7a7e90");
        var rockB = new Color("8a8ea0");
        var plate = new Color("1a3a2a");
        var trace = new Color("3af08a");
        var pond = new Color("2a78d0");
        var pondLight = new Color("5aa8f0");
        var flowerColors = new[] { new Color("ff6a8a"), new Color("ffe060"), new Color("ffffff"), new Color("a07aff") };

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float inland = Inland(p);
                Color c;
                if (inland < -7) c = new Color(0, 0, 0, 0); // open sea: drawn by the shader
                else if (inland < 0) c = shallow.Lerp(new Color("80d8ff"), Hash(x / 3, y / 3, 4) * 0.4f);
                else if (inland < 9) c = ((x / 4 + y / 4) % 2 == 0) ? sand : sandDark.Lerp(sand, 0.5f);
                else
                {
                    bool check = ((x >> 4) + (y >> 4)) % 2 == 0;
                    c = check ? grassA : grassB;
                    float noise = Fbm(x / 24f, y / 24f, 3);
                    c = c.Lerp(new Color("2a7a30"), Mathf.Clamp((noise - 0.55f) * 2.5f, 0, 0.6f));
                    if (Hash(x, y, 9) > 0.992f) c = flowerColors[(int)(Hash(x, y, 10) * 4) % 4];

                    if (InRocks(p))
                    {
                        float r = p.DistanceTo(Peak);
                        c = (((x >> 3) + (y >> 3)) % 2 == 0 ? rockA : rockB).Lerp(new Color("5a5e70"), Fbm(x / 10f, y / 10f, 12) * 0.6f);
                        if (r > 52) c = c.Lerp(grassA, 0.5f);
                    }
                    if (InCircuit(p))
                    {
                        // A circuit board: dark plate with glowing traces on a grid.
                        c = plate;
                        bool hTrace = y % 16 == 8 && Hash(x / 16, y / 16, 20) > 0.35f;
                        bool vTrace = x % 16 == 8 && Hash(x / 16, y / 16, 21) > 0.35f;
                        if (hTrace || vTrace) c = trace;
                        if (x % 16 is >= 6 and <= 10 && y % 16 is >= 6 and <= 10 && Hash(x / 16, y / 16, 22) > 0.6f)
                            c = new Color("d0f0e0");
                        if (p.DistanceTo(Core) > 58) c = new Color("8a8ea0");
                    }
                    float pd = p.DistanceTo(Pond);
                    if (pd < 30 + 4 * Mathf.Sin(Mathf.Atan2(y - Pond.Y, x - Pond.X) * 3))
                        c = pd < 22 ? (((x + y) / 5) % 7 == 0 ? pondLight : pond) : sand;
                }

                // The path: a dirt road with a dashed centre line, and the stop pads.
                if (inland > 0)
                {
                    float d = PathDistance(p);
                    if (d < 7) c = d > 5.5f ? dirtEdge : dirt.Lerp(new Color("b07838"), Hash(x / 2, y / 2, 5) * 0.3f);
                }
                foreach (var stop in Stops)
                {
                    float sd = p.DistanceTo(stop);
                    if (sd < 13) c = sd > 11 ? dirtEdge : sd > 9 ? new Color("f0f0ff") : new Color("4a5aa8");
                }

                int i = (y * n + x) * 4;
                data[i] = (byte)(c.R * 255);
                data[i + 1] = (byte)(c.G * 255);
                data[i + 2] = (byte)(c.B * 255);
                data[i + 3] = (byte)(c.A * 255);
            }
        }
        // Dashes along the path.
        for (int s = 0; s + 1 < Stops.Length; s++)
        {
            var a = Stops[s];
            var b = Stops[s + 1];
            float len = a.DistanceTo(b);
            for (float t = 14; t < len - 14; t += 1)
            {
                if ((int)(t / 5) % 2 == 1) continue;
                var q = a + (b - a) * (t / len);
                int i = ((int)q.Y * n + (int)q.X) * 4;
                data[i] = 240;
                data[i + 1] = 200;
                data[i + 2] = 120;
            }
        }
        return Image.CreateFromData(n, n, false, Image.Format.Rgba8, data);
    }

    private static Image PaintSea()
    {
        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        var deep = new Color("1e5ab8");
        var mid = new Color("2a6cc8");
        var light = new Color("7ac0ff");
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                var c = ((x / 8 + y / 8) % 2 == 0) ? deep : mid;
                // Little wave crests.
                int wy = (y + (int)(2 * Mathf.Sin(x * 0.4f))) % 16;
                if (wy == 0 && x % 16 < 6) c = light;
                img.SetPixel(x, y, c);
            }
        return img;
    }

    /// <summary>A 512×64 panorama with transparent sky: hills, mountains (or a city of circuits) and clouds.</summary>
    private static Image PaintSky(Color far, Color near, bool clouds, bool city)
    {
        const int w = 512, h = 64;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int x = 0; x < w; x++)
        {
            float a = x / (float)w * Mathf.Tau;
            if (city)
            {
                // Towers of a server city, with windows.
                int bw = 6 + (int)(Hash(x / 12, 0, 40) * 10);
                int top = 18 + (int)(Hash(x / bw, 1, 41) * 30);
                for (int y = top; y < h; y++)
                {
                    var c = ((x % bw) == 0) ? far.Darkened(0.4f) : far;
                    if (y > top + 2 && x % 4 == 1 && y % 5 == 0 && Hash(x, y, 42) > 0.5f) c = new Color("ffe080");
                    img.SetPixel(x, y, c);
                }
                int front = 44 + (int)(Hash(x / 9, 2, 43) * 12);
                for (int y = front; y < h; y++)
                {
                    var c = near;
                    if (y > front + 1 && x % 3 == 0 && y % 4 == 2 && Hash(x, y, 44) > 0.6f) c = new Color("5ef2ff");
                    img.SetPixel(x, y, c);
                }
                continue;
            }
            // Wrapping mountains: sums of sines with whole numbers of cycles so the seam is invisible.
            float m = 30 + 10 * Mathf.Sin(a * 3) + 6 * Mathf.Sin(a * 7 + 1) + 3 * Mathf.Sin(a * 17 + 2);
            int mTop = (int)(h - m);
            for (int y = Math.Max(0, mTop); y < h; y++)
            {
                var c = far;
                if (y < mTop + 4 && m > 38) c = new Color("e8f0ff"); // snow caps on the tallest
                img.SetPixel(x, y, c);
            }
            float hill = 14 + 5 * Mathf.Sin(a * 5 + 3) + 3 * Mathf.Sin(a * 11);
            for (int y = (int)(h - hill); y < h; y++) img.SetPixel(x, y, near);
        }
        if (clouds)
        {
            for (int k = 0; k < 9; k++)
            {
                int cx = (int)(Hash(k, 0, 50) * w), cy = 6 + (int)(Hash(k, 1, 50) * 18);
                int len = 18 + (int)(Hash(k, 2, 50) * 26);
                for (int dx = -len; dx <= len; dx++)
                {
                    int thick = (int)(4 * Mathf.Sqrt(1 - (dx / (float)len) * (dx / (float)len))) + 1;
                    for (int dy = -thick; dy <= thick / 2; dy++)
                    {
                        int x = ((cx + dx) % w + w) % w;
                        int y = cy + dy;
                        if (y < 0 || y >= h) continue;
                        if (img.GetPixel(x, y).A > 0) continue;
                        img.SetPixel(x, y, dy > 0 ? new Color("c8d0f0") : new Color("ffffff"));
                    }
                }
            }
        }
        return img;
    }

    /// <summary>The floor under the number line: a 64×64 circuit tile in purples and blues.</summary>
    private static Image PaintFloor()
    {
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var a = new Color("1e2050");
        var b = new Color("262a62");
        var line = new Color("4a54b0");
        var glow = new Color("5ef2ff");
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var c = ((x / 32 + y / 32) % 2 == 0) ? a : b;
                if (x % 32 == 0 || y % 32 == 0) c = line;
                if ((x == 16 && y > 8 && y < 24) || (y == 16 && x > 8 && x < 24)) c = line.Lightened(0.15f);
                if (x is >= 14 and <= 18 && y is >= 14 and <= 18) c = (x is 16 && y is 16) ? glow : line.Lightened(0.3f);
                if ((x == 48 && y > 36 && y < 60) || (y == 52 && x > 40 && x < 56)) c = new Color("3a8a6a");
                img.SetPixel(x, y, c);
            }
        return img;
    }
}
