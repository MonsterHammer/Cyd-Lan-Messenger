using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Draws a customizable animated CydLan companion pet with modular bodies, patterns, faces, hats, wings, outfits, props, and vibes.</summary>
public sealed partial class FloatingPetVisual : FrameworkElement
{
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentSoftBrushProperty = DependencyProperty.Register(
        nameof(AccentSoftBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.LightBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SurfaceBrushProperty = DependencyProperty.Register(
        nameof(SurfaceBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InkBrushProperty = DependencyProperty.Register(
        nameof(InkBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OnlineBrushProperty = DependencyProperty.Register(
        nameof(OnlineBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.MediumSeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AwayBrushProperty = DependencyProperty.Register(
        nameof(AwayBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.Goldenrod, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DangerBrushProperty = DependencyProperty.Register(
        nameof(DangerBrush), typeof(Brush), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThemeDesignProperty = DependencyProperty.Register(
        nameof(ThemeDesign), typeof(DecorationDesign), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(DecorationDesign.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PetStyleProperty = DependencyProperty.Register(
        nameof(PetStyle), typeof(PetAppearance), typeof(FloatingPetVisual),
        new FrameworkPropertyMetadata(PetAppearance.Default, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public Brush AccentSoftBrush { get => (Brush)GetValue(AccentSoftBrushProperty); set => SetValue(AccentSoftBrushProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Brush InkBrush { get => (Brush)GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }
    public Brush OnlineBrush { get => (Brush)GetValue(OnlineBrushProperty); set => SetValue(OnlineBrushProperty, value); }
    public Brush AwayBrush { get => (Brush)GetValue(AwayBrushProperty); set => SetValue(AwayBrushProperty, value); }
    public Brush DangerBrush { get => (Brush)GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public DecorationDesign ThemeDesign { get => (DecorationDesign)GetValue(ThemeDesignProperty); set => SetValue(ThemeDesignProperty, value); }
    public PetAppearance PetStyle { get => (PetAppearance?)GetValue(PetStyleProperty) ?? PetAppearance.Default; set => SetValue(PetStyleProperty, value ?? PetAppearance.Default); }

    private double breathPhase;
    private double blinkProgress;
    private double hopProgress;
    private double stepPhase;
    private double facing = 1;
    private bool isHovered;
    private bool isExcited;
    private bool isSleepy;
    private bool isOnline;
    private bool isRoaming;
    private bool isDancing;
    private string userStatus = "chat";

    public void SetMotionState(
        double breathPhase,
        double blinkProgress,
        double hopProgress,
        double stepPhase,
        double facing,
        bool hovered,
        bool excited,
        bool sleepy,
        bool online,
        bool roaming,
        string status,
        bool dancing = false)
    {
        this.breathPhase = breathPhase;
        this.blinkProgress = blinkProgress;
        this.hopProgress = hopProgress;
        this.stepPhase = stepPhase;
        this.facing = facing;
        isHovered = hovered;
        isExcited = excited;
        isSleepy = sleepy;
        isOnline = online;
        isRoaming = roaming;
        isDancing = dancing;
        userStatus = status;
        InvalidateVisual();
    }

    public FloatingPetVisual()
    {
        IsHitTestVisible = false;
        SetResourceReference(AccentBrushProperty, "Accent");
        SetResourceReference(AccentSoftBrushProperty, "AccentSoft");
        SetResourceReference(SurfaceBrushProperty, "Surface");
        SetResourceReference(LineBrushProperty, "Line");
        SetResourceReference(InkBrushProperty, "Ink");
        SetResourceReference(OnlineBrushProperty, "Online");
        SetResourceReference(AwayBrushProperty, "Away");
        SetResourceReference(DangerBrushProperty, "Danger");
        SetResourceReference(ThemeDesignProperty, "ThemeDecorationDesign");
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 12 || h <= 12) return;

        PetAppearance style = PetStyle;
        Color themeAccent = ColorOf(AccentBrush, Color.FromRgb(0x3B, 0x7A, 0x8C));
        Color themeAccentSoft = ColorOf(AccentSoftBrush, Color.FromRgb(0xE1, 0xED, 0xF1));
        Color themeSurface = ColorOf(SurfaceBrush, Color.FromRgb(0xF0, 0xF2, 0xF3));
        Color themeLine = ColorOf(LineBrush, Color.FromRgb(0xC0, 0xC8, 0xCE));
        Color themeInk = ColorOf(InkBrush, Color.FromRgb(0x1D, 0x23, 0x26));
        Color onlineCol = ColorOf(OnlineBrush, Color.FromRgb(0x4E, 0x95, 0xA9));
        Color awayCol = ColorOf(AwayBrush, Color.FromRgb(0xB0, 0x8A, 0x3E));
        Color dangerCol = ColorOf(DangerBrush, Color.FromRgb(0xB4, 0x49, 0x5B));

        Color accent = AppearanceOptions.TryParseHex(style.AccentColor, out var customAcc) ? customAcc : themeAccent;
        Color surface = AppearanceOptions.TryParseHex(style.BodyColor, out var customBody) ? customBody : themeSurface;
        Color detail = AppearanceOptions.TryParseHex(style.DetailColor, out var customDetail)
            ? customDetail
            : EffectPalette.Blend(accent, Color.FromRgb(0xF4, 0x72, 0xB6), .45);
        Color accentSoft = AppearanceOptions.TryParseHex(style.AccentColor, out _) || AppearanceOptions.TryParseHex(style.BodyColor, out _)
            ? EffectPalette.Blend(surface, accent, .26)
            : themeAccentSoft;
        Color line = AppearanceOptions.TryParseHex(style.BodyColor, out _)
            ? EffectPalette.Blend(surface, AppearanceOptions.RelativeLuminance(surface) < .42 ? Colors.White : Colors.Black, .28)
            : themeLine;

        Color signalColor = userStatus == "dnd" ? dangerCol
            : userStatus is "away" or "busy" ? awayCol
            : isOnline ? onlineCol
            : EffectPalette.Blend(line, themeInk, .25);

        bool sleepyMood = isSleepy || (style.Vibe == "sleepy" && !isHovered && !isExcited);
        double cadence = style.Vibe switch
        {
            "zoomies" => 1.55,
            "calm" => .72,
            "sleepy" => .58,
            _ => 1.0
        };
        double breath = sleepyMood
            ? Math.Sin(breathPhase * .6) * .9
            : Math.Sin(breathPhase * cadence) * (style.Vibe == "zoomies" ? 1.55 : 1.35);
        double hopArc = hopProgress > 0 ? Math.Sin(hopProgress * Math.PI) * 12.0 : 0;
        double walkHop = isRoaming ? Math.Abs(Math.Sin(stepPhase)) * (style.Vibe == "zoomies" ? 3.8 : 2.8) : 0;
        double danceHop = isDancing ? Math.Abs(Math.Sin(breathPhase * 3.1)) * 3.2 : 0;
        double hoverBob = style.Feet == "hover"
            ? 5.0 + Math.Sin(breathPhase * cadence * 1.3) * 2.2
            : style.Body == "ghost" ? 3.0 + Math.Sin(breathPhase * cadence * 1.1) * 1.6 : 0;
        double totalLift = hopArc + Math.Max(walkHop, danceHop) + hoverBob;
        double tiltDeg = isRoaming
            ? Math.Sin(stepPhase) * 4.5
            : isDancing ? Math.Sin(breathPhase * 3.1) * 4.8
            : hopArc > 2 ? facing * 3.5
            : isHovered ? -3.0 * facing
            : Math.Sin(breathPhase * .55 * cadence) * (style.Vibe == "zoomies" ? 2.2 : 1.1);

        double baseFit = Math.Min(w / 144.0, h / 130.0);
        double totalScale = baseFit * FloatingPetStyle.ClampSize(style.Size);
        double groundScreenY = h - Math.Max(6, 7 * baseFit);

        dc.PushTransform(new TranslateTransform(w * .5, groundScreenY));
        dc.PushTransform(new ScaleTransform(totalScale, totalScale));

        // Ground shadow (softly shrinks/fades as the companion rises).
        double shadowScale = Math.Clamp(1.0 - totalLift / 34.0, .58, 1.0);
        byte shadowAlpha = (byte)Math.Round((style.Feet == "hover" ? 28 : 40) * shadowScale);
        dc.DrawEllipse(FrozenBrush(Color.FromArgb(shadowAlpha, themeInk.R, themeInk.G, themeInk.B)), null,
            new Point(0, 0), 20 * shadowScale, 4.3 * shadowScale);

        double bodyBottom = -4.5 - totalLift;
        var (bodyGeo, bodyW, bodyH, bodyTop, faceCenterY) = BuildBodyMetrics(style.Body, bodyBottom, breath);

        // Ambient aura rendered softly behind the companion.
        if (style.Aura != FloatingPetStyle.None)
            DrawAura(dc, style.Aura, faceCenterY, bodyW, accent, detail, breathPhase);

        bool darkBody = AppearanceOptions.RelativeLuminance(surface) < .40;
        Color featureInk = darkBody ? Color.FromRgb(0xF8, 0xFA, 0xFC) : Color.FromRgb(0x18, 0x1F, 0x26);
        Color outlineColor = AppearanceOptions.RelativeLuminance(surface) < .30
            ? EffectPalette.Blend(accent, Colors.White, .38)
            : EffectPalette.Blend(accent, line, .44);
        Pen outlinePen = FrozenPen(outlineColor, 1.5);

        dc.PushTransform(new RotateTransform(tiltDeg, 0, bodyBottom));

        // 1. Back wings / cape / tail / jetpack (drawn behind ears & body).
        DrawBackAccessory(dc, style.Back, 0, bodyBottom, bodyTop, bodyW, bodyH, accent, accentSoft, surface, detail, outlinePen, breathPhase, hopArc, isRoaming, facing);

        // 2. Ears / horns (behind upper head edge).
        string effectiveEars = style.Ears == FloatingPetStyle.Auto
            ? (ThemeDesign == DecorationDesign.GundamFreedom ? "mecha" : "cat")
            : style.Ears;
        if (effectiveEars != FloatingPetStyle.None && effectiveEars != "mecha")
            DrawEars(dc, effectiveEars, bodyTop, bodyW, surface, accent, accentSoft, detail, outlinePen, breathPhase, isHovered, isExcited);

        // 3. Feet / wheels / hover glow (below bodyBottom).
        DrawFeet(dc, style.Feet, bodyBottom, surface, accent, detail, outlinePen, stepPhase, isRoaming, breathPhase);

        // 4. Body fill + clipped pattern markings + soft top highlight.
        Color topFill = EffectPalette.Blend(surface, Colors.White, darkBody ? .15 : .42);
        Color botFill = style.Pattern == "gradient"
            ? EffectPalette.Blend(surface, detail, .58)
            : EffectPalette.Blend(accentSoft, surface, .46);
        var bodyFill = new LinearGradientBrush(topFill, botFill, 75);
        bodyFill.Freeze();

        dc.PushClip(bodyGeo);
        dc.DrawGeometry(bodyFill, null, bodyGeo);
        DrawPatternOverlay(dc, style.Pattern, bodyTop, bodyBottom, bodyW, bodyH, surface, accent, accentSoft, detail, topFill, darkBody);

        // Glossy top-left highlight crescent.
        dc.DrawRoundedRectangle(
            FrozenBrush(Color.FromArgb((byte)(darkBody ? 32 : 54), 255, 255, 255)), null,
            new Rect(-bodyW * .34, bodyTop + 4, bodyW * .56, 6.2), 3.1, 3.1);
        dc.Pop();

        // Crisp body outline around perimeter.
        dc.DrawGeometry(null, outlinePen, bodyGeo);

        // 5. Outfit / chest badge.
        string effectiveOutfit = style.Outfit == FloatingPetStyle.Auto
            ? (ThemeDesign == DecorationDesign.IronMan ? "arc" : FloatingPetStyle.None)
            : style.Outfit;
        if (effectiveOutfit != FloatingPetStyle.None)
            DrawOutfit(dc, effectiveOutfit, bodyBottom, bodyW, bodyH, surface, accent, accentSoft, detail, featureInk, outlinePen, facing);

        // 6. Face: cheeks, eyes, mouth, and optional glasses.
        double lookX = facing * 2.3;
        double eyeY = faceCenterY + (isExcited || hopArc > 3 ? -1.4 : 0);
        double eyeSep = style.Body == "bean" ? 7.2 : 8.4;

        DrawCheeks(dc, style.Cheeks, lookX, eyeY, eyeSep, accent, detail, featureInk, isHovered, isExcited);
        DrawEyes(dc, style.Eyes, lookX, eyeY, eyeSep, accent, detail, featureInk, surface, blinkProgress, isHovered, isExcited, sleepyMood);
        if (style.Brows != FloatingPetStyle.None)
            DrawBrows(dc, style.Brows, lookX, eyeY - 6.3, eyeSep, featureInk);
        DrawMouth(dc, style.Mouth, lookX, eyeY + 4.5, featureInk, detail, isHovered, isExcited, sleepyMood, blinkProgress);
        if (style.Glasses != FloatingPetStyle.None)
            DrawGlasses(dc, style.Glasses, lookX, eyeY, eyeSep, accent, detail, featureInk, facing);

        // 7. Front V-fin crest & headwear (above upper contour).
        if (effectiveEars == "mecha")
            DrawMechaFin(dc, bodyTop, accent, outlinePen);
        DrawHeadwear(dc, style.Hat, bodyTop, bodyW, accent, accentSoft, surface, detail, signalColor, outlinePen, breathPhase, facing, ThemeDesign == DecorationDesign.Pineapple, isOnline);

        // 8. Props / orbiting particles & sleepy/excited/dancing mood cues.
        DrawPropAndCues(dc, style.Prop, bodyTop, bodyBottom, bodyW, accent, detail, featureInk, breathPhase, hopArc, facing, isExcited, sleepyMood, isDancing);

        dc.Pop(); // tilt
        dc.Pop(); // scale
        dc.Pop(); // translate
    }

    private static (Geometry Geo, double Width, double Height, double Top, double FaceY) BuildBodyMetrics(
        string body, double bodyBottom, double breath)
    {
        switch (body)
        {
            case "round":
            {
                double w = 48 + breath * .75;
                double h = 38 - breath * .75;
                double top = bodyBottom - h;
                var geo = new RectangleGeometry(new Rect(-w * .5, top, w, h), w * .46, h * .48);
                geo.Freeze();
                return (geo, w, h, top, top + h * .49);
            }
            case "bean":
            {
                double w = 39 + breath * .5;
                double h = 44 - breath * .6;
                double top = bodyBottom - h;
                var geo = new RectangleGeometry(new Rect(-w * .5, top, w, h), 19, 19);
                geo.Freeze();
                return (geo, w, h, top, top + h * .44);
            }
            case "pear":
            {
                double w = 47 + breath * .6;
                double h = 43 - breath * .7;
                double top = bodyBottom - h;
                double topW = w * .34;
                double hipW = w * .50;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(0, top), true, true);
                    ctx.BezierTo(new Point(topW, top), new Point(hipW, top + h * .44), new Point(hipW, bodyBottom - h * .28), true, false);
                    ctx.BezierTo(new Point(hipW, bodyBottom), new Point(w * .24, bodyBottom), new Point(0, bodyBottom), true, false);
                    ctx.BezierTo(new Point(-w * .24, bodyBottom), new Point(-hipW, bodyBottom), new Point(-hipW, bodyBottom - h * .28), true, false);
                    ctx.BezierTo(new Point(-hipW, top + h * .44), new Point(-topW, top), new Point(0, top), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .48);
            }
            case "star":
            {
                double w = 48 + breath * .6;
                double h = 44 - breath * .6;
                double top = bodyBottom - h;
                double cy = top + h * .52;
                double outerR = 25.0 - breath * .2;
                double innerR = 16.8 + breath * .2;
                var pts = new Point[10];
                for (int i = 0; i < 10; i++)
                {
                    double ang = -Math.PI * .5 + i * Math.PI / 5.0;
                    double r = i % 2 == 0 ? outerR : innerR;
                    pts[i] = new Point(Math.Cos(ang) * r, cy + Math.Sin(ang) * r * .92);
                }
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    Point Mid(Point a, Point b) => new((a.X + b.X) * .5, (a.Y + b.Y) * .5);
                    Point start = Mid(pts[^1], pts[0]);
                    ctx.BeginFigure(start, true, true);
                    for (int i = 0; i < pts.Length; i++)
                    {
                        Point next = Mid(pts[i], pts[(i + 1) % pts.Length]);
                        ctx.QuadraticBezierTo(pts[i], next, true, false);
                    }
                }
                g.Freeze();
                return (g, w, h, top, cy);
            }
            case "mecha":
            {
                double w = 45 + breath * .5;
                double h = 40 - breath * .5;
                double top = bodyBottom - h;
                double l = -w * .5, r = w * .5, c = 8.5;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(l + c, top), true, true);
                    ctx.LineTo(new Point(r - c, top), true, false);
                    ctx.LineTo(new Point(r, top + c), true, false);
                    ctx.LineTo(new Point(r, bodyBottom - c), true, false);
                    ctx.LineTo(new Point(r - c, bodyBottom), true, false);
                    ctx.LineTo(new Point(l + c, bodyBottom), true, false);
                    ctx.LineTo(new Point(l, bodyBottom - c), true, false);
                    ctx.LineTo(new Point(l, top + c), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .48);
            }
            case "cloud":
            {
                double w = 48 + breath * .7;
                double h = 40 - breath * .6;
                double top = bodyBottom - h;
                double l = -w * .5, r = w * .5;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(l + 10, bodyBottom), true, true);
                    ctx.QuadraticBezierTo(new Point(l - 4, bodyBottom - 2), new Point(l + 2, top + h * .55), true, false);
                    ctx.QuadraticBezierTo(new Point(l + 1, top + 4), new Point(-7, top + 4), true, false);
                    ctx.QuadraticBezierTo(new Point(0, top - 4), new Point(7, top + 4), true, false);
                    ctx.QuadraticBezierTo(new Point(r - 1, top + 4), new Point(r - 2, top + h * .55), true, false);
                    ctx.QuadraticBezierTo(new Point(r + 4, bodyBottom - 2), new Point(r - 10, bodyBottom), true, false);
                    ctx.QuadraticBezierTo(new Point(6, bodyBottom + 3), new Point(0, bodyBottom - 1), true, false);
                    ctx.QuadraticBezierTo(new Point(-6, bodyBottom + 3), new Point(l + 10, bodyBottom), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .49);
            }
            case "ghost":
            {
                double w = 44 + breath * .6;
                double h = 43 - breath * .6;
                double top = bodyBottom - h;
                double l = -w * .5, r = w * .5;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(l, bodyBottom - 4), true, true);
                    ctx.BezierTo(new Point(l, top + 4), new Point(l + 6, top), new Point(0, top), true, false);
                    ctx.BezierTo(new Point(r - 6, top), new Point(r, top + 4), new Point(r, bodyBottom - 4), true, false);
                    ctx.QuadraticBezierTo(new Point(r - 4, bodyBottom + 4), new Point(r - 11, bodyBottom - 3), true, false);
                    ctx.QuadraticBezierTo(new Point(4, bodyBottom + 4), new Point(0, bodyBottom - 3), true, false);
                    ctx.QuadraticBezierTo(new Point(-4, bodyBottom + 4), new Point(l + 11, bodyBottom - 3), true, false);
                    ctx.QuadraticBezierTo(new Point(l + 4, bodyBottom + 4), new Point(l, bodyBottom - 4), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .43);
            }
            case "heart":
            {
                double w = 48 + breath * .7;
                double h = 42 - breath * .6;
                double top = bodyBottom - h;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(0, bodyBottom), true, true);
                    ctx.BezierTo(new Point(w * .56, bodyBottom - h * .34), new Point(w * .54, top - 2), new Point(0, top + 9), true, false);
                    ctx.BezierTo(new Point(-w * .54, top - 2), new Point(-w * .56, bodyBottom - h * .34), new Point(0, bodyBottom), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .46);
            }
            case "cube":
            {
                double w = 44 + breath * .5;
                double h = 42 - breath * .5;
                double top = bodyBottom - h;
                var geo = new RectangleGeometry(new Rect(-w * .5, top, w, h), 6, 6);
                geo.Freeze();
                return (geo, w, h, top, top + h * .48);
            }
            default: // "squircle"
            {
                double w = 45 + breath * .6;
                double h = 41 - breath * .7;
                double top = bodyBottom - h;
                var geo = new RectangleGeometry(new Rect(-w * .5, top, w, h), 17, 17);
                geo.Freeze();
                return (geo, w, h, top, top + h * .48);
            }
        }
    }

    private static Color ColorOf(Brush? brush, Color fallback) =>
        brush is SolidColorBrush s ? s.Color
        : brush is GradientBrush g && g.GradientStops.Count > 0 ? g.GradientStops[0].Color
        : fallback;

    private static SolidColorBrush FrozenBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static Pen FrozenPen(Color c, double t) { var p = new Pen(FrozenBrush(c), t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; p.Freeze(); return p; }
}

internal static class DrawingContextPolygonExtensions
{
    internal static void DrawPolygon(this DrawingContext dc, Brush? fill, Pen? pen, params Point[] points)
    {
        if (points.Length < 2) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(points[0], fill is not null, true);
            for (int i = 1; i < points.Length; i++) g.LineTo(points[i], true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(fill, pen, geo);
    }
}
