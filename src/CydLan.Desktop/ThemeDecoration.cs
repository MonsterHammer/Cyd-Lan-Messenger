using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum DecorationZone { Title, Sidebar, Composer }
public enum DecorationDesign { None, Sakura, Steampunk, Forest, Paper }

public sealed class ThemeDecoration : FrameworkElement
{
    public static readonly DependencyProperty DesignProperty = DependencyProperty.Register(
        nameof(Design), typeof(DecorationDesign), typeof(ThemeDecoration),
        new FrameworkPropertyMetadata(DecorationDesign.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ZoneProperty = DependencyProperty.Register(
        nameof(Zone), typeof(DecorationZone), typeof(ThemeDecoration),
        new FrameworkPropertyMetadata(DecorationZone.Sidebar, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush SakuraWash = Gradient(0x00D6779E, 0x49D6779E);
    private static readonly Brush BrassWash = Gradient(0x32BF9062, 0x00864827);
    private static readonly Pen Branch = Stroke(0x9B91455D, 1.35);
    private static readonly Brush Blossom = Solid(0xD4C66F96);
    private static readonly Brush BlossomCenter = Solid(0xC6FFF4F8);
    private static readonly Pen MetalHighlight = Stroke(0xA8B67F52, 1);
    private static readonly Pen MetalShadow = Stroke(0xA31B100C, 1);
    private static readonly Pen LeafOutline = Stroke(0x346EB892, .85);
    private static readonly Pen PaperRule = Stroke(0x275A6E69, .8);
    private static readonly Lazy<DrawingBrush> PaperGrain = new(CreatePaperGrain);
    private static readonly Geometry Petal = Shape("M0,0 C-3,-4 -3,-8 0,-7 L1,-6 L2,-7 C5,-6 3,-2 0,0 Z");
    private static readonly Geometry Leaf = Shape("M0,11 Q0,-1 16,0 Q15,12 0,11 Z M-2,13 L12,3");
    private static readonly Geometry Twig = Shape("M0,24 Q29,18 52,9 Q76,0 102,1 M24,18 Q27,11 34,7 M46,11 Q46,5 53,2 M68,5 Q75,8 87,11");

    public DecorationDesign Design { get => (DecorationDesign)GetValue(DesignProperty); set => SetValue(DesignProperty, value); }
    public DecorationZone Zone { get => (DecorationZone)GetValue(ZoneProperty); set => SetValue(ZoneProperty, value); }

    public ThemeDecoration()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(DesignProperty, "ThemeDecorationDesign");
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 30 || height < 18) return;
        var bounds = new Rect(RenderSize);
        switch (Design, Zone)
        {
            case (DecorationDesign.Sakura, DecorationZone.Sidebar):
                dc.DrawRectangle(SakuraWash, null, bounds);
                break;
            case (DecorationDesign.Sakura, DecorationZone.Title):
                dc.PushTransform(new TranslateTransform(width - 107, (height - 23) / 2 + 2));
                dc.DrawGeometry(null, Branch, Twig);
                DrawBlossom(dc, 28, 14); DrawBlossom(dc, 52, 6); DrawBlossom(dc, 74, 5); DrawBlossom(dc, 90, 12);
                dc.Pop();
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Sidebar):
                dc.DrawRectangle(BrassWash, null, bounds);
                dc.DrawLine(MetalHighlight, new Point(width - 2.5, 0), new Point(width - 2.5, height));
                foreach (double y in new[] { 13d, height / 2, height - 13 })
                    Rivet(dc, new Point(width - 6, y));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Title):
                dc.DrawLine(MetalHighlight, new Point(8, height / 2), new Point(width - 8, height / 2));
                for (int x = 12; x < width - 8; x += 14) Rivet(dc, new Point(x, height / 2));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Composer):
                dc.DrawRoundedRectangle(null, MetalHighlight, new Rect(2.5, 2.5, width - 5, height - 5), 5, 5);
                dc.DrawRoundedRectangle(null, MetalShadow, new Rect(4, 4, width - 8, height - 8), 5, 5);
                break;
            case (DecorationDesign.Forest, DecorationZone.Sidebar):
                for (int y = 27; y < height; y += 41)
                    for (int x = 23 + (y % 2) * 17; x < width - 15; x += 48)
                    {
                        dc.PushTransform(new TranslateTransform(x, y));
                        dc.DrawGeometry(null, LeafOutline, Leaf);
                        dc.Pop();
                    }
                break;
            case (DecorationDesign.Paper, DecorationZone.Sidebar):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                for (int y = 23; y < height; y += 23)
                    dc.DrawLine(PaperRule, new Point(0, y + .5), new Point(width, y + .5));
                break;
            case (DecorationDesign.Paper, DecorationZone.Title):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                dc.DrawLine(PaperRule, new Point(5, height - 7.5), new Point(width - 5, height - 7.5));
                dc.DrawLine(PaperRule, new Point(5, height - 5.5), new Point(width - 5, height - 5.5));
                break;
        }
    }

    private static void DrawBlossom(DrawingContext dc, double x, double y)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        for (int angle = 0; angle < 360; angle += 72)
        {
            dc.PushTransform(new RotateTransform(angle));
            dc.DrawGeometry(Blossom, null, Petal);
            dc.Pop();
        }
        dc.DrawEllipse(BlossomCenter, null, new Point(), 1.7, 1.7);
        dc.Pop();
    }

    private static void Rivet(DrawingContext dc, Point center)
    {
        dc.DrawEllipse(MetalHighlight.Brush, MetalShadow, center, 2, 2);
        dc.DrawLine(MetalShadow, new Point(center.X - .8, center.Y + .8), new Point(center.X + .8, center.Y - .8));
    }

    private static DrawingBrush CreatePaperGrain()
    {
        var random = new Random(127);
        var tile = new DrawingGroup();
        using (var dc = tile.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
            var fleck = Solid(0x33576259);
            for (int i = 0; i < 85; i++)
                dc.DrawEllipse(fleck, null, new Point(random.NextDouble() * 32, random.NextDouble() * 32), .42, .32);
        }
        var brush = new DrawingBrush(tile) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 32, 32) };
        brush.Freeze();
        return brush;
    }

    private static Brush Solid(uint color)
    {
        var brush = new SolidColorBrush(Argb(color));
        brush.Freeze();
        return brush;
    }

    private static Brush Gradient(uint from, uint to)
    {
        var brush = new LinearGradientBrush(Argb(from), Argb(to), 90);
        brush.Freeze();
        return brush;
    }

    private static Pen Stroke(uint color, double thickness)
    {
        var pen = new Pen(Solid(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static Geometry Shape(string value)
    {
        var shape = Geometry.Parse(value);
        shape.Freeze();
        return shape;
    }

    private static Color Argb(uint color) => Color.FromArgb((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color);
}
