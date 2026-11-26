using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum HeaderEffectKind { Aurora, Sparkles, PulseWave }

/// <summary>Renders palette-adaptive animated effects across the title bar and conversation header.</summary>
public sealed class HeaderEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(HeaderEffectKind), typeof(HeaderEffect),
        new FrameworkPropertyMetadata(HeaderEffectKind.Aurora, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry StarShape = FrozenGeometry("M0,-5.5 Q0,0 5.5,0 Q0,0 0,5.5 Q0,0 -5.5,0 Q0,0 0,-5 Z");
    private static readonly SparkleSeed[] Sparkles = CreateSparkles();
    private Brush[]? ribbonFills;
    private Brush[]? secondaryRibbonFills;
    private Brush[]? sparkleFills;
    private Pen[]? shimmerPens;

    public HeaderEffectKind Kind { get => (HeaderEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        ribbonFills = null;
        secondaryRibbonFills = null;
        sparkleFills = null;
        shimmerPens = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 32 || height < 16) return;
        double intensity = Math.Clamp(Intensity, .1, 1);

        switch (Kind)
        {
            case HeaderEffectKind.Aurora: DrawAurora(dc, width, height, intensity); break;
            case HeaderEffectKind.Sparkles: DrawSparkles(dc, width, height, intensity); break;
            case HeaderEffectKind.PulseWave: DrawPulseWave(dc, width, height, intensity); break;
        }
    }

    private void DrawAurora(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .30);
        secondaryRibbonFills ??= BuildRibbonBrushes(EffectPalette.Blend(Tint, Color.FromRgb(120, 220, 210), .35), brightShift: .18);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Progress * Math.Tau;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.26 * intensity + .08)],
            baseY: height * .46, amplitude: Math.Min(12, height * .22), thickness: height * .52, phase: time, frequency: 2.2);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.22 * intensity + .06)],
            baseY: height * .62, amplitude: Math.Min(10, height * .18), thickness: height * .40, phase: -time * 1.4 + 1.1, frequency: 3.1);

        int motes = Math.Clamp((int)(width / 68), 4, 14);
        for (int i = 0; i < motes; i++)
        {
            double u = (i * .61803398875 + Progress * (.4 + (i % 3) * .2)) % 1;
            double edge = Math.Min(1, Math.Min(u, 1 - u) * 5);
            double blink = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(time * 2 + i * 1.7), 2);
            double x = 18 + (width - 36) * u;
            double y = height * (.24 + .54 * ((i * .41421356) % 1)) + Math.Sin(time + i) * 3.5;
            double radius = 1.2 + (i % 3) * .45;
            var fill = sparkleFills[EffectPalette.OpacityIndex(edge * blink * (.30 + .55 * intensity))];
            dc.DrawEllipse(fill, null, new Point(x, y), radius, radius);
        }
    }

    private static void DrawRibbon(DrawingContext dc, double width, double height, Brush fill,
        double baseY, double amplitude, double thickness, double phase, double frequency)
    {
        const int steps = 48;
        var wave = new StreamGeometry();
        using (var ctx = wave.Open())
        {
            var top = new Point[steps + 1];
            var bottom = new Point[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double x = t * width;
                double envelope = Math.Sin(t * Math.PI);
                double centerY = baseY + Math.Sin(t * Math.Tau * frequency + phase) * amplitude * envelope;
                double half = thickness * (.45 + .55 * envelope) * .5;
                top[s] = new Point(x, Math.Max(0, centerY - half));
                bottom[s] = new Point(x, Math.Min(height, centerY + half));
            }
            ctx.BeginFigure(top[0], isFilled: true, isClosed: true);
            for (int s = 1; s <= steps; s++) ctx.LineTo(top[s], isStroked: false, isSmoothJoin: true);
            for (int s = steps; s >= 0; s--) ctx.LineTo(bottom[s], isStroked: false, isSmoothJoin: true);
        }
        wave.Freeze();
        dc.DrawGeometry(fill, null, wave);
    }

    private void DrawSparkles(DrawingContext dc, double width, double height, double intensity)
    {
        sparkleFills ??= BuildSparkleBrushes(Tint);
        shimmerPens ??= BuildShimmerPens(Tint);

        double lineSweep = (Progress * 2.2) % 1;
        double sweepX = lineSweep * (width + 120) - 60;
        var sweepPen = shimmerPens[EffectPalette.OpacityIndex(.22 * intensity)];
        dc.DrawLine(sweepPen, new Point(Math.Max(0, sweepX - 50), height - 1), new Point(Math.Min(width, sweepX + 50), height - 1));

        int count = Math.Clamp((int)(width / 34 * (.45 + .55 * intensity)), 5, Sparkles.Length);
        for (int i = 0; i < count; i++)
        {
            var seed = Sparkles[i];
            double u = (seed.Start + Progress * seed.Speed) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
            double pulse = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin((Progress * 6 + seed.Start) * Math.Tau), 2);
            double x = 10 + (width - 20) * u;
            double y = 6 + (height - 12) * seed.Lane + Math.Sin((u * 2 + seed.Lane) * Math.Tau) * 2.5;
            double scale = seed.Scale * (.65 + .35 * pulse);

            var matrix = Matrix.Identity;
            matrix.Scale(scale, scale);
            matrix.Rotate(seed.Tilt + Progress * 90 * (i % 2 == 0 ? 1 : -1));
            matrix.Translate(x, y);

            dc.PushTransform(new MatrixTransform(matrix));
            var fill = sparkleFills[EffectPalette.OpacityIndex(fade * pulse * (.35 + .60 * intensity))];
            dc.DrawGeometry(fill, null, StarShape);
            dc.Pop();
        }
    }

    private void DrawPulseWave(DrawingContext dc, double width, double height, double intensity)
    {
        shimmerPens ??= BuildShimmerPens(Tint);
        sparkleFills ??= BuildSparkleBrushes(Tint);
        double time = Progress * Math.Tau * 3;
        double baseY = height - 8;

        for (int layer = 0; layer < 2; layer++)
        {
            const int steps = 48;
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps;
                    double env = Math.Sin(t * Math.PI);
                    double amp = (layer == 0 ? 12 : 7) * (.35 + .65 * intensity) * env;
                    double y = baseY - (layer == 0 ? 8 : 3) + Math.Sin(t * Math.Tau * (3 + layer * 2) - time * (1 + layer * .5)) * amp;
                    var pt = new Point(12 + t * (width - 24), Math.Clamp(y, 4, height - 2));
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            line.Freeze();
            dc.DrawGeometry(null, shimmerPens[EffectPalette.OpacityIndex((layer == 0 ? .45 : .25) * (.4 + .6 * intensity))], line);
        }

        double sweep = (Progress * 3) % 1;
        double pulseX = 16 + sweep * (width - 32);
        double pulseY = baseY - 8 + Math.Sin(sweep * Math.Tau * 3 - time) * 12 * (.35 + .65 * intensity);
        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(.55 + .40 * intensity)], null, new Point(pulseX, Math.Clamp(pulseY, 4, height - 2)), 2.5, 2.5);
    }

    private static Brush[] BuildRibbonBrushes(Color tint, double brightShift)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var core = EffectPalette.Blend(tint, Colors.White, brightShift);
        for (int i = 0; i < brushes.Length; i++)
        {
            var transparent = Color.FromArgb(0, core.R, core.G, core.B);
            var mid = EffectPalette.WithOpacity(core, i);
            var edge = EffectPalette.WithOpacity(tint, i / 2);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                [
                    new GradientStop(transparent, 0),
                    new GradientStop(edge, .2),
                    new GradientStop(mid, .5),
                    new GradientStop(edge, .8),
                    new GradientStop(transparent, 1)
                ]
            };
            brush.Freeze();
            brushes[i] = brush;
        }
        return brushes;
    }

    private static Brush[] BuildSparkleBrushes(Color tint)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .42);
        for (int i = 0; i < brushes.Length; i++)
        {
            var brush = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            brush.Freeze();
            brushes[i] = brush;
        }
        return brushes;
    }

    private static Pen[] BuildShimmerPens(Color tint)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .35);
        for (int i = 0; i < pens.Length; i++)
        {
            var brush = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            brush.Freeze();
            var pen = new Pen(brush, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            pens[i] = pen;
        }
        return pens;
    }

    private static Geometry FrozenGeometry(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }

    private static SparkleSeed[] CreateSparkles()
    {
        var random = new Random(724);
        var list = new SparkleSeed[18];
        for (int i = 0; i < list.Length; i++)
            list[i] = new SparkleSeed(
                Start: random.NextDouble(),
                Lane: .15 + ((i * .61803398875) % 1) * .70,
                Scale: .45 + random.NextDouble() * .55,
                Speed: 1 + (i % 3) * .5,
                Tilt: random.Next(0, 45));
        return list;
    }

    private sealed record SparkleSeed(double Start, double Lane, double Scale, double Speed, int Tilt);
}
