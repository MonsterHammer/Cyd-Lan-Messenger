using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CydLan.Core;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Opt-in entrance animations for newly sent or received chat bubbles.</summary>
public static class MessageMotion
{
    public const string Off = "off";
    public const string Fade = "fade";
    public const string Slide = "slide";
    public const string Pop = "pop";

    public static string ActiveStyle { get; private set; } = Off;

    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(MessageMotion), new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    public static string Normalize(string? value) => value is Fade or Slide or Pop ? value : Off;

    public static void Configure(string? style) => ActiveStyle = Normalize(style);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        if ((bool)e.NewValue) element.Loaded += Element_Loaded;
        else element.Loaded -= Element_Loaded;
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        string style = ActiveStyle;
        if (style == Off || !SystemParameters.ClientAreaAnimation) return;
        // Animate only newly sent/received messages, never historic rows when switching conversations.
        if (element.DataContext is ChatMessage msg && Math.Abs((DateTimeOffset.Now - msg.Time).TotalSeconds) > 3.5)
            return;

        double speed = Math.Clamp(AmbientEffect.SpeedMultiplier, .4, 2.0);
        var duration = new Duration(TimeSpan.FromMilliseconds(Math.Clamp(220 / speed, 110, 420)));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        element.BeginAnimation(UIElement.OpacityProperty, fade);

        if (style == Slide)
        {
            var shift = new TranslateTransform(0, 14);
            element.RenderTransform = shift;
            var slide = new DoubleAnimation(14, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            slide.Completed += (_, _) => { if (ReferenceEquals(element.RenderTransform, shift)) element.RenderTransform = Transform.Identity; };
            shift.BeginAnimation(TranslateTransform.YProperty, slide);
        }
        else if (style == Pop)
        {
            element.RenderTransformOrigin = new Point(.5, .5);
            var scale = new ScaleTransform(.90, .90);
            element.RenderTransform = scale;
            var pop = new DoubleAnimation(.90, 1, duration) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .35 }, FillBehavior = FillBehavior.Stop };
            pop.Completed += (_, _) => { if (ReferenceEquals(element.RenderTransform, scale)) element.RenderTransform = Transform.Identity; };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
    }
}
