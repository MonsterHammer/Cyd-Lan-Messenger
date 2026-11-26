using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum DecorationZone { Title, Sidebar, Composer, Header, Rail }
public enum DecorationDesign
{
    None, Sakura, Steampunk, Forest, Paper, Anime, Aurora, Arcade, Pixel, Ocean, Lavender, Ember, Sand,
    Cyberpunk, Midnight, Sunset, Mint, Matcha, Frost
}

/// <summary>Draws optional static theme details across the rail, side panel, title bar, header, and composer.</summary>
public sealed class ThemeDecoration : FrameworkElement
{
    public static readonly DependencyProperty DesignProperty = DependencyProperty.Register(
        nameof(Design), typeof(DecorationDesign), typeof(ThemeDecoration),
        new FrameworkPropertyMetadata(DecorationDesign.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ZoneProperty = DependencyProperty.Register(
        nameof(Zone), typeof(DecorationZone), typeof(ThemeDecoration),
        new FrameworkPropertyMetadata(DecorationZone.Sidebar, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(ThemeDecoration),
        new FrameworkPropertyMetadata(Color.FromRgb(120, 160, 180),
            FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ThemeDecoration)d).InvalidateTint()));

    private static readonly Brush SakuraWash = Gradient(0x00D6779E, 0x49D6779E);
    private static readonly Brush BrassWash = Gradient(0x32BF9062, 0x00864827);
    private static readonly Pen Branch = Stroke(0x9B91455D, 1.35);
    private static readonly Brush Blossom = Solid(0xD4C66F96);
    private static readonly Brush BlossomCenter = Solid(0xC6FFF4F8);
    private static readonly Pen MetalHighlight = Stroke(0xA8B67F52, 1);
    private static readonly Pen MetalShadow = Stroke(0xA31B100C, 1);
    private static readonly Pen LeafOutline = Stroke(0x346EB892, .85);
    private static readonly Pen PaperRule = Stroke(0x275A6E69, .8);
    private static readonly Pen MarginRule = Stroke(0x2EA64B58, .85);
    private static readonly Lazy<DrawingBrush> PaperGrain = new(CreatePaperGrain);
    private static readonly Geometry Petal = Shape("M0,0 C-3,-4 -3,-8 0,-7 L1,-6 L2,-7 C5,-6 3,-2 0,0 Z");
    private static readonly Geometry Leaf = Shape("M0,11 Q0,-1 16,0 Q15,12 0,11 Z M-2,13 L12,3");
    private static readonly Geometry Twig = Shape("M0,24 Q29,18 52,9 Q76,0 102,1 M24,18 Q27,11 34,7 M46,11 Q46,5 53,2 M68,5 Q75,8 87,11");
    private static readonly Geometry Star = Shape("M0,-5 Q0,0 5,0 Q0,0 0,5 Q0,0 -5,0 Q0,0 0,-5 Z");
    private static readonly Geometry Diamond = Shape("M0,-4 L3,0 L0,4 L-3,0 Z");

    private Brush? softWash, subtleWash, headerWash, accentFill, glowFill;
    private Pen? accentLine, softLine, brightLine;

    public DecorationDesign Design { get => (DecorationDesign)GetValue(DesignProperty); set => SetValue(DesignProperty, value); }
    public DecorationZone Zone { get => (DecorationZone)GetValue(ZoneProperty); set => SetValue(ZoneProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public ThemeDecoration()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(DesignProperty, "ThemeDecorationDesign");
        SetResourceReference(TintProperty, "EffectColor");
    }

    private void InvalidateTint()
    {
        softWash = null; subtleWash = null; headerWash = null;
        accentFill = null; glowFill = null;
        accentLine = null; softLine = null; brightLine = null;
    }

    private void EnsureTintPalette()
    {
        if (softWash is not null) return;
        Color bright = EffectPalette.Blend(Tint, Colors.White, .32);
        softWash = VerticalWash(WithAlpha(Tint, 0x26), WithAlpha(Tint, 0x00), WithAlpha(Tint, 0x1E));
        subtleWash = VerticalWash(WithAlpha(Tint, 0x18), WithAlpha(Tint, 0x00), WithAlpha(Tint, 0x12));
        headerWash = HorizontalWash(WithAlpha(Tint, 0x06), WithAlpha(Tint, 0x22), WithAlpha(Tint, 0x08));
        accentFill = FreezeBrush(new SolidColorBrush(WithAlpha(Tint, 0x42)));
        glowFill = FreezeBrush(new SolidColorBrush(WithAlpha(bright, 0x58)));
        accentLine = FreezePen(new Pen(FreezeBrush(new SolidColorBrush(WithAlpha(Tint, 0x4E))), 1));
        softLine = FreezePen(new Pen(FreezeBrush(new SolidColorBrush(WithAlpha(Tint, 0x26))), .85));
        brightLine = FreezePen(new Pen(FreezeBrush(new SolidColorBrush(WithAlpha(bright, 0x56))), 1.1));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 30 || height < 18) return;
        EnsureTintPalette();
        var bounds = new Rect(RenderSize);

        switch (Design, Zone)
        {
            case (DecorationDesign.Sakura, DecorationZone.Sidebar):
                dc.DrawRectangle(SakuraWash, null, bounds);
                DrawBlossom(dc, width - 24, height - 18);
                DrawBlossom(dc, width - 38, height - 11);
                break;
            case (DecorationDesign.Sakura, DecorationZone.Rail):
                dc.DrawRectangle(SakuraWash, null, bounds);
                DrawBlossom(dc, width / 2, height * .56);
                break;
            case (DecorationDesign.Sakura, DecorationZone.Title):
                DrawSakuraTwig(dc, width - 107, (height - 23) / 2 + 2);
                break;
            case (DecorationDesign.Sakura, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawSakuraTwig(dc, width - 122, height - 22);
                break;
            case (DecorationDesign.Sakura, DecorationZone.Composer):
                dc.DrawRoundedRectangle(null, softLine, new Rect(2.5, 2.5, width - 5, height - 5), 11, 11);
                break;

            case (DecorationDesign.Steampunk, DecorationZone.Sidebar):
                dc.DrawRectangle(BrassWash, null, bounds);
                dc.DrawLine(MetalHighlight, new Point(width - 2.5, 0), new Point(width - 2.5, height));
                foreach (double y in new[] { 13d, height / 2, height - 13 })
                    Rivet(dc, new Point(width - 6, y));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Rail):
                dc.DrawRectangle(BrassWash, null, bounds);
                dc.DrawLine(MetalHighlight, new Point(width - 1.5, 8), new Point(width - 1.5, height - 8));
                Rivet(dc, new Point(width - 5, height * .52));
                Rivet(dc, new Point(width - 5, height * .52 + 18));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Title):
                dc.DrawLine(MetalHighlight, new Point(8, height / 2), new Point(width - 8, height / 2));
                for (int x = 12; x < width - 8; x += 14) Rivet(dc, new Point(x, height / 2));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Header):
                dc.DrawRectangle(BrassWash, null, new Rect(0, 0, width, 16));
                dc.DrawLine(MetalHighlight, new Point(16, height - 2.5), new Point(width - 16, height - 2.5));
                dc.DrawLine(MetalShadow, new Point(16, height - 1.5), new Point(width - 16, height - 1.5));
                for (double x = 24; x < width - 20; x += 36)
                    dc.DrawLine(MetalHighlight, new Point(x, height - 6), new Point(x, height - 3));
                Rivet(dc, new Point(16, height - 6));
                Rivet(dc, new Point(width - 16, height - 6));
                break;
            case (DecorationDesign.Steampunk, DecorationZone.Composer):
                dc.DrawRoundedRectangle(null, MetalHighlight, new Rect(2.5, 2.5, width - 5, height - 5), 5, 5);
                dc.DrawRoundedRectangle(null, MetalShadow, new Rect(4, 4, width - 8, height - 8), 5, 5);
                break;

            case (DecorationDesign.Forest, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                for (int y = 27; y < height; y += 41)
                    for (int x = 23 + (y % 2) * 17; x < width - 15; x += 48)
                        DrawLeaf(dc, x, y);
                break;
            case (DecorationDesign.Forest, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawLeaf(dc, width / 2 - 7, height * .55);
                break;
            case (DecorationDesign.Forest, DecorationZone.Title):
                DrawLeaf(dc, width - 54, (height - 12) / 2);
                DrawLeaf(dc, width - 30, (height - 12) / 2 + 2);
                break;
            case (DecorationDesign.Forest, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                for (double x = width - 110; x < width - 20; x += 28)
                    DrawLeaf(dc, x, height - 18);
                dc.DrawLine(LeafOutline, new Point(22, height - 1.5), new Point(width - 22, height - 1.5));
                break;
            case (DecorationDesign.Forest, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 10, LeafOutline, margin: 4);
                break;

            case (DecorationDesign.Paper, DecorationZone.Sidebar):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                dc.DrawLine(MarginRule, new Point(11.5, 0), new Point(11.5, height));
                for (int y = 23; y < height; y += 23)
                    dc.DrawLine(PaperRule, new Point(0, y + .5), new Point(width, y + .5));
                break;
            case (DecorationDesign.Paper, DecorationZone.Rail):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                dc.DrawLine(MarginRule, new Point(width - 3.5, 0), new Point(width - 3.5, height));
                break;
            case (DecorationDesign.Paper, DecorationZone.Title):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                dc.DrawLine(PaperRule, new Point(5, height - 7.5), new Point(width - 5, height - 7.5));
                dc.DrawLine(PaperRule, new Point(5, height - 5.5), new Point(width - 5, height - 5.5));
                break;
            case (DecorationDesign.Paper, DecorationZone.Header):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                dc.DrawLine(PaperRule, new Point(16, height - 4.5), new Point(width - 16, height - 4.5));
                dc.DrawLine(MarginRule, new Point(16, height - 2.5), new Point(width - 16, height - 2.5));
                break;
            case (DecorationDesign.Paper, DecorationZone.Composer):
                dc.DrawRectangle(PaperGrain.Value, null, bounds);
                DrawCornerBrackets(dc, width, height, 8, PaperRule, margin: 4);
                break;

            case (DecorationDesign.Anime, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawStar(dc, width - 22, 18, 1.0);
                DrawStar(dc, width - 26, height - 22, .85);
                DrawStar(dc, width - 26, height * .52, .7);
                break;
            case (DecorationDesign.Anime, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawStar(dc, width / 2, height * .56, .85);
                break;
            case (DecorationDesign.Anime, DecorationZone.Title):
                DrawStar(dc, width - 58, height / 2 - 2, .9);
                DrawStar(dc, width - 38, height / 2 + 4, .65);
                DrawStar(dc, width - 22, height / 2 - 3, .8);
                break;
            case (DecorationDesign.Anime, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                for (int i = 0; i < 4; i++)
                    dc.DrawLine(softLine, new Point(width - 150 + i * 24, 0), new Point(width - 178 + i * 24, height));
                DrawStar(dc, width - 34, 18, 1.0);
                DrawStar(dc, width - 56, height - 18, .75);
                dc.DrawLine(accentLine, new Point(22, height - 1.5), new Point(width - 22, height - 1.5));
                break;
            case (DecorationDesign.Anime, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 10, accentLine, margin: 4);
                break;

            case (DecorationDesign.Aurora, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawWave(dc, 0, height - 28, width, 10, 1.5, accentLine!);
                DrawDot(dc, width - 24, 22, 1.5);
                DrawDot(dc, width - 26, height - 46, 1.4);
                DrawDot(dc, width * .55, height * .38, 1.2);
                break;
            case (DecorationDesign.Aurora, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawDot(dc, width / 2, height * .56, 1.6);
                break;
            case (DecorationDesign.Aurora, DecorationZone.Title):
                DrawWave(dc, 8, height / 2 + 2, width - 16, 6, 1.2, brightLine!);
                DrawDot(dc, width - 26, height / 2 - 4, 1.4);
                break;
            case (DecorationDesign.Aurora, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawWave(dc, 16, height * .48, width - 32, 11, 1.8, softLine!);
                DrawWave(dc, 16, height * .64, width - 32, 8, 2.4, brightLine!);
                DrawDot(dc, width * .32, 18, 1.3);
                DrawDot(dc, width * .74, 24, 1.6);
                break;
            case (DecorationDesign.Aurora, DecorationZone.Composer):
                dc.DrawLine(brightLine, new Point(14, 1.5), new Point(width - 14, 1.5));
                break;

            case (DecorationDesign.Arcade, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                for (int y = 6; y < height; y += 6)
                    dc.DrawLine(softLine, new Point(0, y + .5), new Point(width, y + .5));
                DrawCornerBrackets(dc, width, height, 12, brightLine, margin: 4);
                break;
            case (DecorationDesign.Arcade, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawCornerBrackets(dc, width, height, 8, accentLine, margin: 3);
                break;
            case (DecorationDesign.Arcade, DecorationZone.Title):
                for (int i = 0; i < 6; i++)
                    dc.DrawRectangle(i % 2 == 0 ? glowFill : accentFill, null, new Rect(14 + i * 14, height / 2 - 3, 10, 6));
                break;
            case (DecorationDesign.Arcade, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                for (int y = 8; y < height - 6; y += 6)
                    dc.DrawLine(softLine, new Point(0, y + .5), new Point(width, y + .5));
                dc.DrawLine(brightLine, new Point(16, height - 2), new Point(width - 16, height - 2));
                dc.DrawRectangle(glowFill, null, new Rect(16, height - 5, 16, 3));
                dc.DrawRectangle(glowFill, null, new Rect(width - 32, height - 5, 16, 3));
                break;
            case (DecorationDesign.Arcade, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 10, brightLine, margin: 3);
                break;

            case (DecorationDesign.Pixel, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawPixelCluster(dc, width - 22, 10);
                DrawPixelCluster(dc, width - 22, height - 22);
                break;
            case (DecorationDesign.Pixel, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                for (int i = 0; i < 3; i++)
                    dc.DrawRectangle(accentFill, null, new Rect(18 + i * 5, height * .53 + i * 5, 4, 4));
                break;
            case (DecorationDesign.Pixel, DecorationZone.Title):
                for (int i = 0; i < 5; i++)
                    dc.DrawRectangle(i % 2 == 0 ? glowFill : accentFill, null, new Rect(18 + i * 14, height / 2 - 4 + (i % 2) * 3, 8, 5));
                break;
            case (DecorationDesign.Pixel, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                for (double x = 20; x < width - 24; x += 14)
                    dc.DrawRectangle(accentFill, null, new Rect(x, height - 4, 8, 3));
                DrawPixelCluster(dc, width - 30, 10);
                break;
            case (DecorationDesign.Pixel, DecorationZone.Composer):
                dc.DrawRectangle(accentFill, null, new Rect(3, 3, 6, 3));
                dc.DrawRectangle(accentFill, null, new Rect(width - 9, 3, 6, 3));
                dc.DrawRectangle(accentFill, null, new Rect(3, height - 6, 6, 3));
                dc.DrawRectangle(accentFill, null, new Rect(width - 9, height - 6, 6, 3));
                break;

            case (DecorationDesign.Ocean, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawBubble(dc, width - 24, height - 28, 5);
                DrawBubble(dc, width - 15, height - 42, 3);
                DrawBubble(dc, width * .62, 22, 4);
                break;
            case (DecorationDesign.Ocean, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawBubble(dc, width / 2, height * .56, 4.5);
                break;
            case (DecorationDesign.Ocean, DecorationZone.Title):
                DrawWave(dc, 10, height / 2 - 1, width - 20, 5, 2.0, brightLine!);
                DrawWave(dc, 10, height / 2 + 4, width - 20, 4, 2.0, softLine!);
                break;
            case (DecorationDesign.Ocean, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawWave(dc, 16, height - 10, width - 32, 5, 3.0, accentLine!);
                DrawWave(dc, 16, height - 5, width - 32, 4, 3.0, softLine!);
                DrawBubble(dc, width - 36, 20, 4);
                DrawBubble(dc, width - 48, 28, 2.5);
                break;
            case (DecorationDesign.Ocean, DecorationZone.Composer):
                DrawWave(dc, 16, height - 4, width - 32, 2.5, 4.0, softLine!);
                break;

            case (DecorationDesign.Lavender, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawDiamond(dc, width - 22, 22, 1.1);
                DrawDiamond(dc, width - 24, height - 24, .9);
                DrawDiamond(dc, width - 30, height * .6, .75);
                break;
            case (DecorationDesign.Lavender, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawDiamond(dc, width / 2, height * .56, .95);
                break;
            case (DecorationDesign.Lavender, DecorationZone.Title):
                DrawDiamond(dc, width - 48, height / 2, 1.0);
                DrawDiamond(dc, width - 30, height / 2 - 4, .75);
                DrawDiamond(dc, width - 16, height / 2 + 3, .8);
                break;
            case (DecorationDesign.Lavender, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawDiamond(dc, width - 34, 20, 1.0);
                DrawDiamond(dc, width - 52, height - 20, .8);
                dc.DrawLine(softLine, new Point(20, height - 1.5), new Point(width - 20, height - 1.5));
                break;
            case (DecorationDesign.Lavender, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 9, softLine, margin: 4);
                break;

            case (DecorationDesign.Ember, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawDot(dc, width - 22, height - 20, 2.2);
                DrawDot(dc, width - 38, height - 34, 1.5);
                DrawDot(dc, width - 18, height - 46, 1.8);
                DrawDot(dc, width - 34, height - 62, 1.3);
                break;
            case (DecorationDesign.Ember, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawDot(dc, width / 2, height * .56, 1.8);
                DrawDot(dc, width / 2 + 6, height * .56 - 9, 1.2);
                break;
            case (DecorationDesign.Ember, DecorationZone.Title):
                DrawDot(dc, width - 56, height / 2 + 2, 1.7);
                DrawDot(dc, width - 40, height / 2 - 4, 2.1);
                DrawDot(dc, width - 22, height / 2 + 1, 1.5);
                break;
            case (DecorationDesign.Ember, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                dc.DrawLine(accentLine, new Point(16, height - 1.5), new Point(width - 16, height - 1.5));
                DrawDot(dc, width - 36, height - 14, 1.8);
                DrawDot(dc, width - 54, height - 22, 1.3);
                break;
            case (DecorationDesign.Ember, DecorationZone.Composer):
                dc.DrawRoundedRectangle(null, softLine, new Rect(2.5, 2.5, width - 5, height - 5), 9, 9);
                break;

            case (DecorationDesign.Sand, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawWave(dc, 0, height - 18, width, 7, 1.0, accentLine!);
                DrawWave(dc, 0, height - 10, width, 5, 1.2, softLine!);
                break;
            case (DecorationDesign.Sand, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawWave(dc, 0, height * .56, width, 4, 1.0, accentLine!);
                break;
            case (DecorationDesign.Sand, DecorationZone.Title):
                DrawWave(dc, 8, height / 2 + 2, width - 16, 5, 1.1, accentLine!);
                break;
            case (DecorationDesign.Sand, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawWave(dc, 16, height - 9, width - 32, 6, 1.5, accentLine!);
                DrawWave(dc, 16, height - 4, width - 32, 4, 1.8, softLine!);
                break;
            case (DecorationDesign.Sand, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 9, softLine, margin: 4);
                break;

            case (DecorationDesign.Cyberpunk, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                dc.DrawLine(accentLine, new Point(10, 0), new Point(10, height * .42));
                dc.DrawLine(accentLine, new Point(10, height * .42), new Point(18, height * .42 + 8));
                dc.DrawLine(brightLine, new Point(width - 2, 16), new Point(width - 2, height - 16));
                DrawCornerBrackets(dc, width, height, 12, brightLine, margin: 4);
                break;
            case (DecorationDesign.Cyberpunk, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                dc.DrawLine(brightLine, new Point(width - 1.5, 12), new Point(width - 1.5, height - 12));
                DrawCornerBrackets(dc, width, height, 7, accentLine, margin: 3);
                break;
            case (DecorationDesign.Cyberpunk, DecorationZone.Title):
                dc.DrawLine(brightLine, new Point(6, height / 2), new Point(width - 24, height / 2));
                dc.DrawLine(accentLine, new Point(width - 24, height / 2), new Point(width - 14, height / 2 - 6));
                dc.DrawRectangle(glowFill, null, new Rect(10, height / 2 - 2, 18, 4));
                break;
            case (DecorationDesign.Cyberpunk, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                dc.DrawLine(brightLine, new Point(16, height - 2.5), new Point(width - 42, height - 2.5));
                dc.DrawLine(brightLine, new Point(width - 42, height - 2.5), new Point(width - 30, height - 12));
                dc.DrawLine(accentLine, new Point(width - 30, height - 12), new Point(width - 14, height - 12));
                dc.DrawRectangle(glowFill, null, new Rect(16, height - 5, 22, 3));
                break;
            case (DecorationDesign.Cyberpunk, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 12, brightLine, margin: 3);
                break;

            case (DecorationDesign.Midnight, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawStar(dc, width - 24, 22, .9);
                DrawStar(dc, width - 38, 34, .6);
                DrawDot(dc, width - 22, height - 22, 1.8);
                DrawDot(dc, width - 36, height - 34, 1.3);
                break;
            case (DecorationDesign.Midnight, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawStar(dc, width / 2, height * .56, .85);
                break;
            case (DecorationDesign.Midnight, DecorationZone.Title):
                DrawStar(dc, width - 52, height / 2, .95);
                DrawDot(dc, width - 34, height / 2 - 4, 1.5);
                DrawDot(dc, width - 20, height / 2 + 3, 1.3);
                break;
            case (DecorationDesign.Midnight, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawStar(dc, width - 36, 20, 1.0);
                DrawDot(dc, width - 56, 16, 1.4);
                DrawDot(dc, width - 48, height - 18, 1.5);
                dc.DrawLine(softLine, new Point(18, height - 1.5), new Point(width - 18, height - 1.5));
                break;
            case (DecorationDesign.Midnight, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 9, softLine, margin: 4);
                break;

            case (DecorationDesign.Sunset, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawWave(dc, 0, height - 20, width, 6, 1.0, accentLine!);
                DrawWave(dc, 0, height - 12, width, 4, 1.5, softLine!);
                break;
            case (DecorationDesign.Sunset, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                dc.DrawEllipse(accentFill, brightLine, new Point(width / 2, height * .56), 5, 5);
                break;
            case (DecorationDesign.Sunset, DecorationZone.Title):
                dc.DrawEllipse(accentFill, brightLine, new Point(width - 36, height / 2 + 2), 6, 6);
                dc.DrawLine(softLine, new Point(12, height / 2 + 4), new Point(width - 12, height / 2 + 4));
                break;
            case (DecorationDesign.Sunset, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawWave(dc, 16, height - 9, width - 32, 6, 2.0, brightLine!);
                DrawWave(dc, 16, height - 4, width - 32, 4, 2.5, softLine!);
                break;
            case (DecorationDesign.Sunset, DecorationZone.Composer):
                dc.DrawLine(accentLine, new Point(14, height - 2.5), new Point(width - 14, height - 2.5));
                break;

            case (DecorationDesign.Mint, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawLeaf(dc, width - 28, 16);
                DrawLeaf(dc, width - 28, height - 28);
                DrawBubble(dc, width - 42, height - 16, 3);
                break;
            case (DecorationDesign.Mint, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawLeaf(dc, width / 2 - 7, height * .55);
                break;
            case (DecorationDesign.Mint, DecorationZone.Title):
                DrawLeaf(dc, width - 42, (height - 12) / 2);
                DrawBubble(dc, width - 20, height / 2, 3);
                break;
            case (DecorationDesign.Mint, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawLeaf(dc, width - 40, height - 18);
                DrawBubble(dc, width - 56, height - 14, 2.5);
                dc.DrawLine(accentLine, new Point(18, height - 1.5), new Point(width - 18, height - 1.5));
                break;
            case (DecorationDesign.Mint, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 9, accentLine, margin: 4);
                break;

            case (DecorationDesign.Matcha, DecorationZone.Sidebar):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawWave(dc, 0, height - 24, width, 5, 1.2, softLine!);
                DrawLeaf(dc, width - 28, height - 20);
                break;
            case (DecorationDesign.Matcha, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                dc.DrawEllipse(null, accentLine, new Point(width / 2, height * .56), 5.5, 5.5);
                break;
            case (DecorationDesign.Matcha, DecorationZone.Title):
                dc.DrawEllipse(null, accentLine, new Point(width - 36, height / 2), 6, 6);
                dc.DrawLine(softLine, new Point(12, height / 2 + 5), new Point(width - 12, height / 2 + 5));
                break;
            case (DecorationDesign.Matcha, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                dc.DrawEllipse(null, accentLine, new Point(width - 32, height - 16), 7, 7);
                dc.DrawLine(accentLine, new Point(16, height - 2.5), new Point(width - 16, height - 2.5));
                break;
            case (DecorationDesign.Matcha, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 8, softLine, margin: 4);
                break;

            case (DecorationDesign.Frost, DecorationZone.Sidebar):
                dc.DrawRectangle(softWash, null, bounds);
                DrawDiamond(dc, width - 22, 20, 1.1);
                DrawDiamond(dc, width - 22, height - 22, 1.0);
                DrawCornerBrackets(dc, width, height, 10, softLine, margin: 4);
                break;
            case (DecorationDesign.Frost, DecorationZone.Rail):
                dc.DrawRectangle(subtleWash, null, bounds);
                DrawDiamond(dc, width / 2, height * .56, 1.0);
                break;
            case (DecorationDesign.Frost, DecorationZone.Title):
                DrawDiamond(dc, width - 48, height / 2, 1.05);
                DrawDiamond(dc, width - 28, height / 2, .85);
                break;
            case (DecorationDesign.Frost, DecorationZone.Header):
                dc.DrawRectangle(headerWash, null, bounds);
                DrawDiamond(dc, width - 34, 20, 1.1);
                DrawDiamond(dc, width - 52, height - 18, .85);
                dc.DrawLine(brightLine, new Point(16, height - 1.5), new Point(width - 16, height - 1.5));
                break;
            case (DecorationDesign.Frost, DecorationZone.Composer):
                DrawCornerBrackets(dc, width, height, 10, brightLine, margin: 4);
                break;
        }
    }

    private void DrawSakuraTwig(DrawingContext dc, double x, double y)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.DrawGeometry(null, Branch, Twig);
        DrawBlossom(dc, 28, 14);
        DrawBlossom(dc, 52, 6);
        DrawBlossom(dc, 74, 5);
        DrawBlossom(dc, 90, 12);
        dc.Pop();
    }

    private void DrawLeaf(DrawingContext dc, double x, double y)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.DrawGeometry(null, LeafOutline, Leaf);
        dc.Pop();
    }

    private void DrawStar(DrawingContext dc, double x, double y, double scale)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(glowFill, null, Star);
        dc.Pop();
        dc.Pop();
    }

    private void DrawDiamond(DrawingContext dc, double x, double y, double scale)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(accentFill, brightLine, Diamond);
        dc.Pop();
        dc.Pop();
    }

    private void DrawBubble(DrawingContext dc, double x, double y, double radius)
    {
        dc.DrawEllipse(null, brightLine, new Point(x, y), radius, radius);
        dc.DrawEllipse(glowFill, null, new Point(x - radius * .3, y - radius * .3), radius * .25, radius * .25);
    }

    private void DrawDot(DrawingContext dc, double x, double y, double radius) =>
        dc.DrawEllipse(glowFill, null, new Point(x, y), radius, radius);

    private void DrawPixelCluster(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(accentFill, null, new Rect(x, y, 4, 4));
        dc.DrawRectangle(glowFill, null, new Rect(x + 6, y, 4, 4));
        dc.DrawRectangle(accentFill, null, new Rect(x + 6, y + 6, 4, 4));
        dc.DrawRectangle(accentFill, null, new Rect(x + 12, y + 6, 4, 4));
    }

    private static void DrawWave(DrawingContext dc, double startX, double baseY, double width, double amplitude, double cycles, Pen pen)
    {
        if (width <= 4) return;
        const int steps = 20;
        var wave = new StreamGeometry();
        using (var ctx = wave.Open())
        {
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                var pt = new Point(startX + t * width, baseY + Math.Sin(t * Math.Tau * cycles) * amplitude);
                if (i == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
            }
        }
        wave.Freeze();
        dc.DrawGeometry(null, pen, wave);
    }

    private static void DrawCornerBrackets(DrawingContext dc, double width, double height, double leg, Pen? pen, double margin)
    {
        if (pen is null || width < leg * 3 || height < leg * 2) return;
        double l = margin, t = margin, r = width - margin, b = height - margin;
        dc.DrawLine(pen, new Point(l, t + leg), new Point(l, t));
        dc.DrawLine(pen, new Point(l, t), new Point(l + leg, t));
        dc.DrawLine(pen, new Point(r - leg, t), new Point(r, t));
        dc.DrawLine(pen, new Point(r, t), new Point(r, t + leg));
        dc.DrawLine(pen, new Point(l, b - leg), new Point(l, b));
        dc.DrawLine(pen, new Point(l, b), new Point(l + leg, b));
        dc.DrawLine(pen, new Point(r - leg, b), new Point(r, b));
        dc.DrawLine(pen, new Point(r, b), new Point(r, b - leg));
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

    private static Brush Solid(uint color) => FreezeBrush(new SolidColorBrush(Argb(color)));

    private static Brush Gradient(uint from, uint to) => FreezeBrush(new LinearGradientBrush(Argb(from), Argb(to), 90));

    private static Brush VerticalWash(Color top, Color middle, Color bottom)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(middle, .5));
        brush.GradientStops.Add(new GradientStop(bottom, 1));
        return FreezeBrush(brush);
    }

    private static Brush HorizontalWash(Color left, Color middle, Color right)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        brush.GradientStops.Add(new GradientStop(left, 0));
        brush.GradientStops.Add(new GradientStop(middle, .6));
        brush.GradientStops.Add(new GradientStop(right, 1));
        return FreezeBrush(brush);
    }

    private static Brush FreezeBrush(Brush brush) { brush.Freeze(); return brush; }
    private static Pen FreezePen(Pen pen) { pen.Freeze(); return pen; }

    private static Pen Stroke(uint color, double thickness) => FreezePen(new Pen(Solid(color), thickness));

    private static Geometry Shape(string value)
    {
        var shape = Geometry.Parse(value);
        shape.Freeze();
        return shape;
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
    private static Color Argb(uint color) => Color.FromArgb((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color);
}
