using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;
using ShapePath = System.Windows.Shapes.Path;

namespace CydLan.Desktop;

public sealed class ChatEffects : Canvas
{
    private static readonly Geometry Sparkles = Geometry.Parse("M14,4 Q14,13 23,13 Q14,13 14,22 Q14,13 5,13 Q14,13 14,4 Z M25,4 V10 M22,7 H28 M5,22 V28 M2,25 H8");
    private static readonly Geometry Steam = Geometry.Parse("M7,26 C2,21 12,18 7,13 C3,9 11,6 8,3 M15,25 C10,20 20,17 15,12 C11,8 19,5 16,2 M23,26 C18,21 28,18 23,13 C19,9 27,6 24,3");
    private static readonly Geometry Ripple = Geometry.Parse("M27,15 A12,12 0 1 1 3,15 A12,12 0 1 1 27,15 Z M21,15 A6,6 0 1 1 9,15 A6,6 0 1 1 21,15 Z");
    private static readonly Geometry InkBloom = Geometry.Parse("M14,3 C18,2 19,8 23,9 C29,9 30,15 25,18 C22,20 24,25 19,26 C14,29 13,23 9,24 C4,25 1,19 5,16 C7,14 3,10 6,7 C8,3 11,6 14,3 Z M26,5 A1.5,1.5 0 1 0 29,5 A1.5,1.5 0 1 0 26,5 Z");

    internal static string Normalize(string? effect, bool typing = false) =>
        effect is "theme" or "sparkles" or "steam" or "ripple" || (typing && effect == "ink") ? effect : "off";

    internal static string Resolve(string? effect, string theme, bool typing) => Normalize(effect, typing) switch
    {
        "theme" when theme == ThemeManager.Steampunk => "steam",
        "theme" when theme is ThemeManager.Anime or ThemeManager.Aurora or ThemeManager.Arcade => "sparkles",
        "theme" => typing ? "sparkles" : "ripple",
        var selected => selected
    };

    internal void Play(Point position, string? effect, string theme, bool typing, double intensity = 1)
    {
        string selected = Resolve(effect, theme, typing);
        if (selected == "off" || !SystemParameters.ClientAreaAnimation || !IsVisible) return;
        bool ink = selected == "ink";
        var shape = selected switch { "ink" => InkBloom, "steam" => Steam, "sparkles" => Sparkles, _ => Ripple };
        AnimateMark(position, shape, ink, intensity, lift: typing || selected == "steam", milliseconds: typing ? 620 : 430);
    }

    internal void PlaySendPulse(Point position, double intensity)
    {
        if (!SystemParameters.ClientAreaAnimation || !IsVisible) return;
        AnimateMark(position, Ripple, filled: false, intensity, lift: false, milliseconds: 390);
    }

    private void AnimateMark(Point position, Geometry geometry, bool filled, double intensity, bool lift, int milliseconds)
    {
        intensity = Math.Clamp(intensity, .1, 1);
        if (Children.Count >= 8) Children.RemoveAt(0);

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
            RenderTransformOrigin = new Point(.5, .5)
        };
        mark.SetResourceReference(filled ? System.Windows.Shapes.Shape.FillProperty : System.Windows.Shapes.Shape.StrokeProperty, "Accent");
        SetLeft(mark, position.X - 14);
        SetTop(mark, position.Y - 14);
        Children.Add(mark);

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
