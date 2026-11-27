using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Draws an animated theme-adaptive CydLan companion pet with expressions, ears/crest, antenna signal orb, and hop/walk motion.</summary>
public sealed class FloatingPetVisual : FrameworkElement
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

    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public Brush AccentSoftBrush { get => (Brush)GetValue(AccentSoftBrushProperty); set => SetValue(AccentSoftBrushProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Brush InkBrush { get => (Brush)GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }
    public Brush OnlineBrush { get => (Brush)GetValue(OnlineBrushProperty); set => SetValue(OnlineBrushProperty, value); }
    public Brush AwayBrush { get => (Brush)GetValue(AwayBrushProperty); set => SetValue(AwayBrushProperty, value); }
    public Brush DangerBrush { get => (Brush)GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public DecorationDesign ThemeDesign { get => (DecorationDesign)GetValue(ThemeDesignProperty); set => SetValue(ThemeDesignProperty, value); }

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
        string status)
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
        if (w <= 8 || h <= 8) return;

        Color accent = ColorOf(AccentBrush, Color.FromRgb(0x3B, 0x7A, 0x8C));
        Color accentSoft = ColorOf(AccentSoftBrush, Color.FromRgb(0xE1, 0xED, 0xF1));
        Color surface = ColorOf(SurfaceBrush, Color.FromRgb(0xF0, 0xF2, 0xF3));
        Color line = ColorOf(LineBrush, Color.FromRgb(0xC0, 0xC8, 0xCE));
        Color ink = ColorOf(InkBrush, Color.FromRgb(0x1D, 0x23, 0x26));
        Color onlineCol = ColorOf(OnlineBrush, Color.FromRgb(0x4E, 0x95, 0xA9));
        Color awayCol = ColorOf(AwayBrush, Color.FromRgb(0xB0, 0x8A, 0x3E));
        Color dangerCol = ColorOf(DangerBrush, Color.FromRgb(0xB4, 0x49, 0x5B));

        Color signalColor = userStatus == "dnd" ? dangerCol
            : userStatus is "away" or "busy" ? awayCol
            : isOnline ? onlineCol
            : EffectPalette.Blend(line, ink, .25);

        double cx = w * .5;
        double groundY = h - 7;
        double breath = isSleepy ? Math.Sin(breathPhase * .6) * .9 : Math.Sin(breathPhase) * 1.4;
        double hopArc = hopProgress > 0 ? Math.Sin(hopProgress * Math.PI) * 13.0 : 0;
        double walkHop = isRoaming ? Math.Abs(Math.Sin(stepPhase)) * 3.0 : 0;
        double totalLift = hopArc + walkHop;
        double tiltDeg = isRoaming ? Math.Sin(stepPhase) * 4.5 : (hopArc > 2 ? facing * 3.5 : (isHovered ? -3.0 * facing : Math.Sin(breathPhase * .5) * 1.0));

        // Ground drop shadow shrinks gently when hopping.
        double shadowScale = Math.Clamp(1.0 - totalLift / 34.0, .62, 1.0);
        byte shadowAlpha = (byte)Math.Round(38 * shadowScale);
        dc.DrawEllipse(FrozenBrush(Color.FromArgb(shadowAlpha, ink.R, ink.G, ink.B)), null,
            new Point(cx, groundY), 19 * shadowScale, 4.2 * shadowScale);

        double bodyW = 44 + breath * .6;
        double bodyH = 40 - breath * .7;
        double bodyBottom = groundY - 4 - totalLift;
        double bodyTop = bodyBottom - bodyH;
        double bodyLeft = cx - bodyW * .5;

        dc.PushTransform(new RotateTransform(tiltDeg, cx, bodyBottom));

        // Little feet below the body (stepping alternately while roaming).
        Color footFill = EffectPalette.Blend(surface, accent, .28);
        Brush footBrush = FrozenBrush(footFill);
        Pen outlinePen = FrozenPen(EffectPalette.Blend(accent, line, .42), 1.5);
        double leftStep = isRoaming ? Math.Max(0, Math.Sin(stepPhase)) * 3.2 : 0;
        double rightStep = isRoaming ? Math.Max(0, -Math.Sin(stepPhase)) * 3.2 : 0;
        dc.DrawRoundedRectangle(footBrush, outlinePen, new Rect(cx - 13, bodyBottom - 3 - leftStep, 9, 6.5), 3.2, 3.2);
        dc.DrawRoundedRectangle(footBrush, outlinePen, new Rect(cx + 4, bodyBottom - 3 - rightStep, 9, 6.5), 3.2, 3.2);

        // Ears / crest behind the head according to theme design.
        DrawTopCrest(dc, cx, bodyTop, bodyW, accent, accentSoft, surface, line, signalColor);

        // Rounded squircle companion body with soft highlight sheen.
        Color topFill = EffectPalette.Blend(surface, Colors.White, AppearanceOptions.RelativeLuminance(surface) < .5 ? .14 : .45);
        Color botFill = EffectPalette.Blend(accentSoft, surface, .45);
        var bodyFill = new LinearGradientBrush(topFill, botFill, 75);
        bodyFill.Freeze();
        var bodyRect = new Rect(bodyLeft, bodyTop, bodyW, bodyH);
        dc.DrawRoundedRectangle(bodyFill, outlinePen, bodyRect, 17, 17);

        // Subtle top-left glossy highlight crescent.
        dc.DrawRoundedRectangle(
            FrozenBrush(Color.FromArgb(46, 255, 255, 255)), null,
            new Rect(bodyLeft + 6, bodyTop + 4, bodyW - 18, 6.5), 3.2, 3.2);

        // Optional Iron Man chest arc-node or CydLan tummy badge.
        if (ThemeDesign == DecorationDesign.IronMan)
        {
            dc.DrawEllipse(FrozenBrush(Color.FromArgb(70, 0x38, 0xBD, 0xF8)), null, new Point(cx + facing * 1.5, bodyBottom - 9), 5.5, 5.5);
            dc.DrawEllipse(FrozenBrush(Color.FromRgb(0x7D, 0xD3, 0xFC)), null, new Point(cx + facing * 1.5, bodyBottom - 9), 2.8, 2.8);
        }

        // Expressive face shifted toward current look/roam facing.
        double lookX = facing * 2.4;
        double eyeY = bodyTop + bodyH * .48 + (isExcited || hopArc > 3 ? -1.5 : 0);
        double eyeSep = 8.4;

        // Soft cheek blush.
        Color blush = EffectPalette.Blend(accent, Color.FromRgb(0xF4, 0x72, 0xB6), .45);
        byte blushAlpha = (byte)(isHovered || isExcited ? 110 : 62);
        Brush blushBrush = FrozenBrush(Color.FromArgb(blushAlpha, blush.R, blush.G, blush.B));
        dc.DrawEllipse(blushBrush, null, new Point(cx + lookX - eyeSep - 3.6, eyeY + 4.8), 3.8, 2.2);
        dc.DrawEllipse(blushBrush, null, new Point(cx + lookX + eyeSep + 3.6, eyeY + 4.8), 3.8, 2.2);

        Pen featurePen = FrozenPen(ink, 1.85);
        Brush inkBrush = FrozenBrush(ink);
        if (isExcited || (isHovered && !isSleepy))
        {
            // Joyful ^  ^ eyes + wide smile.
            DrawHappyEye(dc, cx + lookX - eyeSep, eyeY, featurePen);
            DrawHappyEye(dc, cx + lookX + eyeSep, eyeY, featurePen);
            DrawSmile(dc, cx + lookX, eyeY + 4.2, 5.4, 3.2, featurePen);
        }
        else if (isSleepy || blinkProgress > .45)
        {
            // Calm closed eyes (blinking or sleepy/DND).
            DrawClosedEye(dc, cx + lookX - eyeSep, eyeY + .5, featurePen);
            DrawClosedEye(dc, cx + lookX + eyeSep, eyeY + .5, featurePen);
            if (isSleepy)
                dc.DrawEllipse(null, FrozenPen(ink, 1.4), new Point(cx + lookX, eyeY + 5.6), 1.9, 1.6);
            else
                DrawSmile(dc, cx + lookX, eyeY + 4.6, 3.8, 1.8, featurePen);
        }
        else
        {
            // Open glossy eyes with gentle blink squash and sparkle highlight.
            double openY = Math.Max(.2, 1.0 - blinkProgress);
            double rY = 3.5 * openY;
            dc.DrawEllipse(inkBrush, null, new Point(cx + lookX - eyeSep, eyeY), 2.7, rY);
            dc.DrawEllipse(inkBrush, null, new Point(cx + lookX + eyeSep, eyeY), 2.7, rY);
            if (openY > .55)
            {
                Brush shine = FrozenBrush(Color.FromArgb(225, 255, 255, 255));
                dc.DrawEllipse(shine, null, new Point(cx + lookX - eyeSep - .9, eyeY - 1.2), 1.0, 1.0);
                dc.DrawEllipse(shine, null, new Point(cx + lookX + eyeSep - .9, eyeY - 1.2), 1.0, 1.0);
            }
            DrawSmile(dc, cx + lookX, eyeY + 4.4, 4.2, 2.1, featurePen);
        }

        // Cute floating sparkle / Z cue.
        if (isExcited || hopArc > 3)
        {
            Brush spark = FrozenBrush(Color.FromArgb(200, accent.R, accent.G, accent.B));
            dc.DrawEllipse(spark, null, new Point(cx - 21, bodyTop - 6), 2.3, 2.3);
            dc.DrawEllipse(spark, null, new Point(cx + 21, bodyTop - 9), 1.8, 1.8);
        }
        else if (isSleepy)
        {
            double drift = Math.Sin(breathPhase * .6) * 2.5;
            Pen zPen = FrozenPen(Color.FromArgb(140, ink.R, ink.G, ink.B), 1.3);
            var zGeo = new StreamGeometry();
            using (var g = zGeo.Open())
            {
                double zx = cx + 19, zy = bodyTop - 5 - Math.Abs(drift);
                g.BeginFigure(new Point(zx, zy), false, false);
                g.LineTo(new Point(zx + 5, zy), true, false);
                g.LineTo(new Point(zx, zy + 5), true, false);
                g.LineTo(new Point(zx + 5, zy + 5), true, false);
            }
            zGeo.Freeze();
            dc.DrawGeometry(null, zPen, zGeo);
        }

        dc.Pop();
    }

    private void DrawTopCrest(
        DrawingContext dc,
        double cx,
        double bodyTop,
        double bodyW,
        Color accent,
        Color accentSoft,
        Color surface,
        Color line,
        Color signalColor)
    {
        Pen outline = FrozenPen(EffectPalette.Blend(accent, line, .42), 1.4);
        Brush earFill = FrozenBrush(EffectPalette.Blend(surface, accentSoft, .5));
        Brush innerFill = FrozenBrush(EffectPalette.Blend(accent, accentSoft, .45));

        if (ThemeDesign == DecorationDesign.GundamFreedom)
        {
            // Sharp mecha V-fin + glowing cyan sensor crystal.
            Pen finPen = FrozenPen(Color.FromRgb(0xF5, 0x9E, 0x0B), 2.1);
            dc.DrawLine(finPen, new Point(cx, bodyTop + 1), new Point(cx - 17, bodyTop - 10));
            dc.DrawLine(finPen, new Point(cx, bodyTop + 1), new Point(cx + 17, bodyTop - 10));
            dc.DrawPolygon(FrozenBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), outline,
                new Point(cx, bodyTop - 5), new Point(cx + 4, bodyTop), new Point(cx, bodyTop + 5), new Point(cx - 4, bodyTop));
            return;
        }

        // Cute cat/companion ears.
        DrawEar(dc, new Point(cx - 16, bodyTop + 6), new Point(cx - 18, bodyTop - 9), new Point(cx - 5, bodyTop + 1), earFill, innerFill, outline);
        DrawEar(dc, new Point(cx + 5, bodyTop + 1), new Point(cx + 18, bodyTop - 9), new Point(cx + 16, bodyTop + 6), earFill, innerFill, outline);

        // Signal orb antenna on crown (or pineapple crown tuft for Pineapple theme).
        double tipX = cx + facing * 2.2;
        double tipY = bodyTop - 12 + Math.Sin(breathPhase * 1.1) * 1.0;
        dc.DrawLine(outline, new Point(cx, bodyTop + 1), new Point(tipX, tipY));

        if (ThemeDesign == DecorationDesign.Pineapple)
        {
            Brush leaf = FrozenBrush(Color.FromRgb(0x1F, 0x8A, 0x5B));
            dc.DrawPolygon(leaf, null, new Point(tipX - 5, tipY + 2), new Point(tipX, tipY - 5), new Point(tipX + 5, tipY + 2));
        }

        double pulse = isOnline ? (.42 + .22 * Math.Sin(breathPhase * 1.8)) : .18;
        dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)Math.Round(120 * pulse), signalColor.R, signalColor.G, signalColor.B)),
            null, new Point(tipX, tipY), 6.5, 6.5);
        dc.DrawEllipse(FrozenBrush(signalColor), FrozenPen(Colors.White, .9), new Point(tipX, tipY), 3.3, 3.3);
    }

    private static void DrawEar(DrawingContext dc, Point a, Point apex, Point b, Brush outer, Brush inner, Pen pen)
    {
        dc.DrawPolygon(outer, pen, a, apex, b);
        var center = new Point((a.X + apex.X + b.X) / 3.0, (a.Y + apex.Y + b.Y) / 3.0);
        Point Inset(Point p) => new(center.X + (p.X - center.X) * .52, center.Y + (p.Y - center.Y) * .52);
        dc.DrawPolygon(inner, null, Inset(a), Inset(apex), Inset(b));
    }

    private static void DrawHappyEye(DrawingContext dc, double cx, double cy, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - 3.6, cy + 1.2), false, false);
            g.QuadraticBezierTo(new Point(cx, cy - 3.6), new Point(cx + 3.6, cy + 1.2), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static void DrawClosedEye(DrawingContext dc, double cx, double cy, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - 3.4, cy - .6), false, false);
            g.QuadraticBezierTo(new Point(cx, cy + 2.8), new Point(cx + 3.4, cy - .6), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static void DrawSmile(DrawingContext dc, double cx, double cy, double halfW, double dip, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - halfW, cy), false, false);
            g.QuadraticBezierTo(new Point(cx, cy + dip), new Point(cx + halfW, cy), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
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
