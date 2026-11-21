using Godot;
using MathRobot.Core;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>
/// One screen of the game, drawn on the 256×224 canvas. An optional Mode 7 plane sits at the back; everything else
/// is drawn by <see cref="Draw"/> on a canvas in front of it, every frame.
/// </summary>
public abstract partial class Screen : Node2D
{
    private sealed partial class Canvas : Node2D
    {
        public Action<CanvasItem>? OnDraw;
        public override void _Draw() => OnDraw?.Invoke(this);
    }

    private Canvas _canvas = null!;

    protected Main M => Main.I;
    protected Pad Pad => Main.I.Pad;
    protected Progress P => Main.I.Progress;
    protected Mode7? Plane { get; private set; }
    protected double Time { get; private set; }
    protected readonly Particles Fx = new();

    /// <summary>Adds the Mode 7 plane behind this screen.</summary>
    protected Mode7 AddPlane()
    {
        Plane = new Mode7();
        AddChild(Plane);
        MoveChild(Plane, 0);
        return Plane;
    }

    public override void _Ready()
    {
        _canvas = new Canvas { OnDraw = ci =>
        {
            Paint(ci);
            Fx.Draw(ci);
            PaintTop(ci);
        } };
        AddChild(_canvas);
    }

    public virtual void Enter() { }
    public virtual void Leave() { }

    /// <summary>Runs every frame: animation and input.</summary>
    protected abstract void Update(double dt);

    /// <summary>Draws the screen (under the particles).</summary>
    protected abstract void Paint(CanvasItem ci);

    /// <summary>Draws over the particles: windows that must stay readable.</summary>
    protected virtual void PaintTop(CanvasItem ci) { }

    public override void _Process(double delta)
    {
        Time += delta;
        Update(delta);
        Fx.Update(delta);
        _canvas.QueueRedraw();
    }

    /// <summary>Ease out: fast then gentle.</summary>
    protected static float EaseOut(float t) => 1 - (1 - Math.Clamp(t, 0, 1)) * (1 - Math.Clamp(t, 0, 1));

    protected static float EaseInOut(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>A springy overshoot for things popping in.</summary>
    protected static float Pop(float t)
    {
        t = Math.Clamp(t, 0, 1);
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    /// <summary>
    /// Draws text whose letters ride a sine wave, like the SNES's line-by-line (HDMA) title effects.
    /// </summary>
    protected static void WavyText(CanvasItem ci, string text, float centerX, float y, Color color, int scale, double time, float amp = 2, Color? outline = null)
    {
        float x = centerX - Gfx.Measure(text, scale) / 2f;
        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            float dy = MathF.Round(amp * MathF.Sin((float)time * 5 + i * 0.6f));
            if (outline is { } o)
            {
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                        if (ox != 0 || oy != 0) Gfx.Text(ci, ch, new Vector2(x + ox * scale / 2f * 2, y + dy + oy * scale / 2f * 2), o, scale, shadow: false);
            }
            Gfx.Text(ci, ch, new Vector2(x, y + dy), color, scale);
            x += (Gfx.Measure(ch, 1) + 1) * scale;
        }
    }
}

/// <summary>Little sprites that fly and fade: sparkles, bolts, confetti, bubbles, number pops.</summary>
public sealed class Particles
{
    public enum Kind { Dot, Sparkle, Bolt, Text, Bubble, Star }

    private sealed class Bit
    {
        public Kind Kind;
        public Vector2 Pos, Vel;
        public Color Color;
        public double Age, Life;
        public float Gravity;
        public string Text = "";
        public int Size = 1;
    }

    private readonly List<Bit> _bits = new();
    private static readonly Random Random = new();

    public void Add(Kind kind, Vector2 pos, Vector2 vel, Color color, double life, float gravity = 0, string text = "", int size = 1) =>
        _bits.Add(new Bit { Kind = kind, Pos = pos, Vel = vel, Color = color, Life = life, Gravity = gravity, Text = text, Size = size });

    /// <summary>A burst of <paramref name="count"/> bits flying out from a point.</summary>
    public void Burst(Kind kind, Vector2 at, int count, Color color, float speed = 60, float gravity = 120, double life = 0.7)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (float)(Random.NextDouble() * Mathf.Tau);
            float s = speed * (0.4f + (float)Random.NextDouble() * 0.8f);
            Add(kind, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s + new Vector2(0, -speed * 0.4f), color,
                life * (0.7 + Random.NextDouble() * 0.6), gravity);
        }
    }

    /// <summary>Confetti in every colour.</summary>
    public void Confetti(Vector2 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (float)(Random.NextDouble() * Mathf.Tau);
            float s = 40 + (float)Random.NextDouble() * 80;
            Add(Kind.Dot, at, new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s - 60), Gfx.Rainbow(Random.NextDouble() * 2),
                1.2 + Random.NextDouble() * 0.6, 140, size: 2);
        }
    }

    /// <summary>A floating label that rises and fades ("+2", "12").</summary>
    public void Pop(string text, Vector2 at, Color color, double life = 0.9) =>
        Add(Kind.Text, at, new Vector2(0, -24), color, life, text: text);

    public void Update(double dt)
    {
        for (int i = _bits.Count - 1; i >= 0; i--)
        {
            var b = _bits[i];
            b.Age += dt;
            if (b.Age >= b.Life)
            {
                _bits.RemoveAt(i);
                continue;
            }
            b.Vel += new Vector2(0, b.Gravity * (float)dt);
            b.Pos += b.Vel * (float)dt;
            if (b.Kind == Kind.Text) b.Vel *= 0.94f;
        }
    }

    public void Draw(CanvasItem ci)
    {
        foreach (var b in _bits)
        {
            float t = (float)(b.Age / b.Life);
            var c = b.Color;
            c.A = t > 0.7f ? (1 - t) / 0.3f : 1;
            switch (b.Kind)
            {
                case Kind.Dot:
                    Gfx.Rect(ci, b.Pos.X, b.Pos.Y, b.Size, b.Size, c);
                    break;
                case Kind.Sparkle:
                {
                    int r = t < 0.5f ? 2 : 1;
                    Gfx.Rect(ci, b.Pos.X - r, b.Pos.Y, 2 * r + 1, 1, c);
                    Gfx.Rect(ci, b.Pos.X, b.Pos.Y - r, 1, 2 * r + 1, c);
                    break;
                }
                case Kind.Star:
                    Sprites.DrawFoot(ci, Sprites.Get("sparkle"), b.Pos, 1, tint: c);
                    break;
                case Kind.Bolt:
                    Sprites.DrawFoot(ci, Sprites.Get("bolt"), b.Pos, 1, tint: c);
                    break;
                case Kind.Bubble:
                {
                    int r = 2 + (int)(t * 2);
                    Gfx.Rect(ci, b.Pos.X - r, b.Pos.Y - r + 1, 1, 2 * r - 1, c);
                    Gfx.Rect(ci, b.Pos.X + r, b.Pos.Y - r + 1, 1, 2 * r - 1, c);
                    Gfx.Rect(ci, b.Pos.X - r + 1, b.Pos.Y - r, 2 * r - 1, 1, c);
                    Gfx.Rect(ci, b.Pos.X - r + 1, b.Pos.Y + r, 2 * r - 1, 1, c);
                    Gfx.Rect(ci, b.Pos.X - r + 2, b.Pos.Y - r + 1, 1, 1, Colors.White);
                    break;
                }
                case Kind.Text:
                    Gfx.TextCentered(ci, b.Text, b.Pos.X, b.Pos.Y, c);
                    break;
            }
        }
    }

    /// <summary>The trail Beep leaves while hopping, from the Style shelf.</summary>
    public void Trail(string? trail, Vector2 at, double time)
    {
        switch (trail)
        {
            case "trail_sparkle":
                Add(Kind.Sparkle, at + new Vector2(Random.Next(-6, 7), Random.Next(-14, 0)), new Vector2(0, 8), new Color("fff6a0"), 0.5);
                break;
            case "trail_bubble":
                if (Random.NextDouble() < 0.5) Add(Kind.Bubble, at + new Vector2(Random.Next(-6, 7), Random.Next(-12, 0)), new Vector2(0, -16), new Color("a0e8ff"), 0.8);
                break;
            case "trail_rainbow":
                for (int i = 0; i < 3; i++)
                    Add(Kind.Dot, at + new Vector2(0, -6 - i * 2), Vector2.Zero, Gfx.Rainbow(time, i * 0.15f), 0.45, size: 2);
                break;
        }
    }
}
