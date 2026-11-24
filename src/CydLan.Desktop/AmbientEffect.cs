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
    private int activeFrameRate;
    internal double Progress => (double)GetValue(ProgressProperty);
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    protected AmbientEffect()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(TintProperty, "EffectColor");
        Loaded += (_, _) =>
        {
            host = Window.GetWindow(this);
            if (host is not null)
            {
                host.StateChanged += HostStateChanged;
                host.Activated += HostStateChanged;
                host.Deactivated += HostStateChanged;
            }
            SystemParameters.StaticPropertyChanged += SystemPropertyChanged;
            UpdateAnimation();
        };
        Unloaded += (_, _) =>
        {
            if (host is not null)
            {
                host.StateChanged -= HostStateChanged;
                host.Activated -= HostStateChanged;
                host.Deactivated -= HostStateChanged;
            }
            host = null;
            SystemParameters.StaticPropertyChanged -= SystemPropertyChanged;
            UpdateAnimation();
        };
        IsVisibleChanged += (_, _) => UpdateAnimation();
        SizeChanged += (_, _) => UpdateAnimation();
    }

    protected virtual void OnTintChanged() { }
    protected virtual int FrameRate => 24;
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

    private void UpdateAnimation()
    {
        int requestedFrameRate = host is { IsActive: true, WindowState: not WindowState.Minimized } && IsVisible &&
            SystemParameters.ClientAreaAnimation ? (IsLargeSurface ? Math.Min(18, FrameRate) : FrameRate) : 0;
        if (activeFrameRate == requestedFrameRate) return;
        activeFrameRate = requestedFrameRate;
        if (requestedFrameRate == 0)
        {
            BeginAnimation(ProgressProperty, null);
            return;
        }

        var drift = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(26)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(drift, requestedFrameRate);
        BeginAnimation(ProgressProperty, drift);
    }
}
