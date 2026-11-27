using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using LinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum BackgroundSideTransitionKind { Off, Left, Right, Horizontal, Vertical, All }

public sealed record BackgroundTransitionSwatch(string Value, string Name, BackgroundSideTransitionKind Kind);

/// <summary>Normalizes and builds smooth edge-fade OpacityMask brushes for chat background images.</summary>
public static class BackgroundSideTransition
{
    public const string Off = "off", Left = "left", Right = "right", Horizontal = "sides", Vertical = "vertical", All = "all";

    public static IReadOnlyList<BackgroundTransitionSwatch> Catalog { get; } =
    [
        new(Off, "Off", BackgroundSideTransitionKind.Off),
        new(Left, "Left side", BackgroundSideTransitionKind.Left),
        new(Right, "Right side", BackgroundSideTransitionKind.Right),
        new(Horizontal, "Left & right", BackgroundSideTransitionKind.Horizontal),
        new(Vertical, "Top & bottom", BackgroundSideTransitionKind.Vertical),
        new(All, "All sides", BackgroundSideTransitionKind.All)
    ];

    public static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        Left or "left side" or "left edge" => Left,
        Right or "right side" or "right edge" => Right,
        Horizontal or "left & right" or "horizontal" or "both sides" => Horizontal,
        Vertical or "top & bottom" or "vertical" => Vertical,
        All or "all sides" or "all edges" or "vignette" => All,
        _ => Off
    };

    public static BackgroundSideTransitionKind KindFor(string? value) => Normalize(value) switch
    {
        Left => BackgroundSideTransitionKind.Left,
        Right => BackgroundSideTransitionKind.Right,
        Horizontal => BackgroundSideTransitionKind.Horizontal,
        Vertical => BackgroundSideTransitionKind.Vertical,
        All => BackgroundSideTransitionKind.All,
        _ => BackgroundSideTransitionKind.Off
    };

    public static Brush? CreateHorizontalMask(BackgroundSideTransitionKind kind, double strength)
    {
        bool fadeStart = kind is BackgroundSideTransitionKind.Left or BackgroundSideTransitionKind.Horizontal or BackgroundSideTransitionKind.All;
        bool fadeEnd = kind is BackgroundSideTransitionKind.Right or BackgroundSideTransitionKind.Horizontal or BackgroundSideTransitionKind.All;
        if (!fadeStart && !fadeEnd) return null;
        return BuildLinearMask(new Point(0, .5), new Point(1, .5), fadeStart, fadeEnd, strength);
    }

    public static Brush? CreateVerticalMask(BackgroundSideTransitionKind kind, double strength)
    {
        bool fadeStart = kind is BackgroundSideTransitionKind.Vertical or BackgroundSideTransitionKind.All;
        bool fadeEnd = fadeStart;
        if (!fadeStart && !fadeEnd) return null;
        return BuildLinearMask(new Point(.5, 0), new Point(.5, 1), fadeStart, fadeEnd, strength);
    }

    private static Brush BuildLinearMask(Point start, Point end, bool fadeStart, bool fadeEnd, double strength)
    {
        double clamped = double.IsFinite(strength) ? Math.Clamp(strength, .1, 1.0) : .55;
        double span = fadeStart && fadeEnd
            ? Math.Clamp(.08 + clamped * .32, .08, .40)
            : Math.Clamp(.12 + clamped * .44, .12, .56);

        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        Color clear = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
        Color low = Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF);
        Color high = Color.FromArgb(0xBF, 0xFF, 0xFF, 0xFF);
        Color solid = Colors.White;

        if (fadeStart)
        {
            brush.GradientStops.Add(new GradientStop(clear, 0));
            brush.GradientStops.Add(new GradientStop(low, span * .30));
            brush.GradientStops.Add(new GradientStop(high, span * .68));
            brush.GradientStops.Add(new GradientStop(solid, span));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(solid, 0));
        }

        if (fadeEnd)
        {
            brush.GradientStops.Add(new GradientStop(solid, 1 - span));
            brush.GradientStops.Add(new GradientStop(high, 1 - span * .68));
            brush.GradientStops.Add(new GradientStop(low, 1 - span * .30));
            brush.GradientStops.Add(new GradientStop(clear, 1));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(solid, 1));
        }

        brush.Freeze();
        return brush;
    }
}

/// <summary>Mini card preview illustrating how each background side transition softens image edges into the canvas.</summary>
public sealed class BackgroundTransitionPreview : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BackgroundSideTransitionKind), typeof(BackgroundTransitionPreview),
        new FrameworkPropertyMetadata(BackgroundSideTransitionKind.Off, FrameworkPropertyMetadataOptions.AffectsRender));

    public BackgroundSideTransitionKind Kind { get => (BackgroundSideTransitionKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(RenderSize);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var canvas = System.Windows.Application.Current?.TryFindResource("Canvas") as Brush ?? new SolidColorBrush(Color.FromRgb(0x16, 0x1A, 0x22));
        var accent = System.Windows.Application.Current?.TryFindResource("Accent") is SolidColorBrush ab ? ab.Color : Color.FromRgb(0x3B, 0x82, 0xF6);
        dc.DrawRectangle(canvas, null, rect);

        var scene = new DrawingGroup();
        using (var sdc = scene.Open())
        {
            var grad = new LinearGradientBrush(
                Color.FromArgb(0xE0, accent.R, accent.G, accent.B),
                EffectPalette.Blend(accent, Color.FromRgb(0x6D, 0x28, 0xD9), .48),
                32);
            grad.Freeze();
            sdc.DrawRectangle(grad, null, rect);
            sdc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x78, 0xFF, 0xFF, 0xFF)), null,
                new Point(rect.Width * .72, rect.Height * .32), rect.Height * .24, rect.Height * .24);
            sdc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x55, 0x0B, 0x10, 0x1A)), null,
                new Rect(0, rect.Height * .62, rect.Width, rect.Height * .38));
        }

        var horiz = BackgroundSideTransition.CreateHorizontalMask(Kind, .85);
        var vert = BackgroundSideTransition.CreateVerticalMask(Kind, .85);
        int pushed = 0;
        if (horiz is not null) { dc.PushOpacityMask(horiz); pushed++; }
        if (vert is not null) { dc.PushOpacityMask(vert); pushed++; }
        dc.DrawDrawing(scene);
        while (pushed-- > 0) dc.Pop();
    }
}
