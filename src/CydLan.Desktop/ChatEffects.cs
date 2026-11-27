using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using ShapePath = System.Windows.Shapes.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Polyline = System.Windows.Shapes.Polyline;

namespace CydLan.Desktop;

/// <summary>Hover, cursor and word-pop settings shared by every effect layer.</summary>
public static class InteractionEffects
{
    public static readonly Color DefaultWordPop = Color.FromRgb(0xFF, 0xE1, 0x4D);
    private static readonly string[] HoverKinds = ["ring", "sparkle", "sweep", "brackets", "pop", "halo", "hearts"];
    private static readonly string[] CursorKinds = ["sparkle", "comet", "ring", "rainbow", "bubbles", "hearts", "pixel", "spotlight"];
    private static bool installed;

    public static string Hover { get; private set; } = "off";
    public static double HoverIntensity { get; private set; } = .65;
    public static string Cursor { get; private set; } = "off";
    public static double CursorIntensity { get; private set; } = .65;
    public static Color WordPop { get; private set; } = DefaultWordPop;
    internal static event Action? Changed;

    public static string NormalizeHover(string? value) => value is not null && HoverKinds.Contains(value) ? value : "off";
    public static string NormalizeCursor(string? value) => value is not null && CursorKinds.Contains(value) ? value : "off";

    public static void Configure(string? hover, double hoverIntensity, string? cursor, double cursorIntensity, string? wordPopHex)
    {
        Hover = NormalizeHover(hover);
        HoverIntensity = double.IsFinite(hoverIntensity) ? Math.Clamp(hoverIntensity, .1, 1) : .65;
        Cursor = NormalizeCursor(cursor);
        CursorIntensity = double.IsFinite(cursorIntensity) ? Math.Clamp(cursorIntensity, .1, 1) : .65;
        WordPop = AppearanceOptions.TryParseHex(wordPopHex, out var c) ? c : DefaultWordPop;
        Changed?.Invoke();
    }

    /// <summary>Plays the hover effect whenever the pointer enters a button or list row in the main window.</summary>
    internal static void Install()
    {
        if (installed) return;
        installed = true;
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(OnEnter));
        EventManager.RegisterClassHandler(typeof(ListBoxItem), UIElement.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(OnEnter));
    }

    private static void OnEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Hover == "off" || sender is not FrameworkElement element || !element.IsVisible) return;
        if (element.ActualWidth < 8 || element.ActualHeight < 8 || element.ActualWidth > 700 || element.ActualHeight > 140) return;
        if (Window.GetWindow(element) is not MainWindow window) return;
        window.PlayHover(element);
    }
}

public sealed class ChatEffects : Canvas
{
    private static readonly Geometry Sparkles = Geometry.Parse("M14,4 Q14,13 23,13 Q14,13 14,22 Q14,13 5,13 Q14,13 14,4 Z M25,4 V10 M22,7 H28 M5,22 V28 M2,25 H8");
    private static readonly Geometry Steam = Geometry.Parse("M7,26 C2,21 12,18 7,13 C3,9 11,6 8,3 M15,25 C10,20 20,17 15,12 C11,8 19,5 16,2 M23,26 C18,21 28,18 23,13 C19,9 27,6 24,3");
    private static readonly Geometry Ripple = Geometry.Parse("M27,15 A12,12 0 1 1 3,15 A12,12 0 1 1 27,15 Z M21,15 A6,6 0 1 1 9,15 A6,6 0 1 1 21,15 Z");
    private static readonly Geometry InkBloom = Geometry.Parse("M14,3 C18,2 19,8 23,9 C29,9 30,15 25,18 C22,20 24,25 19,26 C14,29 13,23 9,24 C4,25 1,19 5,16 C7,14 3,10 6,7 C8,3 11,6 14,3 Z M26,5 A1.5,1.5 0 1 0 29,5 A1.5,1.5 0 1 0 26,5 Z");
    private static readonly Geometry Burst = Geometry.Parse("M14,2 V8 M14,20 V26 M2,14 H8 M20,14 H26 M6,6 L10,10 M18,18 L22,22 M22,6 L18,10 M10,18 L6,22");
    private static readonly Geometry Petals = Geometry.Parse("M14,4 C11,7 11,11 14,13 C17,11 17,7 14,4 Z M6,14 C9,12 12,14 12,17 C9,19 6,17 6,14 Z M22,14 C19,12 16,14 16,17 C19,19 22,17 22,14 Z");
    private static readonly Geometry Feather = Geometry.Parse("M22,4 C16,7 11,13 8,20 L5,25 L11,22 C16,18 20,12 24,6 L17,12 Z");
    private static readonly Geometry Repulsor = Geometry.Parse("M23,14 A9,9 0 1 1 5,14 A9,9 0 1 1 23,14 Z M17.5,14 A3.5,3.5 0 1 1 10.5,14 A3.5,3.5 0 1 1 17.5,14 Z M14,2 V6 M14,22 V26 M2,14 H6 M22,14 H26");
    private static readonly Geometry Bolt = Geometry.Parse("M16,1 L6,16 H13 L10,27 L22,10 H15 Z");
    private static readonly Geometry Flame = Geometry.Parse("M0,-6 C3,-2 4,1 2.5,4 C1.5,6 -1.5,6 -2.5,4 C-4,1 -2,-1 -1,-2 C-1,0 0,1 0.6,0.4 C1.2,-1.5 0,-4 0,-6 Z");
    private static readonly Geometry Flake = Geometry.Parse("M0,-5 V5 M-4.3,-2.5 L4.3,2.5 M-4.3,2.5 L4.3,-2.5");
    private static readonly string[] Onomatopoeia = ["POW!", "BAM!", "ZAP!", "WOW!", "BOOM!", "KA-POW!", "WHAM!", "POP!"];
    private static readonly Random Rng = new();

    private const int MaxChildren = 90;
    private Point lastTrail = new(double.NaN, double.NaN);
    private double trailHue;
    private Ellipse? ringFollower;
    private Ellipse? spotlight;
    private DateTime lastHover;
    private FrameworkElement? lastHoverTarget;

    private static readonly HashSet<string> Shared = ["theme", "sparkles", "steam", "ripple", "burst", "petals", "feathers", "repulsor",
        "hearts", "stars", "confetti", "bubbles", "zap", "notes", "snow", "fire", "comic"];

    internal static string Normalize(string? effect, bool typing = false) =>
        effect is not null && (Shared.Contains(effect) || (typing && effect is "ink" or "wordpop") || (!typing && effect == "shockwave")) ? effect : "off";

    internal static string Resolve(string? effect, string theme, bool typing) => Normalize(effect, typing) switch
    {
        "theme" when theme == ThemeManager.Steampunk => "steam",
        "theme" when theme is ThemeManager.Sakura => "petals",
        "theme" when theme == ThemeManager.FreedomWings => "feathers",
        "theme" when theme == ThemeManager.ArcReactor => "repulsor",
        "theme" when theme is ThemeManager.Cyberpunk or ThemeManager.Arcade => "burst",
        "theme" when theme is ThemeManager.Anime or ThemeManager.Aurora or ThemeManager.Midnight => "sparkles",
        "theme" => typing ? "sparkles" : "ripple",
        var selected => selected
    };

    private bool CanAnimate => SystemParameters.ClientAreaAnimation && IsVisible;

    private Color Accent => TryFindResource("Accent") is SolidColorBrush b ? b.Color : Color.FromRgb(158, 63, 91);

    private static SolidColorBrush Solid(Color c, double alpha = 1)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    private void Add(UIElement element)
    {
        if (Children.Count >= MaxChildren)
            for (int i = 0; i < Children.Count; i++)
                if (Children[i] != ringFollower && Children[i] != spotlight) { Children.RemoveAt(i); break; }
        Children.Add(element);
    }

    internal void Play(Point position, string? effect, string theme, bool typing, double intensity = 1)
    {
        string selected = Resolve(effect, theme, typing);
        if (selected == "off" || selected == "wordpop" || !CanAnimate) return;
        intensity = Math.Clamp(intensity, .1, 1);
        var accent = Accent;
        switch (selected)
        {
            case "hearts":
                Spray(position, 5, i => Shape(FxDraw.Heart, Solid(i % 2 == 0 ? Color.FromRgb(244, 63, 94) : Color.FromRgb(249, 168, 212)), 2.2), 26, 30, 0, 700, intensity, spin: false);
                return;
            case "stars":
                Spray(position, 7, i => Shape(FxDraw.Spark, Solid(i % 3 == 0 ? Color.FromRgb(253, 224, 71) : EffectPalette.Blend(accent, Colors.White, .4)), 1.8), 34, 6, 0, 560, intensity, spin: true);
                return;
            case "confetti":
                Spray(position, 12, i => new Rectangle { Width = 5, Height = 3, Fill = Solid(FxDraw.Party[i % FxDraw.Party.Length]) }, 38, 14, 34, 760, intensity, spin: true);
                return;
            case "bubbles":
                Spray(position, 5, _ => new Ellipse { Width = 8, Height = 8, Stroke = Solid(EffectPalette.Blend(accent, Colors.White, .5)), StrokeThickness = 1.2, Fill = Solid(Colors.White, .15) }, 14, 34, 0, 820, intensity, spin: false);
                return;
            case "zap":
                AnimateMark(position, Bolt, filled: true, intensity, lift: false, milliseconds: 360, Solid(Color.FromRgb(250, 204, 21)));
                AnimateMark(position, Ripple, filled: false, intensity * .7, lift: false, milliseconds: 300);
                return;
            case "notes":
                Spray(position, 3, i => Shape(FxDraw.Note, Solid(i == 1 ? accent : EffectPalette.Blend(accent, Colors.White, .35)), 2.2), 16, 34, 0, 900, intensity, spin: false);
                return;
            case "snow":
                Spray(position, 6, _ => Shape(Flake, null, 1.3, Solid(Color.FromRgb(224, 242, 254))), 24, -6, 18, 900, intensity, spin: true);
                return;
            case "fire":
                Spray(position, 6, i => Shape(Flame, Solid(i % 2 == 0 ? Color.FromRgb(249, 115, 22) : Color.FromRgb(250, 204, 21)), 1.8), 10, 32, 0, 620, intensity, spin: false);
                return;
            case "comic":
                ComicBurst(position, intensity);
                return;
            case "shockwave":
                Shockwave(position, intensity, accent);
                return;
        }
        bool filled = selected is "ink" or "petals" or "feathers";
        var shape = selected switch
        {
            "ink" => InkBloom,
            "steam" => Steam,
            "sparkles" => Sparkles,
            "burst" => Burst,
            "petals" => Petals,
            "feathers" => Feather,
            "repulsor" => Repulsor,
            _ => Ripple
        };
        AnimateMark(position, shape, filled, intensity, lift: typing || selected is "steam" or "petals" or "feathers", milliseconds: typing ? 620 : 430);
    }

    internal void PlaySendPulse(Point position, double intensity)
    {
        if (!CanAnimate) return;
        AnimateMark(position, Ripple, filled: false, intensity, lift: false, milliseconds: 390);
    }

    private static ShapePath Shape(Geometry geometry, System.Windows.Media.Brush? fill, double scale, System.Windows.Media.Brush? stroke = null) => new()
    {
        Data = geometry,
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = stroke is null ? 0 : 1.2,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        RenderTransform = new ScaleTransform(scale, scale)
    };

    /// <summary>Throws a handful of small shapes out from a point, with optional lift, gravity and spin.</summary>
    private void Spray(Point at, int count, Func<int, FrameworkElement> make, double spread, double lift, double gravity, int milliseconds, double intensity, bool spin)
    {
        count = Math.Max(2, (int)Math.Round(count * (.55 + .45 * intensity)));
        var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
        for (int i = 0; i < count; i++)
        {
            var shape = make(i);
            double angle = (i + Rng.NextDouble() * .6) / count * Math.Tau;
            double distance = spread * (.5 + .7 * Rng.NextDouble()) * (.7 + .5 * intensity);
            var move = new TranslateTransform();
            var turn = new RotateTransform(Rng.NextDouble() * 40 - 20);
            var group = new TransformGroup();
            if (shape.RenderTransform is ScaleTransform baseScale) group.Children.Add(baseScale);
            group.Children.Add(turn);
            group.Children.Add(move);
            shape.RenderTransform = group;
            shape.IsHitTestVisible = false;
            SetLeft(shape, at.X);
            SetTop(shape, at.Y);
            Add(shape);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Math.Cos(angle) * distance, duration) { EasingFunction = ease });
            double endY = Math.Sin(angle) * distance * .6 - lift * (.6 + .6 * Rng.NextDouble());
            var fall = new DoubleAnimationUsingKeyFrames { Duration = duration };
            fall.KeyFrames.Add(new EasingDoubleKeyFrame(endY, KeyTime.FromPercent(.55), ease));
            fall.KeyFrames.Add(new EasingDoubleKeyFrame(endY + gravity, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            move.BeginAnimation(TranslateTransform.YProperty, fall);
            if (spin) turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(turn.Angle, turn.Angle + (Rng.NextDouble() < .5 ? -1 : 1) * 320, duration));
            var fade = new DoubleAnimation(.5 + .5 * intensity, 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (_, _) => Children.Remove(shape);
            shape.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void Shockwave(Point at, double intensity, Color accent)
    {
        for (int ring = 0; ring < 2; ring++)
        {
            var e = new Ellipse { Width = 10, Height = 10, Stroke = Solid(accent), StrokeThickness = ring == 0 ? 3 : 1.2, IsHitTestVisible = false, RenderTransformOrigin = new Point(.5, .5) };
            var scale = new ScaleTransform(.4, .4);
            e.RenderTransform = scale;
            SetLeft(e, at.X - 5);
            SetTop(e, at.Y - 5);
            Add(e);
            var duration = new Duration(TimeSpan.FromMilliseconds(420 + ring * 140));
            var grow = new DoubleAnimation(.4, 4.5 + 2.5 * intensity + ring * 1.5, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            var fade = new DoubleAnimation(.9 * intensity + .1, 0, duration);
            fade.Completed += (_, _) => Children.Remove(e);
            e.BeginAnimation(OpacityProperty, fade);
        }
    }

    /// <summary>A comic-book starburst with a sound word that pops and fades.</summary>
    private void ComicBurst(Point at, double intensity)
    {
        var color = InteractionEffects.WordPop;
        double r = 15 + 7 * intensity;
        var burst = new StreamGeometry();
        using (var c = burst.Open())
        {
            for (int i = 0; i < 20; i++)
            {
                double a = i * Math.Tau / 20, rad = i % 2 == 0 ? r : r * .62;
                var p = new Point(Math.Cos(a) * rad * 1.3, Math.Sin(a) * rad);
                if (i == 0) c.BeginFigure(p, true, true); else c.LineTo(p, true, false);
            }
        }
        burst.Freeze();
        var root = new Canvas { IsHitTestVisible = false };
        root.Children.Add(new ShapePath { Data = burst, Fill = Solid(Colors.Black, .35), RenderTransform = new TranslateTransform(2.5, 2.5) });
        root.Children.Add(new ShapePath { Data = burst, Fill = Solid(color), Stroke = Solid(Color.FromRgb(20, 20, 20)), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round });
        var word = new TextBlock
        {
            Text = Onomatopoeia[Rng.Next(Onomatopoeia.Length)],
            FontWeight = FontWeights.Black,
            FontSize = 10 + 3 * intensity,
            Foreground = Solid(Color.FromRgb(220, 38, 38))
        };
        word.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(word, -word.DesiredSize.Width / 2);
        SetTop(word, -word.DesiredSize.Height / 2);
        root.Children.Add(word);
        PopIn(root, at, Rng.NextDouble() * 20 - 10, 900);
    }

    /// <summary>Highlights a just-typed word with a tilted comic marker, outline and action lines.</summary>
    internal void PlayWordPop(Rect word, double intensity)
    {
        if (!CanAnimate || word.Width < 2) return;
        intensity = Math.Clamp(intensity, .1, 1);
        var color = InteractionEffects.WordPop;
        var ink = Color.FromRgb(24, 24, 27);
        word.Inflate(4, 2.5);
        double w = word.Width, h = word.Height;
        var marker = new StreamGeometry();
        using (var c = marker.Open())
        {
            // Zig-zag top and bottom edges give it a hand-cut comic feel.
            int teeth = Math.Max(3, (int)(w / 7));
            c.BeginFigure(new Point(-w / 2, -h / 2), true, true);
            for (int i = 1; i <= teeth; i++) c.LineTo(new Point(-w / 2 + w * i / teeth, -h / 2 + (i % 2 == 0 ? 0 : -2.2)), true, false);
            c.LineTo(new Point(w / 2 + 2, h / 2), true, false);
            for (int i = teeth - 1; i >= 0; i--) c.LineTo(new Point(-w / 2 + w * i / teeth, h / 2 + (i % 2 == 0 ? 0 : 2.2)), true, false);
        }
        marker.Freeze();
        var root = new Canvas { IsHitTestVisible = false };
        root.Children.Add(new ShapePath { Data = marker, Fill = Solid(Colors.Black, .22), RenderTransform = new TranslateTransform(2, 2) });
        root.Children.Add(new ShapePath { Data = marker, Fill = Solid(color, .38 + .22 * intensity), Stroke = Solid(ink, .85), StrokeThickness = 1.4, StrokeLineJoin = PenLineJoin.Round });
        // Halftone dots in one corner.
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 2; j++)
            {
                var dot = new Ellipse { Width = 1.8, Height = 1.8, Fill = Solid(ink, .35) };
                SetLeft(dot, w / 2 - 4 - i * 3.2);
                SetTop(dot, -h / 2 + 2 + j * 3.2);
                root.Children.Add(dot);
            }
        foreach (var (x, y, dx, dy) in new[] { (w / 2 + 3, -h / 2 - 1, 5.0, -4.0), (w / 2 + 5, -h / 2 + 4, 6.0, -1.0), (-w / 2 - 3, -h / 2 - 1, -5.0, -4.0) })
            root.Children.Add(new Line { X1 = x, Y1 = y, X2 = x + dx, Y2 = y + dy, Stroke = Solid(ink, .8), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        PopIn(root, new Point(word.X + w / 2, word.Y + h / 2), Rng.NextDouble() * 5 - 2.5, 1000);
    }

    private void PopIn(FrameworkElement root, Point at, double tilt, int milliseconds)
    {
        var scale = new ScaleTransform(.3, .3);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(new RotateTransform(tilt));
        root.RenderTransform = group;
        SetLeft(root, at.X);
        SetTop(root, at.Y);
        Add(root);
        var pop = new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = .7, EasingMode = EasingMode.EaseOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(milliseconds) };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.55)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        fade.Completed += (_, _) => Children.Remove(root);
        root.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Plays the hover effect around an element's bounds.</summary>
    internal void PlayHover(FrameworkElement target)
    {
        string kind = InteractionEffects.Hover;
        if (kind == "off" || !CanAnimate) return;
        if (target == lastHoverTarget && (DateTime.UtcNow - lastHover).TotalMilliseconds < 300) return;
        lastHoverTarget = target;
        lastHover = DateTime.UtcNow;
        Rect r;
        try { r = target.TransformToVisual(this).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight)); }
        catch (InvalidOperationException) { return; }
        double k = InteractionEffects.HoverIntensity;
        var accent = Accent;
        var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        switch (kind)
        {
            case "ring":
            case "pop":
            {
                var box = new Border
                {
                    Width = r.Width + 6,
                    Height = r.Height + 6,
                    CornerRadius = new CornerRadius(Math.Min(10, r.Height / 2)),
                    BorderBrush = kind == "ring" ? Solid(accent) : null,
                    BorderThickness = new Thickness(kind == "ring" ? 1.8 : 0),
                    Background = kind == "pop" ? Solid(accent, .22) : null,
                    IsHitTestVisible = false,
                    RenderTransformOrigin = new Point(.5, .5)
                };
                var scale = new ScaleTransform(kind == "ring" ? 1.12 : .9, kind == "ring" ? 1.12 : .9);
                box.RenderTransform = scale;
                SetLeft(box, r.X - 3);
                SetTop(box, r.Y - 3);
                Add(box);
                var duration = new Duration(TimeSpan.FromMilliseconds(520));
                var settle = new DoubleAnimation(scale.ScaleX, 1, duration) { EasingFunction = new BackEase { Amplitude = .4, EasingMode = EasingMode.EaseOut } };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, settle);
                var fade = new DoubleAnimation(.4 + .6 * k, 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                fade.Completed += (_, _) => Children.Remove(box);
                box.BeginAnimation(OpacityProperty, fade);
                break;
            }
            case "sparkle":
                for (int i = 0; i < 3; i++)
                {
                    var p = new Point(r.X + Rng.NextDouble() * r.Width, Rng.NextDouble() < .5 ? r.Y : r.Bottom);
                    var root = new Canvas { IsHitTestVisible = false };
                    root.Children.Add(new ShapePath { Data = FxDraw.Spark, Fill = Solid(i == 0 ? Color.FromRgb(253, 224, 71) : EffectPalette.Blend(accent, Colors.White, .45), .5 + .5 * k), RenderTransform = new ScaleTransform(1.4, 1.4) });
                    PopIn(root, p, Rng.NextDouble() * 45, 520 + i * 80);
                }
                break;
            case "sweep":
            {
                var line = new Rectangle { Height = 2, Width = r.Width, Fill = Solid(accent, .5 + .5 * k), RadiusX = 1, RadiusY = 1, IsHitTestVisible = false, RenderTransformOrigin = new Point(0, .5) };
                var scale = new ScaleTransform(0, 1);
                line.RenderTransform = scale;
                SetLeft(line, r.X);
                SetTop(line, r.Bottom - 1);
                Add(line);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(380) };
                fade.Completed += (_, _) => Children.Remove(line);
                line.BeginAnimation(OpacityProperty, fade);
                break;
            }
            case "brackets":
                foreach (var (cx, cy, sx, sy) in new[] { (r.Left, r.Top, 1, 1), (r.Right, r.Top, -1, 1), (r.Left, r.Bottom, 1, -1), (r.Right, r.Bottom, -1, -1) })
                {
                    var bracket = new Polyline
                    {
                        Points = [new Point(0, 7 * sy), new Point(0, 0), new Point(7 * sx, 0)],
                        Stroke = Solid(accent, .5 + .5 * k),
                        StrokeThickness = 1.8,
                        StrokeLineJoin = PenLineJoin.Round,
                        IsHitTestVisible = false
                    };
                    var move = new TranslateTransform(-5 * sx, -5 * sy);
                    bracket.RenderTransform = move;
                    SetLeft(bracket, cx);
                    SetTop(bracket, cy);
                    Add(bracket);
                    var ease = new BackEase { Amplitude = .5, EasingMode = EasingMode.EaseOut };
                    move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-5 * sx, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
                    move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5 * sy, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(420) };
                    fade.Completed += (_, _) => Children.Remove(bracket);
                    bracket.BeginAnimation(OpacityProperty, fade);
                }
                break;
            case "halo":
            {
                var halo = new Ellipse { Width = Math.Min(r.Width * .7, 40), Height = 7, Stroke = Solid(Color.FromRgb(253, 224, 71)), StrokeThickness = 1.8, IsHitTestVisible = false };
                var root = new Canvas { IsHitTestVisible = false };
                SetLeft(halo, -halo.Width / 2);
                SetTop(halo, -3.5);
                root.Children.Add(halo);
                PopIn(root, new Point(center.X, r.Top - 3), 0, 620);
                break;
            }
            case "hearts":
                Spray(new Point(center.X, r.Top + 2), 2, _ => Shape(FxDraw.Heart, Solid(Color.FromRgb(244, 63, 94)), 1.6), 12, 22, 0, 700, k, spin: false);
                break;
        }
    }

    /// <summary>Leaves a trail behind the pointer, or moves a follower to it.</summary>
    internal void Trail(Point p)
    {
        string kind = InteractionEffects.Cursor;
        if (kind == "off" || !CanAnimate) { HideFollowers(); return; }
        double k = InteractionEffects.CursorIntensity;
        var accent = Accent;
        if (kind is "ring" or "spotlight")
        {
            Follow(p, kind, k, accent);
            return;
        }
        double step = kind is "comet" or "rainbow" ? 5 : 16;
        if (!double.IsNaN(lastTrail.X) && (p - lastTrail).Length < step) return;
        lastTrail = p;
        switch (kind)
        {
            case "sparkle":
                Spray(p, 2, i => Shape(FxDraw.Spark, Solid(i == 0 ? Color.FromRgb(253, 224, 71) : EffectPalette.Blend(accent, Colors.White, .45)), 1.2), 10, -6, 12, 520, k, spin: true);
                break;
            case "bubbles":
                Spray(p, 2, _ => new Ellipse { Width = 6, Height = 6, Stroke = Solid(EffectPalette.Blend(accent, Colors.White, .5)), StrokeThickness = 1 }, 6, 18, 0, 700, k, spin: false);
                break;
            case "hearts":
                Spray(p, 2, _ => Shape(FxDraw.Heart, Solid(Color.FromRgb(244, 63, 94)), 1.3), 8, 16, 0, 650, k, spin: false);
                break;
            case "pixel":
                Spray(p, 3, _ => new Rectangle { Width = 3, Height = 3, Fill = Solid(FxDraw.Hue(trailHue += .07)) }, 10, -4, 16, 520, k, spin: false);
                break;
            case "comet":
            case "rainbow":
            {
                var color = kind == "rainbow" ? FxDraw.Hue(trailHue += .025) : EffectPalette.Blend(accent, Colors.White, .4);
                double size = 4 + 4 * k;
                var dot = new Ellipse { Width = size, Height = size, Fill = Solid(color, .8), IsHitTestVisible = false, RenderTransformOrigin = new Point(.5, .5) };
                var scale = new ScaleTransform(1, 1);
                dot.RenderTransform = scale;
                SetLeft(dot, p.X - size / 2);
                SetTop(dot, p.Y - size / 2);
                Add(dot);
                var duration = new Duration(TimeSpan.FromMilliseconds(380));
                var shrink = new DoubleAnimation(1, .1, duration);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
                var fade = new DoubleAnimation(.5 + .5 * k, 0, duration);
                fade.Completed += (_, _) => Children.Remove(dot);
                dot.BeginAnimation(OpacityProperty, fade);
                break;
            }
        }
    }

    private void Follow(Point p, string kind, double k, Color accent)
    {
        if (kind == "ring")
        {
            if (spotlight is not null) { Children.Remove(spotlight); spotlight = null; }
            if (ringFollower is null)
            {
                ringFollower = new Ellipse { Width = 26, Height = 26, StrokeThickness = 1.6, IsHitTestVisible = false };
                Children.Add(ringFollower);
                SetLeft(ringFollower, p.X - 13);
                SetTop(ringFollower, p.Y - 13);
            }
            ringFollower.Stroke = Solid(accent, .4 + .6 * k);
            Glide(ringFollower, p, 13, 160);
        }
        else
        {
            if (ringFollower is not null) { Children.Remove(ringFollower); ringFollower = null; }
            if (spotlight is null)
            {
                spotlight = new Ellipse { Width = 220, Height = 220, IsHitTestVisible = false };
                Children.Insert(0, spotlight);
            }
            var fill = new RadialGradientBrush();
            fill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * k + 20), accent.R, accent.G, accent.B), 0));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1));
            fill.Freeze();
            spotlight.Fill = fill;
            Glide(spotlight, p, 110, 90);
        }
    }

    private static void Glide(FrameworkElement element, Point p, double half, int milliseconds)
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(LeftProperty, new DoubleAnimation(p.X - half, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
        element.BeginAnimation(TopProperty, new DoubleAnimation(p.Y - half, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
    }

    internal void HideFollowers()
    {
        if (ringFollower is not null) { Children.Remove(ringFollower); ringFollower = null; }
        if (spotlight is not null) { Children.Remove(spotlight); spotlight = null; }
        lastTrail = new Point(double.NaN, double.NaN);
    }

    private void AnimateMark(Point position, Geometry geometry, bool filled, double intensity, bool lift, int milliseconds, System.Windows.Media.Brush? brush = null)
    {
        intensity = Math.Clamp(intensity, .1, 1);

        var scale = new ScaleTransform(.55, .55);
        var drift = new TranslateTransform();
        var motion = new TransformGroup();
        motion.Children.Add(scale);
        motion.Children.Add(drift);
        var mark = new ShapePath
        {
            Data = geometry,
            Width = 28,
            Height = 28,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = motion,
            RenderTransformOrigin = new Point(.5, .5),
            IsHitTestVisible = false
        };
        if (brush is not null) mark.Fill = brush;
        else mark.SetResourceReference(filled ? System.Windows.Shapes.Shape.FillProperty : System.Windows.Shapes.Shape.StrokeProperty, "Accent");
        SetLeft(mark, position.X - 14);
        SetTop(mark, position.Y - 14);
        Add(mark);

        var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
        var grow = new DoubleAnimation(.55, .75 + .45 * intensity, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        if (lift)
            drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -27, duration));
        var fade = new DoubleAnimation((filled ? .23 + .46 * intensity : .2 + .65 * intensity), 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => Children.Remove(mark);
        mark.BeginAnimation(OpacityProperty, fade);
    }
}
