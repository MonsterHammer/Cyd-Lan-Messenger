using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public sealed class SteampunkEngine : FrameworkElement
{
    private static readonly DependencyProperty ShaftAngleProperty = DependencyProperty.Register(
        nameof(ShaftAngle), typeof(double), typeof(SteampunkEngine),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(SteampunkEngine),
        new FrameworkPropertyMetadata(Color.FromRgb(230, 180, 107), FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((SteampunkEngine)d).housing = null));

    private Brush Iron = null!, Brass = null!, Glow = null!, Steam = null!, Alloy = null!, Copper = null!;
    private Pen Seam = null!, Trim = null!, Highlight = null!, Pipe = null!, Rod = null!;
    private DrawingGroup? housing;
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    private static readonly Geometry Flywheel = Cog(16, 15);
    private static readonly Geometry Pinion = Cog(12, 11.25);
    private Window? host;
    private bool subscribed;
    private bool running;
    private double activeSpeed = 1.0;
    private double accumulatedAngle;

    internal double ShaftAngle => (double)GetValue(ShaftAngleProperty);

    public SteampunkEngine()
    {
        SetResourceReference(TintProperty, "EffectColor");
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdateAnimation();
    }

    private void Attach()
    {
        if (subscribed) { UpdateAnimation(); return; }
        subscribed = true;
        host = Window.GetWindow(this);
        if (host is not null)
        {
            host.StateChanged += HostStateChanged;
            host.Closed += HostClosed;
        }
        SystemParameters.StaticPropertyChanged += SystemPropertyChanged;
        AmbientEffect.SpeedChanged += OnSpeedChanged;
        UpdateAnimation();
    }

    private void Detach()
    {
        if (!subscribed) return;
        subscribed = false;
        if (host is not null)
        {
            host.StateChanged -= HostStateChanged;
            host.Closed -= HostClosed;
        }
        host = null;
        SystemParameters.StaticPropertyChanged -= SystemPropertyChanged;
        AmbientEffect.SpeedChanged -= OnSpeedChanged;
        UpdateAnimation();
    }

    private void HostClosed(object? sender, EventArgs e) => Detach();
    private void OnSpeedChanged() => UpdateAnimation();

    private void HostStateChanged(object? sender, EventArgs e) => UpdateAnimation();

    private void SystemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            Dispatcher.BeginInvoke(UpdateAnimation);
    }

    private void UpdateAnimation()
    {
        bool shouldRun = host is { IsVisible: true, WindowState: not WindowState.Minimized } && IsVisible
            && SystemParameters.ClientAreaAnimation;
        double speed = AmbientEffect.SpeedMultiplier;
        if (running == shouldRun && (!shouldRun || Math.Abs(activeSpeed - speed) < .001)) return;
        if (running) accumulatedAngle = (accumulatedAngle + ShaftAngle) % 1080;
        running = shouldRun;
        activeSpeed = speed;
        InvalidateVisual();
        if (!running)
        {
            BeginAnimation(ShaftAngleProperty, null);
            return;
        }

        // Three flywheel turns keep the 16:12 gears in step; IsCumulative avoids any repeat hitch.
        var rotation = new DoubleAnimation(0, 1080, TimeSpan.FromSeconds(7.2 / speed))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            IsCumulative = true
        };
        Timeline.SetDesiredFrameRate(rotation, 24);
        BeginAnimation(ShaftAngleProperty, rotation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawDrawing(housing ??= BuildHousing());
        double angle = accumulatedAngle + ShaftAngle;
        DrawCog(dc, new Point(108, 17), Pinion, -angle * 4 / 3 + 15, 7);
        DrawCog(dc, new Point(133, 18), Flywheel, angle, 10);

        double radians = angle * Math.PI / 180;
        var crank = new Point(133 + Math.Cos(radians) * 8, 18 + Math.Sin(radians) * 8);
        const double rodLength = 61;
        double rise = crank.Y - 18;
        double piston = crank.X + Math.Sqrt(rodLength * rodLength - rise * rise);
        dc.DrawLine(Rod, crank, new Point(piston, 18));
        dc.DrawRoundedRectangle(Alloy, Seam, new Rect(piston - 3, 11, 8, 14), 1.2, 1.2);
        dc.DrawLine(Highlight, new Point(piston + 2, 13), new Point(piston + 2, 22));
        dc.DrawEllipse(Glow, Seam, crank, 2.2, 2.2);

        dc.PushTransform(new RotateTransform(-8 + Math.Sin(radians) * 28, 56, 18));
        dc.DrawLine(Highlight, new Point(56, 19), new Point(59, 11));
        dc.Pop();
        dc.DrawEllipse(Brass, Seam, new Point(56, 18), 1.8, 1.8);

        if (running)
        {
            double phase = (angle % 360) / 360;
            Puff(dc, phase);
            Puff(dc, (phase + 0.18) % 1);
        }
    }

    private void Puff(DrawingContext dc, double phase)
    {
        if (phase >= 0.44) return;
        double progress = phase / 0.44;
        dc.PushOpacity(0.66 * (1 - progress));
        dc.DrawEllipse(Steam, null, new Point(253 + progress * 28, 14 - progress * 8),
            2 + progress * 9, 1.2 + progress * 3);
        dc.Pop();
    }

    private void DrawCog(DrawingContext dc, Point center, Geometry cog, double angle, double rim)
    {
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new RotateTransform(angle));
        dc.DrawGeometry(Alloy, Seam, cog);
        dc.DrawEllipse(Iron, Trim, new Point(), rim, rim);
        for (int spoke = 0; spoke < 4; spoke++)
        {
            double direction = spoke * Math.PI / 4;
            double x = Math.Cos(direction) * rim;
            double y = Math.Sin(direction) * rim;
            dc.DrawLine(Rod, new Point(-x, -y), new Point(x, y));
        }
        dc.DrawEllipse(Copper, Trim, new Point(), 3.4, 3.4);
        dc.Pop();
        dc.Pop();
    }

    private DrawingGroup BuildHousing()
    {
        Color Shade(double amount) => EffectPalette.Blend(Tint, Colors.Black, amount);
        Color Shine(double amount) => EffectPalette.Blend(Tint, Colors.White, amount);
        Iron = Paint(Shade(.87));
        Brass = Paint(Tint);
        Glow = Paint(Shine(.45));
        Steam = Paint(Shine(.7));
        Alloy = Metal(Shine(.25), Shade(.32), Shade(.66));
        Copper = Metal(Tint, Shade(.38), Shade(.7));
        Seam = Edge(Iron, 1);
        Trim = Edge(Brass, .75);
        Highlight = Edge(Glow, .75);
        Pipe = Edge(Copper, 5);
        Rod = Edge(Alloy, 3);

        var housing = new DrawingGroup();
        using (var dc = housing.Open())
        {
            dc.DrawRoundedRectangle(Iron, Seam, new Rect(2, 3, 290, 30), 11, 11);
            dc.DrawRoundedRectangle(null, Trim, new Rect(4, 5, 286, 26), 9, 9);
            dc.DrawGeometry(null, Pipe, Geometry.Parse("M24,18 H42 M67,19 H75 Q82,19 82,24 Q82,28 88,28 H98 M216,18 H237 Q243,18 243,23 H252 V16"));
            dc.DrawGeometry(null, Trim, Geometry.Parse("M23,17 H42 M68,18 H75 Q83,18 83,24 Q83,27 88,27 H98 M218,17 H237 Q244,17 244,22 H252"));

            dc.DrawRoundedRectangle(Copper, Seam, new Rect(13, 10, 22, 16), 4, 4);
            dc.DrawGeometry(null, Highlight, Geometry.Parse("M17,13 V23 M21,12 V24 M25,12 V24 M29,13 V23"));
            dc.DrawEllipse(Alloy, Seam, new Point(56, 18), 12, 12);
            dc.DrawEllipse(Iron, Trim, new Point(56, 18), 9, 9);
            dc.DrawGeometry(null, Trim, Geometry.Parse("M49,19 L51,19 M51,13 L52,15 M56,11 V13 M61,13 L60,15 M63,18 H61"));

            dc.DrawRoundedRectangle(Copper, Trim, new Rect(178, 8, 49, 20), 3, 3);
            dc.DrawRoundedRectangle(Iron, Seam, new Rect(179, 11, 39, 14), 1, 1);
            dc.DrawGeometry(null, Highlight, Geometry.Parse("M220,11 V25 M224,12 V24"));
            dc.DrawRoundedRectangle(Alloy, Seam, new Rect(247, 13, 11, 5), 1, 1);
            dc.DrawEllipse(Glow, Seam, new Point(9, 18), 1.5, 1.5);
            dc.DrawEllipse(Glow, Seam, new Point(284, 18), 1.5, 1.5);
        }
        housing.Freeze();
        return housing;
    }

    private static Geometry Cog(int teeth, double radius)
    {
        var outline = new StreamGeometry();
        using (var points = outline.Open())
        {
            for (int point = 0; point < teeth * 4; point++)
            {
                double angle = point * Math.Tau / (teeth * 4);
                double edge = point % 4 is 1 or 2 ? radius : radius * 0.85;
                var position = new Point(Math.Cos(angle) * edge, Math.Sin(angle) * edge);
                if (point == 0) points.BeginFigure(position, isFilled: true, isClosed: true);
                else points.LineTo(position, isStroked: true, isSmoothJoin: false);
            }
        }
        outline.Freeze();
        return outline;
    }

    private static Brush Paint(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Brush Metal(Color light, Color middle, Color dark)
    {
        var brush = new LinearGradientBrush(light, dark, 90);
        brush.GradientStops.Add(new GradientStop(middle, 0.55));
        brush.Freeze();
        return brush;
    }

    private static Pen Edge(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }
}
