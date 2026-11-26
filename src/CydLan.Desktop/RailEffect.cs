using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum RailEffectKind { Beacon, Stream, Stardust, Scan }

/// <summary>Renders palette-adaptive animated effects inside the left navigation rail.</summary>
public sealed class RailEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(RailEffectKind), typeof(RailEffect),
        new FrameworkPropertyMetadata(RailEffectKind.Beacon, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry MicroStar = FrozenGeometry("M0,-3.5 Q0,0 3.5,0 Q0,0 0,3.5 Q0,0 -3.5,0 Q0,0 0,-3.5 Z");
    private static readonly StarSeed[] Stars = CreateStars();
    private BitmapSource[]? orbSprites;
    private Brush[]? fills;
    private Pen[]? pens;

    public RailEffectKind Kind { get => (RailEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnTintChanged()
    {
        orbSprites = null;
        fills = null;
        pens = null;
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
        }
    }

    private void DrawBeacon(DrawingContext dc, double width, double height, double intensity)
    {
        orbSprites ??= BuildOrbSprites(Tint);
        pens ??= BuildPens(Tint);

        double pulse = .5 + .5 * Math.Sin(Progress * Math.Tau * 3);
        double y = height * .5 + Math.Sin(Progress * Math.Tau) * Math.Min(height * .26, 65);
        var center = new Point(width / 2, y);
        double radius = 16 + 7 * pulse;
        dc.DrawImage(orbSprites[EffectPalette.OpacityIndex(.32 + .48 * intensity)], new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        dc.DrawEllipse(null, pens[EffectPalette.OpacityIndex((.25 + .45 * intensity) * (1 - pulse * .5))], center, 8 + 10 * pulse, 8 + 10 * pulse);
    }

    private void DrawStream(DrawingContext dc, double width, double height, double intensity)
    {
        orbSprites ??= BuildOrbSprites(Tint);
        pens ??= BuildPens(Tint);

        double x = width - 2;
        for (int s = 0; s < 2; s++)
        {
            double u = (Progress * (2.5 + s) + s * .5) % 1;
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
            double u = (star.Start + Progress * star.Speed) % 1;
            double fade = Math.Min(1, Math.Min(u, 1 - u) * 7);
            double twinkle = .25 + .75 * Math.Pow(.5 + .5 * Math.Sin((Progress * 5 + star.Start) * Math.Tau), 2);
            double x = 8 + (width - 16) * star.Lane + Math.Sin((u * 2 + star.Start) * Math.Tau) * 3;
            double y = (1 - u) * (height - 12) + 6;
            double s = star.Scale * (.7 + .3 * twinkle);

            var m = Matrix.Identity;
            m.Scale(s, s);
            m.Rotate(Progress * 120 + i * 25);
            m.Translate(x, y);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(fills[EffectPalette.OpacityIndex(fade * twinkle * (.38 + .58 * intensity))], null, MicroStar);
            dc.Pop();
        }
    }

    private void DrawScan(DrawingContext dc, double width, double height, double intensity)
    {
        pens ??= BuildPens(Tint);
        fills ??= BuildFills(Tint);

        double u = (Progress * 3.5) % 1;
        double y = u * height;
        var pen = pens[EffectPalette.OpacityIndex(.42 + .50 * intensity)];
        dc.DrawLine(pen, new Point(4, y), new Point(width - 4, y));
        dc.DrawRectangle(fills[EffectPalette.OpacityIndex(.14 * intensity)], null, new Rect(4, Math.Max(0, y - 10), width - 8, 10));

        for (double tickY = 14; tickY < height - 10; tickY += 32)
        {
            double dist = Math.Max(0, 1 - Math.Abs(tickY - y) / 40);
            if (dist <= .05) continue;
            dc.DrawLine(pens[EffectPalette.OpacityIndex(dist * (.35 + .55 * intensity))], new Point(3, tickY), new Point(7, tickY));
            dc.DrawLine(pens[EffectPalette.OpacityIndex(dist * (.35 + .55 * intensity))], new Point(width - 7, tickY), new Point(width - 3, tickY));
        }
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

    private static Pen[] BuildPens(Color tint)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(tint, Colors.White, .35);
        for (int i = 0; i < pens.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            var p = new Pen(b, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            pens[i] = p;
        }
        return pens;
    }

    private static BitmapSource[] BuildOrbSprites(Color tint)
    {
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
        return sprites;
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
