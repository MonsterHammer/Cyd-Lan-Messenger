using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CydLan.Desktop;

/// <summary>Clips any element (for example an image) to rounded corners that follow its size.</summary>
public static class Rounded
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached("Radius", typeof(double), typeof(Rounded),
        new PropertyMetadata(0.0, (d, _) => { if (d is FrameworkElement e) { e.SizeChanged -= Update; e.SizeChanged += Update; Apply(e); } }));
    public static double GetRadius(DependencyObject d) => (double)d.GetValue(RadiusProperty);
    public static void SetRadius(DependencyObject d, double value) => d.SetValue(RadiusProperty, value);
    private static void Update(object sender, SizeChangedEventArgs e) => Apply((FrameworkElement)sender);
    private static void Apply(FrameworkElement e)
    {
        double r = GetRadius(e);
        e.Clip = r <= 0 ? null : new RectangleGeometry(new Rect(e.RenderSize), r, r);
    }
}

/// <summary>Turns a stored image path into a downscaled, unlocked bitmap for message bubbles.</summary>
public sealed class ImagePathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string { Length: > 0 } path ? Images.Load(path, 640) : null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Loads a small profile picture; null (so the initials show) when there is none.</summary>
public sealed class AvatarPathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string { Length: > 0 } path ? Images.Load(path, 112) : null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
