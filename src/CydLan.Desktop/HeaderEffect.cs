using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum HeaderEffectKind
{
    Aurora,
    Sparkles,
    PulseWave,
    // Title bar effects
    Shimmer,
    Comet,
    Equalizer,
    Laser,
    TitleAurora,
    TitleWave,
    // Chat header effects
    Prism,
    Ripples,
    Circuit,
    Starlight,
    Nebula,
    Silk,
    Plasma
}

/// <summary>Renders palette-adaptive animated effects across the full top title bar and the conversation chat header.</summary>
public sealed class HeaderEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(HeaderEffectKind), typeof(HeaderEffect),
        new FrameworkPropertyMetadata(HeaderEffectKind.Aurora, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry StarShape = FrozenGeometry("M0,-5.5 Q0,0 5.5,0 Q0,0 0,5.5 Q0,0 -5.5,0 Q0,0 0,-5 Z");
    private static readonly Geometry DiamondShape = FrozenGeometry("M0,-4.5 L3.2,0 L0,4.5 L-3.2,0 Z");
    private static readonly SparkleSeed[] Sparkles = CreateSparkles();
    private Brush[]? ribbonFills;
    private Brush[]? secondaryRibbonFills;
    private Brush[]? tertiaryRibbonFills;
    private Brush[]? sparkleFills;
    private Brush[]? altFills;
    private Pen[]? shimmerPens;
    private Pen[]? altPens;
    private Pen[]? finePens;

    public HeaderEffectKind Kind { get => (HeaderEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        ribbonFills = null;
        secondaryRibbonFills = null;
        tertiaryRibbonFills = null;
        sparkleFills = null;
        altFills = null;
        shimmerPens = null;
        altPens = null;
        finePens = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 32 || height < 14) return;
        double intensity = Math.Clamp(Intensity, .1, 1);

        switch (Kind)
        {
            case HeaderEffectKind.Aurora: DrawAurora(dc, width, height, intensity); break;
            case HeaderEffectKind.Sparkles: DrawSparkles(dc, width, height, intensity); break;
            case HeaderEffectKind.PulseWave: DrawPulseWave(dc, width, height, intensity); break;
            case HeaderEffectKind.Shimmer: DrawShimmer(dc, width, height, intensity); break;
            case HeaderEffectKind.Comet: DrawComet(dc, width, height, intensity); break;
            case HeaderEffectKind.Equalizer: DrawEqualizer(dc, width, height, intensity); break;
            case HeaderEffectKind.Laser: DrawLaser(dc, width, height, intensity); break;
            case HeaderEffectKind.TitleAurora: DrawTitleAurora(dc, width, height, intensity); break;
            case HeaderEffectKind.TitleWave: DrawTitleWave(dc, width, height, intensity); break;
            case HeaderEffectKind.Prism: DrawPrism(dc, width, height, intensity); break;
            case HeaderEffectKind.Ripples: DrawRipples(dc, width, height, intensity); break;
            case HeaderEffectKind.Circuit: DrawCircuit(dc, width, height, intensity); break;
            case HeaderEffectKind.Starlight: DrawStarlight(dc, width, height, intensity); break;
            case HeaderEffectKind.Nebula: DrawNebula(dc, width, height, intensity); break;
            case HeaderEffectKind.Silk: DrawSilk(dc, width, height, intensity); break;
            case HeaderEffectKind.Plasma: DrawPlasma(dc, width, height, intensity); break;
        }
    }

    private void DrawAurora(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .30);
        secondaryRibbonFills ??= BuildRibbonBrushes(EffectPalette.Blend(Tint, Color.FromRgb(120, 220, 210), .35), brightShift: .18);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.26 * intensity + .08)],
            baseY: height * .46, amplitude: Math.Min(12, height * .22), thickness: height * .52, phase: time, frequency: 2.2);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.22 * intensity + .06)],
            baseY: height * .62, amplitude: Math.Min(10, height * .18), thickness: height * .40, phase: -time * 1.4 + 1.1, frequency: 3.1);

        DrawDriftingMotes(dc, width, height, intensity, sparkleFills, time);
    }

    private void DrawTitleAurora(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .34);
        secondaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(Tint), brightShift: .24);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.28 * intensity + .08)],
            baseY: height * .46, amplitude: height * .20, thickness: height * .62, phase: time, frequency: 2.4);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.22 * intensity + .06)],
            baseY: height * .58, amplitude: height * .16, thickness: height * .48, phase: -time * 1.3 + .9, frequency: 3.4);

        DrawDriftingMotes(dc, width, height, intensity * .85, sparkleFills, time);
    }

    private void DrawTitleWave(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .28);
        shimmerPens ??= BuildShimmerPens(Tint, 1.45);
        altPens ??= BuildShimmerPens(ShiftColor(Tint), 1.05);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau * 2;
        double midY = height * .50;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.18 * intensity + .05)],
            baseY: midY, amplitude: height * .20, thickness: height * .46, phase: -time, frequency: 2.6);

        for (int layer = 0; layer < 2; layer++)
        {
            const int steps = 56;
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps;
                    double env = Math.Sin(t * Math.PI);
                    double amp = height * (layer == 0 ? .24 : .16) * (.45 + .55 * intensity) * env;
                    double y = midY + Math.Sin(t * Math.Tau * (2.6 + layer * 1.4) - time * (1 + layer * .4)) * amp;
                    var pt = new Point(t * width, Math.Clamp(y, 2, height - 2));
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            line.Freeze();
            var pen = (layer == 0 ? shimmerPens : altPens)[EffectPalette.OpacityIndex((layer == 0 ? .48 : .30) * (.45 + .55 * intensity))];
            dc.DrawGeometry(null, pen, line);
        }

        double sweep = (Time * 2.2) % 1;
        double px = sweep * width;
        double envP = Math.Sin(sweep * Math.PI);
        double py = midY + Math.Sin(sweep * Math.Tau * 2.6 - time) * height * .24 * (.45 + .55 * intensity) * envP;
        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(envP * (.55 + .40 * intensity))], null, new Point(px, py), 2.3, 2.3);
    }

    private void DrawSparkles(DrawingContext dc, double width, double height, double intensity)
    {
        sparkleFills ??= BuildSparkleBrushes(Tint);
        shimmerPens ??= BuildShimmerPens(Tint, 1.5);

        double lineSweep = (Time * 2.2) % 1;
        double sweepX = lineSweep * (width + 120) - 60;
        var sweepPen = shimmerPens[EffectPalette.OpacityIndex(.22 * intensity)];
        dc.DrawLine(sweepPen, new Point(Math.Max(0, sweepX - 50), height - 1), new Point(Math.Min(width, sweepX + 50), height - 1));

        int count = Math.Clamp((int)(width / 34 * (.45 + .55 * intensity)), 5, Sparkles.Length);
        for (int i = 0; i < count; i++)
        {
            var seed = Sparkles[i];
            double u = (seed.Start + Time * seed.Speed) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
            double pulse = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin((Time * 6 + seed.Start) * Math.Tau), 2);
            double x = 10 + (width - 20) * u;
            double y = 6 + (height - 12) * seed.Lane + Math.Sin((u * 2 + seed.Lane) * Math.Tau) * 2.5;
            double scale = seed.Scale * (.65 + .35 * pulse);

            var matrix = Matrix.Identity;
            matrix.Scale(scale, scale);
            matrix.Rotate(seed.Tilt + Time * 90 * (i % 2 == 0 ? 1 : -1));
            matrix.Translate(x, y);

            dc.PushTransform(new MatrixTransform(matrix));
            var fill = sparkleFills[EffectPalette.OpacityIndex(fade * pulse * (.35 + .60 * intensity))];
            dc.DrawGeometry(fill, null, StarShape);
            dc.Pop();
        }
    }

    private void DrawPulseWave(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .24);
        shimmerPens ??= BuildShimmerPens(Tint, 1.55);
        altPens ??= BuildShimmerPens(ShiftColor(Tint), 1.15);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau * 2.5;
        double midY = height * .50;

        // Soft glowing ribbon bed rolling through the vertical center like Header aurora
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.18 * intensity + .05)],
            baseY: midY, amplitude: Math.Min(14, height * .22), thickness: height * .42, phase: -time * .65, frequency: 2.8);

        for (int layer = 0; layer < 2; layer++)
        {
            const int steps = 56;
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps;
                    double env = Math.Sin(t * Math.PI);
                    double amp = Math.Min(16, height * (layer == 0 ? .26 : .17)) * (.45 + .55 * intensity) * env;
                    double y = midY + Math.Sin(t * Math.Tau * (3 + layer * 1.8) - time * (1 + layer * .45)) * amp;
                    var pt = new Point(t * width, Math.Clamp(y, 4, height - 4));
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            line.Freeze();
            var pen = (layer == 0 ? shimmerPens : altPens)[EffectPalette.OpacityIndex((layer == 0 ? .52 : .32) * (.45 + .55 * intensity))];
            dc.DrawGeometry(null, pen, line);
        }

        double sweep = (Time * 2.5) % 1;
        double envS = Math.Sin(sweep * Math.PI);
        double pulseX = sweep * width;
        double pulseY = midY + Math.Sin(sweep * Math.Tau * 3 - time) * Math.Min(16, height * .26) * (.45 + .55 * intensity) * envS;
        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(envS * (.25 + .22 * intensity))], null, new Point(pulseX, pulseY), 7.5, 7.5);
        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(envS * (.65 + .35 * intensity))], null, new Point(pulseX, pulseY), 2.8, 2.8);
    }

    private void DrawShimmer(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .48);
        secondaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(Tint), brightShift: .30);
        shimmerPens ??= BuildShimmerPens(Tint, 1.3);

        for (int b = 0; b < 2; b++)
        {
            double u = (Time * (b == 0 ? 1.8 : 1.1) + b * .48) % 1;
            double bandW = Math.Clamp(width * (b == 0 ? .28 : .20), 54, 240);
            double x = u * (width + bandW) - bandW;
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                double slant = height * .65;
                ctx.BeginFigure(new Point(x + slant, 0), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(x + bandW + slant, 0), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(x + bandW, height), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(x, height), isStroked: false, isSmoothJoin: false);
            }
            geom.Freeze();
            var fills = b == 0 ? ribbonFills : secondaryRibbonFills;
            dc.DrawGeometry(fills[EffectPalette.OpacityIndex((b == 0 ? .34 : .20) * intensity)], null, geom);
        }

        double rimU = (Time * 1.8) % 1;
        double rimX = rimU * width;
        double fade = Math.Sin(rimU * Math.PI);
        dc.DrawLine(shimmerPens[EffectPalette.OpacityIndex(.45 * intensity * fade)],
            new Point(Math.Max(0, rimX - 48), height - 1), new Point(Math.Min(width, rimX + 48), height - 1));
    }

    private void DrawComet(DrawingContext dc, double width, double height, double intensity)
    {
        shimmerPens ??= BuildShimmerPens(Tint, 1.8);
        altPens ??= BuildShimmerPens(ShiftColor(Tint), 1.3);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        for (int i = 0; i < 3; i++)
        {
            double dir = i == 1 ? -1 : 1;
            double speed = 1.6 + i * .55;
            double u = (Time * speed + i * .34) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 5.5);
            double headX = dir > 0 ? u * width : (1 - u) * width;
            double laneY = height * (.26 + i * .24) + Math.Sin(u * Math.Tau * 2 + i) * 2.2;
            double tailLen = Math.Clamp(width * (.16 + i * .04), 28, 140);

            for (int seg = 0; seg < 5; seg++)
            {
                double f0 = seg / 5.0, f1 = (seg + 1) / 5.0;
                double x0 = Math.Clamp(headX - dir * tailLen * f0, 0, width);
                double x1 = Math.Clamp(headX - dir * tailLen * f1, 0, width);
                double segAlpha = fade * (1 - f0 * .85) * (.48 + .42 * intensity);
                var pen = (i == 1 ? altPens : shimmerPens)[EffectPalette.OpacityIndex(segAlpha)];
                dc.DrawLine(pen, new Point(x0, laneY), new Point(x1, laneY));
            }

            double r = 2.1 + (i == 0 ? .7 : 0);
            dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(fade * (.62 + .35 * intensity))], null, new Point(headX, laneY), r, r);
        }
    }

    private void DrawEqualizer(DrawingContext dc, double width, double height, double intensity)
    {
        sparkleFills ??= BuildSparkleBrushes(Tint);
        altFills ??= BuildSparkleBrushes(ShiftColor(Tint));

        int bars = Math.Clamp((int)(width / 10), 10, 84);
        double step = width / bars;
        double barW = Math.Max(2.2, step * .52);
        double time = Time * Math.Tau * 4;

        for (int i = 0; i < bars; i++)
        {
            double u = i / (double)Math.Max(1, bars - 1);
            double envelope = Math.Pow(Math.Sin(u * Math.PI), .65);
            double wave = .5 + .5 * Math.Sin(time + i * .65) * Math.Cos(time * .5 - i * .31);
            double maxH = Math.Max(6, height - 8);
            double h = Math.Max(2.5, maxH * (.18 + .78 * wave) * (.35 + .65 * envelope) * (.45 + .55 * intensity));
            double x = i * step + (step - barW) * .5;
            double alpha = (.22 + .50 * intensity) * (.35 + .65 * envelope);
            var fill = (i % 3 == 0 ? altFills : sparkleFills)[EffectPalette.OpacityIndex(alpha)];
            dc.DrawRoundedRectangle(fill, null, new Rect(x, height - h - 2, barW, h), barW * .45, barW * .45);
        }
    }

    private void DrawLaser(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .30);
        shimmerPens ??= BuildShimmerPens(Tint, 1.5);
        altPens ??= BuildShimmerPens(ShiftColor(Tint), 1.15);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double u1 = (Time * 2.2) % 1;
        double u2 = 1 - ((Time * 1.7 + .35) % 1);
        double y1 = height * .36;
        double y2 = height * .66;

        // Soft feathered edge-to-edge horizon rails
        dc.DrawRectangle(ribbonFills[EffectPalette.OpacityIndex(.16 * intensity)], null, new Rect(0, y1 - .8, width, 1.6));
        dc.DrawRectangle(ribbonFills[EffectPalette.OpacityIndex(.14 * intensity)], null, new Rect(0, y2 - .7, width, 1.4));

        // Active bright laser spans + glowing focus nodes with smooth edge fade
        double span = Math.Clamp(width * .24, 34, 180);
        double x1 = u1 * width;
        double x2 = u2 * width;
        double f1 = Math.Min(1, Math.Min(u1, 1 - u1) * 6);
        double f2 = Math.Min(1, Math.Min(u2, 1 - u2) * 6);

        dc.DrawLine(shimmerPens[EffectPalette.OpacityIndex(.72 * intensity * f1)],
            new Point(Math.Max(0, x1 - span * .5), y1), new Point(Math.Min(width, x1 + span * .5), y1));
        dc.DrawLine(altPens[EffectPalette.OpacityIndex(.65 * intensity * f2)],
            new Point(Math.Max(0, x2 - span * .45), y2), new Point(Math.Min(width, x2 + span * .45), y2));

        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(.85 * intensity * f1)], null, new Point(x1, y1), 2.8, 2.8);
        dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(.75 * intensity * f2)], null, new Point(x2, y2), 2.3, 2.3);
    }

    // ---------------- Lush Aurora-Caliber Rolling Chat Header Effects ----------------

    private void DrawPrism(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .36);
        secondaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(Tint), brightShift: .28);
        tertiaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(ShiftColor(Tint)), brightShift: .32);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        // Three interlocking prismatic spectral ribbons rolling gracefully across the center
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.24 * intensity + .07)],
            baseY: height * .42, amplitude: Math.Min(13, height * .22), thickness: height * .48, phase: time * 1.1, frequency: 1.9);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.22 * intensity + .06)],
            baseY: height * .56, amplitude: Math.Min(11, height * .20), thickness: height * .42, phase: -time * 1.3 + 2.1, frequency: 2.7);
        DrawRibbon(dc, width, height, tertiaryRibbonFills[EffectPalette.OpacityIndex(.16 * intensity + .05)],
            baseY: height * .50, amplitude: Math.Min(9, height * .16), thickness: height * .30, phase: time * 1.7 + 4.0, frequency: 3.5);

        // Drifting crystal diamond glints evenly spread across the header
        int glints = Math.Clamp((int)(width / 85), 4, 12);
        for (int i = 0; i < glints; i++)
        {
            double u = (i / (double)glints + Time * (.32 + (i % 3) * .12)) % 1;
            double edge = Math.Min(1, Math.Min(u, 1 - u) * 5);
            double x = 14 + u * (width - 28);
            double y = height * (.24 + .52 * ((i * .61803398875) % 1)) + Math.Sin(time + i * 1.4) * 4;
            double blink = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(time * 2.2 + i * 1.9), 2);
            var m = Matrix.Identity;
            double sc = .55 + .45 * blink;
            m.Scale(sc, sc);
            m.Translate(x, y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(sparkleFills[EffectPalette.OpacityIndex(edge * blink * (.32 + .52 * intensity))], null, DiamondShape);
            dc.Pop();
        }
    }

    private void DrawRipples(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .24);
        shimmerPens ??= BuildShimmerPens(Tint, 1.35);
        altPens ??= BuildShimmerPens(ShiftColor(Tint), 1.05);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        // Soft water-swell horizon ribbon rolling gently behind the centered ripples
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.18 * intensity + .05)],
            baseY: height * .52, amplitude: Math.Min(9, height * .16), thickness: height * .46, phase: time * .8, frequency: 1.8);

        int sources = width > 320 ? 3 : 2;
        for (int s = 0; s < sources; s++)
        {
            double drift = Math.Sin(time + s * 2.1) * Math.Min(28, width * .06);
            double cx = width * (sources == 3 ? .22 + s * .28 : .32 + s * .36) + drift;
            double cy = height * .50 + Math.Cos(time * 1.2 + s) * height * .08;
            for (int r = 0; r < 3; r++)
            {
                double phase = (Time * 1.5 + s * .33 + r * .33) % 1;
                double fade = Math.Sin(phase * Math.PI) * (1 - phase * .3);
                double radX = 10 + phase * Math.Min(width * .26, 145);
                double radY = 4 + phase * Math.Min(height * .46, 30);
                var pen = (r % 2 == 0 ? shimmerPens : altPens)[EffectPalette.OpacityIndex(fade * (.28 + .48 * intensity))];
                dc.DrawEllipse(null, pen, new Point(cx, cy), radX, radY);
            }
            dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(.32 * intensity)], null, new Point(cx, cy), 1.8, 1.8);
        }
    }

    private void DrawCircuit(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .24);
        finePens ??= BuildShimmerPens(Tint, 1.0);
        shimmerPens ??= BuildShimmerPens(Tint, 1.75);
        altFills ??= BuildSparkleBrushes(ShiftColor(Tint));
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        // Soft luminous data-stream ribbon rolling across the middle
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.16 * intensity + .04)],
            baseY: height * .50, amplitude: Math.Min(8, height * .12), thickness: height * .44, phase: -time * .9, frequency: 2.0);

        double[] lanes = [height * .30, height * .50, height * .70];
        for (int i = 0; i < lanes.Length; i++)
        {
            double y = lanes[i];
            double jogX = width * (.28 + i * .22);
            double jogY = i == 1 ? y : (i == 0 ? y + 6 : y - 6);

            var track = new StreamGeometry();
            using (var ctx = track.Open())
            {
                ctx.BeginFigure(new Point(8, y), isFilled: false, isClosed: false);
                ctx.LineTo(new Point(jogX - 6, y), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(jogX, jogY), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(width - 8, jogY), isStroked: true, isSmoothJoin: false);
            }
            track.Freeze();
            dc.DrawGeometry(null, finePens[EffectPalette.OpacityIndex(.22 * intensity)], track);
            dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(.38 * intensity)], null, new Point(jogX, jogY), 2.1, 2.1);

            for (int pIdx = 0; pIdx < 2; pIdx++)
            {
                double u = (Time * (1.8 + i * .35) + i * .31 + pIdx * .5) % 1;
                double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
                double px = 8 + u * (width - 16);
                double py = px < jogX - 6 ? y : px > jogX ? jogY : y + (jogY - y) * ((px - (jogX - 6)) / 6.0);
                double pulseLen = Math.Clamp(width * .11, 18, 72);
                dc.DrawLine(shimmerPens[EffectPalette.OpacityIndex(.68 * intensity * fade)],
                    new Point(Math.Max(8, px - pulseLen), py), new Point(px, py));
                var headFill = (i == 1 ? altFills : sparkleFills)[EffectPalette.OpacityIndex(.82 * intensity * fade)];
                dc.DrawEllipse(headFill, null, new Point(px, py), 2.4, 2.4);
            }
        }
    }

    private void DrawStarlight(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .22);
        sparkleFills ??= BuildSparkleBrushes(Tint);
        finePens ??= BuildShimmerPens(Tint, .9);

        double time = Time * Math.Tau;
        // Soft Milky-Way cosmic ribbon rolling gently behind the constellation
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.18 * intensity + .05)],
            baseY: height * .50, amplitude: Math.Min(10, height * .18), thickness: height * .52, phase: time * .6, frequency: 1.5);

        int count = Math.Clamp((int)(width / 44), 6, 18);
        var pts = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double baseU = (i + .5) / count;
            double u = (baseU + Time * (.14 + (i % 3) * .04)) % 1;
            double x = 10 + u * (width - 20);
            double lane = .18 + ((i * .61803398875) % 1) * .64;
            double y = height * lane + Math.Sin(time + i * 1.3) * 3.5;
            pts[i] = new Point(x, Math.Clamp(y, 5, height - 5));
        }

        double maxDistSq = Math.Pow(Math.Clamp(width * .20, 42, 120), 2);
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                double dx = pts[i].X - pts[j].X, dy = pts[i].Y - pts[j].Y;
                double d2 = dx * dx + dy * dy;
                if (d2 < maxDistSq)
                {
                    double alpha = (1 - Math.Sqrt(d2 / maxDistSq)) * (.24 + .32 * intensity);
                    dc.DrawLine(finePens[EffectPalette.OpacityIndex(alpha)], pts[i], pts[j]);
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            double edge = Math.Min(1, Math.Min(pts[i].X / width, 1 - pts[i].X / width) * 6);
            double twinkle = .35 + .65 * Math.Pow(.5 + .5 * Math.Sin(time * 2.6 + i * 2.1), 2);
            double r = 1.4 + (i % 3) * .55;
            dc.DrawEllipse(sparkleFills[EffectPalette.OpacityIndex(edge * twinkle * (.45 + .50 * intensity))], null, pts[i], r, r);
        }
    }

    private void DrawNebula(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .34);
        secondaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(Tint), brightShift: .24);
        tertiaryRibbonFills ??= BuildRibbonBrushes(EffectPalette.Blend(Tint, Color.FromRgb(180, 120, 255), .45), brightShift: .22);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.26 * intensity + .08)],
            baseY: height * .44, amplitude: Math.Min(14, height * .24), thickness: height * .64, phase: time * .9, frequency: 1.6);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.24 * intensity + .06)],
            baseY: height * .56, amplitude: Math.Min(13, height * .22), thickness: height * .56, phase: -time * 1.2 + 1.4, frequency: 2.3);
        DrawRibbon(dc, width, height, tertiaryRibbonFills[EffectPalette.OpacityIndex(.20 * intensity + .05)],
            baseY: height * .50, amplitude: Math.Min(10, height * .18), thickness: height * .42, phase: time * 1.6 + 2.8, frequency: 3.0);

        DrawDriftingMotes(dc, width, height, intensity * 1.1, sparkleFills, time * 1.1);
    }

    private void DrawSilk(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .38);
        secondaryRibbonFills ??= BuildRibbonBrushes(EffectPalette.Blend(Tint, Colors.White, .28), brightShift: .45);
        shimmerPens ??= BuildShimmerPens(Tint, 1.1);

        double time = Time * Math.Tau;
        DrawRibbonWithCrest(dc, width, height,
            ribbonFills[EffectPalette.OpacityIndex(.25 * intensity + .07)],
            shimmerPens[EffectPalette.OpacityIndex(.38 * intensity)],
            baseY: height * .44, amplitude: Math.Min(13, height * .22), thickness: height * .46, phase: time, frequency: 1.8);
        DrawRibbonWithCrest(dc, width, height,
            secondaryRibbonFills[EffectPalette.OpacityIndex(.20 * intensity + .06)],
            shimmerPens[EffectPalette.OpacityIndex(.28 * intensity)],
            baseY: height * .58, amplitude: Math.Min(11, height * .19), thickness: height * .38, phase: -time * 1.25 + 1.6, frequency: 2.6);
    }

    private void DrawPlasma(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonFills ??= BuildRibbonBrushes(Tint, brightShift: .32);
        secondaryRibbonFills ??= BuildRibbonBrushes(ShiftColor(Tint), brightShift: .36);
        shimmerPens ??= BuildShimmerPens(Tint, 1.35);
        sparkleFills ??= BuildSparkleBrushes(Tint);

        double time = Time * Math.Tau * 1.4;
        DrawRibbon(dc, width, height, ribbonFills[EffectPalette.OpacityIndex(.27 * intensity + .08)],
            baseY: height * .48, amplitude: Math.Min(15, height * .26), thickness: height * .48, phase: time, frequency: 2.8);
        DrawRibbon(dc, width, height, secondaryRibbonFills[EffectPalette.OpacityIndex(.23 * intensity + .06)],
            baseY: height * .52, amplitude: Math.Min(14, height * .24), thickness: height * .40, phase: -time * 1.35 + .7, frequency: 3.6);

        // Glowing braided energy core thread running through the vertical center
        const int steps = 56;
        var core = new StreamGeometry();
        using (var ctx = core.Open())
        {
            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double env = Math.Sin(t * Math.PI);
                double y = height * .50
                    + Math.Sin(t * Math.Tau * 2.8 + time) * Math.Min(12, height * .20) * env
                    + Math.Cos(t * Math.Tau * 4.5 - time * 1.3) * Math.Min(5, height * .09) * env;
                var pt = new Point(t * width, Math.Clamp(y, 4, height - 4));
                if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
            }
        }
        core.Freeze();
        dc.DrawGeometry(null, shimmerPens[EffectPalette.OpacityIndex(.46 * intensity)], core);
        DrawDriftingMotes(dc, width, height, intensity, sparkleFills, time);
    }

    private static void DrawDriftingMotes(DrawingContext dc, double width, double height, double intensity, Brush[] fills, double time)
    {
        int motes = Math.Clamp((int)(width / 68), 4, 16);
        for (int i = 0; i < motes; i++)
        {
            double u = (i * .61803398875 + (time / Math.Tau) * (.4 + (i % 3) * .2)) % 1;
            double edge = Math.Min(1, Math.Min(u, 1 - u) * 5);
            double blink = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(time * 2 + i * 1.7), 2);
            double x = 14 + (width - 28) * u;
            double y = height * (.22 + .56 * ((i * .41421356) % 1)) + Math.Sin(time + i) * 3.5;
            double radius = 1.2 + (i % 3) * .45;
            var fill = fills[EffectPalette.OpacityIndex(edge * blink * (.30 + .55 * intensity))];
            dc.DrawEllipse(fill, null, new Point(x, Math.Clamp(y, 3, height - 3)), radius, radius);
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

    private static void DrawRibbonWithCrest(DrawingContext dc, double width, double height, Brush fill, Pen crestPen,
        double baseY, double amplitude, double thickness, double phase, double frequency)
    {
        const int steps = 48;
        var wave = new StreamGeometry();
        var crest = new StreamGeometry();
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
        using (var ctx = wave.Open())
        {
            ctx.BeginFigure(top[0], isFilled: true, isClosed: true);
            for (int s = 1; s <= steps; s++) ctx.LineTo(top[s], isStroked: false, isSmoothJoin: true);
            for (int s = steps; s >= 0; s--) ctx.LineTo(bottom[s], isStroked: false, isSmoothJoin: true);
        }
        wave.Freeze();
        dc.DrawGeometry(fill, null, wave);

        using (var ctx = crest.Open())
        {
            ctx.BeginFigure(top[0], isFilled: false, isClosed: false);
            for (int s = 1; s <= steps; s++) ctx.LineTo(top[s], isStroked: true, isSmoothJoin: true);
        }
        crest.Freeze();
        dc.DrawGeometry(null, crestPen, crest);
    }

    private static Color ShiftColor(Color tint) => Color.FromRgb(
        (byte)((tint.G * .55 + 135) % 255),
        (byte)((tint.B * .55 + 115) % 255),
        (byte)Math.Min(255, tint.R * .45 + 195));

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

    private static Pen[] BuildShimmerPens(Color tint, double thickness)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .35);
        for (int i = 0; i < pens.Length; i++)
        {
            var brush = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            brush.Freeze();
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
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
