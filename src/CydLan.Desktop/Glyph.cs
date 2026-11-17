using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace CydLan.Desktop;

/// <summary>Draws a 24x24 outline icon geometry with a constant on-screen stroke, inheriting the text colour.</summary>
public sealed class Glyph : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(Geometry), typeof(Glyph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(Glyph),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(Glyph),
        new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(Glyph),
        new FrameworkPropertyMetadata(1.8, FrameworkPropertyMetadataOptions.AffectsRender));

    public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    // Defaults go in metadata (not the constructor) so values set inside control templates still win.
    static Glyph()
    {
        HorizontalAlignmentProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(System.Windows.HorizontalAlignment.Center));
        VerticalAlignmentProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(System.Windows.VerticalAlignment.Center));
        SnapsToDevicePixelsProperty.OverrideMetadata(typeof(Glyph), new FrameworkPropertyMetadata(true));
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size available) => new(Size, Size);

    protected override void OnRender(DrawingContext dc)
    {
        if (Data is null || Size <= 0) return;
        double scale = Size / 24;
        var pen = new Pen(Foreground, StrokeThickness / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(null, pen, Data);
        dc.Pop();
    }
}
