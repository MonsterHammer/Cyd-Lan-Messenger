using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum WeatherEffectKind { Fireflies, Rain, Snow }

public sealed class WeatherEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WeatherEffectKind), typeof(WeatherEffect),
        new FrameworkPropertyMetadata(WeatherEffectKind.Fireflies, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry Flake = Geometry.Parse(
        "M0,-5 V5 M-4.3,-2.5 L4.3,2.5 M-4.3,2.5 L4.3,-2.5 M-1.6,-3.8 L0,-2.5 L1.6,-3.8 M-1.6,3.8 L0,2.5 L1.6,3.8");
    private static readonly Lazy<RainTrack[]> RainTracks = new(BuildRainTracks);
    private BitmapSource[]? fireflySprites;
    private Pen[]? streakPens;
    public WeatherEffectKind Kind { get => (WeatherEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override int FrameRate => Kind == WeatherEffectKind.Rain ? 30 : base.FrameRate;
    protected override void OnTintChanged() { fireflySprites = null; streakPens = null; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 24 || ActualHeight < 24) return;
        double intensity = Math.Clamp(Intensity, .1, 1);
        if (Kind == WeatherEffectKind.Rain)
        {
            DrawRain(dc, intensity);
            return;
        }
        int count = (int)Math.Clamp(ActualWidth * ActualHeight / 6100 * intensity, 5, IsLargeSurface ? 24 : 32);
        for (int i = 0; i < count; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .41421356237);
            double size = .62 + .58 * Fraction(i * .754877666);
            double phase = Fraction(offset + Progress * (1 + i % 2));
            double wave = (phase + lane) * Math.Tau;
            double x = 12 + (ActualWidth - 24) * lane;
            double y = phase * (ActualHeight + 24) - 12;

            switch (Kind)
            {
                case WeatherEffectKind.Fireflies:
                    double blink = .2 + .8 * Math.Pow(.5 + .5 * Math.Cos(wave * 2 + offset), 2);
                    var light = new Point(x + 14 * Math.Sin(wave), 10 + (ActualHeight - 20) * offset + 10 * Math.Cos(wave));
                    fireflySprites ??= BuildFireflySprites();
                    double radius = 11 * size;
                    var sprite = fireflySprites[EffectPalette.OpacityIndex(blink * (.35 + .65 * intensity))];
                    dc.DrawImage(sprite, new Rect(light.X - radius, light.Y - radius, radius * 2, radius * 2));
                    break;
                case WeatherEffectKind.Snow:
                    var matrix = Matrix.Identity;
                    matrix.Scale(size, size);
                    matrix.Rotate(phase * 360 + lane * 360);
                    matrix.Translate(x + 11 * Math.Sin(wave), y);
                    dc.PushTransform(new MatrixTransform(matrix));
                    dc.DrawGeometry(null, StreakPen(Fade(phase) * (.27 + .58 * intensity)), Flake);
                    dc.Pop();
                    break;
            }
        }
    }

    private void DrawRain(DrawingContext dc, double intensity)
    {
        var tracks = RainTracks.Value;
        double travel = ActualHeight + 60;
        int count = (int)Math.Clamp(ActualWidth * ActualHeight / 2050 * (.18 + .82 * intensity), 10, IsLargeSurface ? 76 : 96);
        for (int i = 0; i < count; i++)
        {
            var (drop, phase) = tracks[i].Sample(Progress);
            double x = drop.Lane * (ActualWidth + 90) - 60 + phase * drop.Drift;
            double y = phase * travel - 15;
            double length = drop.Length;
            var pen = StreakPen(drop.Opacity * (.30 + .60 * intensity));
            dc.DrawLine(pen, new Point(x - length * drop.Drift / travel, y - length), new Point(x, y));
        }
    }

    private static RainTrack[] BuildRainTracks()
    {
        var random = new Random(1867);
        var tracks = new RainTrack[128];
        for (int i = 0; i < tracks.Length; i++)
        {
            var drops = new Raindrop[random.Next(32, 57)];
            for (int n = 0; n < drops.Length; n++)
                drops[n] = new Raindrop(random.NextDouble(), 27 + random.NextDouble() * 60,
                    10 + random.NextDouble() * 29, .30 + random.NextDouble() * .65);
            tracks[i] = new RainTrack(random.NextDouble() * drops.Length, drops);
        }
        return tracks;
    }

    private readonly record struct Raindrop(double Lane, double Drift, double Length, double Opacity);
    private readonly record struct RainTrack(double Offset, Raindrop[] Drops)
    {
        internal (Raindrop Drop, double Phase) Sample(double progress)
        {
            double position = Offset + progress * Drops.Length;
            int cycle = (int)position;
            return (Drops[cycle % Drops.Length], position - cycle);
        }
    }

    private static double Fraction(double value) => value % 1;
    private static double Fade(double phase) => Math.Min(1, Math.Min(phase, 1 - phase) * 8);

    private Pen StreakPen(double opacity)
    {
        if (streakPens is null)
        {
            streakPens = new Pen[EffectPalette.OpacityLevels];
            for (int index = 0; index < streakPens.Length; index++)
            {
                var brush = new SolidColorBrush(EffectPalette.WithOpacity(Tint, index));
                var pen = new Pen(brush, 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                streakPens[index] = pen;
            }
        }
        return streakPens[EffectPalette.OpacityIndex(opacity)];
    }

    private BitmapSource[] BuildFireflySprites()
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
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double glow = Math.Max(0, 1 - distance);
                    double center = Math.Clamp(1 - distance * 5, 0, 1);
                    byte alpha = (byte)(255 * strength * Math.Min(1, glow * glow * .8 + center * .45));
                    var color = EffectPalette.Blend(Tint, Colors.White, center * .55);
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
}
