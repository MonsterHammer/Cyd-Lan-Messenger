using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum SidebarEffectKind { Motes, EdgeGlow, Prism, Sweep }

/// <summary>Renders palette-adaptive animated effects inside the side panel without reducing text legibility.</summary>
public sealed class SidebarEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SidebarEffectKind), typeof(SidebarEffect),
        new FrameworkPropertyMetadata(SidebarEffectKind.Motes, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly MoteSeed[] Motes = CreateMotes();
    private BitmapSource[]? moteSprites;
    private Brush[]? glowWashes;
    private Brush[]? prismWashes;
    private Pen[]? glowPens;

    public SidebarEffectKind Kind { get => (SidebarEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        moteSprites = null;
        glowWashes = null;
        prismWashes = null;
        glowPens = null;
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
        }
    }

    private void DrawMotes(DrawingContext dc, double width, double height, double intensity)
    {
        moteSprites ??= BuildMoteSprites(Tint);
        int count = Math.Clamp((int)(width * height / 6200 * (.35 + .65 * intensity)), 6, Motes.Length);
        for (int i = 0; i < count; i++)
        {
            var mote = Motes[i];
            double rise = (mote.Start + Progress * mote.Speed) % 1;
            double sway = Math.Sin((rise * 2 + mote.Lane) * Math.Tau) * 10;
            double x = 12 + (width - 24) * mote.Lane + sway;
            double y = (1 - rise) * (height + 16) - 8;
            double fade = Math.Min(1, Math.Min(rise, 1 - rise) * 7);
            double blink = .35 + .65 * Math.Pow(.5 + .5 * Math.Cos((Progress * 3 + mote.Start) * Math.Tau), 2);
            double radius = 10 * mote.Size;
            var sprite = moteSprites[EffectPalette.OpacityIndex(fade * blink * (.24 + .52 * intensity))];
            dc.DrawImage(sprite, new Rect(x - radius, y - radius, radius * 2, radius * 2));
        }
    }

    private void DrawEdgeGlow(DrawingContext dc, double width, double height, double intensity)
    {
        glowWashes ??= BuildGlowWashes(Tint);
        glowPens ??= BuildGlowPens(Tint);
        moteSprites ??= BuildMoteSprites(Tint);

        double breathe = .45 + .55 * Math.Sin(Progress * Math.Tau * 2);
        double washWidth = Math.Min(36, width * .16);
        var wash = glowWashes[EffectPalette.OpacityIndex((.10 + .18 * breathe) * intensity)];
        dc.DrawRectangle(wash, null, new Rect(width - washWidth, 0, washWidth, height));

        double travel = (Progress * 2.5) % 1;
        double beamCenter = travel * (height + 120) - 60;
        double beamHalf = Math.Min(65, height * .25);
        double xEdge = width - 1.5;

        var beamPen = glowPens[EffectPalette.OpacityIndex((.38 + .50 * intensity) * (.7 + .3 * breathe))];
        dc.DrawLine(beamPen,
            new Point(xEdge, Math.Max(0, beamCenter - beamHalf)),
            new Point(xEdge, Math.Min(height, beamCenter + beamHalf)));

        if (height > 120)
        {
            double headerPulse = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(Progress * Math.Tau * 2), 2);
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
        double u = (Progress * 2) % 1;
        double bandY = u * (height + 160) - 80;
        double bandH = Math.Min(130, height * .45);
        var fill = prismWashes[EffectPalette.OpacityIndex(.22 + .36 * intensity)];
        dc.DrawRectangle(fill, null, new Rect(0, bandY - bandH / 2, width, bandH));

        double waveX = (.5 + .5 * Math.Sin(Progress * Math.Tau * 2)) * width;
        var accentWash = prismWashes[EffectPalette.OpacityIndex(.12 + .22 * intensity)];
        dc.DrawEllipse(accentWash, null, new Point(waveX, height * .35), width * .55, height * .30);
    }

    private void DrawSweep(DrawingContext dc, double width, double height, double intensity)
    {
        glowPens ??= BuildGlowPens(Tint);
        moteSprites ??= BuildMoteSprites(Tint);

        double u = (Progress * 3) % 1;
        double y = u * height;
        var pen = glowPens[EffectPalette.OpacityIndex(.34 + .52 * intensity)];
        dc.DrawLine(pen, new Point(10, y), new Point(width - 10, y));

        for (double rowY = 74; rowY < height - 52; rowY += 46)
        {
            double proximity = Math.Max(0, 1 - Math.Abs(rowY - y) / 48);
            if (proximity <= .05) continue;
            var tickPen = glowPens[EffectPalette.OpacityIndex(proximity * (.22 + .38 * intensity))];
            dc.DrawLine(tickPen, new Point(14, rowY), new Point(width - 14, rowY));
        }

        double dotX = 16 + ((Progress * 5) % 1) * (width - 32);
        dc.DrawImage(moteSprites[EffectPalette.OpacityIndex(.35 + .50 * intensity)], new Rect(dotX - 7, y - 7, 14, 14));
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

    private static Pen[] BuildGlowPens(Color tint)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .32);
        for (int i = 0; i < pens.Length; i++)
        {
            var brush = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            brush.Freeze();
            var pen = new Pen(brush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            pens[i] = pen;
        }
        return pens;
    }

    private static BitmapSource[] BuildMoteSprites(Color tint)
    {
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
        return sprites;
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
