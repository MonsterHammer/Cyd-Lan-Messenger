using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using static CydLan.Desktop.FxDraw;

namespace CydLan.Desktop;

/// <summary>Additional effects for the tall side nav and side panel; several can play at once.</summary>
public static class PanelFxCatalog
{
    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new("neon", "Neon tubes"),
        new("waterfall", "Waterfall"),
        new("lava", "Lava lamp"),
        new("helix", "DNA helix"),
        new("vines", "Creeping vines"),
        new("equalizer", "Equalizer"),
        new("fiber", "Fiber optics"),
        new("fairylights", "Fairy lights"),
        new("binary", "Binary pulse"),
        new("rainbow", "Rainbow edge"),
        new("comet", "Edge comet"),
        new("starfall", "Starfall"),
        new("frost", "Frost creep"),
        new("ecg", "Heart monitor"),
        new("candles", "Candlelight"),
        new("sakura", "Sakura drift")
    ];

    public static List<string> Normalize(IEnumerable<string>? values) =>
        (values ?? []).Where(v => All.Any(o => o.Value == v)).Distinct().ToList();
}

public sealed class PanelFxLayer : MultiFxLayer
{
    public PanelFxLayer() => ClipToBounds = true;

    protected override void Draw(DrawingContext dc, string kind, double w, double h, double k, double sec, Color tint)
    {
        switch (kind)
        {
            case "neon": Neon(dc, w, h, k, sec, tint); break;
            case "waterfall": Waterfall(dc, w, h, k, sec, tint); break;
            case "lava": Lava(dc, w, h, k, sec, tint); break;
            case "helix": Helix(dc, w, h, k, sec, tint); break;
            case "vines": Vines(dc, w, h, k, sec, tint); break;
            case "equalizer": Equalizer(dc, w, h, k, sec, tint); break;
            case "fiber": Fiber(dc, w, h, k, sec, tint); break;
            case "fairylights": FairyLights(dc, w, h, k, sec); break;
            case "binary": Binary(dc, w, h, k, sec, tint); break;
            case "rainbow": RainbowEdge(dc, w, h, k, sec); break;
            case "comet": EdgeComet(dc, w, h, k, sec, tint); break;
            case "starfall": Starfall(dc, w, h, k, sec, tint); break;
            case "frost": Frost(dc, w, h, k, sec, tint); break;
            case "ecg": Ecg(dc, w, h, k, sec, tint); break;
            case "candles": Candles(dc, w, h, k, sec); break;
            case "sakura": Sakura(dc, w, h, k, sec); break;
        }
    }

    private static void Neon(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color a = EffectPalette.Blend(Color.FromRgb(236, 72, 153), tint, .3), b = EffectPalette.Blend(Color.FromRgb(34, 211, 238), tint, .3);
        int glitch = (int)(sec * 8);
        double flickerA = R(glitch) > .93 ? .3 : 1, flickerB = R(glitch * 3 + 1) > .95 ? .35 : 1;
        foreach (var (x, c, f) in new[] { (3.0, a, flickerA), (w - 3, b, flickerB) })
        {
            dc.DrawLine(Stroke(c, .18 * k * f, 7), new Point(x, 8), new Point(x, h - 8));
            dc.DrawLine(Stroke(c, .5 * k * f, 2.5), new Point(x, 8), new Point(x, h - 8));
            dc.DrawLine(Stroke(Colors.White, .6 * k * f, .8), new Point(x, 8), new Point(x, h - 8));
        }
    }

    private static void Waterfall(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color water = EffectPalette.Blend(Color.FromRgb(125, 211, 252), tint, .35);
        int streams = Math.Clamp((int)(w / 6), 6, 50);
        for (int i = 0; i < streams; i++)
        {
            double x = (i + R(i)) / streams * w;
            double speed = 60 + R(i * 3) * 80;
            double len = 18 + R(i * 5) * 40;
            for (int s = 0; s < 3; s++)
            {
                double y = Frac(R(i * 7 + s) + sec * speed / (h + 60)) * (h + 60) - 30;
                dc.DrawLine(Stroke(water, (.15 + .3 * k) * (.5 + .5 * R(i + s)), 1 + R(i * 9)), new Point(x, y), new Point(x, y + len));
            }
        }
        dc.DrawRectangle(Fill(water, .12 * k), null, new Rect(0, h - 14, w, 14));
    }

    private static void Lava(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color hot = EffectPalette.Blend(Color.FromRgb(249, 115, 22), tint, .3), deep = EffectPalette.Blend(Color.FromRgb(225, 29, 72), tint, .3);
        int blobs = Math.Clamp((int)(h / 90), 4, 14);
        for (int i = 0; i < blobs; i++)
        {
            double t = .5 + .5 * Math.Sin(sec * (.12 + R(i) * .1) + i * 2);
            double r = Math.Min(w * .35, 34) * (.6 + R(i * 3) * .7);
            var p = new Point(w * (.2 + .6 * R(i * 5)) + Math.Sin(sec * .3 + i) * w * .08, r + t * (h - r * 2));
            dc.DrawEllipse(Fill(i % 2 == 0 ? hot : deep, (.12 + .18 * k)), null, p, r * (1 + .1 * Math.Sin(sec + i)), r * (1 - .1 * Math.Sin(sec + i)));
            GlowAt(dc, hot, .25 * k, p, r * 1.6);
        }
    }

    private static void Helix(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color a = EffectPalette.Blend(tint, Colors.White, .4), b = EffectPalette.Blend(Color.FromRgb(167, 139, 250), tint, .4);
        double cx = w * .5, amp = Math.Min(w * .32, 40);
        for (double y = 6; y < h; y += 9)
        {
            double phase = y * .045 - sec * 1.6;
            double depth = Math.Cos(phase);
            var p1 = new Point(cx + Math.Sin(phase) * amp, y);
            var p2 = new Point(cx - Math.Sin(phase) * amp, y);
            dc.DrawLine(Stroke(a, .12 * k, 1), p1, p2);
            dc.DrawEllipse(Fill(a, (.35 + .35 * (depth + 1) / 2) * k), null, p1, 1.4 + depth * .6, 1.4 + depth * .6);
            dc.DrawEllipse(Fill(b, (.35 + .35 * (1 - depth) / 2) * k), null, p2, 1.4 - depth * .6, 1.4 - depth * .6);
        }
    }

    private static void Vines(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color green = EffectPalette.Blend(Color.FromRgb(74, 222, 128), tint, .35);
        foreach (double edge in new[] { 4.0, w - 4 })
        {
            double dir = edge < w / 2 ? 1 : -1;
            var path = new StreamGeometry();
            using (var c = path.Open())
            {
                c.BeginFigure(new Point(edge, 0), false, false);
                for (double y = 8; y <= h; y += 8)
                    c.LineTo(new Point(edge + dir * (3 + Math.Sin(y * .05 + sec * .4) * 3), y), true, true);
            }
            path.Freeze();
            dc.DrawGeometry(null, Stroke(green, .45 * k, 1.4), path);
            for (double y = 20; y < h; y += 34)
            {
                double sway = Math.Sin(sec * 1.4 + y * .1) * 14;
                var at = new Point(edge + dir * (3 + Math.Sin(y * .05 + sec * .4) * 3), y);
                Shape(dc, FxDraw.Leaf, Fill(green, (.35 + .35 * k)), null, new Point(at.X + dir * 5, at.Y), 1.1, (dir > 0 ? -25 : 205) + sway);
            }
        }
    }

    private static void Equalizer(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        double bar = 7;
        int rows = (int)(h / (bar + 3));
        for (int i = 0; i < rows; i++)
        {
            double level = .15 + .85 * Math.Abs(Math.Sin(sec * (2 + R(i) * 3) + i * .7)) * (.4 + .6 * Math.Abs(Math.Sin(sec * .7 + i * .2)));
            var color = Hue(.55 - i / (double)rows * .55);
            dc.DrawRoundedRectangle(Fill(color, (.18 + .3 * k)), null, new Rect(0, i * (bar + 3), level * w * .9, bar), 2, 2);
        }
    }

    private static void Fiber(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        int strands = Math.Clamp((int)(w / 18), 3, 14);
        for (int i = 0; i < strands; i++)
        {
            double x0 = (i + .5) / strands * w;
            double bend = (R(i) - .5) * w * .3;
            var color = Hue(R(i * 3) * .4 + .5);
            dc.DrawLine(Stroke(color, .06 * k, 1), new Point(x0, 0), new Point(x0 + bend, h));
            for (int d = 0; d < 3; d++)
            {
                double t = Frac(R(i * 5 + d) + sec * (.15 + R(i * 7) * .15));
                var p = new Point(x0 + bend * t, t * h);
                GlowAt(dc, color, (.5 + .3 * k), p, 5);
            }
        }
    }

    private static void FairyLights(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color wire = Color.FromRgb(120, 113, 108);
        double swing = Math.Sin(sec * .8) * 3;
        var points = new List<Point>();
        for (double y = 0; y <= h; y += 30)
            points.Add(new Point(w * (.5 + .38 * Math.Sin(y * .02)) + swing * Math.Sin(y * .05), y));
        for (int i = 1; i < points.Count; i++) dc.DrawLine(Stroke(wire, .35 * k, .8), points[i - 1], points[i]);
        for (int i = 0; i < points.Count; i++)
        {
            double tw = .4 + .6 * Math.Pow(.5 + .5 * Math.Sin(sec * 2.5 + i * 1.3), 2);
            var c = Party[i % Party.Length];
            GlowAt(dc, c, tw * .7 * k, points[i], 8);
            dc.DrawEllipse(Fill(EffectPalette.Blend(c, Colors.White, .4), tw * k), null, points[i], 2, 2.6);
        }
    }

    private static void Binary(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color bit = EffectPalette.Blend(Color.FromRgb(74, 222, 128), tint, .4);
        int cols = Math.Clamp((int)(w / 10), 3, 30), rows = (int)(h / 10);
        int frame = (int)(sec * 6);
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                double v = R(c * 131 + r * 7 + frame / 3 * 17);
                if (v < .7) continue;
                double wave = .5 + .5 * Math.Sin(r * .2 - sec * 3 + c);
                var at = new Point(c * 10 + 5, r * 10 + 5);
                if (v > .85) dc.DrawRectangle(Fill(bit, wave * .45 * k), null, new Rect(at.X - 1, at.Y - 3, 2, 6));
                else dc.DrawEllipse(null, Stroke(bit, wave * .45 * k, .9), at, 2, 3);
            }
    }

    private static void RainbowEdge(DrawingContext dc, double w, double h, double k, double sec)
    {
        int segs = 40;
        for (int s = 0; s < segs; s++)
        {
            double y0 = s / (double)segs * h, y1 = (s + 1) / (double)segs * h;
            var c = Hue(s / (double)segs + sec * .08);
            dc.DrawLine(Stroke(c, .55 * k, 3), new Point(w - 2, y0), new Point(w - 2, y1));
            dc.DrawRectangle(Fill(c, .06 * k), null, new Rect(0, y0, w, y1 - y0 + .5));
        }
    }

    private static void EdgeComet(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color head = EffectPalette.Blend(tint, Colors.White, .5);
        double perimeter = 2 * (w + h);
        for (int c = 0; c < 2; c++)
        {
            double t = Frac(sec * .07 + c * .5);
            for (int tail = 16; tail >= 0; tail--)
            {
                double d = Frac(t - tail * .004) * perimeter;
                Point p = d < h ? new Point(w - 1.5, h - d) : d < h + w ? new Point(w - (d - h), 1.5) : d < 2 * h + w ? new Point(1.5, d - h - w) : new Point(d - 2 * h - w, h - 1.5);
                double fade = 1 - tail / 17.0;
                GlowAt(dc, head, fade * (.45 + .4 * k), p, 2 + fade * 6);
            }
        }
    }

    private static void Starfall(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color star = EffectPalette.Blend(tint, Colors.White, .6);
        int n = Count(w, h, 5000, 6, 40);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.05 + R(i * 3) * .05));
            var p = new Point(R(i * 5) * w, t * (h + 20) - 10);
            double tw = .5 + .5 * Math.Sin(sec * 5 + i);
            dc.DrawLine(Stroke(star, .25 * k, .7), new Point(p.X, p.Y - 10), p);
            Shape(dc, Spark, Fill(star, (.4 + .5 * tw) * k), null, p, .5 + .4 * tw, sec * 40);
        }
    }

    private static void Frost(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color ice = EffectPalette.Blend(Color.FromRgb(224, 242, 254), tint, .2);
        double grow = .6 + .4 * Math.Sin(sec * .25);
        for (int i = 0; i < Math.Clamp((int)(h / 40), 6, 40); i++)
        {
            double y = R(i) * h;
            bool left = i % 2 == 0;
            double x = left ? 0 : w;
            double dir = left ? 1 : -1;
            double len = (10 + R(i * 3) * 26) * grow;
            var tip = new Point(x + dir * len, y + (R(i * 5) - .5) * 10);
            dc.DrawLine(Stroke(ice, .45 * k, 1), new Point(x, y), tip);
            for (int b = 1; b <= 3; b++)
            {
                var at = new Point(x + dir * len * b / 4, y + (tip.Y - y) * b / 4);
                double bl = len * .3 * (1 - b / 4.0);
                dc.DrawLine(Stroke(ice, .35 * k, .7), at, new Point(at.X + dir * bl * .7, at.Y - bl));
                dc.DrawLine(Stroke(ice, .35 * k, .7), at, new Point(at.X + dir * bl * .7, at.Y + bl));
            }
        }
    }

    private static void Ecg(DrawingContext dc, double w, double h, double k, double sec, Color tint)
    {
        Color line = EffectPalette.Blend(Color.FromRgb(74, 222, 128), tint, .3);
        double x0 = w * .5;
        double head = Frac(sec * .25) * h;
        var path = new StreamGeometry();
        using (var c = path.Open())
        {
            c.BeginFigure(new Point(x0, 0), false, false);
            for (double y = 2; y <= h; y += 2)
            {
                double beat = Frac(y / 120);
                double dx = beat is > .4 and < .44 ? -10 : beat is > .44 and < .5 ? 16 : beat is > .5 and < .54 ? -6 : 0;
                c.LineTo(new Point(x0 + dx * Math.Min(1, w / 60), y), true, false);
            }
        }
        path.Freeze();
        dc.PushOpacityMask(new LinearGradientBrush(
            new GradientStopCollection { new(Color.FromArgb(0, 0, 0, 0), Math.Max(0, head / h - .45)), new(Colors.Black, head / h), new(Color.FromArgb(0, 0, 0, 0), head / h + .001) },
            new Point(0, 0), new Point(0, 1)));
        dc.DrawGeometry(null, Stroke(line, .7 * k, 1.4), path);
        dc.Pop();
        GlowAt(dc, line, .8 * k, new Point(x0, head), 6);
    }

    private static void Candles(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color flame = Color.FromRgb(253, 186, 116), core = Color.FromRgb(254, 240, 138);
        int n = Math.Clamp((int)(h / 120), 3, 10);
        for (int i = 0; i < n; i++)
        {
            var p = new Point(w * (.25 + .5 * R(i)), (i + .5) / n * h);
            double flick = .75 + .25 * Math.Sin(sec * 11 + i * 3) * Math.Sin(sec * 7 + i);
            GlowAt(dc, flame, .4 * k * flick, p, 22 * flick);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(p.X, p.Y - 9 * flick), true, true);
                c.QuadraticBezierTo(new Point(p.X + 4, p.Y - 1), new Point(p.X, p.Y + 2), true, true);
                c.QuadraticBezierTo(new Point(p.X - 4, p.Y - 1), new Point(p.X + Math.Sin(sec * 6 + i) * .8, p.Y - 9 * flick), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(Fill(core, .8 * k), null, g);
        }
    }

    private static void Sakura(DrawingContext dc, double w, double h, double k, double sec)
    {
        Color pink = Color.FromRgb(249, 168, 212);
        int n = Count(w, h, 6000, 6, 36);
        for (int i = 0; i < n; i++)
        {
            double t = Frac(R(i) + sec * (.04 + R(i * 3) * .03));
            var p = new Point(Frac(R(i * 5) + Math.Sin(sec * .5 + i) * .05) * w, t * (h + 20) - 10);
            var m = Matrix.Identity;
            m.Scale(1, .35 + .65 * Math.Abs(Math.Cos(sec * 2 + i)));
            m.Rotate(sec * 50 + i * 40);
            m.Translate(p.X, p.Y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawEllipse(Fill(pink, (.45 + .4 * k)), null, new Point(0, 0), 3.4, 2);
            dc.Pop();
        }
    }
}
