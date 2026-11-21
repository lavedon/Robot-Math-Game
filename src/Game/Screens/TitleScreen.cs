using Godot;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>The title: Gear Island turning slowly far below in Mode 7, the wavy logo, and Beep bouncing.</summary>
public partial class TitleScreen : Screen
{
    private double _pressed = -1;

    public override void Enter()
    {
        var plane = AddPlane();
        plane.SetMap(WorldArt.Map, tiled: false);
        plane.SetSea(WorldArt.Sea);
        plane.SetSky(WorldArt.SkyDusk);
        plane.SetColors(new Color("1a1050"), new Color("ff9ac0"), new Color("ffb8d0"), 900);
        plane.Focal = 140;
        plane.Horizon = 92;
        plane.CamHeight = 150;
        M.Sound.PlayMusic("title");
    }

    protected override void Update(double dt)
    {
        if (Plane is { } plane)
        {
            float a = (float)Time * 0.12f;
            var center = new Vector2(252, 278);
            plane.Angle = a;
            plane.Cam = center - plane.Forward * 330;
        }
        if (_pressed < 0 && Time > 0.5 && (Pad.Pressed(Btn.A) || Pad.Pressed(Btn.Start)))
        {
            _pressed = Time;
            M.Sound.Play("confirm");
            Fx.Confetti(new Vector2(128, 150), 40);
        }
        if (_pressed >= 0 && Time - _pressed > 0.45) M.Go(new MapScreen(swoop: true), 0.4, 0.2);
    }

    protected override void Paint(CanvasItem ci)
    {
        // Logo: two wavy lines of big letters, dropping in.
        float y = -40 + 62 * Pop((float)Time * 1.2f);
        WavyText(ci, "BEEP'S", 128, y, Gfx.Cyan, 2, Time, 2, Gfx.Ink);
        WavyText(ci, "NUMBER LINE", 128, y + 22, Gfx.Gold, 3, Time + 0.5, 2, Gfx.Ink);
        WavyText(ci, "DASH!", 128, y + 52, Gfx.Rainbow(Time), 3, Time + 1, 3, Gfx.Ink);

        // Beep bouncing on a little number line.
        float lineY = 176;
        Gfx.Rect(ci, 60, lineY - 1, 136, 2, Gfx.White);
        for (int n = 0; n <= 10; n++)
        {
            float x = 68 + n * 12;
            Gfx.Rect(ci, x, lineY - 3, 1, 7, n % 5 == 0 ? Gfx.Gold : Gfx.White);
            Gfx.Tiny(ci, n.ToString(), x + 0.5f, lineY + 6, Gfx.White);
        }
        // Hop from 10 back to 0, a step at a time, again and again.
        double cycle = Time % 6.0;
        int step = Math.Min(10, (int)(cycle / 0.45));
        float t = (float)((cycle / 0.45) - step);
        float from = 10 - step, to = Math.Max(0, from - 1);
        float at = step >= 10 ? 0 : Mathf.Lerp(from, to, t);
        float lift = step >= 10 ? 0 : 10 * 4 * t * (1 - t);
        BeepArt.Draw(ci, new Vector2(68 + at * 12, lineY - 2 - lift), Outfit.Of(P), Time, 1, step >= 10 ? "happy" : null, flip: step < 10);

        if (_pressed < 0 && (Time % 1.0) < 0.65 && Time > 1)
            Gfx.Help(ci, "PRESS {A} TO PLAY!", 128 - Gfx.HelpWidth("PRESS {A} TO PLAY!") / 2, 200, Gfx.White);
        Gfx.Rect(ci, 0, 212, Gfx.W, 12, new Color(0, 0, 0.1f, 0.6f));
        Gfx.TextCentered(ci, "M sound   F11 full screen   F2 scanlines", 128, 213, new Color(1, 1, 1, 0.7f));
    }
}
