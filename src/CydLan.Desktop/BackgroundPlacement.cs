using System.Windows;
using System.Windows.Controls;
using Image = System.Windows.Controls.Image;
using Panel = System.Windows.Controls.Panel;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace CydLan.Desktop;

public sealed record BackgroundAnchorOption(string Value, string Name, double X, double Y);

/// <summary>Normalizes and computes 9-point placement rects for chat background images across fit modes.</summary>
public static class BackgroundAnchor
{
    public const string TopLeft = "top-left";
    public const string Top = "top";
    public const string TopRight = "top-right";
    public const string Left = "left";
    public const string Center = "center";
    public const string Right = "right";
    public const string BottomLeft = "bottom-left";
    public const string Bottom = "bottom";
    public const string BottomRight = "bottom-right";

    public static IReadOnlyList<BackgroundAnchorOption> Catalog { get; } =
    [
        new(TopLeft, "Top left", 0.0, 0.0),
        new(Top, "Top", 0.5, 0.0),
        new(TopRight, "Top right", 1.0, 0.0),
        new(Left, "Left", 0.0, 0.5),
        new(Center, "Center", 0.5, 0.5),
        new(Right, "Right", 1.0, 0.5),
        new(BottomLeft, "Bottom left", 0.0, 1.0),
        new(Bottom, "Bottom", 0.5, 1.0),
        new(BottomRight, "Bottom right", 1.0, 1.0)
    ];

    public static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        TopLeft or "top left" or "topleft" or "tl" => TopLeft,
        Top or "top center" or "top-center" or "t" => Top,
        TopRight or "top right" or "topright" or "tr" => TopRight,
        Left or "center left" or "middle left" or "left center" or "l" => Left,
        Right or "center right" or "middle right" or "right center" or "r" => Right,
        BottomLeft or "bottom left" or "bottomleft" or "bl" => BottomLeft,
        Bottom or "bottom center" or "bottom-center" or "b" => Bottom,
        BottomRight or "bottom right" or "bottomright" or "br" => BottomRight,
        _ => Center
    };

    public static BackgroundAnchorOption OptionFor(string? value)
    {
        string normalized = Normalize(value);
        for (int i = 0; i < Catalog.Count; i++)
        {
            if (Catalog[i].Value == normalized) return Catalog[i];
        }
        return Catalog[4];
    }

    public static string DisplayName(string? value) => OptionFor(value).Name;

    public static (double X, double Y) Coordinates(string? value)
    {
        var opt = OptionFor(value);
        return (opt.X, opt.Y);
    }

    public static Rect CalculateRect(Size viewport, Size imageSize, string? fit, string? anchor, double scale)
    {
        double vw = double.IsFinite(viewport.Width) ? Math.Max(0, viewport.Width) : 0;
        double vh = double.IsFinite(viewport.Height) ? Math.Max(0, viewport.Height) : 0;
        if (vw <= 0 || vh <= 0 ||
            !double.IsFinite(imageSize.Width) || !double.IsFinite(imageSize.Height) ||
            imageSize.Width <= 0 || imageSize.Height <= 0)
            return new Rect(0, 0, vw, vh);

        double iw = imageSize.Width;
        double ih = imageSize.Height;

        (double baseW, double baseH) = AppearanceCodec.NormalizeBgFit(fit) switch
        {
            "fit" => ScaleBy(iw, ih, Math.Min(vw / iw, vh / ih)),
            "stretch" => (vw, vh),
            "center" => (iw, ih),
            _ => ScaleBy(iw, ih, Math.Max(vw / iw, vh / ih))
        };

        double factor = double.IsFinite(scale) ? Math.Clamp(scale, 1.0, 1.6) : 1.0;
        double finalW = baseW * factor;
        double finalH = baseH * factor;
        var (ax, ay) = Coordinates(anchor);
        return new Rect((vw - finalW) * ax, (vh - finalH) * ay, finalW, finalH);
    }

    private static (double Width, double Height) ScaleBy(double w, double h, double k) => (w * k, h * k);
}

/// <summary>Clips and places its child Image according to Fit, Anchor, and ImageScale.</summary>
public sealed class BackgroundImageViewport : Panel
{
    public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
        nameof(Fit), typeof(string), typeof(BackgroundImageViewport),
        new FrameworkPropertyMetadata("fill", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty AnchorProperty = DependencyProperty.Register(
        nameof(Anchor), typeof(string), typeof(BackgroundImageViewport),
        new FrameworkPropertyMetadata(BackgroundAnchor.Center, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ImageScaleProperty = DependencyProperty.Register(
        nameof(ImageScale), typeof(double), typeof(BackgroundImageViewport),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public string Fit { get => (string)GetValue(FitProperty); set => SetValue(FitProperty, value); }
    public string Anchor { get => (string)GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }
    public double ImageScale { get => (double)GetValue(ImageScaleProperty); set => SetValue(ImageScaleProperty, value); }

    static BackgroundImageViewport()
    {
        ClipToBoundsProperty.OverrideMetadata(typeof(BackgroundImageViewport), new FrameworkPropertyMetadata(true));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        bool finiteViewport = double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height);
        foreach (UIElement child in InternalChildren)
        {
            if (child is Image { Source: { Width: > 0, Height: > 0 } src } && finiteViewport)
            {
                var rect = BackgroundAnchor.CalculateRect(availableSize, new Size(src.Width, src.Height), Fit, Anchor, ImageScale);
                child.Measure(rect.Size);
            }
            else
            {
                child.Measure(availableSize);
            }
        }
        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child is Image { Source: { Width: > 0, Height: > 0 } src })
            {
                var rect = BackgroundAnchor.CalculateRect(finalSize, new Size(src.Width, src.Height), Fit, Anchor, ImageScale);
                child.Arrange(rect);
            }
            else
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        return finalSize;
    }
}
