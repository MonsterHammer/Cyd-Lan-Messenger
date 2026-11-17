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

/// <summary>LAN Messenger emoticon pictures, loaded once from the app's resources.</summary>
public static class SmileyImages
{
    private static readonly System.Windows.Media.Imaging.BitmapImage?[] cache = new System.Windows.Media.Imaging.BitmapImage?[CydLan.Core.Smileys.Count + 1];
    public static System.Windows.Media.Imaging.BitmapImage Get(int picture)
    {
        if (cache[picture] is { } hit) return hit;
        var image = new System.Windows.Media.Imaging.BitmapImage(new Uri($"pack://application:,,,/CydLan;component/Assets/smileys/{picture:00}.png"));
        image.Freeze();
        return cache[picture] = image;
    }
}

/// <summary>Fills a TextBlock with text, drawing LAN Messenger emoticon codes as their pictures.</summary>
public static class SmileyText
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(SmileyText),
        new PropertyMetadata("", (d, e) => { if (d is System.Windows.Controls.TextBlock block) Fill(block, e.NewValue as string ?? ""); }));
    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);
    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);
    private static void Fill(System.Windows.Controls.TextBlock block, string text)
    {
        block.Inlines.Clear();
        foreach (var segment in CydLan.Core.Smileys.Split(text))
        {
            if (segment.Picture == 0) { block.Inlines.Add(new System.Windows.Documents.Run(segment.Text)); continue; }
            var image = new System.Windows.Controls.Image { Source = SmileyImages.Get(segment.Picture), Width = 19, Height = 19, ToolTip = segment.Text };
            block.Inlines.Add(new System.Windows.Documents.InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center });
        }
    }
}

/// <summary>Visible when the text contains an emoticon code (or, with parameter "plain", when it does not).</summary>
public sealed class SmileyVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool has = value is string text && CydLan.Core.Smileys.HasAny(text);
        return has != (parameter as string == "plain") ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
