using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using static CydLan.Desktop.FxDraw;

namespace CydLan.Desktop;

/// <summary>Additional ambient effects for the chat (and optionally the side panel); several can play at once.</summary>
public static class AmbientFxCatalog
{
    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new("aurora", "Aurora veil"),
        new("leaves", "Autumn leaves"),
        new("meteors", "Meteor shower"),
        new("butterflies", "Butterflies"),
        new("lanterns", "Sky lanterns"),
        new("hearts", "Floating hearts"),
        new("confetti", "Confetti"),
        new("dandelion", "Dandelion seeds"),
        new("jellyfish", "Jellyfish"),
        new("ripples", "Pond ripples"),
        new("storm", "Thunderstorm"),
        new("dust", "Drifting dust"),
        new("glitter", "Glitter"),
        new("feathers", "Feathers"),
        new("notes", "Music notes"),
        new("fog", "Rolling fog"),
        new("godrays", "Light rays"),
        new("wisps", "Wisps"),
        new("pixelrain", "Pixel rain"),
        new("galaxy", "Galaxy swirl"),
        new("planes", "Paper planes"),
        new("hexpulse", "Hex pulse")
    ];

    public static List<string> Normalize(IEnumerable<string>? values) =>
        (values ?? []).Where(v => All.Any(o => o.Value == v)).Distinct().ToList();
}

public sealed class AmbientFxLayer : MultiFxLayer
{
    private static readonly Dictionary<(Color, int), System.Windows.Media.Brush> Curtains = [];

    public AmbientFxLayer() => ClipToBounds = true;

    private static System.Windows.Media.Brush Curtain(Color color, double alpha)
    {
        int level = Level(alpha);
        if (Curtains.TryGetValue((color, level), out var brush)) return brush;
        if (Curtains.Count > 128) Curtains.Clear();
        var top = EffectPalette.WithOpacity(color, 0);
        var hem = EffectPalette.WithOpacity(color, level);
        var gradient = new LinearGradientBrush(top, hem, new Point(0, 0), new Point(0, 1));
        gradient.GradientStops.Insert(1, new GradientStop(EffectPalette.WithOpacity(color, level / 3), .6));
        gradient.Freeze();
        return Curtains[(color, level)] = gradient;
    }

    protected override void Draw(DrawingContext dc, string kind, double w, double h, double k, double sec, Color tint)
    {
        switch (kind)
        {
            case "aurora": Aurora(dc, w, h, k, sec, tint); break;
            case "leaves": Leaves(dc, w, h, k, sec); break;
            case "meteors": Meteors(dc, w, h, k, sec, tint); break;
            case "butterflies": Butterflies(dc, w, h, k, sec); break;
            case "lanterns": Lanterns(dc, w, h, k, sec); break;
            case "hearts": Hearts(dc, w, h, k, sec, tint); break;
            case "confetti": Confetti(dc, w, h, k, sec); break;
            case "dandelion": Dandelion(dc, w, h, k, sec); break;
            case "jellyfish": Jellyfish(dc, w, h, k, sec, tint); break;
            case "ripples": Ripples(dc, w, h, k, sec, tint); break;
            case "storm": Storm(dc, w, h, k, sec, tint); break;
            case "dust": Dust(dc, w, h, k, sec, tint); break;
            case "glitter": Glitter(dc, w, h, k, sec, tint); break;
            case "feathers": Feathers(dc, w, h, k, sec, tint); break;
            case "notes": Notes(dc, w, h, k, sec, tint); break;
            case "fog": Fog(dc, w, h, k, sec, tint); break;
            case "godrays": GodRays(dc, w, h, k, sec, tint); break;
            case "wisps": Wisps(dc, w, h, k, sec, tint); break;
            case "pixelrain": PixelRain(dc, w, h, k, sec); break;
            case "galaxy": Galaxy(dc, w, h, k, sec, tint); break;
            case "planes": Planes(dc, w, h, k, sec, tint); break;
            case "hexpulse": HexPulse(dc, w, h, k, sec, tint); break;
        }
    }

    private static void Aurora(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color[] hues = [EffectPalette.Blend(Color.FromRgb(52, 211, 153), tint, .3), EffectPalette.Blend(Color.FromRgb(34, 211, 238), tint, .3), EffectPalette.Blend(Color.FromRgb(167, 139, 250), tint, .3)];
        for (int band = 0; band < 3; band++)
        {
            var color = hues[band];
            double baseY = h * (.14 + band * .08);
            int steps = Math.Clamp((int)(w / 5), 24, 260);
            double slice = w / steps;
            for (int s = 0; s <= steps; s++)
            {
                double u = s / (double)steps, x = u * w;
                double y = baseY + Math.Sin(u * 5 + sec * .35 + band * 2) * h * .05 + Math.Sin(u * 11 - sec * .5) * h * .015;
                double len = h * (.12 + .08 * Math.Sin(u * 7 + sec * .6 + band));
                double shimmer = .5 + .5 * Math.Sin(u * 17 + sec * 1.4 + band);
                // Thin flat curtain slices that fade upward from a bright lower hem.
                dc.DrawRectangle(Curtain(color, (.12 + .22 * shimmer) * k), null, new Rect(x - slice * .5, y - len, slice + .6, len));
            }
        }
    }

    private static void Leaves(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color[] fall = [Color.FromRgb(234, 88, 12), Color.FromRgb(217, 119, 6), Color.FromRgb(185, 28, 28), Color.FromRgb(202, 138, 4)];
        int n = Count(w, h, 14000, 6, 30);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.05 + R(i * 3) * .05));
            double x = Frac(R(i * 7) + Math.Sin(t * Math.Tau + i) * .04 + t * .15) * (w + 40) - 20;
            double y = t * (h + 40) - 20;
            double flip = Math.Cos(sec * (1.5 + R(i * 5)) + i);
            var color = fall[i % fall.Length];
            Shape(dc, FxDraw.Leaf, Fill(color, (.45 + .4 * k) * Math.Sin(t * Math.PI)), Stroke(EffectPalette.Blend(color, Colors.Black, .3), .4 * k, .6),
                new Point(x, y), 1.2 + R(i * 9), sec * 60 * (R(i * 2) - .5) + i * 40, Math.Max(.15, Math.Abs(flip)));
        }
    }

    private static void Meteors(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color head = EffectPalette.Blend(tint, Colors.White, .6);
        int n = Count(w, h, 60000, 3, 9);
        for (int i = 0; i < n; i++)
        {
            double period = 2.5 + R(i * 3) * 3;
            double local = (sec + R(i) * period) / period;
            int cycle = (int)Math.Floor(local);
            double t = local - cycle;
            if (t > .45) continue;
            double u = t / .45;
            var start = new Point(R(i * 7 + cycle * 13) * w * 1.1, -10 + R(i * 11 + cycle) * h * .4);
            var p = new Point(start.X - u * w * .35, start.Y + u * w * .22);
            double fade = Math.Sin(u * Math.PI);
            for (int s = 0; s < 6; s++)
            {
                var a = new Point(p.X + s * 7, p.Y - s * 4.4);
                var b = new Point(a.X + 7, a.Y - 4.4);
                dc.DrawLine(Stroke(head, fade * (1 - s / 6.0) * (.4 + .5 * k), 2 - s * .25), a, b);
            }
            GlowAt(dc, head, fade * .8 * k, p, 6);
        }
    }

    private static void Butterflies(DrawingContext dc, double w, double h, double k, double sec)
    {
        int n = Count(w, h, 40000, 3, 10);
        for (int i = 0; i < n; i++)
        {
            double t = sec * (.04 + R(i) * .03) + R(i * 3);
            var p = new Point(Frac(t) * (w + 40) - 20, h * (.2 + .6 * R(i * 5)) + Math.Sin(sec * 1.3 + i) * h * .08 + Math.Sin(sec * 4 + i) * 4);
            double flap = .25 + .75 * Math.Abs(Math.Sin(sec * 11 + i * 2));
            var fill = Fill(Hue(R(i * 13) * .9 + .5), .55 + .35 * k);
            var m = Matrix.Identity;
            m.Scale(flap * 1.6, 1.6);
            m.Translate(p.X, p.Y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(fill, null, Wing);
            dc.PushTransform(new ScaleTransform(-1, 1));
            dc.DrawGeometry(fill, null, Wing);
            dc.Pop();
            dc.Pop();
        }
    }

    private static void Lanterns(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color warm = Color.FromRgb(251, 146, 60), core = Color.FromRgb(253, 230, 138);
        int n = Count(w, h, 30000, 4, 14);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.025 + R(i * 3) * .02));
            double depth = .5 + R(i * 7) * .7;
            var p = new Point(R(i * 5) * w + Math.Sin(sec * .6 + i) * 10, h + 20 - t * (h + 50));
            double fade = Math.Min(1, Math.Sin(t * Math.PI) * 2);
            double flicker = .8 + .2 * Math.Sin(sec * 9 + i * 3);
            GlowAt(dc, warm, fade * .55 * k * flicker, p, 16 * depth);
            dc.DrawRoundedRectangle(Fill(EffectPalette.Blend(core, warm, .4), fade * (.5 + .4 * k)), null, new Rect(p.X - 3.5 * depth, p.Y - 5 * depth, 7 * depth, 10 * depth), 2.5 * depth, 2.5 * depth);
        }
    }

    private static void Hearts(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color pink = EffectPalette.Blend(Color.FromRgb(244, 63, 94), tint, .25);
        int n = Count(w, h, 20000, 5, 22);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.04 + R(i * 3) * .04));
            var p = new Point(R(i * 5) * w + Math.Sin(t * Math.Tau * 2 + i) * 12, h + 12 - t * (h + 24));
            double beat = 1 + .12 * Math.Max(0, Math.Sin(sec * 7 + i));
            Shape(dc, FxDraw.Heart, Fill(i % 3 == 0 ? Color.FromRgb(249, 168, 212) : pink, Math.Sin(t * Math.PI) * (.35 + .45 * k)), null, p, (1.2 + R(i * 9) * 1.3) * beat, Math.Sin(sec + i) * 15);
        }
    }

    private static void Confetti(DrawingContext dc, double w, double h, double k, double sec)
    {
        int n = Count(w, h, 5000, 12, 80);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.06 + R(i * 3) * .06));
            var p = new Point(R(i * 5) * w + Math.Sin(t * 9 + i) * 8, t * (h + 20) - 10);
            double spin = sec * (3 + R(i * 7) * 5) + i;
            var color = Party[i % Party.Length];
            var m = Matrix.Identity;
            m.Scale(1, Math.Abs(Math.Cos(spin)) + .1);
            m.Rotate(i * 37 + sec * 90 * (R(i) - .5));
            m.Translate(p.X, p.Y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawRectangle(Fill(color, .55 + .35 * k), null, new Rect(-2.5, -1.5, 5, 3));
            dc.Pop();
        }
    }

    private static void Dandelion(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color seed = Color.FromRgb(250, 250, 245);
        int n = Count(w, h, 18000, 5, 26);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.03 + R(i * 3) * .025));
            var p = new Point(t * (w + 60) - 30, h * (.15 + .7 * R(i * 5)) + Math.Sin(sec * .8 + i) * 18 - t * 30);
            double alpha = Math.Sin(t * Math.PI) * (.4 + .45 * k);
            double tilt = Math.Sin(sec * 1.5 + i) * .4;
            var stemEnd = new Point(p.X - Math.Sin(tilt) * 7, p.Y + Math.Cos(tilt) * 7);
            dc.DrawLine(Stroke(seed, alpha * .8, .6), p, stemEnd);
            for (int f = 0; f < 9; f++)
            {
                double a = -Math.PI / 2 + tilt + (f - 4) * .3;
                dc.DrawLine(Stroke(seed, alpha * .6, .45), p, new Point(p.X + Math.Cos(a) * 5, p.Y + Math.Sin(a) * 5));
            }
            dc.DrawEllipse(Fill(seed, alpha), null, stemEnd, 1, 1);
        }
    }

    private static void Jellyfish(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color body = EffectPalette.Blend(Color.FromRgb(192, 132, 252), tint, .3);
        int n = Count(w, h, 70000, 2, 7);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.018 + R(i * 3) * .012));
            double pulse = .5 + .5 * Math.Sin(sec * 2.2 + i * 2);
            double size = 10 + R(i * 7) * 10;
            var p = new Point(R(i * 5) * w + Math.Sin(sec * .4 + i) * 20, h + size * 3 - t * (h + size * 6));
            GlowAt(dc, body, (.25 + .2 * pulse) * k, p, size * 2.2);
            var bell = new StreamGeometry();
            double bw = size * (1 + .12 * pulse), bh = size * (1 - .15 * pulse);
            using (var c = bell.Open())
            {
                c.BeginFigure(new Point(p.X - bw, p.Y), true, true);
                c.BezierTo(new Point(p.X - bw, p.Y - bh * 1.4), new Point(p.X + bw, p.Y - bh * 1.4), new Point(p.X + bw, p.Y), true, true);
                c.QuadraticBezierTo(new Point(p.X, p.Y - bh * .25), new Point(p.X - bw, p.Y), true, true);
            }
            bell.Freeze();
            dc.DrawGeometry(Fill(body, (.22 + .2 * k)), Stroke(EffectPalette.Blend(body, Colors.White, .4), .5 * k, .8), bell);
            for (int tnt = 0; tnt < 5; tnt++)
            {
                double x0 = p.X - bw * .7 + tnt * bw * .35;
                var path = new StreamGeometry();
                using (var c = path.Open())
                {
                    c.BeginFigure(new Point(x0, p.Y), false, false);
                    for (int s = 1; s <= 4; s++)
                        c.LineTo(new Point(x0 + Math.Sin(sec * 3 + s + tnt) * 2.5 * s * .5, p.Y + s * size * .45), true, true);
                }
                path.Freeze();
                dc.DrawGeometry(null, Stroke(body, .35 * k, .8), path);
            }
        }
    }

    private static void Ripples(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color water = EffectPalette.Blend(tint, Colors.White, .35);
        int n = Count(w, h, 40000, 3, 10);
        for (int i = 0; i < n; i++)
        {
            double period = 3 + R(i * 3) * 2;
            double local = (sec + R(i) * period) / period;
            int cycle = (int)Math.Floor(local);
            double t = local - cycle;
            var c = new Point(R(i * 7 + cycle * 3) * w, R(i * 11 + cycle * 5) * h);
            for (int ring = 0; ring < 3; ring++)
            {
                double u = t - ring * .12;
                if (u <= 0) continue;
                double r = u * 46;
                dc.DrawEllipse(null, Stroke(water, (1 - u) * (1 - u) * (.35 + .4 * k), 1.1), c, r, r * .45);
            }
        }
    }

    private static void Storm(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        double period = 4.6;
        int cycle = (int)Math.Floor(sec / period);
        double t = sec / period - cycle;
        if (t > .14) return;
        double flash = t < .03 ? 1 : t < .06 ? .25 : t < .09 ? .8 : (.14 - t) / .05 * .6;
        Color bolt = EffectPalette.Blend(Color.FromRgb(224, 231, 255), tint, .15);
        dc.DrawRectangle(Fill(bolt, flash * .12 * k), null, new Rect(0, 0, w, h));
        var p = new Point(w * (.15 + .7 * R(cycle)), 0);
        var path = new StreamGeometry();
        using (var c = path.Open())
        {
            c.BeginFigure(p, false, false);
            double y = 0;
            int seg = 0;
            while (y < h * (.45 + .35 * R(cycle * 3)))
            {
                y += 10 + R(cycle * 17 + seg) * 18;
                p = new Point(p.X + (R(cycle * 31 + seg++) - .5) * 26, y);
                c.LineTo(p, true, false);
            }
        }
        path.Freeze();
        dc.DrawGeometry(null, Stroke(bolt, flash * .35 * k, 5), path);
        dc.DrawGeometry(null, Stroke(Colors.White, flash * (.6 + .4 * k), 1.6), path);
    }

    private static void Dust(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color mote = EffectPalette.Blend(Color.FromRgb(253, 230, 138), tint, .4);
        int n = Count(w, h, 2200, 20, 160);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.02 + R(i * 3) * .03));
            var p = new Point(t * (w + 20) - 10, Frac(R(i * 5) + Math.Sin(sec * .3 + i) * .02) * h);
            double shimmer = .5 + .5 * Math.Sin(sec * 3 + i);
            dc.DrawEllipse(Fill(mote, (.2 + .3 * shimmer) * k), null, p, .6 + R(i * 7), .6 + R(i * 7));
        }
    }

    private static void Glitter(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color shine = EffectPalette.Blend(tint, Colors.White, .65);
        int n = Count(w, h, 4500, 10, 90);
        for (int i = 0; i < n; i++)
        {
            double tw = Math.Pow(Math.Max(0, Math.Sin(sec * (1.5 + R(i * 3) * 2.5) + R(i) * 20)), 8);
            if (tw < .05) continue;
            var p = new Point(R(i * 5) * w, R(i * 7) * h);
            Shape(dc, Spark, Fill(i % 5 == 0 ? Hue(R(i)) : shine, tw * (.5 + .5 * k)), null, p, .5 + tw * (.6 + R(i * 9)), sec * 30);
        }
    }

    private static void Feathers(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color plume = EffectPalette.Blend(Colors.White, tint, .25);
        int n = Count(w, h, 24000, 4, 16);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.025 + R(i * 3) * .02));
            double sway = Math.Sin(sec * 1.1 + i * 2);
            var p = new Point(R(i * 5) * w + sway * 30, t * (h + 40) - 20);
            Shape(dc, FxDraw.Feather, Fill(plume, Math.Sin(t * Math.PI) * (.35 + .4 * k)), Stroke(EffectPalette.Blend(plume, tint, .5), .35 * k, .5),
                p, 1.4 + R(i * 7), sway * 35 + 20);
        }
    }

    private static void Notes(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color ink = EffectPalette.Blend(tint, Colors.White, .3);
        int n = Count(w, h, 22000, 4, 18);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.035 + R(i * 3) * .03));
            var p = new Point(R(i * 5) * w + Math.Sin(t * Math.Tau * 1.5 + i) * 18, h + 10 - t * (h + 20));
            Shape(dc, FxDraw.Note, Fill(i % 4 == 0 ? Hue(R(i * 2)) : ink, Math.Sin(t * Math.PI) * (.4 + .45 * k)), null, p, 1.6 + R(i * 7), Math.Sin(sec * 2 + i) * 14);
        }
    }

    private static void Fog(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color mist = EffectPalette.Blend(Color.FromRgb(226, 232, 240), tint, .2);
        for (int i = 0; i < 6; i++)
        {
            double x = Frac(R(i) + sec * (.008 + R(i * 3) * .008)) * (w + w * .8) - w * .4;
            double y = h * (.35 + .6 * R(i * 5));
            double rx = w * (.25 + .2 * R(i * 7)), ry = h * (.1 + .08 * R(i * 9));
            dc.DrawImage(Glow(mist, (.18 + .2 * k)), new Rect(x - rx, y - ry, rx * 2, ry * 2));
        }
    }

    private static void GodRays(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color light = EffectPalette.Blend(Color.FromRgb(254, 249, 195), tint, .3);
        var origin = new Point(w * .15, -h * .1);
        for (int i = 0; i < 7; i++)
        {
            double a = .45 + i * .11 + Math.Sin(sec * .2 + i) * .03;
            double spread = .025 + .015 * R(i);
            double len = Math.Sqrt(w * w + h * h) * 1.1;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(origin, true, true);
                c.LineTo(new Point(origin.X + Math.Cos(a - spread) * len, origin.Y + Math.Sin(a - spread) * len), false, false);
                c.LineTo(new Point(origin.X + Math.Cos(a + spread) * len, origin.Y + Math.Sin(a + spread) * len), false, false);
            }
            g.Freeze();
            dc.DrawGeometry(Fill(light, (.05 + .06 * (.5 + .5 * Math.Sin(sec * .7 + i * 1.7))) * k), null, g);
        }
    }

    private static void Wisps(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color glow = EffectPalette.Blend(Color.FromRgb(125, 211, 252), tint, .35);
        int n = Count(w, h, 50000, 3, 8);
        for (int i = 0; i < n; i++)
        {
            for (int tail = 8; tail >= 0; tail--)
            {
                double s = sec - tail * .06;
                var p = new Point(w * (.5 + .42 * Math.Sin(s * (.17 + R(i) * .1) + i * 2)), h * (.5 + .4 * Math.Sin(s * (.23 + R(i * 3) * .1) + i)));
                double fade = 1 - tail / 9.0;
                GlowAt(dc, glow, fade * (.35 + .4 * k), p, 3 + fade * 7);
            }
        }
    }

    private static void PixelRain(DrawingContext dc, double w, double h, double k, double sec)
    {
        double cell = 6;
        int columns = Math.Clamp((int)(w / (cell * 3)), 4, 60);
        for (int c = 0; c < columns; c++)
        {
            double t = Frac(R(c) + sec * (.15 + R(c * 3) * .2));
            double x = Math.Round((c + .5) / columns * w / cell) * cell;
            double headY = Math.Round(t * (h + 60) / cell) * cell - 30;
            var color = Hue(R(c * 7) * .3 + .25);
            for (int s = 0; s < 6; s++)
                dc.DrawRectangle(Fill(color, (1 - s / 6.0) * (.3 + .45 * k)), null, new Rect(x, headY - s * cell, cell - 1, cell - 1));
        }
    }

    private static void Galaxy(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        var c = new Point(w * .5, h * .5);
        double radius = Math.Min(w, h) * .55;
        Color core = EffectPalette.Blend(tint, Colors.White, .5);
        GlowAt(dc, core, .3 * k, c, radius * .25);
        int n = Count(w, h, 1500, 60, 260);
        for (int i = 0; i < n; i++)
        {
            double d = Math.Pow(R(i), .7);
            int arm = i % 3;
            double angle = arm * Math.Tau / 3 + d * 5.5 + sec * .05 * (1.2 - d) + (R(i * 3) - .5) * .5;
            var p = new Point(c.X + Math.Cos(angle) * d * radius * 1.4, c.Y + Math.Sin(angle) * d * radius * .8);
            double tw = .5 + .5 * Math.Sin(sec * 2 + i);
            dc.DrawEllipse(Fill(i % 7 == 0 ? Hue(.6 + R(i) * .3) : core, (1 - d * .6) * (.2 + .3 * tw) * k), null, p, .7 + R(i * 5) * .7, .7 + R(i * 5) * .7);
        }
    }

    private static void Planes(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color paper = EffectPalette.Blend(Colors.White, tint, .2);
        int n = Count(w, h, 70000, 2, 6);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.03 + R(i * 3) * .02));
            double bob = Math.Sin(sec * 1.6 + i) * 10;
            var p = new Point(t * (w + 60) - 30, h * (.2 + .6 * R(i * 5)) + bob - t * h * .1);
            for (int s = 1; s <= 6; s++)
                dc.DrawEllipse(Fill(paper, (1 - s / 7.0) * .18 * k), null, new Point(p.X - s * 6, p.Y + Math.Sin(sec * 1.6 + i - s * .15) * 2), .9, .9);
            Shape(dc, Plane, Fill(paper, .5 + .35 * k), Stroke(EffectPalette.Blend(paper, Colors.Black, .3), .4 * k, .6), p, 1.6, Math.Cos(sec * 1.6 + i) * 12 - 6);
        }
    }

    private static void HexPulse(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        double size = 18, hw = size * Math.Sqrt(3);
        Color line = EffectPalette.Blend(tint, Colors.White, .3);
        double wave = Frac(sec * .18) * (w + h);
        for (double row = 0, y = 0; y < h + size; row++, y += size * 1.5)
            for (double x = row % 2 == 0 ? 0 : hw / 2; x < w + hw; x += hw)
            {
                double d = Math.Abs(x + y - wave);
                if (d > 120) continue;
                double a = (1 - d / 120) * (.25 + .4 * k);
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    for (int s = 0; s < 6; s++)
                    {
                        double ang = Math.PI / 6 + s * Math.PI / 3;
                        var p = new Point(x + Math.Cos(ang) * size * .92, y + Math.Sin(ang) * size * .92);
                        if (s == 0) c.BeginFigure(p, false, true); else c.LineTo(p, true, false);
                    }
                }
                g.Freeze();
                dc.DrawGeometry(null, Stroke(line, a, 1), g);
            }
    }
}
