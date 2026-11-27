using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum RailEffectKind { Beacon, Stream, Stardust, Scan, Aurora, Pulse, Plasma, Cascade, WingTrail, ArcReactor }

/// <summary>Renders palette-adaptive animated effects inside the left navigation rail.</summary>
public sealed class RailEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(RailEffectKind), typeof(RailEffect),
        new FrameworkPropertyMetadata(RailEffectKind.Beacon, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry MicroStar = FrozenGeometry("M0,-3.5 Q0,0 3.5,0 Q0,0 0,3.5 Q0,0 -3.5,0 Q0,0 0,-3.5 Z");
    private static readonly StarSeed[] Stars = CreateStars();
    private static readonly Dictionary<Color, BitmapSource[]> OrbSpriteCache = [];
    private BitmapSource[]? orbSprites;
    private BitmapSource[]? altOrbSprites;
    private Brush[]? fills;
    private Brush[]? ribbonFills;
    private Brush[]? altRibbonFills;
    private Pen[]? pens;
    private Pen[]? altPens;

    public RailEffectKind Kind { get => (RailEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        orbSprites = null;
        altOrbSprites = null;
        fills = null;
        ribbonFills = null;
        altRibbonFills = null;
        pens = null;
        altPens = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 20 || height < 24) return;
        double intensity = Math.Clamp(Intensity, .1, 1);

        switch (Kind)
        {
            case RailEffectKind.Beacon: DrawBeacon(dc, width, height, intensity); break;
            case RailEffectKind.Stream: DrawStream(dc, width, height, intensity); break;
            case RailEffectKind.Stardust: DrawStardust(dc, width, height, intensity); break;
            case RailEffectKind.Scan: DrawScan(dc, width, height, intensity); break;
            case RailEffectKind.Aurora: DrawAurora(dc, width, height, intensity); break;
            case RailEffectKind.Pulse: DrawPulse(dc, width, height, intensity); break;
            case RailEffectKind.Plasma: DrawPlasma(dc, width, height, intensity); break;
            case RailEffectKind.Cascade: DrawCascade(dc, width, height, intensity); break;
            case RailEffectKind.WingTrail: DrawWingTrail(dc, width, height, intensity); break;
            case RailEffectKind.ArcReactor: DrawArcReactor(dc, width, height, intensity); break;
        }
    }

    private void DrawBeacon(DrawingContext dc, double width, double height, double intensity)
    {
        orbSprites ??= BuildOrbSprites(Tint);
        pens ??= BuildPens(Tint, 1.5);

        double pulse = .5 + .5 * Math.Sin(Time * Math.Tau * 3);
        double y = height * .5 + Math.Sin(Time * Math.Tau) * Math.Min(height * .26, 65);
        var center = new Point(width / 2, y);
        double radius = 16 + 7 * pulse;
        dc.DrawImage(orbSprites[EffectPalette.OpacityIndex(.32 + .48 * intensity)], new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        dc.DrawEllipse(null, pens[EffectPalette.OpacityIndex((.25 + .45 * intensity) * (1 - pulse * .5))], center, 8 + 10 * pulse, 8 + 10 * pulse);
    }

    private void DrawStream(DrawingContext dc, double width, double height, double intensity)
    {
        orbSprites ??= BuildOrbSprites(Tint);
        pens ??= BuildPens(Tint, 1.5);

        double x = width - 2;
        for (int s = 0; s < 2; s++)
        {
            double u = (Time * (2.5 + s) + s * .5) % 1;
            double headY = (1 - u) * (height + 80) - 40;
            double tailY = Math.Min(height, headY + 48);
            var pen = pens[EffectPalette.OpacityIndex(.40 + .50 * intensity)];
            dc.DrawLine(pen, new Point(x, Math.Max(0, headY)), new Point(x, Math.Max(0, tailY)));
            if (headY >= -8 && headY <= height + 8)
                dc.DrawImage(orbSprites[EffectPalette.OpacityIndex(.45 + .50 * intensity)], new Rect(x - 8, headY - 8, 16, 16));
        }
    }

    private void DrawStardust(DrawingContext dc, double width, double height, double intensity)
    {
        fills ??= BuildFills(Tint);
        int count = Math.Clamp((int)(height / 24 * (.4 + .6 * intensity)), 5, Stars.Length);
        for (int i = 0; i < count; i++)
        {
            var star = Stars[i];
            double u = (star.Start + Time * star.Speed) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 7);
            double twinkle = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin((Time * 5 + star.Start) * Math.Tau), 2);
            double x = 8 + (width - 16) * star.Lane + Math.Sin((u * 2 + star.Start) * Math.Tau) * 3;
            double y = (1 - u) * (height - 12) + 6;
            double s = star.Scale * (.7 + .3 * twinkle);

            var m = Matrix.Identity;
            m.Scale(s, s);
            m.Rotate(Time * 120 + i * 25);
            m.Translate(x, y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(fills[EffectPalette.OpacityIndex(fade * twinkle * (.38 + .58 * intensity))], null, MicroStar);
            dc.Pop();
        }
    }

    private void DrawScan(DrawingContext dc, double width, double height, double intensity)
    {
        pens ??= BuildPens(Tint, 1.5);
        fills ??= BuildFills(Tint);

        double u = (Time * 3.5) % 1;
        double fade = Math.Min(1, Math.Min(u, 1 - u) * 8);
        double y = u * height;
        var pen = pens[EffectPalette.OpacityIndex(fade * (.42 + .50 * intensity))];
        dc.DrawLine(pen, new Point(4, y), new Point(width - 4, y));
        dc.DrawRectangle(fills[EffectPalette.OpacityIndex(fade * .14 * intensity)], null, new Rect(4, Math.Max(0, y - 10), width - 8, 10));

        for (double tickY = 14; tickY < height - 10; tickY += 32)
        {
            double dist = Math.Max(0, 1 - Math.Abs(tickY - y) / 40);
            if (dist <= .05) continue;
            dc.DrawLine(pens[EffectPalette.OpacityIndex(dist * (.35 + .55 * intensity))], new Point(3, tickY), new Point(7, tickY));
            dc.DrawLine(pens[EffectPalette.OpacityIndex(dist * (.35 + .55 * intensity))], new Point(width - 7, tickY), new Point(width - 3, tickY));
        }
    }

    private void DrawAurora(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildVerticalRibbons(Tint, .34);
        altRibbonFills ??= BuildVerticalRibbons(ShiftColor(Tint), .24);
        fills ??= BuildFills(Tint);

        double time = Time * Math.Tau;
        DrawVerticalRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.30 * intensity + .08)],
            baseX: width * .46, amplitude: width * .24, thickness: width * .56, phase: time, frequency: 2.0);
        DrawVerticalRibbon(dc, width, height, altRibbonFills[EffectPalette.OpacityIndex(.24 * intensity + .06)],
            baseX: width * .56, amplitude: width * .22, thickness: width * .44, phase: -time * 1.3 + 1.4, frequency: 2.8);

        int motes = Math.Clamp((int)(height / 52), 4, 12);
        for (int i = 0; i < motes; i++)
        {
            double u = (i * .61803398875 + Time * (.45 + (i % 3) * .18)) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
            double blink = .3 + .7 * Math.Pow(.5 + .5 * Math.Sin(time * 2.4 + i * 1.8), 2);
            double x = width * (.24 + .52 * ((i * .41421356) % 1)) + Math.Sin(time + i) * 3;
            double y = (1 - u) * height;
            dc.DrawEllipse(fills[EffectPalette.OpacityIndex(fade * blink * (.38 + .55 * intensity))], null, new Point(x, y), 1.4, 1.4);
        }
    }

    private void DrawPulse(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildVerticalRibbons(Tint, .26);
        pens ??= BuildPens(Tint, 1.5);
        altPens ??= BuildPens(ShiftColor(Tint), 1.1);
        orbSprites ??= BuildOrbSprites(Tint);

        double time = Time * Math.Tau * 2.2;
        double midX = width * .50;
        DrawVerticalRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.22 * intensity + .05)],
            baseX: midX, amplitude: width * .20, thickness: width * .46, phase: -time * .6, frequency: 2.4);

        for (int layer = 0; layer < 2; layer++)
        {
            const int steps = 40;
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps;
                    double env = Math.Sin(t * Math.PI);
                    double amp = width * (layer == 0 ? .28 : .18) * (.45 + .55 * intensity) * env;
                    double x = midX + Math.Sin(t * Math.Tau * (2.6 + layer * 1.4) - time * (1 + layer * .4)) * amp;
                    var pt = new Point(Math.Clamp(x, 3, width - 3), t * height);
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            line.Freeze();
            var pen = (layer == 0 ? pens : altPens)[EffectPalette.OpacityIndex((layer == 0 ? .55 : .34) * (.45 + .55 * intensity))];
            dc.DrawGeometry(null, pen, line);
        }

        double u = (Time * 2.2) % 1;
        double envU = Math.Sin(u * Math.PI);
        double oy = u * height;
        double ox = midX + Math.Sin(u * Math.Tau * 2.6 - time) * width * .28 * (.45 + .55 * intensity) * envU;
        dc.DrawImage(orbSprites[EffectPalette.OpacityIndex(envU * (.55 + .40 * intensity))], new Rect(ox - 8, oy - 8, 16, 16));
    }

    private void DrawPlasma(DrawingContext dc, double width, double height, double intensity)
    {
        pens ??= BuildPens(Tint, 1.45);
        altPens ??= BuildPens(ShiftColor(Tint), 1.35);
        orbSprites ??= BuildOrbSprites(Tint);
        altOrbSprites ??= BuildOrbSprites(ShiftColor(Tint));

        double time = Time * Math.Tau * 1.8;
        double midX = width * .50;
        const int steps = 44;

        for (int strand = 0; strand < 2; strand++)
        {
            double phaseShift = strand * Math.PI;
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps;
                    double env = Math.Sin(t * Math.PI);
                    double x = midX + Math.Sin(t * Math.Tau * 2.5 - time + phaseShift) * width * .28 * env;
                    var pt = new Point(Math.Clamp(x, 3, width - 3), t * height);
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            geom.Freeze();
            var pen = (strand == 0 ? pens : altPens)[EffectPalette.OpacityIndex(.52 * (.45 + .55 * intensity))];
            dc.DrawGeometry(null, pen, geom);
        }

        // Glowing plasma nodes gliding along the helix
        for (int n = 0; n < 4; n++)
        {
            double u = (Time * 1.5 + n * .25) % 1;
            double fade = Math.Sin(u * Math.PI);
            double ny = u * height;
            double nx = midX + Math.Sin(u * Math.Tau * 2.5 - time + (n % 2) * Math.PI) * width * .28 * fade;
            var sprites = n % 2 == 0 ? orbSprites : altOrbSprites;
            dc.DrawImage(sprites[EffectPalette.OpacityIndex(fade * (.45 + .50 * intensity))], new Rect(nx - 7, ny - 7, 14, 14));
        }
    }

    private static readonly Lazy<Pen[]> FreedomCyanPens = new(() => BuildPens(Color.FromRgb(0x38, 0xBD, 0xF8), 1.5));
    private static readonly Lazy<Pen[]> FreedomCrimsonPens = new(() => BuildPens(Color.FromRgb(0xE1, 0x1D, 0x48), 1.25));
    private static readonly Lazy<Pen[]> StarkGoldPens = new(() => BuildPens(Color.FromRgb(0xF5, 0xB7, 0x00), 1.35));
    private static readonly Lazy<BitmapSource[]> ArcCyanSprites = new(() => BuildOrbSprites(Color.FromRgb(0x38, 0xBD, 0xF8)));
    private static readonly Lazy<BitmapSource[]> StarkGoldSprites = new(() => BuildOrbSprites(Color.FromRgb(0xF5, 0xB7, 0x00)));

    private void DrawWingTrail(DrawingContext dc, double width, double height, double intensity)
    {
        var cyanPens = FreedomCyanPens.Value;
        var crimsonPens = FreedomCrimsonPens.Value;
        var goldPens = StarkGoldPens.Value;
        var cyanOrbs = ArcCyanSprites.Value;

        int beams = Math.Clamp((int)(height / 54), 4, 8);
        for (int i = 0; i < beams; i++)
        {
            double u = (i * .31 + Time * (1.7 + (i % 2) * .45)) % 1;
            double fade = Math.Sin(u * Math.PI);
            double x = width * (.24 + .52 * ((i * .618) % 1));
            double headY = (1 - u) * height;
            double len = 36 + (i % 3) * 14;
            var pen = (i % 3 == 0 ? crimsonPens : cyanPens)[EffectPalette.OpacityIndex(fade * (.42 + .52 * intensity))];
            dc.DrawLine(pen, new Point(x, Math.Max(4, headY)), new Point(Math.Clamp(x + (i % 2 == 0 ? -8 : 8), 4, width - 4), Math.Min(height - 4, headY + len)));
            dc.DrawImage(cyanOrbs[EffectPalette.OpacityIndex(fade * (.45 + .50 * intensity))], new Rect(x - 7, headY - 7, 14, 14));
        }

        double vy = height * .14;
        double midX = width * .50;
        dc.DrawLine(goldPens[EffectPalette.OpacityIndex(.38 + .42 * intensity)], new Point(midX - 11, vy - 5), new Point(midX, vy + 3));
        dc.DrawLine(goldPens[EffectPalette.OpacityIndex(.38 + .42 * intensity)], new Point(midX + 11, vy - 5), new Point(midX, vy + 3));
    }

    private void DrawArcReactor(DrawingContext dc, double width, double height, double intensity)
    {
        var cyanPens = FreedomCyanPens.Value;
        var goldPens = StarkGoldPens.Value;
        var crimsonPens = FreedomCrimsonPens.Value;
        var cyanOrbs = ArcCyanSprites.Value;
        var goldOrbs = StarkGoldSprites.Value;

        double cx = width * .50;
        double cy = height * .50 + Math.Sin(Time * Math.Tau) * Math.Min(height * .26, 70);
        double pulse = .5 + .5 * Math.Sin(Time * Math.Tau * 3);

        dc.DrawLine(crimsonPens[EffectPalette.OpacityIndex(.24 + .32 * intensity)], new Point(width - 2.5, 16), new Point(width - 2.5, height - 16));
        dc.DrawImage(cyanOrbs[EffectPalette.OpacityIndex(.40 + .50 * intensity)], new Rect(cx - 17, cy - 17, 34, 34));
        dc.DrawEllipse(null, cyanPens[EffectPalette.OpacityIndex((.42 + .50 * intensity) * (1 - pulse * .35))], new Point(cx, cy), 10 + 7 * pulse, 10 + 7 * pulse);
        dc.DrawEllipse(null, goldPens[EffectPalette.OpacityIndex(.34 + .46 * intensity)], new Point(cx, cy), 6.5, 6.5);

        for (int n = 0; n < 3; n++)
        {
            double u = (Time * 1.4 + n / 3.0) % 1;
            double ny = (1 - u) * height;
            double fade = Math.Sin(u * Math.PI);
            dc.DrawImage(goldOrbs[EffectPalette.OpacityIndex(fade * (.36 + .48 * intensity))], new Rect(cx - 5.5, ny - 5.5, 11, 11));
        }
    }

    private void DrawCascade(DrawingContext dc, double width, double height, double intensity)
    {
        pens ??= BuildPens(Tint, 1.6);
        altPens ??= BuildPens(ShiftColor(Tint), 1.25);
        orbSprites ??= BuildOrbSprites(Tint);
        altOrbSprites ??= BuildOrbSprites(ShiftColor(Tint));

        int drops = Math.Clamp((int)(height / 36 * (.5 + .5 * intensity)), 4, 9);
        for (int i = 0; i < drops; i++)
        {
            double lane = .18 + ((i * .61803398875) % 1) * .64;
            double u = (i * .37 + Time * (1.8 + (i % 3) * .65)) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
            double x = width * lane;
            double headY = u * height;
            double tailLen = Math.Clamp(height * .16, 14, 58);

            for (int seg = 0; seg < 4; seg++)
            {
                double f0 = seg / 4.0, f1 = (seg + 1) / 4.0;
                double y0 = Math.Max(0, headY - tailLen * f0);
                double y1 = Math.Max(0, headY - tailLen * f1);
                var pen = (i % 2 == 0 ? pens : altPens)[EffectPalette.OpacityIndex(fade * (1 - f0 * .8) * (.44 + .48 * intensity))];
                dc.DrawLine(pen, new Point(x, y0), new Point(x, y1));
            }

            var sprites = i % 2 == 0 ? orbSprites : altOrbSprites;
            dc.DrawImage(sprites[EffectPalette.OpacityIndex(fade * (.48 + .48 * intensity))], new Rect(x - 6.5, headY - 6.5, 13, 13));
        }
    }

    private static void DrawVerticalRibbon(DrawingContext dc, double width, double height, Brush fill,
        double baseX, double amplitude, double thickness, double phase, double frequency)
    {
        const int steps = 40;
        var wave = new StreamGeometry();
        using (var ctx = wave.Open())
        {
            var left = new Point[steps + 1];
            var right = new Point[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double y = t * height;
                double env = Math.Sin(t * Math.PI);
                double cx = baseX + Math.Sin(t * Math.Tau * frequency + phase) * amplitude * env;
                double half = thickness * (.42 + .58 * env) * .5;
                left[s] = new Point(Math.Max(0, cx - half), y);
                right[s] = new Point(Math.Min(width, cx + half), y);
            }
            ctx.BeginFigure(left[0], isFilled: true, isClosed: true);
            for (int s = 1; s <= steps; s++) ctx.LineTo(left[s], isStroked: false, isSmoothJoin: true);
            for (int s = steps; s >= 0; s--) ctx.LineTo(right[s], isStroked: false, isSmoothJoin: true);
        }
        wave.Freeze();
        dc.DrawGeometry(fill, null, wave);
    }

    private static Color ShiftColor(Color tint) => Color.FromRgb(
        (byte)((tint.G * .55 + 135) % 255),
        (byte)((tint.B * .55 + 115) % 255),
        (byte)Math.Min(255, tint.R * .45 + 195));

    private static Brush[] BuildVerticalRibbons(Color tint, double brightShift)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var core = EffectPalette.Blend(tint, Colors.White, brightShift);
        for (int i = 0; i < brushes.Length; i++)
        {
            var clear = Color.FromArgb(0, core.R, core.G, core.B);
            var mid = EffectPalette.WithOpacity(core, i);
            var edge = EffectPalette.WithOpacity(tint, i / 2);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                [
                    new GradientStop(clear, 0),
                    new GradientStop(edge, .2),
                    new GradientStop(mid, .5),
                    new GradientStop(edge, .8),
                    new GradientStop(clear, 1)
                ]
            };
            brush.Freeze();
            brushes[i] = brush;
        }
        return brushes;
    }

    private static Brush[] BuildFills(Color tint)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .38);
        for (int i = 0; i < brushes.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            brushes[i] = b;
        }
        return brushes;
    }

    private static Pen[] BuildPens(Color tint, double thickness)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .35);
        for (int i = 0; i < pens.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            var p = new Pen(b, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            pens[i] = p;
        }
        return pens;
    }

    private static BitmapSource[] BuildOrbSprites(Color tint)
    {
        if (OrbSpriteCache.TryGetValue(tint, out var cached)) return cached;
        if (OrbSpriteCache.Count >= 16) OrbSpriteCache.Clear();

        const int size = 32, stride = size * 4;
        var sprites = new BitmapSource[EffectPalette.OpacityLevels];
        for (int index = 0; index < sprites.Length; index++)
        {
            var pixels = new byte[size * stride];
            double strength = index / (double)(sprites.Length - 1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = (x + .5) / size * 2 - 1, dy = (y + .5) / size * 2 - 1;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double glow = Math.Max(0, 1 - d);
                    double core = Math.Clamp(1 - d * 4, 0, 1);
                    byte alpha = (byte)(255 * strength * Math.Min(1, glow * glow * .72 + core * .45));
                    var color = EffectPalette.Blend(tint, Colors.White, core * .5);
                    int offset = y * stride + x * 4;
                    pixels[offset] = (byte)(color.B * alpha / 255);
                    pixels[offset + 1] = (byte)(color.G * alpha / 255);
                    pixels[offset + 2] = (byte)(color.R * alpha / 255);
                    pixels[offset + 3] = alpha;
                }
            }
            var sprite = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            sprite.Freeze();
            sprites[index] = sprite;
        }
        return OrbSpriteCache[tint] = sprites;
    }

    private static Geometry FrozenGeometry(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }

    private static StarSeed[] CreateStars()
    {
        var random = new Random(519);
        var list = new StarSeed[16];
        for (int i = 0; i < list.Length; i++)
            list[i] = new StarSeed((i * .61803398875) % 1, random.NextDouble(), .6 + random.NextDouble() * .6, 1 + (i % 2));
        return list;
    }

    private sealed record StarSeed(double Lane, double Start, double Scale, int Speed);
}
