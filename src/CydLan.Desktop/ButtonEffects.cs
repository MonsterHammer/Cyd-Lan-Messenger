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

/// <summary>Applies opt-in glow, sheen sweep, hover bounce, accent ring, pulse halo, hover lift, and spark border effects to buttons.</summary>
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
    public static bool PulseEnabled { get; private set; }
    public static double PulseIntensity { get; private set; } = .65;
    public static bool LiftEnabled { get; private set; }
    public static double LiftIntensity { get; private set; } = .65;
    public static bool SparkEnabled { get; private set; }
    public static double SparkIntensity { get; private set; } = .65;

    private static event Action? StateChanged;
    private static readonly Dictionary<Button, Action> Subscriptions = [];

    public static void Configure(
        bool glow, double glowIntensity,
        bool sheen, double sheenIntensity,
        bool bounce, double bounceIntensity,
        bool ring, double ringIntensity) =>
        Configure(glow, glowIntensity, sheen, sheenIntensity, bounce, bounceIntensity, ring, ringIntensity,
            pulse: false, pulseIntensity: .65, lift: false, liftIntensity: .65, spark: false, sparkIntensity: .65);

    public static void Configure(
        bool glow, double glowIntensity,
        bool sheen, double sheenIntensity,
        bool bounce, double bounceIntensity,
        bool ring, double ringIntensity,
        bool pulse, double pulseIntensity,
        bool lift, double liftIntensity,
        bool spark, double sparkIntensity)
    {
        GlowEnabled = glow;
        GlowIntensity = Math.Clamp(glowIntensity, .1, 1);
        SheenEnabled = sheen;
        SheenIntensity = Math.Clamp(sheenIntensity, .1, 1);
        BounceEnabled = bounce;
        BounceIntensity = Math.Clamp(bounceIntensity, .1, 1);
        RingEnabled = ring;
        RingIntensity = Math.Clamp(ringIntensity, .1, 1);
        PulseEnabled = pulse;
        PulseIntensity = Math.Clamp(pulseIntensity, .1, 1);
        LiftEnabled = lift;
        LiftIntensity = Math.Clamp(liftIntensity, .1, 1);
        SparkEnabled = spark;
        SparkIntensity = Math.Clamp(sparkIntensity, .1, 1);
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
            button.IsVisibleChanged += Button_IsVisibleChanged;
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
            button.IsVisibleChanged -= Button_IsVisibleChanged;
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
    private static void Button_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Button b && b.IsLoaded) UpdateVisuals(b, pressed: false); }

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
        if (button.Effect is DropShadowEffect existingGlow)
        {
            existingGlow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            existingGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            button.Effect = null;
        }
        if (button.RenderTransform is ScaleTransform or TransformGroup) button.RenderTransform = Transform.Identity;
        if (button.Template?.FindName("ButtonAura", button) is TargetAura aura)
            aura.Visibility = Visibility.Collapsed;
        if (button.Template?.FindName("AccentRing", button) is Border ring)
        {
            ring.BeginAnimation(UIElement.OpacityProperty, null);
            ring.Opacity = 0;
            if (ring.Tag is RotateTransform rot) rot.BeginAnimation(RotateTransform.AngleProperty, null);
            ring.Tag = null;
            ring.SetResourceReference(Border.BorderBrushProperty, "Accent");
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

        UpdateGlowAndPulse(button, active, hovered);
        UpdateMotion(button, hovered, pressed);
        UpdateRingAndSpark(button, active, hovered);
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

    private static void UpdateGlowAndPulse(Button button, bool active, bool hovered)
    {
        bool wantGlow = GlowEnabled && active && button.IsVisible;
        bool wantPulse = PulseEnabled && active && button.IsVisible;
        if (!wantGlow && !wantPulse)
        {
            if (button.Effect is DropShadowEffect existing)
            {
                existing.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                existing.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
                button.Effect = null;
            }
            return;
        }
        Color accent = Application.Current?.Resources["EffectColor"] is Color c ? c : Color.FromRgb(90, 170, 210);
        double baseStrength = wantGlow ? GlowIntensity : PulseIntensity;
        double blur = 10 + 10 * baseStrength;
        double opacity = (.35 + .55 * baseStrength) * (hovered ? 1.0 : .78);

        if (button.Effect is not DropShadowEffect effect || effect.IsFrozen)
        {
            effect = new DropShadowEffect { Color = accent, ShadowDepth = 0, BlurRadius = blur, Opacity = opacity };
            button.Effect = effect;
        }
        else
        {
            effect.Color = accent;
        }

        if (wantPulse && SystemParameters.ClientAreaAnimation)
        {
            double loOp = Math.Max(.14, opacity * .38);
            double hiOp = Math.Min(1.0, opacity * (1.05 + .22 * PulseIntensity));
            var opAnim = new DoubleAnimation(loOp, hiOp, TimeSpan.FromMilliseconds(950))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Timeline.SetDesiredFrameRate(opAnim, 24);
            effect.BeginAnimation(DropShadowEffect.OpacityProperty, opAnim);

            double hiBlur = blur + 6 + 6 * PulseIntensity;
            var blurAnim = new DoubleAnimation(blur * .78, hiBlur, TimeSpan.FromMilliseconds(950))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Timeline.SetDesiredFrameRate(blurAnim, 24);
            effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnim);
        }
        else
        {
            effect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            effect.BlurRadius = blur;
            effect.Opacity = opacity;
        }
    }

    private static void UpdateMotion(Button button, bool hovered, bool pressed)
    {
        bool wantBounce = BounceEnabled && button.IsEnabled && SystemParameters.ClientAreaAnimation;
        bool wantLift = LiftEnabled && button.IsEnabled && SystemParameters.ClientAreaAnimation;
        if (!wantBounce && !wantLift)
        {
            if (button.RenderTransform is ScaleTransform or TransformGroup) button.RenderTransform = Transform.Identity;
            return;
        }
        ScaleTransform scale;
        TranslateTransform lift;
        if (button.RenderTransform is TransformGroup { Children.Count: 2 } group &&
            group.Children[0] is ScaleTransform s && group.Children[1] is TranslateTransform t)
        {
            scale = s;
            lift = t;
        }
        else
        {
            button.RenderTransformOrigin = new Point(.5, .5);
            scale = new ScaleTransform(1, 1);
            lift = new TranslateTransform(0, 0);
            var created = new TransformGroup();
            created.Children.Add(scale);
            created.Children.Add(lift);
            button.RenderTransform = created;
        }

        double targetScale = wantBounce ? (pressed ? .95 : hovered ? 1 + .08 * BounceIntensity : 1) : 1;
        double targetY = wantLift ? (pressed ? .8 : hovered ? -(1.4 + 2.6 * LiftIntensity) : 0) : 0;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(pressed ? 70 : 140);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = ease });
        lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, duration) { EasingFunction = ease });
    }

    private static void UpdateRingAndSpark(Button button, bool active, bool hovered)
    {
        if (button.Template?.FindName("AccentRing", button) is not Border ring) return;
        bool wantRing = RingEnabled && active;
        bool wantSpark = SparkEnabled && active && button.IsVisible;

        if (!wantRing && !wantSpark)
        {
            if (ring.Tag is RotateTransform idleRot) idleRot.BeginAnimation(RotateTransform.AngleProperty, null);
            if (!SystemParameters.ClientAreaAnimation)
            {
                ring.BeginAnimation(UIElement.OpacityProperty, null);
                ring.Opacity = 0;
            }
            else
            {
                ring.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(140)));
            }
            return;
        }

        if (wantSpark)
        {
            Color accent = Application.Current?.Resources["EffectColor"] is Color c ? c : Color.FromRgb(90, 170, 210);
            Color hi = EffectPalette.Blend(accent, Colors.White, .78);
            var rot = ring.Tag as RotateTransform ?? new RotateTransform(0, .5, .5);
            ring.Tag = rot;
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                RelativeTransform = rot,
                GradientStops =
                [
                    new GradientStop(Color.FromArgb(wantRing ? (byte)160 : (byte)45, accent.R, accent.G, accent.B), 0),
                    new GradientStop(hi, .48),
                    new GradientStop(Color.FromArgb(wantRing ? (byte)160 : (byte)45, accent.R, accent.G, accent.B), 1)
                ]
            };
            ring.BorderBrush = brush;
            if (SystemParameters.ClientAreaAnimation)
            {
                var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(2200 - 900 * SparkIntensity))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Timeline.SetDesiredFrameRate(spin, 24);
                rot.BeginAnimation(RotateTransform.AngleProperty, spin);
            }
        }
        else
        {
            if (ring.Tag is RotateTransform oldRot) oldRot.BeginAnimation(RotateTransform.AngleProperty, null);
            ring.Tag = null;
            ring.SetResourceReference(Border.BorderBrushProperty, "Accent");
        }

        double strength = Math.Max(wantRing ? RingIntensity : 0, wantSpark ? SparkIntensity : 0);
        double target = (.52 + .48 * strength) * (hovered ? 1.0 : .85);
        if (!SystemParameters.ClientAreaAnimation)
        {
            ring.BeginAnimation(UIElement.OpacityProperty, null);
            ring.Opacity = target;
            return;
        }
        ring.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(140)));
    }

    private static void UpdateSheen(Button button, bool active, bool hovered)
    {
        if (button.Template?.FindName("SheenOverlay", button) is not Border sheen) return;
        if (!SheenEnabled || !active || !button.IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            sheen.BeginAnimation(UIElement.OpacityProperty, null);
            sheen.Opacity = 0;
            if (sheen.Tag is TranslateTransform idleShift)
                idleShift.BeginAnimation(TranslateTransform.XProperty, null);
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
