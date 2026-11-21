using Godot;
using MathRobot.Core;

namespace MathRobot.Game.Snes;

/// <summary>What Beep is wearing: ids from the shop's Style shelf, or null for the classic look.</summary>
public sealed record Outfit(string? Phones = null, string? Eyes = null, string? Hat = null, string? Trail = null)
{
    public static Outfit Of(Progress p) => new(p.Worn(Slot.Phones), p.Worn(Slot.Eyes), p.Worn(Slot.Hat), p.Worn(Slot.Trail));

    /// <summary>This outfit with one item swapped in (the shop's try-on).</summary>
    public Outfit With(ShopItem item) => item.Slot switch
    {
        Slot.Phones => this with { Phones = item.Id },
        Slot.Eyes => this with { Eyes = item.Id },
        Slot.Hat => this with { Hat = item.Id },
        Slot.Trail => this with { Trail = item.Id },
        _ => this,
    };
}

/// <summary>Draws Beep, the hero: a little desk robot with a screen face, glowing eyes and big headphones.</summary>
public static class BeepArt
{
    public static (Color Band, Color Cups) PhoneColors(string? phones, double time) => phones switch
    {
        "phones_cherry" => (new Color("e83050"), new Color("ffc0cc")),
        "phones_lime" => (new Color("58c838"), new Color("f0ff70")),
        "phones_gold" => (new Color("e0a820"), new Color("fff2a0")),
        "phones_rainbow" => (Gfx.Rainbow(time), Gfx.Rainbow(time, 0.5f)),
        _ => (new Color("6a5cff"), new Color("44e8f4")),
    };

    public static Color EyeColor(string? eyes) => eyes switch
    {
        "eyes_heart" => new Color("ff70c8"),
        "eyes_star" => new Color("ffe060"),
        _ => new Color("58f4ff"),
    };

    /// <summary>
    /// Draws Beep standing with feet at <paramref name="foot"/>. <paramref name="mood"/> overrides the eyes
    /// ("happy", "sad", "wide", "blink"); <paramref name="squash"/> stretches him for hops.
    /// </summary>
    public static void Draw(CanvasItem ci, Vector2 foot, Outfit outfit, double time, float scale = 1, string? mood = null,
        Vector2? squash = null, bool flip = false)
    {
        var sq = squash ?? Vector2.One;
        float sx = scale * sq.X, sy = scale * sq.Y;
        var size = new Vector2(Sprites.BeepSize.X * sx, Sprites.BeepSize.Y * sy);
        var topLeft = new Vector2(Mathf.Floor(foot.X - size.X / 2), Mathf.Floor(foot.Y - size.Y));
        var rect = new Rect2(topLeft, size);
        var (band, cups) = PhoneColors(outfit.Phones, time);

        // A soft shadow under his feet.
        Gfx.Rect(ci, foot.X - 9 * scale, foot.Y - 1, 18 * scale, 2, new Color(0, 0, 0, 0.3f));

        ci.DrawTextureRect(Sprites.BeepLayer("body"), rect, false);
        ci.DrawTextureRect(Sprites.BeepLayer("band"), rect, false, band);
        ci.DrawTextureRect(Sprites.BeepLayer("cups"), rect, false, cups);

        // Eyes: style from the outfit unless a mood overrides; blink now and then.
        string style = mood ?? (outfit.Eyes switch
        {
            "eyes_round" => "round",
            "eyes_heart" => "heart",
            "eyes_star" => "star",
            "eyes_happy" => "happy",
            _ => "square",
        });
        if (mood is null && (time % 3.7) < 0.12) style = "blink";
        var eyeTex = Sprites.Eye(style);
        var eyeColor = mood is "sad" ? new Color("7aa8ff") : EyeColor(outfit.Eyes);
        float look = flip ? -1 : 0;
        for (int i = 0; i < 2; i++)
        {
            var at = topLeft + new Vector2((Sprites.EyeAt.X + 5 * i + look) * sx, Sprites.EyeAt.Y * sy);
            // A glow behind the eye, like the photo's lit screen.
            var glow = eyeColor;
            glow.A = 0.25f;
            ci.DrawTextureRect(eyeTex, new Rect2(at - new Vector2(sx, 0), new Vector2(5 * sx, 4 * sy)), false, glow);
            ci.DrawTextureRect(eyeTex, new Rect2(at, new Vector2(3 * sx, 4 * sy)), false, eyeColor);
        }

        if (outfit.Hat is { } hat && Sprites.Hats.ContainsKey(hat))
        {
            var tex = Sprites.Get(hat);
            var hatFoot = topLeft + new Vector2(12 * sx, 3 * sy);
            var hs = tex.GetSize();
            var hr = new Rect2(new Vector2(Mathf.Floor(hatFoot.X - hs.X * sx / 2), Mathf.Floor(hatFoot.Y - hs.Y * sy)), hs * new Vector2(sx, sy));
            ci.DrawTextureRect(tex, hr, false);
            if (hat == "hat_propeller")
            {
                // The propeller spins: its blade narrows and widens.
                float spin = Mathf.Abs(Mathf.Sin((float)time * 14f));
                float bw = (2 + 8 * spin) * sx;
                Gfx.Rect(ci, hatFoot.X - bw / 2, hr.Position.Y, bw, Math.Max(1, sy), Gfx.Gold);
            }
        }
    }
}
