using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace CydLan.Desktop;

public sealed record LogoOption(string Value, string Name, ImageSource Preview);

/// <summary>Alternate CydLan marks drawn in a 64×64 box: a stroke color plus a contrasting dot color.</summary>
public static class LogoCatalog
{
    public const string Classic = "classic";

    private enum Part { Stroke, Fill, Dot }
    private sealed record Piece(Part Kind, Geometry Geometry, double Thickness = 7);

    private static Piece S(string path, double t = 6.5) => new(Part.Stroke, Geometry.Parse(path), t);
    private static Piece F(string path) => new(Part.Fill, Geometry.Parse(path));
    private static Piece D(double x, double y, double r) => new(Part.Dot, new EllipseGeometry(new Point(x, y), r, r));
    private static Piece FC(double x, double y, double r) => new(Part.Fill, new EllipseGeometry(new Point(x, y), r, r));
    private static Piece Ring(double x, double y, double rx, double ry, double t = 6) => new(Part.Stroke, new EllipseGeometry(new Point(x, y), rx, ry), t);

    private static readonly (string Value, string Name, Piece[] Pieces)[] Styles =
    [
        (Classic, "Classic", [S("M44,17 C40,13 35,11 30,11 C18,11 10,20 10,32 C10,39 13,45 18,49 L15,57 L29,53 C35,53 40,51 44,47", 7), S("M32,32 L44,32", 5), D(50, 32, 5)]),
        ("bubble", "Bubble", [S("M14,12 H50 Q56,12 56,18 V38 Q56,44 50,44 H30 L19,54 V44 H14 Q8,44 8,38 V18 Q8,12 14,12 Z"), D(22, 28, 3.5), D(32, 28, 3.5), D(42, 28, 3.5)]),
        ("orbit", "Orbit", [Ring(32, 32, 23, 14), FC(32, 32, 8), D(52, 22, 4.5)]),
        ("bolt", "Bolt", [S("M37,6 L16,36 H30 L26,58 L48,26 H34 Z", 5.5), D(50, 50, 4)]),
        ("heart", "Heart", [S("M32,54 C12,41 7,29 10,21 C13,12 26,10 32,20 C38,10 51,12 54,21 C57,29 52,41 32,54 Z"), D(44, 22, 4)]),
        ("star", "Star", [S("M32,7 L39,24 L57,25 L43,37 L48,55 L32,45 L16,55 L21,37 L7,25 L25,24 Z", 5.5), D(32, 32, 4)]),
        ("spark", "Spark", [F("M32,4 Q36,28 60,32 Q36,36 32,60 Q28,36 4,32 Q28,28 32,4 Z"), D(50, 14, 4)]),
        ("paw", "Paw", [new(Part.Fill, new EllipseGeometry(new Point(32, 42), 13, 11)), FC(16, 28, 5.5), FC(26, 18, 5.5), FC(38, 18, 5.5), FC(48, 28, 5.5), D(32, 43, 4)]),
        ("leaf", "Leaf", [S("M12,52 C12,24 30,10 54,10 C54,34 40,52 12,52 Z", 5.5), S("M12,52 L40,24", 4), D(46, 18, 3.5)]),
        ("wave", "Wave", [S("M6,38 Q16,22 26,38 T46,38", 6.5), S("M18,50 Q28,36 38,50 T58,50", 4.5), D(54, 22, 5)]),
        ("diamond", "Gem", [S("M20,10 H44 L56,26 L32,56 L8,26 Z", 5), S("M8,26 H56 M24,10 L32,26 L40,10", 3.5), D(32, 40, 3.5)]),
        ("rocket", "Rocket", [S("M32,6 C44,16 46,32 40,46 H24 C18,32 20,16 32,6 Z", 5.5), S("M24,40 L15,51 L24,48 M40,40 L49,51 L40,48", 4.5), D(32, 25, 5), D(32, 55, 3)]),
        ("ghost", "Ghost", [S("M14,56 V28 C14,16 22,8 32,8 C42,8 50,16 50,28 V56 L44,50 L38,56 L32,50 L26,56 L20,50 Z", 5.5), D(26, 28, 4), D(38, 28, 4)]),
        ("cat", "Cat", [S("M12,24 L17,8 L28,18 H36 L47,8 L52,24 C56,38 49,55 32,55 C15,55 8,38 12,24 Z", 5.5), D(24, 34, 3.5), D(40, 34, 3.5), S("M29,43 L32,46 L35,43", 3)]),
        ("pixel", "Pixel", [F("M20,8 H44 V16 H20 Z M12,16 H20 V48 H12 Z M20,48 H44 V56 H20 Z M44,16 H52 V24 H44 Z M44,40 H52 V48 H44 Z"), new(Part.Dot, Geometry.Parse("M30,28 H38 V36 H30 Z"))]),
        ("crown", "Crown", [S("M10,48 L8,18 L22,32 L32,12 L42,32 L56,18 L54,48 Z", 5.5), S("M10,56 H54", 5), D(32, 40, 4)]),
        ("planet", "Planet", [FC(32, 32, 14), new(Part.Stroke, Geometry.Parse("M6,40 C2,30 62,16 58,26 C56,32 34,40 14,42"), 4.5), D(50, 12, 3.5)]),
        ("flame", "Flame", [S("M32,58 C18,58 12,48 14,38 C16,28 26,24 26,12 C36,18 40,26 38,34 C42,32 44,28 44,24 C52,32 54,42 50,50 C47,55 40,58 32,58 Z", 5.5), D(32, 46, 5)]),
        ("note", "Music", [S("M24,48 V14 L50,8 V42", 5.5), FC(18, 48, 7), FC(44, 42, 7), D(50, 8, 3)]),
        ("smile", "Smile", [Ring(32, 32, 24, 24, 6), D(24, 26, 4), D(40, 26, 4), S("M21,38 Q32,49 43,38", 5)])
    ];

    public static IReadOnlyList<string> Values { get; } = Styles.Select(s => s.Value).ToArray();

    public static string Normalize(string? value) => Styles.Any(s => s.Value == value) ? value! : Classic;

    public static DrawingImage Build(string? style, Color stroke, Color dot, string? dotEffect = null)
    {
        var pieces = Styles.First(s => s.Value == Normalize(style)).Pieces;
        string effect = LogoDotEffects.Normalize(dotEffect);
        var strokeBrush = Frozen(stroke);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, Geometry.Parse("M0,0 H64 V64 H0 Z")));
        int dotIndex = 0;
        foreach (var piece in pieces)
        {
            group.Children.Add(piece.Kind switch
            {
                Part.Stroke => new GeometryDrawing(null, new Pen(strokeBrush, piece.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, piece.Geometry),
                Part.Fill => new GeometryDrawing(strokeBrush, null, piece.Geometry),
                _ => LogoDotEffects.Dot(piece.Geometry, dot, effect, dotIndex++)
            });
        }
        var image = new DrawingImage(group);
        if (effect == LogoDotEffects.None) image.Freeze();
        return image;
    }

    /// <summary>The logo on the dark rounded tile used for the window, taskbar and tray icon.</summary>
    public static DrawingImage BuildAppIcon(string? style, Color stroke, Color dot)
    {
        var tile = new SolidColorBrush(Color.FromRgb(0x20, 0x27, 0x29));
        tile.Freeze();
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(tile, null, Geometry.Parse("M15,0 H49 Q64,0 64,15 V49 Q64,64 49,64 H15 Q0,64 0,49 V15 Q0,0 15,0 Z")));
        group.Children.Add(Build(style, stroke, dot).Drawing);
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    public static IReadOnlyList<LogoOption> Options(Color stroke, Color dot) =>
        Styles.Select(s => new LogoOption(s.Value, s.Name, Build(s.Value, stroke, dot))).ToList();

    private static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}

/// <summary>Logo motion presets; the CydLan mark everywhere in the app plays the chosen one.</summary>
public static class LogoEffects
{
    public const string None = "none";

    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new(None, "None"),
        new("glow", "Glow"),
        new("pulse", "Pulse"),
        new("heartbeat", "Heartbeat"),
        new("spin", "Spin"),
        new("wobble", "Wobble"),
        new("bounce", "Bounce"),
        new("float", "Float"),
        new("flip", "Flip"),
        new("jelly", "Jelly"),
        new("rainbow", "Rainbow"),
        new("neon", "Neon flicker")
    ];

    public static string Normalize(string? value) => All.Any(o => o.Value == value) ? value! : None;
}

/// <summary>Motion for the logo's dot(s), drawn into the mark itself so every copy of the logo plays it.</summary>
public static class LogoDotEffects
{
    public const string None = "none";

    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new(None, "None"),
        new("pulse", "Pulse"),
        new("blink", "Blink"),
        new("glow", "Glow"),
        new("ripple", "Ripple"),
        new("heartbeat", "Heartbeat"),
        new("bounce", "Bounce"),
        new("orbit", "Orbit"),
        new("rainbow", "Rainbow"),
        new("twinkle", "Twinkle")
    ];

    public static string Normalize(string? value) => All.Any(o => o.Value == value) ? value! : None;

    private static DoubleAnimation Loop(double from, double to, double seconds, double delay = 0) =>
        new(from, to, TimeSpan.FromSeconds(seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };

    private static DoubleAnimationUsingKeyFrames Keys(double seconds, double delay, bool discrete, params (double T, double V)[] frames)
    {
        var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(seconds), BeginTime = TimeSpan.FromSeconds(delay) };
        foreach (var (t, v) in frames)
        {
            var time = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t));
            anim.KeyFrames.Add(discrete ? new DiscreteDoubleKeyFrame(v, time) : new EasingDoubleKeyFrame(v, time));
        }
        return anim;
    }

    /// <summary>Builds one dot; with an effect it stays unfrozen so its animations run.</summary>
    internal static Drawing Dot(Geometry geometry, Color color, string effect, int index)
    {
        if (effect == None)
        {
            var still = new SolidColorBrush(color);
            still.Freeze();
            return new GeometryDrawing(still, null, geometry);
        }

        var bounds = geometry.Bounds;
        var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        double r = Math.Max(bounds.Width, bounds.Height) / 2;
        double stagger = index * .18;
        var brush = new SolidColorBrush(color);
        var group = new DrawingGroup();
        var body = new GeometryDrawing(brush, null, geometry);

        switch (effect)
        {
            case "pulse":
            {
                var scale = new ScaleTransform(1, 1, center.X, center.Y);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(1, 1.4, .8, stagger));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(1, 1.4, .8, stagger));
                group.Transform = scale;
                group.Children.Add(body);
                break;
            }
            case "blink":
                group.Children.Add(body);
                group.BeginAnimation(DrawingGroup.OpacityProperty, Keys(2.4, stagger, true, (0, 1), (1.6, 0), (1.9, 1), (2.4, 1)));
                break;
            case "glow":
            {
                var halo = new RadialGradientBrush(Color.FromArgb(0xCC, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                var haloGroup = new DrawingGroup { Children = { new GeometryDrawing(halo, null, new EllipseGeometry(center, r * 2.6, r * 2.6)) } };
                haloGroup.BeginAnimation(DrawingGroup.OpacityProperty, Loop(.25, 1, 1.4, stagger));
                group.Children.Add(haloGroup);
                group.Children.Add(body);
                break;
            }
            case "ripple":
            {
                var ring = new EllipseGeometry(center, r, r);
                var grow = new DoubleAnimation(r, r * 2.8, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(stagger) };
                ring.BeginAnimation(EllipseGeometry.RadiusXProperty, grow);
                ring.BeginAnimation(EllipseGeometry.RadiusYProperty, grow);
                var ringGroup = new DrawingGroup { Children = { new GeometryDrawing(null, new Pen(brush, Math.Max(1.2, r * .45)), ring) } };
                ringGroup.BeginAnimation(DrawingGroup.OpacityProperty, new DoubleAnimation(.9, 0, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(stagger) });
                group.Children.Add(ringGroup);
                group.Children.Add(body);
                break;
            }
            case "heartbeat":
            {
                var scale = new ScaleTransform(1, 1, center.X, center.Y);
                var beat = Keys(1.3, stagger, false, (0, 1), (.12, 1.45), (.24, 1), (.36, 1.35), (.5, 1), (1.3, 1));
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, beat);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, beat);
                group.Transform = scale;
                group.Children.Add(body);
                break;
            }
            case "bounce":
            {
                var shift = new TranslateTransform();
                shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -Math.Max(4, r * 1.3), TimeSpan.FromSeconds(.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(stagger), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                group.Transform = shift;
                group.Children.Add(body);
                break;
            }
            case "orbit":
            {
                var spin = new RotateTransform(0, 32, 32);
                spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3.2)) { RepeatBehavior = RepeatBehavior.Forever });
                group.Transform = spin;
                group.Children.Add(body);
                break;
            }
            case "rainbow":
            {
                var hues = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(3.6), BeginTime = TimeSpan.FromSeconds(stagger) };
                Color[] colors = [Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0x84, 0xCC, 0x16), Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xEC, 0x48, 0x99), Color.FromRgb(0xEF, 0x44, 0x44)];
                for (int i = 0; i < colors.Length; i++)
                    hues.KeyFrames.Add(new LinearColorKeyFrame(colors[i], KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.6 * i / (colors.Length - 1)))));
                brush.BeginAnimation(SolidColorBrush.ColorProperty, hues);
                group.Children.Add(body);
                break;
            }
            case "twinkle":
            {
                var scale = new ScaleTransform(1, 1, center.X, center.Y);
                var twinkle = Keys(1.8, stagger * 2, false, (0, 1), (.9, 1), (1.05, .35), (1.2, 1.3), (1.4, 1), (1.8, 1));
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, twinkle);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, twinkle);
                group.Transform = scale;
                group.Children.Add(body);
                group.BeginAnimation(DrawingGroup.OpacityProperty, Keys(1.8, stagger * 2, false, (0, 1), (.9, 1), (1.05, .3), (1.2, 1), (1.8, 1)));
                break;
            }
            default:
                group.Children.Add(body);
                break;
        }
        return group;
    }
}

/// <summary>The app's logo image: follows the BrandMark resource and plays the LogoEffect resource.</summary>
public sealed class BrandLogo : Image
{
    public static readonly DependencyProperty LogoEffectProperty = DependencyProperty.Register(
        nameof(LogoEffect), typeof(string), typeof(BrandLogo), new PropertyMetadata(LogoEffects.None, (d, _) => ((BrandLogo)d).Restart()));

    public string LogoEffect { get => (string)GetValue(LogoEffectProperty); set => SetValue(LogoEffectProperty, value); }

    private readonly ScaleTransform scale = new(1, 1);
    private readonly RotateTransform rotate = new(0);
    private readonly TranslateTransform shift = new(0, 0);

    public BrandLogo()
    {
        SetResourceReference(SourceProperty, "BrandMark");
        SetResourceReference(LogoEffectProperty, "LogoEffect");
        RenderTransformOrigin = new Point(.5, .5);
        RenderTransform = new TransformGroup { Children = { scale, rotate, shift } };
        Loaded += (_, _) => Restart();
        Unloaded += (_, _) => Stop();
    }

    private void Stop()
    {
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        shift.BeginAnimation(TranslateTransform.YProperty, null);
        BeginAnimation(OpacityProperty, null);
        Effect = null;
    }

    private static DoubleAnimation Loop(double from, double to, double seconds, bool reverse = true, IEasingFunction? ease = null) =>
        new(from, to, TimeSpan.FromSeconds(seconds)) { AutoReverse = reverse, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease ?? new SineEase { EasingMode = EasingMode.EaseInOut } };

    private Color GlowColor() => TryFindResource("EffectColor") is Color c ? c : Color.FromRgb(0x85, 0xB1, 0xBF);

    private void Restart()
    {
        Stop();
        if (!IsLoaded) return;
        double size = Math.Max(ActualWidth, Width is > 0 ? Width : 20);
        switch (LogoEffects.Normalize(LogoEffect))
        {
            case "glow":
            {
                var glow = new DropShadowEffect { ShadowDepth = 0, BlurRadius = Math.Max(6, size * .45), Color = GlowColor(), Opacity = .9 };
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, Loop(.35, 1, 1.6));
                Effect = glow;
                break;
            }
            case "pulse":
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(1, 1.14, .9));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(1, 1.14, .9));
                break;
            case "heartbeat":
            {
                var beat = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(1.3) };
                foreach (var (t, v) in new[] { (0.0, 1.0), (.12, 1.2), (.24, 1.0), (.36, 1.16), (.5, 1.0), (1.3, 1.0) })
                    beat.KeyFrames.Add(new EasingDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t))));
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, beat);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, beat);
                break;
            }
            case "spin":
                rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(4)) { RepeatBehavior = RepeatBehavior.Forever });
                break;
            case "wobble":
                rotate.BeginAnimation(RotateTransform.AngleProperty, Loop(-12, 12, .7));
                break;
            case "bounce":
                shift.BeginAnimation(TranslateTransform.YProperty, Loop(0, -size * .18, .45, ease: new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                break;
            case "float":
                shift.BeginAnimation(TranslateTransform.YProperty, Loop(size * .08, -size * .08, 1.8));
                rotate.BeginAnimation(RotateTransform.AngleProperty, Loop(-4, 4, 2.6));
                break;
            case "flip":
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(1, -1, 1.4));
                break;
            case "jelly":
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(1.12, .9, .6));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(.9, 1.12, .6));
                break;
            case "rainbow":
            {
                var glow = new DropShadowEffect { ShadowDepth = 0, BlurRadius = Math.Max(6, size * .5), Opacity = 1 };
                var hues = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(4.2) };
                Color[] colors = [Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0x84, 0xCC, 0x16), Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xEC, 0x48, 0x99), Color.FromRgb(0xEF, 0x44, 0x44)];
                for (int i = 0; i < colors.Length; i++)
                    hues.KeyFrames.Add(new LinearColorKeyFrame(colors[i], KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4.2 * i / (colors.Length - 1)))));
                glow.BeginAnimation(DropShadowEffect.ColorProperty, hues);
                Effect = glow;
                break;
            }
            case "neon":
            {
                Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = Math.Max(6, size * .4), Color = GlowColor(), Opacity = 1 };
                var flicker = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(3) };
                foreach (var (t, v) in new[] { (0.0, 1.0), (1.8, 1.0), (1.85, .35), (1.9, 1.0), (2.0, .5), (2.05, 1.0), (3.0, 1.0) })
                    flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t))));
                BeginAnimation(OpacityProperty, flicker);
                break;
            }
        }
    }
}
