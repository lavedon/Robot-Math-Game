using Godot;

namespace MathRobot.Game.Snes;

/// <summary>
/// The SNES "Mode 7" floor: a flat textured plane seen in perspective from a camera that can move, turn, rise and
/// fall, with a sky panorama above the horizon that turns with the camera. Like the real hardware it works line by
/// line: every row of the screen below the horizon is one slice of the plane at one distance (done here in a shader).
/// <see cref="Project"/> does the same sum for one point, so sprites can stand on the plane.
/// </summary>
public partial class Mode7 : ColorRect
{
    private const string Code = """
        shader_type canvas_item;
        render_mode unshaded;
        uniform sampler2D map_tex : filter_nearest, repeat_enable;
        uniform sampler2D sea_tex : filter_nearest, repeat_enable;
        uniform sampler2D sky_tex : filter_nearest, repeat_enable;
        uniform vec2 cam = vec2(256.0, 256.0);
        uniform float angle = 0.0;
        uniform float height = 40.0;
        uniform float focal = 128.0;
        uniform float horizon = 80.0;
        uniform vec2 map_size = vec2(512.0, 512.0);
        uniform bool tile_map = false;
        uniform vec4 sky_top : source_color = vec4(0.1, 0.2, 0.6, 1.0);
        uniform vec4 sky_bottom : source_color = vec4(0.9, 0.6, 0.8, 1.0);
        uniform vec4 fog : source_color = vec4(0.8, 0.7, 0.9, 1.0);
        uniform float fog_far = 900.0;
        uniform float sky_scroll = 0.0;
        uniform float time = 0.0;
        const vec2 SCREEN = vec2(256.0, 224.0);

        void fragment() {
            vec2 p = floor(UV * SCREEN) + 0.5;
            if (p.y < horizon) {
                // Sky: a banded gradient (like an HDMA colour table) with the panorama sitting on the horizon.
                float t = clamp(p.y / horizon, 0.0, 1.0);
                t = floor(t * 16.0) / 16.0;
                vec4 c = mix(sky_top, sky_bottom, t);
                float u = (p.x - 128.0) / 512.0 + angle / 6.2831853 + sky_scroll;
                float v = (p.y - (horizon - 64.0)) / 64.0;
                if (v >= 0.0) {
                    vec4 s = texture(sky_tex, vec2(u, v));
                    c.rgb = mix(c.rgb, s.rgb, s.a);
                }
                COLOR = vec4(c.rgb, 1.0);
            } else {
                float dy = p.y - horizon + 0.5;
                float z = height * focal / dy;
                float sx = (p.x - 128.0) * z / focal;
                vec2 fwd = vec2(sin(angle), -cos(angle));
                vec2 right = vec2(cos(angle), sin(angle));
                vec2 w = cam + fwd * z + right * sx;
                vec4 c = vec4(0.0);
                if (tile_map) {
                    c = texture(map_tex, w / map_size);
                } else if (w.x >= 0.0 && w.y >= 0.0 && w.x < map_size.x && w.y < map_size.y) {
                    c = texture(map_tex, w / map_size);
                }
                if (c.a < 0.5) {
                    // The sea: a tile that drifts and sways.
                    vec2 sw = w / 32.0 + vec2(time * 0.04, sin(time * 0.8 + w.y / 37.0) * 0.06);
                    c = texture(sea_tex, sw);
                }
                float f = clamp(z / fog_far, 0.0, 1.0);
                c.rgb = mix(c.rgb, fog.rgb, f * f);
                COLOR = vec4(c.rgb, 1.0);
            }
        }
        """;

    private ShaderMaterial _mat = null!;
    private double _time;

    public Vector2 Cam;
    public float Angle;
    public float CamHeight = 40;
    public float Focal = 128;
    public float Horizon = 80;
    public float SkyScroll;

    public Mode7()
    {
        Position = Vector2.Zero;
        Size = new Vector2(Gfx.W, Gfx.H);
        Color = Colors.White;
        MouseFilter = MouseFilterEnum.Ignore;
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        Material = _mat;
    }

    public void SetMap(Texture2D map, bool tiled)
    {
        _mat.SetShaderParameter("map_tex", map);
        _mat.SetShaderParameter("map_size", map.GetSize());
        _mat.SetShaderParameter("tile_map", tiled);
    }

    public void SetSea(Texture2D sea) => _mat.SetShaderParameter("sea_tex", sea);
    public void SetSky(Texture2D sky) => _mat.SetShaderParameter("sky_tex", sky);

    public void SetColors(Color skyTop, Color skyBottom, Color fog, float fogFar)
    {
        _mat.SetShaderParameter("sky_top", skyTop);
        _mat.SetShaderParameter("sky_bottom", skyBottom);
        _mat.SetShaderParameter("fog", fog);
        _mat.SetShaderParameter("fog_far", fogFar);
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _mat.SetShaderParameter("cam", Cam);
        _mat.SetShaderParameter("angle", Angle);
        _mat.SetShaderParameter("height", CamHeight);
        _mat.SetShaderParameter("focal", Focal);
        _mat.SetShaderParameter("horizon", Horizon);
        _mat.SetShaderParameter("sky_scroll", SkyScroll);
        _mat.SetShaderParameter("time", (float)_time);
    }

    public Vector2 Forward => new(Mathf.Sin(Angle), -Mathf.Cos(Angle));
    public Vector2 Right => new(Mathf.Cos(Angle), Mathf.Sin(Angle));

    /// <summary>
    /// Where a point on the plane shows on screen, and how big a sprite there is drawn (1 = one texel per pixel).
    /// False when it is behind the camera or too close.
    /// </summary>
    public bool Project(Vector2 world, out Vector2 screen, out float scale, out float depth)
    {
        var rel = world - Cam;
        depth = rel.Dot(Forward);
        float x = rel.Dot(Right);
        if (depth < 6)
        {
            screen = default;
            scale = 0;
            return false;
        }
        screen = new Vector2(Gfx.W / 2f + x * Focal / depth, Horizon + CamHeight * Focal / depth);
        scale = Focal / depth;
        return screen.X > -64 && screen.X < Gfx.W + 64 && screen.Y < Gfx.H + 64;
    }

    /// <summary>Points the camera from behind <paramref name="target"/> along <paramref name="angle"/>, <paramref name="distance"/> back.</summary>
    public void Chase(Vector2 target, float angle, float distance)
    {
        Angle = angle;
        Cam = target - Forward * distance;
    }
}
