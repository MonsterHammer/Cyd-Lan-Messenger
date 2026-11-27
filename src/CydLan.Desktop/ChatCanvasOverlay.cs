using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum ChatCanvasGradientKind { Off, Vignette, Spotlight, Mesh, Diagonal, Aurora, FullBurst, ArcCore, Sunrise, DeepSea, Nebula, Candy, Mist, Prism, Twilight, Lava, Breeze, Royal, Peach, Duotone, Halo, Horizon }

public sealed record ChatCanvasSwatch(string Value, string Name, ChatCanvasGradientKind Kind);

/// <summary>Renders optional procedural gradient treatments behind the conversation canvas.</summary>
public sealed class ChatCanvasOverlay : FrameworkElement
{
    public const string Off = "off";
    public const string Vignette = "vignette";
    public const string Spotlight = "spotlight";
    public const string Mesh = "mesh";
    public const string Diagonal = "diagonal";
    public const string Aurora = "aurora";
    public const string FullBurst = "fullburst";
    public const string ArcCore = "arccore";
    public const string Sunrise = "sunrise";
    public const string DeepSea = "deepsea";
    public const string Nebula = "nebula";
    public const string Candy = "candy";
    public const string Mist = "mist";
    public const string Prism = "prism";
    public const string Twilight = "twilight";
    public const string Lava = "lava";
    public const string Breeze = "breeze";
    public const string Royal = "royal";
    public const string Peach = "peach";
    public const string Duotone = "duotone";
    public const string Halo = "halo";
    public const string Horizon = "horizon";

    public static IReadOnlyList<ChatCanvasSwatch> Catalog { get; } =
    [
        new(Off, "None", ChatCanvasGradientKind.Off),
        new(Vignette, "Vignette", ChatCanvasGradientKind.Vignette),
        new(Spotlight, "Spotlight", ChatCanvasGradientKind.Spotlight),
        new(Mesh, "Mesh glow", ChatCanvasGradientKind.Mesh),
        new(Diagonal, "Diagonal", ChatCanvasGradientKind.Diagonal),
        new(Aurora, "Aurora dusk", ChatCanvasGradientKind.Aurora),
        new(FullBurst, "Beam burst", ChatCanvasGradientKind.FullBurst),
        new(ArcCore, "Arc core", ChatCanvasGradientKind.ArcCore),
        new(Sunrise, "Sunrise", ChatCanvasGradientKind.Sunrise),
        new(DeepSea, "Deep sea", ChatCanvasGradientKind.DeepSea),
        new(Nebula, "Nebula", ChatCanvasGradientKind.Nebula),
        new(Candy, "Candy", ChatCanvasGradientKind.Candy),
        new(Mist, "Forest mist", ChatCanvasGradientKind.Mist),
        new(Prism, "Prism corners", ChatCanvasGradientKind.Prism),
        new(Twilight, "Twilight", ChatCanvasGradientKind.Twilight),
        new(Lava, "Lava", ChatCanvasGradientKind.Lava),
        new(Breeze, "Mint breeze", ChatCanvasGradientKind.Breeze),
        new(Royal, "Royal", ChatCanvasGradientKind.Royal),
        new(Peach, "Peach glow", ChatCanvasGradientKind.Peach),
        new(Duotone, "Duotone", ChatCanvasGradientKind.Duotone),
        new(Halo, "Halo", ChatCanvasGradientKind.Halo),
        new(Horizon, "Horizon", ChatCanvasGradientKind.Horizon)
    ];

    public static readonly DependencyProperty GradientProperty = DependencyProperty.Register(
        nameof(Gradient), typeof(string), typeof(ChatCanvasOverlay),
        new FrameworkPropertyMetadata(Off, FrameworkPropertyMetadataOptions.AffectsRender, OnGradientStringChanged));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ChatCanvasGradientKind), typeof(ChatCanvasOverlay),
        new FrameworkPropertyMetadata(ChatCanvasGradientKind.Off, FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ChatCanvasOverlay),
        new FrameworkPropertyMetadata(.55, FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(ChatCanvasOverlay),
        new FrameworkPropertyMetadata(Color.FromRgb(59, 122, 140), FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

    public static readonly DependencyProperty SurfaceColorProperty = DependencyProperty.Register(
        nameof(SurfaceColor), typeof(Color), typeof(ChatCanvasOverlay),
        new FrameworkPropertyMetadata(Color.FromRgb(255, 255, 255), FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

    private Brush? layerA;
    private Brush? layerB;
    private Brush? layerC;
    private ChatCanvasGradientKind cachedKind;
    private double cachedIntensity = -1;
    private Color cachedTint;
    private Color cachedSurface;

    public string Gradient { get => (string)GetValue(GradientProperty); set => SetValue(GradientProperty, value); }
    public ChatCanvasGradientKind Kind { get => (ChatCanvasGradientKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public Color SurfaceColor { get => (Color)GetValue(SurfaceColorProperty); set => SetValue(SurfaceColorProperty, value); }

    public ChatCanvasOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(TintProperty, "EffectColor");
        SetResourceReference(SurfaceColorProperty, "ChromeSurfaceColor");
    }

    public static string Normalize(string? value) =>
        value is not null && Catalog.Any(c => c.Value == value) ? value : Off;

    public static ChatCanvasGradientKind KindFor(string? value) => Normalize(value) switch
    {
        Vignette => ChatCanvasGradientKind.Vignette,
        Spotlight => ChatCanvasGradientKind.Spotlight,
        Mesh => ChatCanvasGradientKind.Mesh,
        Diagonal => ChatCanvasGradientKind.Diagonal,
        Aurora => ChatCanvasGradientKind.Aurora,
        FullBurst => ChatCanvasGradientKind.FullBurst,
        ArcCore => ChatCanvasGradientKind.ArcCore,
        Sunrise => ChatCanvasGradientKind.Sunrise,
        DeepSea => ChatCanvasGradientKind.DeepSea,
        Nebula => ChatCanvasGradientKind.Nebula,
        Candy => ChatCanvasGradientKind.Candy,
        Mist => ChatCanvasGradientKind.Mist,
        Prism => ChatCanvasGradientKind.Prism,
        Twilight => ChatCanvasGradientKind.Twilight,
        Lava => ChatCanvasGradientKind.Lava,
        Breeze => ChatCanvasGradientKind.Breeze,
        Royal => ChatCanvasGradientKind.Royal,
        Peach => ChatCanvasGradientKind.Peach,
        Duotone => ChatCanvasGradientKind.Duotone,
        Halo => ChatCanvasGradientKind.Halo,
        Horizon => ChatCanvasGradientKind.Horizon,
        _ => ChatCanvasGradientKind.Off
    };

    private static void OnGradientStringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ChatCanvasOverlay)d).Kind = KindFor(e.NewValue as string);
    }

    private static void Invalidate(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (ChatCanvasOverlay)d;
        overlay.layerA = null;
        overlay.layerB = null;
        overlay.layerC = null;
        overlay.cachedIntensity = -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Kind == ChatCanvasGradientKind.Off || ActualWidth < 8 || ActualHeight < 8) return;
        double intensity = Math.Clamp(Intensity, .1, 1);
        EnsureBrushes(Kind, intensity);
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        if (layerA is not null) dc.DrawRectangle(layerA, null, rect);
        if (layerB is not null) dc.DrawRectangle(layerB, null, rect);
        if (layerC is not null) dc.DrawRectangle(layerC, null, rect);
    }

    private void EnsureBrushes(ChatCanvasGradientKind kind, double intensity)
    {
        if (cachedKind == kind && Math.Abs(cachedIntensity - intensity) < .001 && cachedTint == Tint && cachedSurface == SurfaceColor && layerA is not null)
            return;
        cachedKind = kind;
        cachedIntensity = intensity;
        cachedTint = Tint;
        cachedSurface = SurfaceColor;
        layerA = layerB = layerC = null;

        bool isLight = AppearanceOptions.RelativeLuminance(SurfaceColor) >= .50;
        Color alt = Color.FromRgb(
            (byte)((Tint.G * .55 + 115) % 255),
            (byte)((Tint.B * .55 + 100) % 255),
            (byte)Math.Min(255, Tint.R * .45 + 175));
        Color deep = isLight ? EffectPalette.Blend(Tint, Colors.Black, .35) : Colors.Black;
        double scale = isLight ? .85 : 1.0;

        switch (kind)
        {
            case ChatCanvasGradientKind.Vignette:
                layerA = Radial(new Point(.5, .5), .85, .85,
                    (WithAlpha(Tint, (byte)Math.Round(12 * intensity * scale)), 0),
                    (WithAlpha(Tint, 0), .55),
                    (WithAlpha(deep, (byte)Math.Round((isLight ? 42 : 95) * intensity)), 1));
                break;
            case ChatCanvasGradientKind.Spotlight:
                layerA = Radial(new Point(.5, .18), .72, .68,
                    (WithAlpha(Tint, (byte)Math.Round(56 * intensity * scale)), 0),
                    (WithAlpha(Tint, (byte)Math.Round(18 * intensity * scale)), .5),
                    (WithAlpha(Tint, 0), 1));
                break;
            case ChatCanvasGradientKind.Mesh:
                layerA = Radial(new Point(.16, .18), .58, .58,
                    (WithAlpha(Tint, (byte)Math.Round(54 * intensity * scale)), 0),
                    (WithAlpha(Tint, 0), 1));
                layerB = Radial(new Point(.86, .82), .62, .62,
                    (WithAlpha(alt, (byte)Math.Round(48 * intensity * scale)), 0),
                    (WithAlpha(alt, 0), 1));
                layerC = Radial(new Point(.78, .20), .44, .44,
                    (WithAlpha(EffectPalette.Blend(Tint, Colors.White, .35), (byte)Math.Round(30 * intensity * scale)), 0),
                    (WithAlpha(Tint, 0), 1));
                break;
            case ChatCanvasGradientKind.Diagonal:
                layerA = Linear(new Point(0, 0), new Point(1, 1),
                    (WithAlpha(Tint, (byte)Math.Round(46 * intensity * scale)), 0),
                    (WithAlpha(Tint, 0), .48),
                    (WithAlpha(alt, (byte)Math.Round(38 * intensity * scale)), 1));
                break;
            case ChatCanvasGradientKind.Aurora:
                layerA = Linear(new Point(0, 0), new Point(1, .85),
                    (WithAlpha(Tint, (byte)Math.Round(44 * intensity * scale)), 0),
                    (WithAlpha(alt, (byte)Math.Round(36 * intensity * scale)), .45),
                    (WithAlpha(Tint, 0), .85));
                layerB = Radial(new Point(.30, .06), .75, .42,
                    (WithAlpha(EffectPalette.Blend(Tint, Color.FromRgb(90, 235, 210), .45), (byte)Math.Round(46 * intensity * scale)), 0),
                    (WithAlpha(Tint, 0), 1));
                break;
            case ChatCanvasGradientKind.FullBurst:
                layerA = Linear(new Point(0, 0), new Point(1, 1),
                    (WithAlpha(Color.FromRgb(0x38, 0xBD, 0xF8), (byte)Math.Round(46 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x1D, 0x4E, 0xD8), (byte)Math.Round(18 * intensity * scale)), .48),
                    (WithAlpha(Color.FromRgb(0xE1, 0x1D, 0x48), (byte)Math.Round(34 * intensity * scale)), 1));
                layerB = Radial(new Point(.84, .16), .64, .64,
                    (WithAlpha(Color.FromRgb(0x38, 0xBD, 0xF8), (byte)Math.Round(48 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x1D, 0x4E, 0xD8), 0), 1));
                break;
            case ChatCanvasGradientKind.ArcCore:
                layerA = Radial(new Point(.5, .46), .62, .62,
                    (WithAlpha(Color.FromRgb(0x38, 0xBD, 0xF8), (byte)Math.Round(52 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xF5, 0xB7, 0x00), (byte)Math.Round(24 * intensity * scale)), .46),
                    (WithAlpha(Color.FromRgb(0x7F, 0x1D, 0x1D), (byte)Math.Round(60 * intensity)), 1));
                layerB = Linear(new Point(0, 0), new Point(1, 1),
                    (WithAlpha(Color.FromRgb(0xF5, 0xB7, 0x00), (byte)Math.Round(22 * intensity)), 0),
                    (WithAlpha(Color.FromRgb(0xF5, 0xB7, 0x00), 0), .5),
                    (WithAlpha(Color.FromRgb(0xB9, 0x1C, 0x1C), (byte)Math.Round(28 * intensity)), 1));
                break;
            case ChatCanvasGradientKind.Sunrise:
                layerA = Linear(new Point(0, 1), new Point(0, 0),
                    (WithAlpha(Color.FromRgb(0xF9, 0x73, 0x16), (byte)Math.Round(50 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xF4, 0x72, 0xB6), (byte)Math.Round(26 * intensity * scale)), .45),
                    (WithAlpha(Color.FromRgb(0x60, 0xA5, 0xFA), 0), 1));
                layerB = Radial(new Point(.5, 1.05), .55, .38,
                    (WithAlpha(Color.FromRgb(0xFA, 0xCC, 0x15), (byte)Math.Round(60 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xFA, 0xCC, 0x15), 0), 1));
                break;
            case ChatCanvasGradientKind.DeepSea:
                layerA = Linear(new Point(0, 0), new Point(0, 1),
                    (WithAlpha(Color.FromRgb(0x22, 0xD3, 0xEE), (byte)Math.Round(26 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x1E, 0x3A, 0x8A), (byte)Math.Round(58 * intensity)), 1));
                layerB = Radial(new Point(.25, 0), .5, .35,
                    (WithAlpha(Colors.White, (byte)Math.Round(22 * intensity * scale)), 0),
                    (WithAlpha(Colors.White, 0), 1));
                break;
            case ChatCanvasGradientKind.Nebula:
                layerA = Radial(new Point(.2, .3), .6, .55,
                    (WithAlpha(Color.FromRgb(0xA8, 0x55, 0xF7), (byte)Math.Round(54 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xA8, 0x55, 0xF7), 0), 1));
                layerB = Radial(new Point(.8, .7), .6, .6,
                    (WithAlpha(Color.FromRgb(0xEC, 0x48, 0x99), (byte)Math.Round(44 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xEC, 0x48, 0x99), 0), 1));
                layerC = Radial(new Point(.6, .15), .35, .3,
                    (WithAlpha(Color.FromRgb(0x38, 0xBD, 0xF8), (byte)Math.Round(38 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x38, 0xBD, 0xF8), 0), 1));
                break;
            case ChatCanvasGradientKind.Candy:
                layerA = Linear(new Point(0, 0), new Point(1, 1),
                    (WithAlpha(Color.FromRgb(0xF9, 0xA8, 0xD4), (byte)Math.Round(48 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xC4, 0xB5, 0xFD), (byte)Math.Round(36 * intensity * scale)), .5),
                    (WithAlpha(Color.FromRgb(0x99, 0xF6, 0xE4), (byte)Math.Round(44 * intensity * scale)), 1));
                break;
            case ChatCanvasGradientKind.Mist:
                layerA = Linear(new Point(0, 1), new Point(0, 0),
                    (WithAlpha(Color.FromRgb(0x16, 0x65, 0x34), (byte)Math.Round(52 * intensity)), 0),
                    (WithAlpha(Color.FromRgb(0x86, 0xEF, 0xAC), (byte)Math.Round(18 * intensity * scale)), .55),
                    (WithAlpha(Colors.White, 0), 1));
                layerB = Linear(new Point(0, .55), new Point(0, .75),
                    (WithAlpha(Colors.White, 0), 0),
                    (WithAlpha(Colors.White, (byte)Math.Round(22 * intensity * scale)), .5),
                    (WithAlpha(Colors.White, 0), 1));
                break;
            case ChatCanvasGradientKind.Prism:
                layerA = Radial(new Point(0, 0), .5, .5,
                    (WithAlpha(Color.FromRgb(0xEF, 0x44, 0x44), (byte)Math.Round(44 * intensity * scale)), 0), (WithAlpha(Color.FromRgb(0xEF, 0x44, 0x44), 0), 1));
                layerB = Radial(new Point(1, 0), .5, .5,
                    (WithAlpha(Color.FromRgb(0x22, 0xC5, 0x5E), (byte)Math.Round(40 * intensity * scale)), 0), (WithAlpha(Color.FromRgb(0x22, 0xC5, 0x5E), 0), 1));
                layerC = Radial(new Point(.5, 1), .6, .5,
                    (WithAlpha(Color.FromRgb(0x3B, 0x82, 0xF6), (byte)Math.Round(46 * intensity * scale)), 0), (WithAlpha(Color.FromRgb(0x3B, 0x82, 0xF6), 0), 1));
                break;
            case ChatCanvasGradientKind.Twilight:
                layerA = Linear(new Point(0, 0), new Point(0, 1),
                    (WithAlpha(Color.FromRgb(0x1E, 0x1B, 0x4B), (byte)Math.Round(62 * intensity)), 0),
                    (WithAlpha(Color.FromRgb(0x7C, 0x3A, 0xED), (byte)Math.Round(30 * intensity * scale)), .6),
                    (WithAlpha(Color.FromRgb(0xFB, 0x92, 0x3C), (byte)Math.Round(40 * intensity * scale)), 1));
                break;
            case ChatCanvasGradientKind.Lava:
                layerA = Radial(new Point(.5, 1.1), .9, .7,
                    (WithAlpha(Color.FromRgb(0xF9, 0x73, 0x16), (byte)Math.Round(62 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xDC, 0x26, 0x26), (byte)Math.Round(34 * intensity * scale)), .5),
                    (WithAlpha(Color.FromRgb(0x7F, 0x1D, 0x1D), 0), 1));
                layerB = Linear(new Point(0, 0), new Point(0, 1),
                    (WithAlpha(Colors.Black, (byte)Math.Round(36 * intensity)), 0),
                    (WithAlpha(Colors.Black, 0), .5));
                break;
            case ChatCanvasGradientKind.Breeze:
                layerA = Linear(new Point(0, 0), new Point(1, 0),
                    (WithAlpha(Color.FromRgb(0x5E, 0xEA, 0xD4), (byte)Math.Round(40 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x5E, 0xEA, 0xD4), 0), .5),
                    (WithAlpha(Color.FromRgb(0xA7, 0xF3, 0xD0), (byte)Math.Round(34 * intensity * scale)), 1));
                break;
            case ChatCanvasGradientKind.Royal:
                layerA = Radial(new Point(.5, .5), .75, .75,
                    (WithAlpha(Color.FromRgb(0xFA, 0xCC, 0x15), (byte)Math.Round(26 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0x4C, 0x1D, 0x95), (byte)Math.Round(48 * intensity * scale)), .7),
                    (WithAlpha(Color.FromRgb(0x2E, 0x10, 0x65), (byte)Math.Round(70 * intensity)), 1));
                break;
            case ChatCanvasGradientKind.Peach:
                layerA = Radial(new Point(.85, .15), .8, .8,
                    (WithAlpha(Color.FromRgb(0xFD, 0xBA, 0x74), (byte)Math.Round(56 * intensity * scale)), 0),
                    (WithAlpha(Color.FromRgb(0xFE, 0xCD, 0xD3), (byte)Math.Round(24 * intensity * scale)), .55),
                    (WithAlpha(Color.FromRgb(0xFE, 0xCD, 0xD3), 0), 1));
                break;
            case ChatCanvasGradientKind.Duotone:
                layerA = Linear(new Point(0, 0), new Point(1, 0),
                    (WithAlpha(Tint, (byte)Math.Round(50 * intensity * scale)), 0),
                    (WithAlpha(Tint, (byte)Math.Round(50 * intensity * scale)), .499),
                    (WithAlpha(alt, (byte)Math.Round(42 * intensity * scale)), .501),
                    (WithAlpha(alt, (byte)Math.Round(42 * intensity * scale)), 1));
                break;
            case ChatCanvasGradientKind.Halo:
                layerA = Radial(new Point(.5, .45), .5, .5,
                    (WithAlpha(Tint, 0), 0),
                    (WithAlpha(Tint, 0), .55),
                    (WithAlpha(EffectPalette.Blend(Tint, Colors.White, .3), (byte)Math.Round(58 * intensity * scale)), .72),
                    (WithAlpha(Tint, 0), .95));
                break;
            case ChatCanvasGradientKind.Horizon:
                layerA = Linear(new Point(0, 0), new Point(0, 1),
                    (WithAlpha(Tint, 0), 0),
                    (WithAlpha(Tint, (byte)Math.Round(14 * intensity * scale)), .58),
                    (WithAlpha(EffectPalette.Blend(Tint, Colors.White, .4), (byte)Math.Round(64 * intensity * scale)), .62),
                    (WithAlpha(alt, (byte)Math.Round(28 * intensity * scale)), 1));
                break;
        }
    }

    private static Brush Radial(Point center, double rx, double ry, params (Color Color, double Offset)[] stops)
    {
        var brush = new RadialGradientBrush { Center = center, GradientOrigin = center, RadiusX = rx, RadiusY = ry };
        foreach (var (c, o) in stops) brush.GradientStops.Add(new GradientStop(c, o));
        brush.Freeze();
        return brush;
    }

    private static Brush Linear(Point start, Point end, params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (c, o) in stops) brush.GradientStops.Add(new GradientStop(c, o));
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
}
