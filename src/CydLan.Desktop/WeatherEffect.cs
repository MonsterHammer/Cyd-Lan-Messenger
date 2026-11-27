using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum WeatherEffectKind { Fireflies, Rain, Snow, Starfield, Bubbles, Embers, Matrix, Constellation, Bokeh, WingPlumes, Repulsor }

public sealed class WeatherEffect : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WeatherEffectKind), typeof(WeatherEffect),
        new FrameworkPropertyMetadata(WeatherEffectKind.Fireflies, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Geometry Flake = Geometry.Parse(
        "M0,-5 V5 M-4.3,-2.5 L4.3,2.5 M-4.3,2.5 L4.3,-2.5 M-1.6,-3.8 L0,-2.5 L1.6,-3.8 M-1.6,3.8 L0,2.5 L1.6,3.8");
    private static readonly Geometry StarGlyph = Geometry.Parse("M0,-4 Q0,0 4,0 Q0,0 0,4 Q0,0 -4,0 Q0,0 0,-4 Z");
    private static readonly Lazy<RainTrack[]> RainTracks = new(BuildRainTracks);
    private static readonly Dictionary<Color, BitmapSource[]> FireflySpriteCache = [];
    private static readonly Dictionary<Color, BitmapSource[]> BokehSpriteCache = [];
    private static readonly Lazy<Brush[]> FreedomCyanFills = new(() => BuildTintedFills(Color.FromRgb(0x38, 0xBD, 0xF8)));
    private static readonly Lazy<Brush[]> FreedomCrimsonFills = new(() => BuildTintedFills(Color.FromRgb(0xE1, 0x1D, 0x48)));
    private static readonly Lazy<Pen[]> FreedomCyanPens = new(() => BuildTintedPens(Color.FromRgb(0x38, 0xBD, 0xF8), 1.35));
    private static readonly Lazy<Pen[]> FreedomCobaltPens = new(() => BuildTintedPens(Color.FromRgb(0x1D, 0x4E, 0xD8), 1.25));
    private static readonly Lazy<Pen[]> ArcGoldPens = new(() => BuildTintedPens(Color.FromRgb(0xF5, 0xB7, 0x00), 1.2));
    private static readonly Lazy<Brush[]> ArcGoldFills = new(() => BuildTintedFills(Color.FromRgb(0xF5, 0xB7, 0x00)));
    private static readonly Lazy<BitmapSource[]> ArcCyanSprites = new(() => BuildColorFireflySprites(Color.FromRgb(0x38, 0xBD, 0xF8)));
    private static readonly Geometry WingFeather = Geometry.Parse("M-8,-1.5 L3,-3.2 L10,0 L3,2.6 L-5,1.2 Z");
    private BitmapSource[]? fireflySprites;
    private BitmapSource[]? bokehSprites;
    private Pen[]? streakPens;
    private Brush[]? tintFills;
    public WeatherEffectKind Kind { get => (WeatherEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override int FrameRate => Kind is WeatherEffectKind.Rain or WeatherEffectKind.Matrix or WeatherEffectKind.WingPlumes or WeatherEffectKind.Repulsor ? 30 : base.FrameRate;
    protected override void OnTintChanged() { fireflySprites = null; bokehSprites = null; streakPens = null; tintFills = null; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 24 || ActualHeight < 24) return;
        double intensity = Math.Clamp(Intensity, .1, 1);
        switch (Kind)
        {
            case WeatherEffectKind.Rain: DrawRain(dc, intensity); return;
            case WeatherEffectKind.Matrix: DrawMatrix(dc, intensity); return;
            case WeatherEffectKind.Constellation: DrawConstellation(dc, intensity); return;
            case WeatherEffectKind.Bokeh: DrawBokeh(dc, intensity); return;
            case WeatherEffectKind.WingPlumes: DrawWingPlumes(dc, intensity); return;
            case WeatherEffectKind.Repulsor: DrawRepulsor(dc, intensity); return;
        }
        int count = (int)Math.Clamp(ActualWidth * ActualHeight / 6100 * intensity, 5, IsLargeSurface ? 24 : 32);
        for (int i = 0; i < count; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .41421356237);
            double size = .62 + .58 * Fraction(i * .754877666);
            double phase = Fraction(offset + Time * (1 + i % 2));
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
                case WeatherEffectKind.Starfield:
                    double twinkle = .2 + .8 * Math.Pow(.5 + .5 * Math.Sin((Time * 4 + offset) * Math.Tau), 2);
                    double sx = Fraction(lane + Time * (.35 + (i % 3) * .2)) * (ActualWidth - 20) + 10;
                    double sy = 10 + (ActualHeight - 20) * offset;
                    var starM = Matrix.Identity;
                    starM.Scale(size * (.75 + .35 * twinkle), size * (.75 + .35 * twinkle));
                    starM.Translate(sx, sy);
                    dc.PushTransform(new MatrixTransform(starM));
                    dc.DrawGeometry(TintFill(twinkle * (.35 + .60 * intensity)), null, StarGlyph);
                    dc.Pop();
                    break;
                case WeatherEffectKind.Bubbles:
                    double riseY = (1 - phase) * (ActualHeight + 28) - 14;
                    double bx = x + 9 * Math.Sin(wave);
                    double br = 4.5 + 5.5 * size;
                    double alpha = Fade(phase) * (.28 + .55 * intensity);
                    dc.DrawEllipse(TintFill(alpha * .22), StreakPen(alpha), new Point(bx, riseY), br, br);
                    dc.DrawEllipse(TintFill(alpha * .85), null, new Point(bx - br * .3, riseY - br * .3), br * .22, br * .22);
                    break;
                case WeatherEffectKind.Embers:
                    double emberY = (1 - phase) * (ActualHeight + 24) - 12;
                    double ex = x + 15 * Math.Sin(wave * 1.5);
                    fireflySprites ??= BuildFireflySprites();
                    double er = 7 * size;
                    double emberFade = Fade(phase) * (.40 + .55 * intensity);
                    dc.DrawImage(fireflySprites[EffectPalette.OpacityIndex(emberFade)], new Rect(ex - er, emberY - er, er * 2, er * 2));
                    break;
            }
        }
        if (Kind == WeatherEffectKind.Starfield)
        {
            double shoot = (Time * 4) % 1;
            if (shoot < .28)
            {
                double t = shoot / .28;
                double sx = ActualWidth * (.15 + .65 * t);
                double sy = ActualHeight * (.12 + .35 * t);
                dc.DrawLine(StreakPen((1 - t) * (.45 + .50 * intensity)), new Point(sx - 28, sy - 14), new Point(sx, sy));
            }
        }
    }

    private void DrawWingPlumes(DrawingContext dc, double intensity)
    {
        var cyanFills = FreedomCyanFills.Value;
        var crimsonFills = FreedomCrimsonFills.Value;
        var cyanPens = FreedomCyanPens.Value;
        var cobaltPens = FreedomCobaltPens.Value;
        var cyanGlow = ArcCyanSprites.Value;

        double width = ActualWidth, height = ActualHeight;
        int plumes = (int)Math.Clamp(width * height / 9500 * intensity, 7, IsLargeSurface ? 22 : 28);
        for (int i = 0; i < plumes; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .41421356237);
            double size = .75 + .55 * Fraction(i * .754877666);
            double progress = Fraction(offset + Time * (1.2 + (i % 3) * .35));
            double fade = Fade(progress);
            double x = width * (1.08 - progress * 1.18) + Math.Sin(progress * Math.Tau + lane * 4) * 14;
            double y = height * (.08 + .82 * lane) + progress * height * .26 - height * .10;
            double angle = -24 + Math.Sin((Time * 2 + lane) * Math.Tau) * 11;

            var m = Matrix.Identity;
            m.Scale(size * 1.15, size * 1.15);
            m.Rotate(angle);
            m.Translate(x, y);
            dc.PushTransform(new MatrixTransform(m));
            var fill = (i % 5 == 0 ? crimsonFills : cyanFills)[EffectPalette.OpacityIndex(fade * (.28 + .42 * intensity))];
            var pen = (i % 3 == 0 ? cobaltPens : cyanPens)[EffectPalette.OpacityIndex(fade * (.36 + .50 * intensity))];
            dc.DrawGeometry(fill, pen, WingFeather);
            dc.Pop();

            if (i % 4 == 0)
            {
                double glowR = 11 * size;
                dc.DrawImage(cyanGlow[EffectPalette.OpacityIndex(fade * (.25 + .35 * intensity))],
                    new Rect(x - glowR, y - glowR, glowR * 2, glowR * 2));
            }
        }

        // Occasional HiMAT high-speed beam flare crossing diagonal
        double sweep = (Time * 3) % 1;
        if (sweep < .42)
        {
            double u = sweep / .42;
            double sx = width * (.92 - u * .85);
            double sy = height * (.12 + u * .55);
            dc.DrawLine(cyanPens[EffectPalette.OpacityIndex(Math.Sin(u * Math.PI) * (.42 + .52 * intensity))],
                new Point(sx + 34, sy - 16), new Point(sx, sy));
        }
    }

    private void DrawRepulsor(DrawingContext dc, double intensity)
    {
        var arcSprites = ArcCyanSprites.Value;
        var cyanPens = FreedomCyanPens.Value;
        var goldPens = ArcGoldPens.Value;
        var goldFills = ArcGoldFills.Value;
        var cyanFills = FreedomCyanFills.Value;
        fireflySprites ??= BuildFireflySprites();

        double width = ActualWidth, height = ActualHeight;
        int sparks = (int)Math.Clamp(width * height / 8800 * intensity, 7, IsLargeSurface ? 22 : 28);
        for (int i = 0; i < sparks; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .381966011);
            double rise = Fraction(offset + Time * (1.4 + (i % 3) * .45));
            double fade = Fade(rise);
            double x = 14 + (width - 28) * lane + Math.Sin((rise * 2 + lane) * Math.Tau) * 8;
            double y = (1 - rise) * (height + 20) - 10;

            if (i % 3 == 0)
            {
                double r = 4.2 + (i % 2) * 2.2;
                double pulse = .55 + .45 * Math.Sin(Time * Math.Tau * 3 + i);
                dc.DrawImage(arcSprites[EffectPalette.OpacityIndex(fade * pulse * (.34 + .46 * intensity))],
                    new Rect(x - r * 2.1, y - r * 2.1, r * 4.2, r * 4.2));
                dc.DrawEllipse(null, cyanPens[EffectPalette.OpacityIndex(fade * (.42 + .48 * intensity))], new Point(x, y), r, r);
            }
            else
            {
                double size = 1.3 + (i % 3) * .45;
                var fill = (i % 2 == 0 ? goldFills : cyanFills)[EffectPalette.OpacityIndex(fade * (.44 + .52 * intensity))];
                dc.DrawEllipse(fill, null, new Point(x, y), size, size);
                dc.DrawLine(goldPens[EffectPalette.OpacityIndex(fade * (.28 + .42 * intensity))],
                    new Point(x, y), new Point(x, y + 7));
            }
        }

        // HUD repulsor calibration pulse near focal zone
        double centerX = width * .82;
        double centerY = height * .26;
        double ringPhase = (Time * 2) % 1;
        double ringR = 14 + ringPhase * 28;
        dc.DrawEllipse(null, cyanPens[EffectPalette.OpacityIndex(Math.Sin(ringPhase * Math.PI) * (.32 + .45 * intensity))],
            new Point(centerX, centerY), ringR, ringR);
        dc.DrawEllipse(null, goldPens[EffectPalette.OpacityIndex(.26 * intensity)],
            new Point(centerX, centerY), 12, 12);
    }

    private static Brush[] BuildTintedFills(Color color)
    {
        var fills = new Brush[EffectPalette.OpacityLevels];
        var bright = EffectPalette.Blend(color, Colors.White, .30);
        for (int i = 0; i < fills.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            fills[i] = b;
        }
        return fills;
    }

    private static Pen[] BuildTintedPens(Color color, double thickness)
    {
        var pens = new Pen[EffectPalette.OpacityLevels];
        for (int i = 0; i < pens.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(color, i));
            b.Freeze();
            var p = new Pen(b, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            pens[i] = p;
        }
        return pens;
    }

    private static BitmapSource[] BuildColorFireflySprites(Color tint)
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
                    var color = EffectPalette.Blend(tint, Colors.White, center * .55);
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

    private Brush TintFill(double opacity)
    {
        if (tintFills is null)
        {
            tintFills = new Brush[EffectPalette.OpacityLevels];
            var bright = EffectPalette.Blend(Tint, Colors.White, .38);
            for (int i = 0; i < tintFills.Length; i++)
            {
                var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
                b.Freeze();
                tintFills[i] = b;
            }
        }
        return tintFills[EffectPalette.OpacityIndex(opacity)];
    }

    private void DrawMatrix(DrawingContext dc, double intensity)
    {
        double colGap = ActualWidth < 180 ? 11 : 18;
        int cols = Math.Clamp((int)(ActualWidth / colGap * (.45 + .55 * intensity)), 6, IsLargeSurface ? 32 : 44);
        double cellH = ActualHeight < 90 ? 6 : 9;
        int tail = ActualHeight < 90 ? 5 : 9;

        for (int i = 0; i < cols; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .38196601125);
            double speed = 2.0 + (i % 4) * .8;
            double phase = Fraction(offset + Time * speed);
            double x = 8 + (ActualWidth - 16) * lane;
            double headY = phase * (ActualHeight + tail * cellH + 16) - 8;

            for (int t = 0; t < tail; t++)
            {
                double y = headY - t * cellH;
                if (y < -cellH || y > ActualHeight + cellH) continue;
                double falloff = 1.0 - t / (double)tail;
                int glyphSeed = (i * 31 + t * 17 + (int)(Time * 40)) & 7;
                double alpha = falloff * falloff * (t == 0 ? .88 : .62) * (.35 + .65 * intensity);
                var brush = TintFill(alpha);
                if (t == 0)
                {
                    dc.DrawRoundedRectangle(brush, null, new Rect(x - 2.2, y - 2.2, 4.4, 4.4), 1, 1);
                }
                else
                {
                    double w = glyphSeed % 3 == 0 ? 4.2 : 2.6;
                    double h = glyphSeed % 2 == 0 ? 4.6 : 2.8;
                    dc.DrawRectangle(brush, null, new Rect(x - w * .5, y - h * .5, w, h));
                    if (glyphSeed is 1 or 5)
                        dc.DrawRectangle(TintFill(alpha * .55), null, new Rect(x + 1.4, y - 1, 1.4, 2.2));
                }
            }
        }
    }

    private void DrawConstellation(DrawingContext dc, double intensity)
    {
        int count = Math.Clamp((int)(ActualWidth * ActualHeight / 10500 * (.5 + .5 * intensity)), 7, IsLargeSurface ? 20 : 26);
        Span<Point> nodes = stackalloc Point[count];
        double linkDist = ActualWidth < 180 ? 52 : 112;
        double linkDistSq = linkDist * linkDist;

        for (int i = 0; i < count; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .41421356237);
            double angle = (Time * (1 + i % 2) + offset) * Math.Tau;
            double x = 10 + (ActualWidth - 20) * lane + Math.Cos(angle + i) * Math.Min(22, ActualWidth * .12);
            double y = 10 + (ActualHeight - 20) * offset + Math.Sin(angle * 1.2 - i) * Math.Min(18, ActualHeight * .14);
            nodes[i] = new Point(Math.Clamp(x, 6, ActualWidth - 6), Math.Clamp(y, 6, ActualHeight - 6));
        }

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                double dx = nodes[i].X - nodes[j].X;
                double dy = nodes[i].Y - nodes[j].Y;
                double dSq = dx * dx + dy * dy;
                if (dSq >= linkDistSq) continue;
                double closeness = 1.0 - Math.Sqrt(dSq) / linkDist;
                dc.DrawLine(StreakPen(closeness * closeness * (.30 + .45 * intensity)), nodes[i], nodes[j]);
            }
        }

        for (int i = 0; i < count; i++)
        {
            double pulse = .45 + .55 * Math.Pow(.5 + .5 * Math.Sin((Time * 4 + i * .37) * Math.Tau), 2);
            double r = (ActualWidth < 180 ? 1.5 : 2.1) + (i % 3) * .45;
            dc.DrawEllipse(TintFill(pulse * (.45 + .52 * intensity)), null, nodes[i], r, r);
        }
    }

    private void DrawBokeh(DrawingContext dc, double intensity)
    {
        bokehSprites ??= BuildBokehSprites();
        int count = Math.Clamp((int)(ActualWidth * ActualHeight / 15000 * (.5 + .5 * intensity)), 5, IsLargeSurface ? 15 : 20);
        double maxR = ActualWidth < 180 ? 16 : 36;
        for (int i = 0; i < count; i++)
        {
            double lane = Fraction((i + 1) * .61803398875);
            double offset = Fraction((i + 1) * .38196601125);
            double depth = .45 + .55 * Fraction(i * .754877666);
            double phase = Fraction(offset + Time * (.5 + depth * .6));
            double x = (ActualWidth + maxR * 2) * lane - maxR + Math.Sin((phase + lane) * Math.Tau) * 24 * depth;
            double y = (1 - phase) * (ActualHeight + maxR * 2) - maxR;
            double r = (maxR * .42 + maxR * .58 * depth);
            double breathe = .65 + .35 * Math.Sin((Time * 3 + offset) * Math.Tau);
            double alpha = Fade(phase) * breathe * (.22 + .42 * intensity) * (.65 + .35 * depth);
            dc.DrawImage(bokehSprites[EffectPalette.OpacityIndex(alpha)], new Rect(x - r, y - r, r * 2, r * 2));
        }
    }

    private BitmapSource[] BuildBokehSprites()
    {
        Color tint = Tint;
        if (BokehSpriteCache.TryGetValue(tint, out var cached)) return cached;
        if (BokehSpriteCache.Count >= 8) BokehSpriteCache.Clear();

        const int size = 48, stride = size * 4;
        var sprites = new BitmapSource[EffectPalette.OpacityLevels];
        Color rimColor = EffectPalette.Blend(tint, Colors.White, .48);
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
                    if (d >= 1) continue;
                    double disc = d < .86 ? .52 + .22 * (1 - d) : Math.Max(0, (1 - d) / .14) * .72;
                    double rim = Math.Exp(-Math.Pow((d - .80) / .11, 2)) * .58;
                    byte alpha = (byte)Math.Clamp(255 * strength * Math.Min(1, disc + rim), 0, 255);
                    Color c = EffectPalette.Blend(tint, rimColor, Math.Clamp(rim * 1.4, 0, 1));
                    int offset = y * stride + x * 4;
                    pixels[offset] = (byte)(c.B * alpha / 255);
                    pixels[offset + 1] = (byte)(c.G * alpha / 255);
                    pixels[offset + 2] = (byte)(c.R * alpha / 255);
                    pixels[offset + 3] = alpha;
                }
            }
            var sprite = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            sprite.Freeze();
            sprites[index] = sprite;
        }
        return BokehSpriteCache[tint] = sprites;
    }

    private void DrawRain(DrawingContext dc, double intensity)
    {
        var tracks = RainTracks.Value;
        double travel = ActualHeight + 60;
        int count = (int)Math.Clamp(ActualWidth * ActualHeight / 2050 * (.18 + .82 * intensity), 10, IsLargeSurface ? 76 : 96);
        for (int i = 0; i < count; i++)
        {
            var (drop, phase) = tracks[i].Sample(Time);
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
        Color tint = Tint;
        if (FireflySpriteCache.TryGetValue(tint, out var cached)) return cached;
        if (FireflySpriteCache.Count >= 8) FireflySpriteCache.Clear();

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
                    var color = EffectPalette.Blend(tint, Colors.White, center * .55);
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
        return FireflySpriteCache[tint] = sprites;
    }
}
