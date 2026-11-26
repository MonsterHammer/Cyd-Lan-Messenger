using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum ChromeTextureKind { Off, Grain, Woven, Topo, Grid, Silk, Hex }

public sealed record ChromeTextureSwatch(string Value, string Name, ChromeTextureKind Kind);

/// <summary>Renders a single continuous surface texture behind unified chrome regions (title bar, side nav, side panel, and header).</summary>
public sealed class ChromeTexture : FrameworkElement
{
    public const string Off = "off";
    public const string Grain = "grain";
    public const string Woven = "woven";
    public const string Topo = "topo";
    public const string Grid = "grid";
    public const string Silk = "silk";
    public const string Hex = "hex";

    public static IReadOnlyList<ChromeTextureSwatch> Catalog { get; } =
    [
        new ChromeTextureSwatch(Off, "None", ChromeTextureKind.Off),
        new ChromeTextureSwatch(Grain, "Frosted grain", ChromeTextureKind.Grain),
        new ChromeTextureSwatch(Woven, "Woven linen", ChromeTextureKind.Woven),
        new ChromeTextureSwatch(Topo, "Topographic", ChromeTextureKind.Topo),
        new ChromeTextureSwatch(Grid, "Drafting grid", ChromeTextureKind.Grid),
        new ChromeTextureSwatch(Silk, "Flowing silk", ChromeTextureKind.Silk),
        new ChromeTextureSwatch(Hex, "Honeycomb", ChromeTextureKind.Hex)
    ];

    public static readonly DependencyProperty TextureProperty = DependencyProperty.Register(
        nameof(Texture), typeof(string), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Off, FrameworkPropertyMetadataOptions.AffectsRender, OnTextureStringChanged));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ChromeTextureKind), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(ChromeTextureKind.Off, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(.65, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(59, 122, 140), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty SurfaceColorProperty = DependencyProperty.Register(
        nameof(SurfaceColor), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(247, 248, 248), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        nameof(LineColor), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(225, 228, 231), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    private DrawingGroup? cachedVector;
    private Brush? cachedTile;
    private Brush? cachedWash;
    private ChromeTextureKind cachedKind;
    private double cachedWidth;
    private double cachedHeight;
    private double cachedIntensity = -1;
    private Color cachedTint;
    private Color cachedSurface;

    public string Texture { get => (string)GetValue(TextureProperty); set => SetValue(TextureProperty, value); }
    public ChromeTextureKind Kind { get => (ChromeTextureKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public Color SurfaceColor { get => (Color)GetValue(SurfaceColorProperty); set => SetValue(SurfaceColorProperty, value); }
    public Color LineColor { get => (Color)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    public ChromeTexture()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(TintProperty, "EffectColor");
        SetResourceReference(SurfaceColorProperty, "ChromeSurfaceColor");
        SetResourceReference(LineColorProperty, "ChromeLineColor");
    }

    public static string Normalize(string? value) => value is Grain or Woven or Topo or Grid or Silk or Hex ? value : Off;

    public static ChromeTextureKind KindFor(string? value) => Normalize(value) switch
    {
        Grain => ChromeTextureKind.Grain,
        Woven => ChromeTextureKind.Woven,
        Topo => ChromeTextureKind.Topo,
        Grid => ChromeTextureKind.Grid,
        Silk => ChromeTextureKind.Silk,
        Hex => ChromeTextureKind.Hex,
        _ => ChromeTextureKind.Off
    };

    private static void OnTextureStringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ChromeTexture)d;
        control.Kind = KindFor(e.NewValue as string);
    }

    private static void InvalidateTexture(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ChromeTexture)d).InvalidateCache();
    }

    private void InvalidateCache()
    {
        cachedVector = null;
        cachedTile = null;
        cachedWash = null;
        cachedIntensity = -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        var kind = Kind;
        if (kind == ChromeTextureKind.Off || width < 8 || height < 8) return;

        double intensity = Math.Clamp(Intensity, .1, 1);
        EnsureCache(kind, width, height, intensity);

        var rect = new Rect(0, 0, width, height);
        if (cachedTile is not null) dc.DrawRectangle(cachedTile, null, rect);
        if (cachedVector is not null) dc.DrawDrawing(cachedVector);
        if (cachedWash is not null) dc.DrawRectangle(cachedWash, null, rect);
    }

    private void EnsureCache(ChromeTextureKind kind, double width, double height, double intensity)
    {
        bool sizeMatters = kind is ChromeTextureKind.Topo or ChromeTextureKind.Silk;
        bool sameSize = !sizeMatters || (Math.Abs(width - cachedWidth) < .5 && Math.Abs(height - cachedHeight) < .5);
        bool sameKey = sameSize && cachedKind == kind && Math.Abs(cachedIntensity - intensity) < .001 &&
            cachedTint == Tint && cachedSurface == SurfaceColor && (cachedTile is not null || cachedVector is not null);
        if (sameKey) return;

        InvalidateCache();
        cachedKind = kind;
        cachedWidth = width;
        cachedHeight = height;
        cachedIntensity = intensity;
        cachedTint = Tint;
        cachedSurface = SurfaceColor;

        double luma = (0.2126 * SurfaceColor.R + 0.7152 * SurfaceColor.G + 0.0722 * SurfaceColor.B) / 255.0;
        bool isLight = luma >= .50;
        Color hi = isLight ? Colors.White : EffectPalette.Blend(Tint, Colors.White, .72);
        Color lo = isLight ? EffectPalette.Blend(SurfaceColor, Colors.Black, .58) : Colors.Black;
        Color accent = EffectPalette.Blend(Tint, isLight ? lo : Colors.White, isLight ? .18 : .22);
        bool compact = width < 180 || height < 100;

        cachedWash = BuildAmbientWash(hi, lo, accent, isLight, intensity, kind);

        switch (kind)
        {
            case ChromeTextureKind.Grain:
                cachedTile = BuildGrainBrush(hi, lo, accent, isLight, intensity);
                break;
            case ChromeTextureKind.Woven:
                cachedTile = BuildWovenBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Grid:
                cachedTile = BuildGridBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Hex:
                cachedTile = BuildHexBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Topo:
                cachedVector = BuildTopoDrawing(width, height, hi, lo, accent, isLight, intensity);
                break;
            case ChromeTextureKind.Silk:
                cachedVector = BuildSilkDrawing(width, height, hi, lo, accent, isLight, intensity);
                break;
        }
    }

    private static Brush BuildAmbientWash(Color hi, Color lo, Color accent, bool isLight, double intensity, ChromeTextureKind kind)
    {
        byte topAlpha = (byte)Math.Round((isLight ? 44 : 18) * intensity);
        byte midAlpha = (byte)Math.Round((isLight ? 14 : 16) * intensity);
        byte edgeAlpha = (byte)Math.Round((isLight ? 18 : 34) * intensity);
        if (kind == ChromeTextureKind.Silk) topAlpha = (byte)(topAlpha / 2);

        var wash = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        wash.GradientStops.Add(new GradientStop(WithAlpha(hi, topAlpha), 0));
        wash.GradientStops.Add(new GradientStop(WithAlpha(accent, midAlpha), .42));
        wash.GradientStops.Add(new GradientStop(WithAlpha(lo, edgeAlpha), 1));
        wash.Freeze();
        return wash;
    }

    private static Brush BuildGrainBrush(Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        const int size = 128;
        byte[] pixels = new byte[size * size * 4];
        double strength = .42 + .58 * intensity;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double coarse = SmoothTileNoise(x * 8.0 / size, y * 8.0 / size, 8);
                double medium = SmoothTileNoise(x * 32.0 / size, y * 32.0 / size, 32);
                double fine = Hash2D(x, y) * 2.0 - 1.0;
                double micro = (Hash2D((x + 17) & (size - 1), (y + 31) & (size - 1))
                    + Hash2D((x + size - 11) & (size - 1), (y + 7) & (size - 1)) - 1.0) * .5;
                double value = (fine * .56 + micro * .22 + (medium - .5) * .36 + (coarse - .5) * .28) * strength;

                int idx = (y * size + x) * 4;
                if (value >= 0)
                {
                    Color c = x % 3 == 0 ? EffectPalette.Blend(hi, accent, .28) : hi;
                    byte a = (byte)Math.Clamp(value * (isLight ? 48 : 30), 0, 84);
                    WritePremul(pixels, idx, c, a);
                }
                else
                {
                    byte a = (byte)Math.Clamp(-value * (isLight ? 22 : 42), 0, 84);
                    WritePremul(pixels, idx, lo, a);
                }
            }
        }

        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    private static Brush BuildWovenBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double cell = compact ? 10 : 12;
        double half = cell * .5;
        double thread = compact ? 1.5 : 1.9;

        var group = new DrawingGroup();
        var warpPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 58 : 24) * intensity)), thread);
        var weftPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 24 : 22) * intensity)), thread * .85);
        var shadowPen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 16 : 30) * intensity)), thread * .8);
        var groovePen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 11 : 20) * intensity)), .65);

        using (var dc = group.Open())
        {
            // Alternating over-under linen weave without heavy dot blocks
            dc.DrawLine(shadowPen, new Point(0, half * .5), new Point(half, half * .5));
            dc.DrawLine(warpPen, new Point(half, half * .5), new Point(cell, half * .5));
            dc.DrawLine(warpPen, new Point(0, half * 1.5), new Point(half, half * 1.5));
            dc.DrawLine(weftPen, new Point(half, half * 1.5), new Point(cell, half * 1.5));

            dc.DrawLine(warpPen, new Point(half * .5, 0), new Point(half * .5, half));
            dc.DrawLine(weftPen, new Point(half * 1.5, half), new Point(half * 1.5, cell));
            dc.DrawLine(shadowPen, new Point(half * 1.5, 0), new Point(half * 1.5, half));
            dc.DrawLine(shadowPen, new Point(half * .5, half), new Point(half * .5, cell));

            dc.DrawLine(groovePen, new Point(0, half), new Point(cell, half));
            dc.DrawLine(groovePen, new Point(half, 0), new Point(half, cell));
        }

        return TiledDrawingBrush(group, cell, cell);
    }

    private static Brush BuildGridBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double sub = compact ? 14 : 20;
        double major = sub * 4;

        var group = new DrawingGroup();
        var subPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 18 : 16) * intensity)), .65);
        var majorPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 36 : 38) * intensity)), .95);
        var crossPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 84 : 58) * intensity)), 1.2);
        var dotBrush = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 54 : 52) * intensity)));

        using (var dc = group.Open())
        {
            for (int i = 1; i < 4; i++)
            {
                double p = sub * i + .5;
                dc.DrawLine(subPen, new Point(p, 0), new Point(p, major));
                dc.DrawLine(subPen, new Point(0, p), new Point(major, p));
            }

            dc.DrawLine(majorPen, new Point(.5, 0), new Point(.5, major));
            dc.DrawLine(majorPen, new Point(0, .5), new Point(major, .5));

            // Precision corner crosshairs + minor quadrant center dot
            double arm = compact ? 3.5 : 5;
            dc.DrawLine(crossPen, new Point(.5, 0), new Point(.5, arm));
            dc.DrawLine(crossPen, new Point(.5, major - arm), new Point(.5, major));
            dc.DrawLine(crossPen, new Point(0, .5), new Point(arm, .5));
            dc.DrawLine(crossPen, new Point(major - arm, .5), new Point(major, .5));
            dc.DrawEllipse(dotBrush, null, new Point(major * .5 + .5, major * .5 + .5), 1.0, 1.0);
        }

        return TiledDrawingBrush(group, major, major);
    }

    private static Brush BuildHexBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double r = compact ? 10 : 15;
        double w = Math.Sqrt(3) * r;
        double stepY = 3 * r;

        var group = new DrawingGroup();
        var bevelLight = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 78 : 34) * intensity)), 1.05);
        var bevelShadow = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 28 : 54) * intensity)), 1.15);
        var ridge = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 30 : 32) * intensity)), .85);
        var cellFill = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 20 : 12) * intensity)));
        var altFill = FrozenBrush(WithAlpha(lo, (byte)Math.Round((isLight ? 10 : 18) * intensity)));

        using (var dc = group.Open())
        {
            Point[] centers =
            [
                new(0, 0), new(w, 0),
                new(w * .5, 1.5 * r), new(-w * .5, 1.5 * r), new(w * 1.5, 1.5 * r),
                new(0, 3 * r), new(w, 3 * r)
            ];
            for (int cIdx = 0; cIdx < centers.Length; cIdx++)
            {
                var c = centers[cIdx];
                Point[] pts = HexPoints(c.X, c.Y, r - .7);
                var poly = BuildHexPolygon(pts);
                dc.DrawGeometry(cIdx % 2 == 0 ? cellFill : altFill, ridge, poly);

                // Top-left facet catchlight & lower-right shadow bevel
                dc.DrawLine(bevelLight, pts[4], pts[5]);
                dc.DrawLine(bevelLight, pts[5], pts[0]);
                dc.DrawLine(bevelShadow, pts[1], pts[2]);
                dc.DrawLine(bevelShadow, pts[2], pts[3]);
            }
        }

        return TiledDrawingBrush(group, w, stepY);
    }

    private static DrawingGroup BuildTopoDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        var group = new DrawingGroup();
        var majorPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 52) * intensity)), 1.25);
        var minorPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 22 : 24) * intensity)), .85);
        var ridgeHighlight = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 68 : 30) * intensity)), .85);

        using var dc = group.Open();
        double scale = Math.Max(width, height);
        (double cx, double cy, double baseR, double phase, int rings, double warp)[] peaks =
        [
            (width * .20, height * .16, scale * .055, .4, 10, .18),
            (width * .66, height * .08, scale * .050, 1.9, 9, .16),
            (width * .11, height * .80, scale * .045, 3.1, 8, .15)
        ];

        foreach (var peak in peaks)
        {
            for (int ring = 1; ring <= peak.rings; ring++)
            {
                double radius = peak.baseR * Math.Pow(ring, 1.18);
                var ringGeom = CreateContourRing(peak.cx, peak.cy, radius, peak.phase + ring * .22, peak.warp);
                bool major = ring % 3 == 0;
                dc.DrawGeometry(null, major ? majorPen : minorPen, ringGeom);
                if (major)
                {
                    var highlightGeom = CreateContourRing(peak.cx - .8, peak.cy - .8, radius, peak.phase + ring * .22, peak.warp);
                    dc.DrawGeometry(null, ridgeHighlight, highlightGeom);
                }
            }
        }

        // Flowing transverse contour strata bridging peaks smoothly across the header & side panel
        int waves = height < 90 ? 4 : 6;
        for (int i = 0; i < waves; i++)
        {
            double frac = (i + .6) / (waves + .4);
            var lineGeom = new StreamGeometry();
            using (var ctx = lineGeom.Open())
            {
                const int segs = 36;
                for (int s = 0; s <= segs; s++)
                {
                    double u = s / (double)segs;
                    double x = u * width;
                    double y = height * frac
                        + Math.Sin(u * Math.Tau * 1.4 + i * .85) * height * .065
                        + Math.Cos(u * Math.Tau * 2.7 - i * .55) * height * .028;
                    var p = new Point(x, y);
                    if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                    else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                }
            }
            lineGeom.Freeze();
            dc.DrawGeometry(null, i % 2 == 0 ? minorPen : majorPen, lineGeom);
        }

        return group;
    }

    private static DrawingGroup BuildSilkDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();

        (double yStart, double yEnd, double amp1, double amp2, double thickness, double phase, bool warm)[] ribbons =
        [
            (.06, .34, .09, .05, .26, 0.2, false),
            (.14, .48, -.08, .06, .22, 1.3, true),
            (.30, .62, .10, -.04, .28, 2.4, false),
            (.46, .78, -.09, .05, .24, 3.6, true),
            (.66, .94, .08, -.05, .26, 4.7, false)
        ];

        byte hiRim = (byte)Math.Round((isLight ? 92 : 44) * intensity);
        var rimPen = FrozenPen(WithAlpha(hi, hiRim), 1.05);
        var accentRimPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 40 : 48) * intensity)), .9);

        foreach (var r in ribbons)
        {
            var fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(.25, 1)
            };
            Color foldColor = r.warm ? accent : hi;
            fill.GradientStops.Add(new GradientStop(WithAlpha(foldColor, (byte)Math.Round((isLight ? 66 : 30) * intensity)), 0));
            fill.GradientStops.Add(new GradientStop(WithAlpha(accent, (byte)Math.Round((isLight ? 24 : 22) * intensity)), .48));
            fill.GradientStops.Add(new GradientStop(WithAlpha(lo, (byte)Math.Round((isLight ? 16 : 36) * intensity)), 1));
            fill.Freeze();

            var crest = new StreamGeometry();
            var band = new StreamGeometry();
            const int steps = 40;
            var topPts = new Point[steps + 1];
            var botPts = new Point[steps + 1];

            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double x = t * width;
                double cy = (r.yStart + (r.yEnd - r.yStart) * t) * height
                    + Math.Sin(t * Math.Tau * 1.2 + r.phase) * height * r.amp1
                    + Math.Cos(t * Math.Tau * 2.1 - r.phase * .7) * height * r.amp2;
                double half = height * r.thickness * (.65 + .35 * Math.Sin(t * Math.PI + .4)) * .5;
                topPts[s] = new Point(x, cy - half);
                botPts[s] = new Point(x, cy + half);
            }

            using (var ctx = band.Open())
            {
                ctx.BeginFigure(topPts[0], isFilled: true, isClosed: true);
                for (int s = 1; s <= steps; s++) ctx.LineTo(topPts[s], isStroked: false, isSmoothJoin: true);
                for (int s = steps; s >= 0; s--) ctx.LineTo(botPts[s], isStroked: false, isSmoothJoin: true);
            }
            band.Freeze();
            dc.DrawGeometry(fill, null, band);

            using (var ctx = crest.Open())
            {
                ctx.BeginFigure(topPts[0], isFilled: false, isClosed: false);
                for (int s = 1; s <= steps; s++) ctx.LineTo(topPts[s], isStroked: true, isSmoothJoin: true);
            }
            crest.Freeze();
            dc.DrawGeometry(null, r.warm ? accentRimPen : rimPen, crest);
        }

        return group;
    }

    private static StreamGeometry CreateContourRing(double cx, double cy, double radius, double phase, double warp)
    {
        const int steps = 48;
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        for (int s = 0; s <= steps; s++)
        {
            double angle = s / (double)steps * Math.Tau;
            double mod = 1.0
                + warp * Math.Sin(angle * 3 + phase)
                + warp * .55 * Math.Cos(angle * 5 - phase * 1.4)
                + warp * .25 * Math.Sin(angle * 7 + phase * .7);
            double r = radius * mod;
            var p = new Point(cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r * .86);
            if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: true);
            else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
        }
        geom.Freeze();
        return geom;
    }

    private static Point[] HexPoints(double cx, double cy, double r)
    {
        var pts = new Point[6];
        for (int i = 0; i < 6; i++)
        {
            double a = Math.PI / 6.0 + i * Math.PI / 3.0;
            pts[i] = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        return pts;
    }

    private static StreamGeometry BuildHexPolygon(Point[] pts)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
        for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], isStroked: true, isSmoothJoin: false);
        geom.Freeze();
        return geom;
    }

    private static Brush TiledDrawingBrush(DrawingGroup drawing, double width, double height)
    {
        drawing.Freeze();
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, width, height),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, height),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color, double thickness)
    {
        var brush = FrozenBrush(color);
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static double Hash2D(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
    }

    private static double SmoothTileNoise(double x, double y, int period)
    {
        int ix = (int)Math.Floor(x);
        int iy = (int)Math.Floor(y);
        double fx = x - ix;
        double fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int x0 = ((ix % period) + period) % period;
        int x1 = (x0 + 1) % period;
        int y0 = ((iy % period) + period) % period;
        int y1 = (y0 + 1) % period;
        double v00 = Hash2D(x0, y0);
        double v10 = Hash2D(x1, y0);
        double v01 = Hash2D(x0, y1);
        double v11 = Hash2D(x1, y1);
        return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
    }

    private static void WritePremul(byte[] buffer, int idx, Color c, byte a)
    {
        buffer[idx] = (byte)(c.B * a / 255);
        buffer[idx + 1] = (byte)(c.G * a / 255);
        buffer[idx + 2] = (byte)(c.R * a / 255);
        buffer[idx + 3] = a;
    }
}
