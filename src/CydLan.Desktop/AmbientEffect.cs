using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;

namespace CydLan.Desktop;

public abstract class AmbientEffect : FrameworkElement
{
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(AmbientEffect), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(AmbientEffect), new FrameworkPropertyMetadata(.75, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(AmbientEffect), new FrameworkPropertyMetadata(Color.FromRgb(158, 63, 91),
            FrameworkPropertyMetadataOptions.AffectsRender, (element, _) => ((AmbientEffect)element).OnTintChanged()));

    private Window? host;
    private bool subscribed;
    private int activeFrameRate;
    private double activeSpeed = 1.0;
    private double accumulatedPhase;

    public static double SpeedMultiplier { get; private set; } = 1.0;
    public static bool AnimateWhenUnfocused { get; private set; }
    internal static event Action? SpeedChanged;
    internal static event Action? UnfocusedChanged;

    public static void SetSpeed(double speed)
    {
        double clamped = double.IsFinite(speed) ? Math.Clamp(speed, .4, 2.0) : 1.0;
        if (Math.Abs(SpeedMultiplier - clamped) < .001) return;
        SpeedMultiplier = clamped;
        SpeedChanged?.Invoke();
    }

    public static void SetAnimateWhenUnfocused(bool enabled)
    {
        if (AnimateWhenUnfocused == enabled) return;
        AnimateWhenUnfocused = enabled;
        UnfocusedChanged?.Invoke();
    }

    internal double Progress => (double)GetValue(ProgressProperty);
    /// <summary>Continuous monotonic phase that advances seamlessly without periodic wrap jumps or focus-switch resets.</summary>
    protected double Time => accumulatedPhase + Progress;
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    protected AmbientEffect()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(TintProperty, "EffectColor");
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdateAnimation();
        SizeChanged += (_, _) => UpdateAnimation();
    }

    private void Attach()
    {
        if (subscribed) { UpdateAnimation(); return; }
        subscribed = true;
        host = Window.GetWindow(this);
        if (host is not null)
        {
            host.StateChanged += HostStateChanged;
            host.Activated += HostStateChanged;
            host.Deactivated += HostStateChanged;
            host.Closed += HostClosed;
        }
        SystemParameters.StaticPropertyChanged += SystemPropertyChanged;
        SpeedChanged += OnSpeedChanged;
        UnfocusedChanged += OnUnfocusedChanged;
        UpdateAnimation();
    }

    private void Detach()
    {
        if (!subscribed) return;
        subscribed = false;
        if (host is not null)
        {
            host.StateChanged -= HostStateChanged;
            host.Activated -= HostStateChanged;
            host.Deactivated -= HostStateChanged;
            host.Closed -= HostClosed;
        }
        host = null;
        SystemParameters.StaticPropertyChanged -= SystemPropertyChanged;
        SpeedChanged -= OnSpeedChanged;
        UnfocusedChanged -= OnUnfocusedChanged;
        UpdateAnimation();
    }

    private void HostClosed(object? sender, EventArgs e) => Detach();
    private void OnSpeedChanged() => UpdateAnimation();
    private void OnUnfocusedChanged() => UpdateAnimation();

    protected virtual void OnTintChanged() { }
    protected virtual int FrameRate => 24;
    protected virtual bool HasActiveContent => true;
    protected bool IsLargeSurface
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return ActualWidth * ActualHeight * dpi.DpiScaleX * dpi.DpiScaleY > 800_000;
        }
    }
    internal int ActiveFrameRate => activeFrameRate;
    private void HostStateChanged(object? sender, EventArgs e) => UpdateAnimation();

    private void SystemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            Dispatcher.BeginInvoke(UpdateAnimation);
    }

    protected void UpdateAnimation()
    {
        int requestedFrameRate = HasActiveContent &&
            host is { WindowState: not WindowState.Minimized } &&
            (host.IsActive || AnimateWhenUnfocused) &&
            IsVisible &&
            SystemParameters.ClientAreaAnimation ? (IsLargeSurface ? Math.Min(18, FrameRate) : FrameRate) : 0;
        double speed = SpeedMultiplier;
        if (activeFrameRate == requestedFrameRate && (requestedFrameRate == 0 || Math.Abs(activeSpeed - speed) < .001)) return;
        if (activeFrameRate > 0)
            accumulatedPhase += Progress;
        activeFrameRate = requestedFrameRate;
        activeSpeed = speed;
        if (requestedFrameRate == 0)
        {
            BeginAnimation(ProgressProperty, null);
            return;
        }

        // IsCumulative advances Progress monotonically (0 -> 1 -> 2 -> ...) so no effect ever resets at cycle boundaries.
        var drift = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(26.0 / speed))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            IsCumulative = true
        };
        Timeline.SetDesiredFrameRate(drift, requestedFrameRate);
        BeginAnimation(ProgressProperty, drift);
    }
}
