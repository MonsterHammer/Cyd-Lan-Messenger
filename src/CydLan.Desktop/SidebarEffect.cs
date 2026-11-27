using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum SidebarEffectKind { Motes, EdgeGlow, Prism, Sweep, Aurora, Nebula, Waves, Ripples, Circuit, BeamWings, HudMatrix }

/// <summary>Renders palette-adaptive animated effects inside the side panel without reducing text legibility.</summary>
public sealed class SidebarEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SidebarEffectKind), typeof(SidebarEffect),
        new FrameworkPropertyMetadata(SidebarEffectKind.Motes, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly MoteSeed[] Motes = CreateMotes();
    private static readonly Dictionary<Color, BitmapSource[]> MoteSpriteCache = [];
    private BitmapSource[]? moteSprites;
    private BitmapSource[]? altMoteSprites;
    private Brush[]? glowWashes;
    private Brush[]? prismWashes;
    private Brush[]? ribbonWashes;
    private Brush[]? altRibbonWashes;
    private Brush[]? softFills;
    private Pen[]? glowPens;
    private Pen[]? finePens;
    private Pen[]? altPens;

    public SidebarEffectKind Kind { get => (SidebarEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        moteSprites = null;
        altMoteSprites = null;
        glowWashes = null;
        prismWashes = null;
        ribbonWashes = null;
        altRibbonWashes = null;
        softFills = null;
        glowPens = null;
        finePens = null;
        altPens = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 24 || height < 24) return;
        double intensity = Math.Clamp(Intensity, .1, 1);

        switch (Kind)
        {
            case SidebarEffectKind.Motes: DrawMotes(dc, width, height, intensity); break;
            case SidebarEffectKind.EdgeGlow: DrawEdgeGlow(dc, width, height, intensity); break;
            case SidebarEffectKind.Prism: DrawPrism(dc, width, height, intensity); break;
            case SidebarEffectKind.Sweep: DrawSweep(dc, width, height, intensity); break;
            case SidebarEffectKind.Aurora: DrawAurora(dc, width, height, intensity); break;
            case SidebarEffectKind.Nebula: DrawNebula(dc, width, height, intensity); break;
            case SidebarEffectKind.Waves: DrawWaves(dc, width, height, intensity); break;
            case SidebarEffectKind.Ripples: DrawRipples(dc, width, height, intensity); break;
            case SidebarEffectKind.Circuit: DrawCircuit(dc, width, height, intensity); break;
            case SidebarEffectKind.BeamWings: DrawBeamWings(dc, width, height, intensity); break;
            case SidebarEffectKind.HudMatrix: DrawHudMatrix(dc, width, height, intensity); break;
        }
    }

    private void DrawMotes(DrawingContext dc, double width, double height, double intensity)
    {
        moteSprites ??= BuildMoteSprites(Tint);
        int count = Math.Clamp((int)(width * height / 6200 * (.35 + .65 * intensity)), 6, Motes.Length);
        for (int i = 0; i < count; i++)
        {
            var mote = Motes[i];
            double rise = (mote.Start + Time * mote.Speed) % 1;
            double sway = Math.Sin((rise * 2 + mote.Lane) * Math.Tau) * 10;
            double x = 12 + (width - 24) * mote.Lane + sway;
            double y = (1 - rise) * (height + 16) - 8;
            double fade = Math.Min(1, Math.Min(rise, 1 - rise) * 7);
            double blink = .35 + .65 * Math.Pow(.5 + .5 * Math.Cos((Time * 3 + mote.Start) * Math.Tau), 2);
            double radius = 10 * mote.Size;
            var sprite = moteSprites[EffectPalette.OpacityIndex(fade * blink * (.24 + .52 * intensity))];
            dc.DrawImage(sprite, new Rect(x - radius, y - radius, radius * 2, radius * 2));
        }
    }

    private void DrawEdgeGlow(DrawingContext dc, double width, double height, double intensity)
    {
        glowWashes ??= BuildGlowWashes(Tint);
        glowPens ??= BuildGlowPens(Tint, 2.0);
        moteSprites ??= BuildMoteSprites(Tint);

        double breathe = .45 + .55 * Math.Sin(Time * Math.Tau * 2);
        double washWidth = Math.Min(36, width * .16);
        var wash = glowWashes[EffectPalette.OpacityIndex((.10 + .18 * breathe) * intensity)];
        dc.DrawRectangle(wash, null, new Rect(width - washWidth, 0, washWidth, height));

        double travel = (Time * 2.5) % 1;
        double beamCenter = travel * (height + 120) - 60;
        double beamHalf = Math.Min(65, height * .25);
        double xEdge = width - 1.5;

        var beamPen = glowPens[EffectPalette.OpacityIndex((.38 + .50 * intensity) * (.7 + .3 * breathe))];
        dc.DrawLine(beamPen,
            new Point(xEdge, Math.Max(0, beamCenter - beamHalf)),
            new Point(xEdge, Math.Min(height, beamCenter + beamHalf)));

        if (height > 120)
        {
            double headerPulse = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(Time * Math.Tau * 2), 2);
            var seamPen = glowPens[EffectPalette.OpacityIndex(.22 * headerPulse * intensity)];
            dc.DrawLine(seamPen, new Point(16, 73.5), new Point(width - 16, 73.5));
        }

        double headY = Math.Clamp(beamCenter, 4, height - 4);
        var bead = moteSprites[EffectPalette.OpacityIndex(.35 + .55 * intensity)];
        dc.DrawImage(bead, new Rect(xEdge - 9, headY - 9, 18, 18));
    }

    private void DrawPrism(DrawingContext dc, double width, double height, double intensity)
    {
        prismWashes ??= BuildPrismWashes(Tint);
        double u = (Time * 2) % 1;
        double bandY = u * (height + 160) - 80;
        double bandH = Math.Min(130, height * .45);
        var fill = prismWashes[EffectPalette.OpacityIndex(.22 + .36 * intensity)];
        dc.DrawRectangle(fill, null, new Rect(0, bandY - bandH / 2, width, bandH));

        double waveX = (.5 + .5 * Math.Sin(Time * Math.Tau * 2)) * width;
        var accentWash = prismWashes[EffectPalette.OpacityIndex(.12 + .22 * intensity)];
        dc.DrawEllipse(accentWash, null, new Point(waveX, height * .35), width * .55, height * .30);
    }

    private void DrawSweep(DrawingContext dc, double width, double height, double intensity)
    {
        glowPens ??= BuildGlowPens(Tint, 2.0);
        moteSprites ??= BuildMoteSprites(Tint);

        double u = (Time * 3) % 1;
        double fade = Math.Min(1, Math.Min(u, 1 - u) * 8);
        double y = u * height;
        var pen = glowPens[EffectPalette.OpacityIndex(fade * (.34 + .52 * intensity))];
        dc.DrawLine(pen, new Point(10, y), new Point(width - 10, y));

        for (double rowY = 74; rowY < height - 52; rowY += 46)
        {
            double proximity = Math.Max(0, 1 - Math.Abs(rowY - y) / 48);
            if (proximity <= .05) continue;
            var tickPen = glowPens[EffectPalette.OpacityIndex(proximity * (.22 + .38 * intensity))];
            dc.DrawLine(tickPen, new Point(14, rowY), new Point(width - 14, rowY));
        }

        double dotX = 16 + ((Time * 5) % 1) * (width - 32);
        dc.DrawImage(moteSprites[EffectPalette.OpacityIndex(fade * (.35 + .50 * intensity))], new Rect(dotX - 7, y - 7, 14, 14));
    }

    private void DrawAurora(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonWashes ??= BuildVerticalRibbons(Tint, .32);
        altRibbonWashes ??= BuildVerticalRibbons(ShiftColor(Tint), .22);
        softFills ??= BuildSolidFills(Tint);

        double time = Time * Math.Tau;
        DrawVerticalRibbon(dc, width, height, ribbonWashes[EffectPalette.OpacityIndex(.24 * intensity + .06)],
            baseX: width * .42, amplitude: width * .24, thickness: width * .52, phase: time, frequency: 1.8);
        DrawVerticalRibbon(dc, width, height, altRibbonWashes[EffectPalette.OpacityIndex(.20 * intensity + .05)],
            baseX: width * .60, amplitude: width * .22, thickness: width * .44, phase: -time * 1.25 + 1.5, frequency: 2.5);

        int count = Math.Clamp((int)(height / 58), 5, 14);
        for (int i = 0; i < count; i++)
        {
            double u = (i * .61803398875 + Time * (.35 + (i % 3) * .16)) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
            double blink = .28 + .72 * Math.Pow(.5 + .5 * Math.Sin(time * 2.2 + i * 1.7), 2);
            double x = width * (.16 + .68 * ((i * .41421356) % 1)) + Math.Sin(time + i) * 7;
            double y = (1 - u) * height;
            dc.DrawEllipse(softFills[EffectPalette.OpacityIndex(fade * blink * (.30 + .50 * intensity))], null, new Point(x, y), 1.4, 1.4);
        }
    }

    private void DrawNebula(DrawingContext dc, double width, double height, double intensity)
    {
        moteSprites ??= BuildMoteSprites(Tint);
        altMoteSprites ??= BuildMoteSprites(ShiftColor(Tint));
        softFills ??= BuildSolidFills(Tint);

        double time = Time * Math.Tau;
        int clouds = height > 200 ? 6 : 4;
        for (int i = 0; i < clouds; i++)
        {
            double angle = time * (.55 + (i % 3) * .22) + i * 1.45;
            double cx = width * (.22 + .56 * ((i * .61803398875) % 1)) + Math.Sin(angle) * width * .16;
            double cy = height * ((i + .5) / clouds) + Math.Cos(angle * .85) * Math.Min(36, height * .14);
            double rx = Math.Min(width * .55, 118) * (.72 + .28 * Math.Sin(time * 1.3 + i));
            double ry = Math.Min(height * .32, 110) * (.72 + .28 * Math.Cos(time * 1.1 - i));
            double alpha = (.16 + .24 * intensity) * (.75 + .25 * Math.Sin(time * 1.7 + i * 2.1));
            var sprites = i % 2 == 0 ? moteSprites : altMoteSprites;
            dc.DrawImage(sprites[EffectPalette.OpacityIndex(alpha)], new Rect(cx - rx, cy - ry, rx * 2, ry * 2));
        }

        int stars = Math.Clamp((int)(height / 45), 6, 16);
        for (int i = 0; i < stars; i++)
        {
            double x = 10 + (width - 20) * ((i * .61803398875) % 1);
            double y = 10 + (height - 20) * ((i * .38196601125 + Time * .12) % 1);
            double fade = Math.Min(1, Math.Min(y / height, 1 - y / height) * 6);
            double twinkle = .2 + .8 * Math.Pow(.5 + .5 * Math.Sin(time * 3.1 + i * 2.3), 2);
            dc.DrawEllipse(softFills[EffectPalette.OpacityIndex(fade * twinkle * (.32 + .52 * intensity))], null, new Point(x, y), 1.35, 1.35);
        }
    }

    private void DrawWaves(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonWashes ??= BuildVerticalRibbons(Tint, .36);
        altRibbonWashes ??= BuildVerticalRibbons(EffectPalette.Blend(Tint, Colors.White, .28), .44);
        finePens ??= BuildGlowPens(Tint, 1.15);
        altPens ??= BuildGlowPens(ShiftColor(Tint), 1.0);

        double time = Time * Math.Tau * 1.3;
        DrawVerticalRibbonWithCrest(dc, width, height,
            ribbonWashes[EffectPalette.OpacityIndex(.22 * intensity + .05)],
            finePens[EffectPalette.OpacityIndex(.40 * intensity)],
            baseX: width * .38, amplitude: width * .24, thickness: width * .42, phase: time, frequency: 1.7);
        DrawVerticalRibbonWithCrest(dc, width, height,
            altRibbonWashes[EffectPalette.OpacityIndex(.18 * intensity + .04)],
            altPens[EffectPalette.OpacityIndex(.32 * intensity)],
            baseX: width * .62, amplitude: width * .22, thickness: width * .36, phase: -time * 1.2 + 1.8, frequency: 2.3);
    }

    private void DrawRipples(DrawingContext dc, double width, double height, double intensity)
    {
        ribbonWashes ??= BuildVerticalRibbons(Tint, .22);
        finePens ??= BuildGlowPens(Tint, 1.25);
        altPens ??= BuildGlowPens(ShiftColor(Tint), 1.0);
        softFills ??= BuildSolidFills(Tint);

        double time = Time * Math.Tau;
        DrawVerticalRibbon(dc, width, height, ribbonWashes[EffectPalette.OpacityIndex(.14 * intensity + .04)],
            baseX: width * .50, amplitude: width * .16, thickness: width * .48, phase: time * .7, frequency: 1.5);

        int centers = height > 220 ? 3 : 2;
        for (int c = 0; c < centers; c++)
        {
            double cx = width * (.32 + (c % 2) * .36) + Math.Sin(time + c * 2.1) * width * .10;
            double cy = height * ((c + .5) / centers) + Math.Cos(time * 1.1 + c) * Math.Min(26, height * .08);
            for (int r = 0; r < 3; r++)
            {
                double phase = (Time * 1.4 + c * .31 + r / 3.0) % 1;
                double fade = Math.Sin(phase * Math.PI) * (1 - phase * .3);
                double rad = 8 + phase * Math.Min(width * .52, 118);
                var pen = (r % 2 == 0 ? finePens : altPens)[EffectPalette.OpacityIndex(fade * (.24 + .44 * intensity))];
                dc.DrawEllipse(null, pen, new Point(cx, cy), rad, rad);
            }
            dc.DrawEllipse(softFills[EffectPalette.OpacityIndex(.35 * intensity)], null, new Point(cx, cy), 1.8, 1.8);
        }
    }

    private void DrawCircuit(DrawingContext dc, double width, double height, double intensity)
    {
        finePens ??= BuildGlowPens(Tint, 1.0);
        glowPens ??= BuildGlowPens(Tint, 1.8);
        altPens ??= BuildGlowPens(ShiftColor(Tint), 1.5);
        moteSprites ??= BuildMoteSprites(Tint);
        altMoteSprites ??= BuildMoteSprites(ShiftColor(Tint));

        double[] lanes = [width * .20, width * .50, width * .80];
        for (int i = 0; i < lanes.Length; i++)
        {
            double x = lanes[i];
            double jogY = height * (.26 + i * .24);
            double jogX = i == 1 ? x : (i == 0 ? x + 10 : x - 10);

            var trace = new StreamGeometry();
            using (var ctx = trace.Open())
            {
                ctx.BeginFigure(new Point(x, 8), isFilled: false, isClosed: false);
                ctx.LineTo(new Point(x, jogY - 8), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(jogX, jogY), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(jogX, height - 8), isStroked: true, isSmoothJoin: false);
            }
            trace.Freeze();
            dc.DrawGeometry(null, finePens[EffectPalette.OpacityIndex(.20 * intensity)], trace);

            for (int p = 0; p < 2; p++)
            {
                double u = (Time * (1.6 + i * .35) + i * .29 + p * .5) % 1;
                double fade = Math.Min(1, Math.Min(u, 1 - u) * 6);
                double py = 8 + u * (height - 16);
                double px = py < jogY - 8 ? x : py > jogY ? jogX : x + (jogX - x) * ((py - (jogY - 8)) / 8.0);
                double tail = Math.Clamp(height * .12, 16, 66);
                double py0 = Math.Max(8, py - tail);
                double px0 = py0 < jogY - 8 ? x : py0 > jogY ? jogX : x + (jogX - x) * ((py0 - (jogY - 8)) / 8.0);

                var pen = (i == 1 ? altPens : glowPens)[EffectPalette.OpacityIndex(.62 * intensity * fade)];
                dc.DrawLine(pen, new Point(px0, py0), new Point(px, py));
                var sprites = i == 1 ? altMoteSprites : moteSprites;
                dc.DrawImage(sprites[EffectPalette.OpacityIndex(.65 * intensity * fade)], new Rect(px - 6, py - 6, 12, 12));
            }
        }
    }

    private static readonly Lazy<Pen[]> BeamCyanPens = new(() => BuildGlowPens(Color.FromRgb(0x38, 0xBD, 0xF8), 1.45));
    private static readonly Lazy<Pen[]> BeamCrimsonPens = new(() => BuildGlowPens(Color.FromRgb(0xE1, 0x1D, 0x48), 1.25));
    private static readonly Lazy<Pen[]> HudGoldPens = new(() => BuildGlowPens(Color.FromRgb(0xF5, 0xB7, 0x00), 1.2));
    private static readonly Lazy<BitmapSource[]> CyanMotes = new(() => BuildMoteSprites(Color.FromRgb(0x38, 0xBD, 0xF8)));
    private static readonly Lazy<BitmapSource[]> GoldMotes = new(() => BuildMoteSprites(Color.FromRgb(0xF5, 0xB7, 0x00)));
    private static readonly Lazy<Brush[]> CyanRibbons = new(() => BuildVerticalRibbons(Color.FromRgb(0x38, 0xBD, 0xF8), .30));

    private void DrawBeamWings(DrawingContext dc, double width, double height, double intensity)
    {
        var cyanPens = BeamCyanPens.Value;
        var crimsonPens = BeamCrimsonPens.Value;
        var goldPens = HudGoldPens.Value;
        var motes = CyanMotes.Value;

        DrawVerticalRibbon(dc, width, height, CyanRibbons.Value[EffectPalette.OpacityIndex(.20 * intensity + .05)],
            baseX: width * .76, amplitude: width * .16, thickness: width * .36, phase: Time * Math.Tau, frequency: 1.6);

        int plumes = Math.Clamp((int)(height / 76), 4, 9);
        for (int i = 0; i < plumes; i++)
        {
            double rise = (i * .29 + Time * (1.2 + (i % 3) * .3)) % 1;
            double fade = Math.Sin(rise * Math.PI);
            double x0 = width * (.94 - i % 3 * .14);
            double y0 = (1 - rise) * (height - 24) + 12;
            var pen = (i % 3 == 0 ? crimsonPens : cyanPens)[EffectPalette.OpacityIndex(fade * (.34 + .48 * intensity))];
            dc.DrawLine(pen, new Point(x0, y0), new Point(Math.Max(10, x0 - 44), Math.Min(height - 6, y0 + 26)));
            dc.DrawImage(motes[EffectPalette.OpacityIndex(fade * (.36 + .48 * intensity))], new Rect(x0 - 8, y0 - 8, 16, 16));
        }

        dc.DrawLine(goldPens[EffectPalette.OpacityIndex(.28 * intensity)], new Point(width * .20, 14), new Point(width * .50, 24));
        dc.DrawLine(goldPens[EffectPalette.OpacityIndex(.28 * intensity)], new Point(width * .80, 14), new Point(width * .50, 24));
    }

    private void DrawHudMatrix(DrawingContext dc, double width, double height, double intensity)
    {
        var cyanPens = BeamCyanPens.Value;
        var goldPens = HudGoldPens.Value;
        var crimsonPens = BeamCrimsonPens.Value;
        var goldMotes = GoldMotes.Value;
        var cyanMotes = CyanMotes.Value;

        double scanU = (Time * 2.2) % 1;
        double scanY = scanU * height;
        double scanFade = Math.Sin(scanU * Math.PI);
        dc.DrawLine(cyanPens[EffectPalette.OpacityIndex(scanFade * (.34 + .46 * intensity))], new Point(8, scanY), new Point(width - 8, scanY));

        // JARVIS-style corner telemetry brackets + reactor rings
        double cx = width * .76;
        double cy = height * .22;
        double pulse = .5 + .5 * Math.Sin(Time * Math.Tau * 2.5);
        dc.DrawEllipse(null, cyanPens[EffectPalette.OpacityIndex((.30 + .42 * intensity) * (.7 + .3 * pulse))], new Point(cx, cy), 18 + pulse * 4, 18 + pulse * 4);
        dc.DrawEllipse(null, goldPens[EffectPalette.OpacityIndex(.28 + .38 * intensity)], new Point(cx, cy), 10, 10);

        dc.DrawLine(crimsonPens[EffectPalette.OpacityIndex(.22 + .28 * intensity)], new Point(width - 2, 18), new Point(width - 2, height - 18));
        for (int i = 0; i < 6; i++)
        {
            double tickY = 58 + (height - 96) * (i / 5.0);
            double proximity = Math.Max(0, 1 - Math.Abs(tickY - scanY) / 52);
            dc.DrawLine(goldPens[EffectPalette.OpacityIndex((.18 + .46 * proximity) * intensity)], new Point(width - 18, tickY), new Point(width - 6, tickY));
            var sprite = (i % 2 == 0 ? goldMotes : cyanMotes)[EffectPalette.OpacityIndex((.22 + .52 * proximity) * intensity)];
            dc.DrawImage(sprite, new Rect(width - 25, tickY - 6, 12, 12));
        }
    }

    private static void DrawVerticalRibbon(DrawingContext dc, double width, double height, Brush fill,
        double baseX, double amplitude, double thickness, double phase, double frequency)
    {
        const int steps = 44;
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

    private static void DrawVerticalRibbonWithCrest(DrawingContext dc, double width, double height, Brush fill, Pen crestPen,
        double baseX, double amplitude, double thickness, double phase, double frequency)
    {
        const int steps = 44;
        var wave = new StreamGeometry();
        var crest = new StreamGeometry();
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
        using (var ctx = wave.Open())
        {
            ctx.BeginFigure(left[0], isFilled: true, isClosed: true);
            for (int s = 1; s <= steps; s++) ctx.LineTo(left[s], isStroked: false, isSmoothJoin: true);
            for (int s = steps; s >= 0; s--) ctx.LineTo(right[s], isStroked: false, isSmoothJoin: true);
        }
        wave.Freeze();
        dc.DrawGeometry(fill, null, wave);

        using (var ctx = crest.Open())
        {
            ctx.BeginFigure(right[0], isFilled: false, isClosed: false);
            for (int s = 1; s <= steps; s++) ctx.LineTo(right[s], isStroked: true, isSmoothJoin: true);
        }
        crest.Freeze();
        dc.DrawGeometry(null, crestPen, crest);
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

    private static Brush[] BuildSolidFills(Color tint)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .40);
        for (int i = 0; i < brushes.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            brushes[i] = b;
        }
        return brushes;
    }

    private static Brush[] BuildGlowWashes(Color tint)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        for (int i = 0; i < brushes.Length; i++)
        {
            var clear = Color.FromArgb(0, tint.R, tint.G, tint.B);
            var edge = EffectPalette.WithOpacity(tint, i);
            var brush = new LinearGradientBrush(clear, edge, 0);
            brush.Freeze();
            brushes[i] = brush;
        }
        return brushes;
    }

    private static Brush[] BuildPrismWashes(Color tint)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        var shift = EffectPalette.Blend(tint, Color.FromRgb(180, 140, 255), .35);
        for (int i = 0; i < brushes.Length; i++)
        {
            var clear = Color.FromArgb(0, tint.R, tint.G, tint.B);
            var mid = EffectPalette.WithOpacity(shift, i);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(.35, 1),
                GradientStops = [new GradientStop(clear, 0), new GradientStop(mid, .5), new GradientStop(clear, 1)]
            };
            brush.Freeze();
            brushes[i] = brush;
        }
        return brushes;
    }

    private static Pen[] BuildGlowPens(Color tint, double thickness)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .32);
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

    private static BitmapSource[] BuildMoteSprites(Color tint)
    {
        if (MoteSpriteCache.TryGetValue(tint, out var cached)) return cached;
        if (MoteSpriteCache.Count >= 16) MoteSpriteCache.Clear();

        const int size = 28, stride = size * 4;
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
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double glow = Math.Max(0, 1 - distance);
                    double core = Math.Clamp(1 - distance * 4.5, 0, 1);
                    byte alpha = (byte)(255 * strength * Math.Min(1, glow * glow * .68 + core * .42));
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
        return MoteSpriteCache[tint] = sprites;
    }

    private static MoteSeed[] CreateMotes()
    {
        var random = new Random(907);
        var list = new MoteSeed[24];
        for (int i = 0; i < list.Length; i++)
            list[i] = new MoteSeed(
                Lane: (i * .61803398875) % 1,
                Start: random.NextDouble(),
                Size: .55 + random.NextDouble() * .75,
                Speed: 1 + (i % 2));
        return list;
    }

    private sealed record MoteSeed(double Lane, double Start, double Size, int Speed);
}
