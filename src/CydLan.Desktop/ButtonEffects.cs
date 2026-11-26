using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Applies opt-in glow, sheen sweep, hover bounce, and accent ring effects to buttons.</summary>
public static class ButtonEffects
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ButtonEffects), new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty IsHighlightProperty = DependencyProperty.RegisterAttached(
        "IsHighlight", typeof(bool), typeof(ButtonEffects), new PropertyMetadata(false, OnVisualTriggerChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
    public static bool GetIsHighlight(DependencyObject obj) => (bool)obj.GetValue(IsHighlightProperty);
    public static void SetIsHighlight(DependencyObject obj, bool value) => obj.SetValue(IsHighlightProperty, value);

    public static bool GlowEnabled { get; private set; }
    public static double GlowIntensity { get; private set; } = .65;
    public static bool SheenEnabled { get; private set; }
    public static double SheenIntensity { get; private set; } = .65;
    public static bool BounceEnabled { get; private set; }
    public static double BounceIntensity { get; private set; } = .65;
    public static bool RingEnabled { get; private set; }
    public static double RingIntensity { get; private set; } = .65;

    private static event Action? StateChanged;
    private static readonly Dictionary<Button, Action> Subscriptions = [];

    public static void Configure(
        bool glow, double glowIntensity,
        bool sheen, double sheenIntensity,
        bool bounce, double bounceIntensity,
        bool ring, double ringIntensity)
    {
        GlowEnabled = glow;
        GlowIntensity = Math.Clamp(glowIntensity, .1, 1);
        SheenEnabled = sheen;
        SheenIntensity = Math.Clamp(sheenIntensity, .1, 1);
        BounceEnabled = bounce;
        BounceIntensity = Math.Clamp(bounceIntensity, .1, 1);
        RingEnabled = ring;
        RingIntensity = Math.Clamp(ringIntensity, .1, 1);
        StateChanged?.Invoke();
    }

    public static void RefreshTheme() => StateChanged?.Invoke();

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button) return;
        if ((bool)e.NewValue)
        {
            button.Loaded += Button_Loaded;
            button.Unloaded += Button_Unloaded;
            button.MouseEnter += Button_StateEvent;
            button.MouseLeave += Button_StateEvent;
            button.PreviewMouseLeftButtonDown += Button_PressEvent;
            button.PreviewMouseLeftButtonUp += Button_StateEvent;
            button.IsEnabledChanged += Button_IsEnabledChanged;
            if (button.IsLoaded) Subscribe(button);
        }
        else
        {
            button.Loaded -= Button_Loaded;
            button.Unloaded -= Button_Unloaded;
            button.MouseEnter -= Button_StateEvent;
            button.MouseLeave -= Button_StateEvent;
            button.PreviewMouseLeftButtonDown -= Button_PressEvent;
            button.PreviewMouseLeftButtonUp -= Button_StateEvent;
            button.IsEnabledChanged -= Button_IsEnabledChanged;
            Unsubscribe(button);
        }
    }

    private static void OnVisualTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Button { IsLoaded: true } button) UpdateVisuals(button, pressed: false);
    }

    private static void Button_Loaded(object sender, RoutedEventArgs e) { if (sender is Button b) Subscribe(b); }
    private static void Button_Unloaded(object sender, RoutedEventArgs e) { if (sender is Button b) Unsubscribe(b); }
    private static void Button_StateEvent(object sender, System.Windows.Input.MouseEventArgs e) { if (sender is Button b) UpdateVisuals(b, pressed: false); }
    private static void Button_PressEvent(object sender, MouseButtonEventArgs e) { if (sender is Button b) UpdateVisuals(b, pressed: true); }
    private static void Button_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Button b) UpdateVisuals(b, pressed: false); }

    private static void Subscribe(Button button)
    {
        if (!Subscriptions.ContainsKey(button))
        {
            void Refresh() => UpdateVisuals(button, pressed: false);
            Subscriptions[button] = Refresh;
            StateChanged += Refresh;
            TargetAura.StateChanged += Refresh;
        }
        UpdateVisuals(button, pressed: false);
    }

    private static void Unsubscribe(Button button)
    {
        if (Subscriptions.Remove(button, out var handler))
        {
            StateChanged -= handler;
            TargetAura.StateChanged -= handler;
        }
        if (button.Effect is DropShadowEffect) button.Effect = null;
        if (button.RenderTransform is ScaleTransform) button.RenderTransform = Transform.Identity;
        if (button.Template?.FindName("ButtonAura", button) is TargetAura aura)
            aura.Visibility = Visibility.Collapsed;
        if (button.Template?.FindName("AccentRing", button) is Border ring)
        {
            ring.BeginAnimation(UIElement.OpacityProperty, null);
            ring.Opacity = 0;
        }
        if (button.Template?.FindName("SheenOverlay", button) is Border sheen)
        {
            sheen.BeginAnimation(UIElement.OpacityProperty, null);
            sheen.Opacity = 0;
            if (sheen.Tag is TranslateTransform shift) shift.BeginAnimation(TranslateTransform.XProperty, null);
        }
    }

    private static void UpdateVisuals(Button button, bool pressed)
    {
        bool highlight = GetIsHighlight(button);
        bool hovered = button.IsMouseOver;
        bool active = button.IsEnabled && (highlight || hovered);

        UpdateGlow(button, active, hovered);
        UpdateBounce(button, hovered, pressed);
        UpdateRing(button, active, hovered);
        UpdateSheen(button, active, hovered);
        UpdateAura(button, highlight);
    }

    private static void UpdateAura(Button button, bool highlight)
    {
        if (button.Template?.FindName("ButtonAura", button) is not TargetAura aura) return;
        var role = TargetAura.GetRole(button);
        if (role == TargetAuraRole.None) role = TargetAuraRole.Button;
        bool show = button.IsEnabled && highlight && TargetAura.ActiveKind != TargetAuraKind.Off &&
            (role == TargetAuraRole.Module ? TargetAura.OnModule : TargetAura.OnButtons);
        aura.Kind = TargetAura.ActiveKind;
        aura.Intensity = TargetAura.ActiveIntensity;
        aura.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void UpdateGlow(Button button, bool active, bool hovered)
    {
        if (!GlowEnabled || !active)
        {
            if (button.Effect is DropShadowEffect) button.Effect = null;
            return;
        }
        Color accent = Application.Current?.Resources["EffectColor"] is Color c ? c : Color.FromRgb(90, 170, 210);
        button.Effect = new DropShadowEffect
        {
            Color = accent,
            ShadowDepth = 0,
            BlurRadius = 10 + 10 * GlowIntensity,
            Opacity = (.35 + .55 * GlowIntensity) * (hovered ? 1.0 : .78)
        };
    }

    private static void UpdateBounce(Button button, bool hovered, bool pressed)
    {
        if (!BounceEnabled || !button.IsEnabled || !SystemParameters.ClientAreaAnimation)
        {
            if (button.RenderTransform is ScaleTransform) button.RenderTransform = Transform.Identity;
            return;
        }
        if (button.RenderTransform is not ScaleTransform scale)
        {
            button.RenderTransformOrigin = new Point(.5, .5);
            scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
        }
        double target = pressed ? .95 : hovered ? 1 + .08 * BounceIntensity : 1;
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(pressed ? 70 : 140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private static void UpdateRing(Button button, bool active, bool hovered)
    {
        if (button.Template?.FindName("AccentRing", button) is not Border ring) return;
        double target = RingEnabled && active ? (.50 + .50 * RingIntensity) * (hovered ? 1.0 : .82) : 0;
        if (!SystemParameters.ClientAreaAnimation)
        {
            ring.BeginAnimation(UIElement.OpacityProperty, null);
            ring.Opacity = target;
            return;
        }
        ring.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(140)));
    }

    private static void UpdateSheen(Button button, bool active, bool hovered)
    {
        if (button.Template?.FindName("SheenOverlay", button) is not Border sheen) return;
        if (!SheenEnabled || !active || !SystemParameters.ClientAreaAnimation)
        {
            sheen.BeginAnimation(UIElement.OpacityProperty, null);
            sheen.Opacity = 0;
            return;
        }
        var shift = sheen.Tag as TranslateTransform;
        if (shift is null)
        {
            shift = new TranslateTransform(-1.2, 0);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, .35),
                RelativeTransform = shift,
                GradientStops =
                [
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), .35),
                    new GradientStop(Color.FromArgb(145, 255, 255, 255), .5),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), .65),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                ]
            };
            sheen.Background = brush;
            sheen.Tag = shift;
        }
        sheen.Opacity = .35 + .55 * SheenIntensity;
        var sweep = new DoubleAnimation(-1.2, 1.3, TimeSpan.FromMilliseconds(hovered ? 520 : 1200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = hovered ? new RepeatBehavior(1) : RepeatBehavior.Forever
        };
        if (!hovered) Timeline.SetDesiredFrameRate(sweep, 24);
        shift.BeginAnimation(TranslateTransform.XProperty, sweep);
    }
}
