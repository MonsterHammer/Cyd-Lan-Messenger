using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Additional scenic and patterned textures.</summary>
public sealed partial class ChromeTexture
{
    private static int Scaled(double width, double height, double perArea, int min, int max, bool compact) =>
        Math.Clamp((int)(width * height / perArea), min, compact ? Math.Max(min, max / 3) : max);

    private static Geometry Polygon(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true, isClosed: true);
            for (int i = 1; i < points.Count; i++) ctx.LineTo(points[i], isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static DrawingGroup BuildAuroraDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(4410);
        Color[] hues =
        [
            BlendTheme(Color.FromRgb(0x34, 0xD3, 0x99), accent, .62),
            BlendTheme(Color.FromRgb(0x22, 0xD3, 0xEE), accent, .55),
            BlendTheme(Color.FromRgb(0xA7, 0x8B, 0xFA), accent, .55)
        ];
        int curtains = compact ? 2 : 4;
        for (int c = 0; c < curtains; c++)
        {
            Color hue = hues[c % hues.Length];
            double baseY = height * (.32 + .14 * c / Math.Max(1, curtains - 1));
            double band = height * (compact ? .45 : .32) * (.7 + rng.NextDouble() * .4);
            double phase = rng.NextDouble() * Math.Tau, freq = 1.1 + rng.NextDouble() * 1.4;
            const int steps = 48;
            var top = new List<Point>();
            var bottom = new List<Point>();
            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps, x = t * width;
                double wave = Math.Sin(t * Math.Tau * freq + phase) * height * .06 + Math.Sin(t * Math.Tau * freq * 2.7 + phase * 2) * height * .025;
                double y = baseY + wave;
                bottom.Add(new Point(x, y));
                top.Add(new Point(x, y - band * (.55 + .45 * Math.Sin(t * Math.PI * 3 + phase))));
            }
            top.Reverse();
            var fill = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
                (WithAlpha(hue, 0), 0),
                (WithAlpha(hue, ScaleByte(isLight ? 26 : 40, intensity)), .7),
                (WithAlpha(hue, ScaleByte(isLight ? 44 : 70, intensity)), 1));
            dc.DrawGeometry(fill, null, Polygon([.. bottom, .. top]));

            // Fine vertical rays hanging from the bright lower edge.
            var rayPen = FrozenPen(WithAlpha(EffectPalette.Blend(hue, Colors.White, .3), ScaleByte(isLight ? 22 : 34, intensity)), .8);
            for (int s = 0; s < steps; s += compact ? 3 : 1)
            {
                var b = bottom[s];
                double len = band * (.35 + rng.NextDouble() * .5);
                dc.DrawLine(rayPen, b, new Point(b.X + 1.5, b.Y - len));
            }
        }
        return group;
    }

    private static DrawingGroup BuildCloudsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(1781);
        // On light surfaces pure white would vanish, so clouds take a soft blue-grey body there.
        Color puff = isLight ? EffectPalette.Blend(lo, accent, .25) : EffectPalette.Blend(hi, accent, .25);
        Color shade = isLight ? EffectPalette.Blend(lo, accent, .3) : EffectPalette.Blend(accent, Colors.Black, .3);
        int clouds = Scaled(width, height, 26000, 3, 16, compact);
        for (int i = 0; i < clouds; i++)
        {
            double cx = rng.NextDouble() * width, cy = height * (.08 + rng.NextDouble() * .8);
            double size = (compact ? 16 : 26) * (.7 + rng.NextDouble() * .9);
            int lobes = 4 + rng.Next(4);
            // Soft underside shadow first, then the bright lobes.
            dc.DrawEllipse(CreateGlowBrush(.5, .5, .5, .5, (WithAlpha(shade, ScaleByte(isLight ? 18 : 30, intensity)), 0), (WithAlpha(shade, 0), 1)),
                null, new Point(cx, cy + size * .35), size * 1.9, size * .55);
            for (int l = 0; l < lobes; l++)
            {
                double ox = (l - (lobes - 1) / 2.0) * size * .55 + (rng.NextDouble() - .5) * size * .3;
                double oy = -Math.Sin((l + .5) / lobes * Math.PI) * size * .45 + (rng.NextDouble() - .5) * size * .15;
                double r = size * (.45 + .35 * Math.Sin((l + .5) / lobes * Math.PI));
                dc.DrawEllipse(CreateGlowBrush(.45, .4, .5, .5,
                    (WithAlpha(puff, ScaleByte(isLight ? 70 : 46, intensity)), 0),
                    (WithAlpha(puff, ScaleByte(isLight ? 40 : 24, intensity)), .6),
                    (WithAlpha(puff, 0), 1)), null, new Point(cx + ox, cy + oy), r * 1.2, r);
            }
        }
        return group;
    }

    private static DrawingGroup BuildMountainsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(905);
        Color rock = BlendTheme(Color.FromRgb(0x5B, 0x6B, 0x86), accent, .5);
        Color far = BlendTheme(Color.FromRgb(0x94, 0xA3, 0xB8), accent, .45);
        Color snow = isLight ? Colors.White : EffectPalette.Blend(hi, Colors.White, .6);

        dc.DrawRectangle(CreateGlowBrush(.7, .35, .5, .6, (WithAlpha(hi, ScaleByte(isLight ? 30 : 26, intensity)), 0), (WithAlpha(hi, 0), 1)), null, new Rect(0, 0, width, height));
        double[] bases = compact ? [.55, .8] : [.5, .66, .86];
        for (int layer = 0; layer < bases.Length; layer++)
        {
            double t = layer / (double)Math.Max(1, bases.Length - 1);
            Color tone = EffectPalette.Blend(far, rock, t);
            double baseY = height * bases[layer];
            double peakSpan = width / (compact ? 3 : 5 + layer * 2);
            var points = new List<Point> { new(0, height + 2) };
            var peaks = new List<(Point Peak, double Spread)>();
            double x = -peakSpan * rng.NextDouble();
            while (x < width + peakSpan)
            {
                double rise = height * (compact ? .22 : .16) * (.6 + rng.NextDouble() * .7) * (1.2 - t * .4);
                double spread = peakSpan * (.4 + rng.NextDouble() * .3);
                points.Add(new Point(x, baseY));
                var peak = new Point(x + spread, baseY - rise);
                // Jagged shoulders on the way up and down.
                points.Add(new Point(x + spread * .55, baseY - rise * (.55 + rng.NextDouble() * .2)));
                points.Add(peak);
                points.Add(new Point(x + spread * 1.4, baseY - rise * (.5 + rng.NextDouble() * .2)));
                peaks.Add((peak, spread));
                x += spread * 2;
            }
            points.Add(new Point(width, height + 2));
            var fill = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
                (WithAlpha(tone, ScaleByte(isLight ? 30 + 14 * t : 40 + 18 * t, intensity)), 0),
                (WithAlpha(tone, ScaleByte(isLight ? 18 : 24, intensity)), 1));
            dc.DrawGeometry(fill, FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 30 : 22, intensity)), .7), Polygon(points));
            if (layer == 0 || (!compact && layer == 1))
            {
                var capBrush = FrozenBrush(WithAlpha(snow, ScaleByte(isLight ? 70 : 54, intensity)));
                foreach (var (peak, spread) in peaks)
                {
                    double cap = spread * .32;
                    dc.DrawGeometry(capBrush, null, Polygon([peak, new Point(peak.X + cap * .7, peak.Y + cap * .55), new Point(peak.X + cap * .25, peak.Y + cap * .4),
                        new Point(peak.X - cap * .15, peak.Y + cap * .6), new Point(peak.X - cap * .55, peak.Y + cap * .5)]));
                }
            }
        }
        return group;
    }

    private static DrawingGroup BuildOceanDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        Color sea = BlendTheme(Color.FromRgb(0x0E, 0x74, 0x90), accent, .55);
        Color deep = BlendTheme(Color.FromRgb(0x1E, 0x3A, 0x8A), isLight ? lo : accent, .55);
        Color foam = isLight ? Colors.White : EffectPalette.Blend(hi, Colors.White, .5);
        dc.DrawRectangle(CreateGlowBrush(.5, .3, .6, .4, (WithAlpha(hi, ScaleByte(isLight ? 34 : 28, intensity)), 0), (WithAlpha(hi, 0), 1)), null, new Rect(0, 0, width, height));
        int bands = compact ? 4 : 7;
        for (int b = 0; b < bands; b++)
        {
            double t = b / (double)(bands - 1);
            Color tone = EffectPalette.Blend(sea, deep, t);
            var fill = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
                (WithAlpha(tone, ScaleByte(isLight ? 20 + 10 * t : 26 + 16 * t, intensity)), 0),
                (WithAlpha(tone, ScaleByte(isLight ? 30 : 40, intensity)), 1));
            var crest = FrozenPen(WithAlpha(foam, ScaleByte((isLight ? 60 : 44) * (1 - t * .4), intensity)), compact ? .8 : 1.1);
            DrawSoftHillRidge(dc, fill, crest, width, height,
                baseY: height * (.38 + .58 * t), ampA: height * (.018 + .012 * t), freqA: 3 + b * .9, phaseA: b * 1.7,
                ampB: height * .01, out _);
        }
        // Sparkling glints on the water surface.
        var rng = new Random(733);
        var glint = FrozenBrush(WithAlpha(foam, ScaleByte(isLight ? 90 : 70, intensity)));
        int glints = Scaled(width, height, 3000, 6, 60, compact);
        for (int i = 0; i < glints; i++)
        {
            double y = height * (.42 + rng.NextDouble() * .5);
            double x = rng.NextDouble() * width;
            dc.DrawRectangle(glint, null, new Rect(x, y, 3 + rng.NextDouble() * 6, .9));
        }
        return group;
    }

    private static DrawingGroup BuildDunesDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        Color sand = BlendTheme(Color.FromRgb(0xE0, 0xA8, 0x5E), accent, .62);
        Color shadow = BlendTheme(Color.FromRgb(0xA8, 0x5F, 0x2E), isLight ? lo : accent, .6);
        Color sun = BlendTheme(Color.FromRgb(0xFB, 0xBF, 0x24), hi, .6);
        dc.DrawEllipse(CreateGlowBrush(.5, .5, .5, .5, (WithAlpha(sun, ScaleByte(isLight ? 70 : 64, intensity)), 0), (WithAlpha(sun, ScaleByte(20, intensity)), .4), (WithAlpha(sun, 0), 1)),
            null, new Point(width * .78, height * .28), height * .3, height * .3);
        int ridges = compact ? 3 : 5;
        for (int r = 0; r < ridges; r++)
        {
            double t = r / (double)(ridges - 1);
            var fill = CreateSweepBrush(new Point(0, 0), new Point(1, 1),
                (WithAlpha(sand, ScaleByte(isLight ? 34 + 14 * t : 30 + 20 * t, intensity)), 0),
                (WithAlpha(shadow, ScaleByte(isLight ? 30 + 16 * t : 36 + 22 * t, intensity)), 1));
            var crest = FrozenPen(WithAlpha(EffectPalette.Blend(sand, Colors.White, .4), ScaleByte(isLight ? 60 : 46, intensity)), 1);
            DrawSoftHillRidge(dc, fill, crest, width, height,
                baseY: height * (.5 + .44 * t), ampA: height * (.07 - .02 * t), freqA: .8 + r * .35, phaseA: r * 2.2,
                ampB: height * .02, out double[] ridge);
            // Wind ripples combed across each dune.
            var ripple = FrozenPen(WithAlpha(shadow, ScaleByte(isLight ? 30 : 34, intensity)), .6);
            for (int k = 1; k <= (compact ? 2 : 4); k++)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int s = 0; s < ridge.Length; s++)
                    {
                        var p = new Point(s / (double)(ridge.Length - 1) * width, ridge[s] + k * height * .025 + Math.Sin(s * .9 + k) * 1.2);
                        if (s == 0) ctx.BeginFigure(p, false, false); else ctx.LineTo(p, true, true);
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, ripple, geometry);
            }
        }
        return group;
    }

    private static void DrawBlossom(DrawingContext dc, Brush petal, Brush heart, Point center, double size, double turn)
    {
        bool sway = PushSway(dc, center, blade: false);
        for (int p = 0; p < 5; p++)
        {
            double a = turn + p * Math.Tau / 5;
            var m = Matrix.Identity;
            m.Rotate(a * 180 / Math.PI + 90);
            m.Translate(center.X + Math.Cos(a) * size * .55, center.Y + Math.Sin(a) * size * .55);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawEllipse(petal, null, new Point(0, 0), size * .38, size * .55);
            dc.Pop();
        }
        dc.DrawEllipse(heart, null, center, size * .18, size * .18);
        if (sway) dc.Pop();
    }

    private static DrawingGroup BuildSakuraDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(3307);
        Color bark = BlendTheme(Color.FromRgb(0x5B, 0x3A, 0x2E), isLight ? lo : accent, .65);
        Color pink = BlendTheme(Color.FromRgb(0xF9, 0xA8, 0xD4), accent, .66);
        Color deepPink = BlendTheme(Color.FromRgb(0xEC, 0x48, 0x99), accent, .6);
        var barkPen = new Pen(FrozenBrush(WithAlpha(bark, ScaleByte(isLight ? 90 : 96, intensity))), compact ? 2.2 : 3.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        barkPen.Freeze();
        var twigPen = FrozenPen(WithAlpha(bark, ScaleByte(isLight ? 76 : 80, intensity)), compact ? 1.1 : 1.6);
        var petal = FrozenBrush(WithAlpha(pink, ScaleByte(isLight ? 96 : 90, intensity)));
        var petalSoft = FrozenBrush(WithAlpha(pink, ScaleByte(isLight ? 54 : 50, intensity)));
        var heart = FrozenBrush(WithAlpha(deepPink, ScaleByte(isLight ? 110 : 100, intensity)));
        double blossom = compact ? 4.5 : 7;

        // Branches reach in from the top corners.
        var origins = compact
            ? new[] { (new Point(-4, height * .1), .25) }
            : new[] { (new Point(-6, height * .08), .28), (new Point(width + 6, height * .18), Math.PI - .35) };
        foreach (var (origin, angle) in origins)
        {
            var anchors = DrawCurvedStem(dc, barkPen, origin, Math.Min(width * .55, compact ? width * .8 : 520), angle, .35, 12);
            for (int i = 2; i < anchors.Count; i += 1)
            {
                var (pt, tan) = anchors[i];
                double side = i % 2 == 0 ? 1 : -1;
                var twig = DrawCurvedStem(dc, twigPen, pt, (compact ? 14 : 34) * (.6 + rng.NextDouble() * .6), tan + side * (.7 + rng.NextDouble() * .4), side * .3, 4);
                foreach (var (tp, _) in twig.Skip(1))
                    DrawBlossom(dc, petal, heart, new Point(tp.X + (rng.NextDouble() - .5) * 4, tp.Y + (rng.NextDouble() - .5) * 4), blossom * (.7 + rng.NextDouble() * .5), rng.NextDouble() * Math.Tau);
            }
        }
        // Drifting loose petals across the surface.
        int loose = Scaled(width, height, 5200, 6, 40, compact);
        for (int i = 0; i < loose; i++)
        {
            var at = new Point(rng.NextDouble() * width, rng.NextDouble() * height);
            bool sway = PushSway(dc, at, blade: false);
            var m = Matrix.Identity;
            m.Rotate(rng.NextDouble() * 360);
            m.Translate(at.X, at.Y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawEllipse(petalSoft, null, new Point(0, 0), blossom * .45, blossom * .25);
            dc.Pop();
            if (sway) dc.Pop();
        }
        return group;
    }

    private static DrawingGroup BuildBambooDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(612);
        Color stalk = BlendTheme(Color.FromRgb(0x65, 0xA3, 0x4B), accent, .6);
        Color leaf = BlendTheme(Color.FromRgb(0x3F, 0x8F, 0x4E), accent, .58);
        int stalks = Math.Clamp((int)(width / (compact ? 34 : 70)), 2, 18);
        for (int i = 0; i < stalks; i++)
        {
            double x = (i + .3 + rng.NextDouble() * .4) / stalks * width;
            double w = (compact ? 4 : 7) * (.75 + rng.NextDouble() * .5);
            double depth = rng.NextDouble();
            byte alpha = ScaleByte((isLight ? 34 : 40) + 26 * depth, intensity);
            var body = CreateSweepBrush(new Point(0, 0), new Point(1, 0),
                (WithAlpha(EffectPalette.Blend(stalk, Colors.White, .25), alpha), 0),
                (WithAlpha(stalk, alpha), .5),
                (WithAlpha(EffectPalette.Blend(stalk, Colors.Black, .25), alpha), 1));
            dc.DrawRoundedRectangle(body, null, new Rect(x - w / 2, -4, w, height + 8), w / 2, w / 2);
            var node = FrozenPen(WithAlpha(EffectPalette.Blend(stalk, Colors.Black, .35), ScaleByte(isLight ? 60 : 70, intensity)), 1.1);
            double segment = (compact ? 22 : 46) * (.8 + rng.NextDouble() * .4);
            var leafBrush = FrozenBrush(WithAlpha(leaf, ScaleByte((isLight ? 46 : 52) + 20 * depth, intensity)));
            for (double y = segment * rng.NextDouble(); y < height; y += segment)
            {
                dc.DrawLine(node, new Point(x - w / 2 - .5, y), new Point(x + w / 2 + .5, y));
                if (rng.NextDouble() < .55)
                {
                    double side = rng.NextDouble() < .5 ? -1 : 1;
                    int count = 2 + rng.Next(3);
                    for (int l = 0; l < count; l++)
                    {
                        double angle = side > 0 ? -.35 + l * .32 : Math.PI + .35 - l * .32;
                        DrawTaperedBlade(dc, leafBrush, null, new Point(x + side * w / 2, y), (compact ? 12 : 26) * (.7 + rng.NextDouble() * .5), angle, side * .25, compact ? 1.6 : 2.8);
                    }
                }
            }
        }
        return group;
    }

    private static DrawingGroup BuildPinesDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(271);
        Color pine = BlendTheme(Color.FromRgb(0x1F, 0x5F, 0x4A), isLight ? lo : accent, .6);
        Color mist = BlendTheme(Color.FromRgb(0x8F, 0xB8, 0xC9), accent, .5);
        dc.DrawRectangle(CreateSweepBrush(new Point(0, 0), new Point(0, 1), (WithAlpha(mist, 0), 0), (WithAlpha(mist, ScaleByte(isLight ? 26 : 22, intensity)), .7), (WithAlpha(mist, 0), 1)), null, new Rect(0, 0, width, height));
        double[] rows = compact ? [.75, .98] : [.62, .8, 1.0];
        for (int r = 0; r < rows.Length; r++)
        {
            double t = r / (double)Math.Max(1, rows.Length - 1);
            var fill = FrozenBrush(WithAlpha(EffectPalette.Blend(mist, pine, .45 + .55 * t), ScaleByte((isLight ? 26 : 32) + 30 * t, intensity)));
            double treeH = height * (compact ? .34 : .2) * (.8 + .5 * t);
            double baseY = height * rows[r];
            double step = treeH * .55;
            for (double x = -rng.NextDouble() * step; x < width + step; x += step * (.6 + rng.NextDouble() * .7))
            {
                double h = treeH * (.7 + rng.NextDouble() * .6), w = h * .42;
                var points = new List<Point>();
                int tiers = 4;
                // Stepped tiers give a pine silhouette rather than a plain triangle.
                for (int k = 0; k < tiers; k++)
                {
                    double ty = baseY - h + h * (k + 1) / tiers;
                    double tw = w * (k + 1) / tiers;
                    points.Add(new Point(x + tw * .62, ty - h * .07));
                    points.Add(new Point(x + tw, ty));
                }
                var right = points.ToList();
                var left = right.Select(p => new Point(2 * x - p.X, p.Y)).Reverse().ToList();
                dc.DrawGeometry(fill, null, Polygon([new Point(x, baseY - h), .. right, new Point(x + w * .1, baseY + 4), new Point(x - w * .1, baseY + 4), .. left]));
            }
        }
        return group;
    }

    private static DrawingGroup BuildSkylineDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(1999);
        Color tower = isLight ? EffectPalette.Blend(lo, accent, .3) : EffectPalette.Blend(accent, Colors.Black, .55);
        Color window = BlendTheme(Color.FromRgb(0xFD, 0xE6, 0x8A), hi, .65);
        dc.DrawRectangle(CreateGlowBrush(.5, 1, .8, .7, (WithAlpha(accent, ScaleByte(isLight ? 30 : 46, intensity)), 0), (WithAlpha(accent, 0), 1)), null, new Rect(0, 0, width, height));
        var star = FrozenBrush(WithAlpha(hi, ScaleByte(isLight ? 50 : 80, intensity)));
        for (int i = 0; i < Scaled(width, height, 4000, 5, 50, compact); i++)
            dc.DrawEllipse(star, null, new Point(rng.NextDouble() * width, rng.NextDouble() * height * .5), .8, .8);
        for (int row = 0; row < 2; row++)
        {
            double x = -10;
            var fill = FrozenBrush(WithAlpha(tower, ScaleByte(isLight ? 30 + 20 * row : 46 + 30 * row, intensity)));
            var lit = FrozenBrush(WithAlpha(window, ScaleByte(isLight ? 70 : 90 + 30 * row, intensity)));
            while (x < width + 10)
            {
                double bw = (compact ? 12 : 22) * (.7 + rng.NextDouble() * .9);
                double bh = height * (compact ? .35 : .22) * (.5 + rng.NextDouble() * (row == 0 ? 1.4 : 1.0));
                var rect = new Rect(x, height - bh, bw, bh + 2);
                dc.DrawRectangle(fill, null, rect);
                if (rng.NextDouble() < .3) dc.DrawRectangle(fill, null, new Rect(x + bw * .4, height - bh - bh * .12, 1.5, bh * .12));
                for (double wy = height - bh + 4; wy < height - 3; wy += compact ? 4 : 6)
                    for (double wx = x + 2.5; wx < x + bw - 3; wx += compact ? 3.5 : 5)
                        if (rng.NextDouble() < .28) dc.DrawRectangle(lit, null, new Rect(wx, wy, compact ? 1.5 : 2.2, compact ? 1.5 : 2.6));
                x += bw + (rng.NextDouble() < .2 ? rng.NextDouble() * 8 : 0);
            }
        }
        return group;
    }

    private static DrawingGroup BuildSnowfallDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(5150);
        Color flake = isLight ? EffectPalette.Blend(accent, lo, .3) : EffectPalette.Blend(hi, Colors.White, .6);
        int count = Scaled(width, height, 900, 20, 420, compact);
        for (int i = 0; i < count; i++)
        {
            double depth = rng.NextDouble();
            var center = new Point(rng.NextDouble() * width, rng.NextDouble() * height);
            byte alpha = ScaleByte((isLight ? 50 : 60) + 80 * depth, intensity);
            if (depth > .9)
                DrawFourPointStar(dc, FrozenBrush(WithAlpha(flake, alpha)), null, center, .5 + depth * .3, rng.NextDouble() * 90);
            else
                dc.DrawEllipse(FrozenBrush(WithAlpha(flake, alpha)), null, center, .7 + depth * 1.8, .7 + depth * 1.8);
        }
        return group;
    }

    private static DrawingGroup BuildLanternsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(808);
        Color warm = BlendTheme(Color.FromRgb(0xFB, 0x92, 0x3C), accent, .66);
        Color core = BlendTheme(Color.FromRgb(0xFD, 0xE6, 0x8A), hi, .7);
        int count = Scaled(width, height, 9000, 4, 40, compact);
        for (int i = 0; i < count; i++)
        {
            double depth = rng.NextDouble();
            double size = (compact ? 5 : 9) * (.55 + depth * .9);
            var c = new Point(rng.NextDouble() * width, rng.NextDouble() * height);
            dc.DrawEllipse(CreateGlowBrush(.5, .5, .5, .5, (WithAlpha(warm, ScaleByte((isLight ? 40 : 60) * (.5 + depth), intensity)), 0), (WithAlpha(warm, 0), 1)),
                null, c, size * 2.6, size * 2.6);
            var body = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
                (WithAlpha(core, ScaleByte((isLight ? 90 : 110) * (.5 + depth * .5), intensity)), 0),
                (WithAlpha(warm, ScaleByte((isLight ? 90 : 120) * (.5 + depth * .5), intensity)), 1));
            dc.DrawRoundedRectangle(body, null, new Rect(c.X - size * .5, c.Y - size * .7, size, size * 1.4), size * .35, size * .35);
        }
        return group;
    }

    private static DrawingGroup BuildRainDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(4242);
        Color drop = isLight ? EffectPalette.Blend(accent, lo, .4) : EffectPalette.Blend(hi, accent, .3);
        int count = Scaled(width, height, 1100, 16, 320, compact);
        for (int i = 0; i < count; i++)
        {
            double depth = rng.NextDouble();
            var pen = FrozenPen(WithAlpha(drop, ScaleByte((isLight ? 30 : 36) + 60 * depth, intensity)), .6 + depth * .7);
            double x = rng.NextDouble() * width, y = rng.NextDouble() * height, len = (compact ? 6 : 10) + 14 * depth;
            dc.DrawLine(pen, new Point(x, y), new Point(x - len * .12, y + len));
        }
        return group;
    }

    private static Brush BuildPlaidBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double size = compact ? 32 : 48;
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            Color second = EffectPalette.Blend(accent, isLight ? lo : hi, .45);
            var wide = FrozenBrush(WithAlpha(accent, ScaleByte(isLight ? 22 : 26, intensity)));
            var mid = FrozenBrush(WithAlpha(second, ScaleByte(isLight ? 20 : 22, intensity)));
            var thin = FrozenBrush(WithAlpha(hi, ScaleByte(isLight ? 60 : 36, intensity)));
            dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, size, size));
            foreach (var (offset, span, brush) in new[] { (0.0, .3, wide), (.45, .12, mid), (.7, .04, thin), (.8, .04, thin) })
            {
                dc.DrawRectangle(brush, null, new Rect(offset * size, 0, span * size, size));
                dc.DrawRectangle(brush, null, new Rect(0, offset * size, size, span * size));
            }
        }
        return TiledDrawingBrush(group, size, size);
    }

    private static Brush BuildChevronBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double w = compact ? 18 : 28, h = w * .5;
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, w, h));
            var pen = new Pen(FrozenBrush(WithAlpha(accent, ScaleByte(isLight ? 40 : 44, intensity))), compact ? 1.4 : 2.2) { LineJoin = PenLineJoin.Miter };
            pen.Freeze();
            var glint = FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 50 : 30, intensity)), .6);
            for (double dy = -h; dy <= h; dy += h)
            {
                dc.DrawLine(pen, new Point(0, dy + h * .25), new Point(w / 2, dy + h * .75));
                dc.DrawLine(pen, new Point(w / 2, dy + h * .75), new Point(w, dy + h * .25));
                dc.DrawLine(glint, new Point(0, dy + h * .55), new Point(w / 2, dy + h * 1.05));
                dc.DrawLine(glint, new Point(w / 2, dy + h * 1.05), new Point(w, dy + h * .55));
            }
        }
        return TiledDrawingBrush(group, w, h);
    }
}
