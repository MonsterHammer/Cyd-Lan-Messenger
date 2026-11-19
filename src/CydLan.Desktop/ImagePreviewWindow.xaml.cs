using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CydLan.Desktop;

/// <summary>Shows one image over the main window: fitted at first, wheel to zoom at the pointer, drag to pan.</summary>
public partial class ImagePreviewWindow : Window
{
    private const double MaxScale = 8, ZoomStep = 1.15;
    private readonly BitmapSource image;
    private double scale = 1, fitScale = 1;
    private bool fitted = true;
    private Point panStart, panOffset;
    private bool panning, panned;

    public ImagePreviewWindow(string fileName, BitmapSource image)
    {
        InitializeComponent();
        this.image = image;
        Title = fileName;
        PreviewImage.Source = image;
    }

    private void Viewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double margin = 48;
        fitScale = Math.Min(1, Math.Min((Viewer.ActualWidth - margin) / image.Width, (Viewer.ActualHeight - margin) / image.Height));
        if (fitScale <= 0) fitScale = 1;
        if (fitted) SetScale(fitScale);
    }

    private void SetScale(double value)
    {
        scale = value;
        PreviewImage.Width = image.Width * scale;
        PreviewImage.Height = image.Height * scale;
    }

    // Zoom about the pointer so the spot under it stays put.
    private void ZoomAt(double target, Point pointer)
    {
        target = Math.Clamp(target, Math.Min(fitScale, 1), MaxScale);
        if (Math.Abs(target - scale) < 0.0001) return;
        var onImage = Viewer.TranslatePoint(pointer, PreviewImage);
        double ratio = target / scale;
        SetScale(target);
        fitted = Math.Abs(target - fitScale) < 0.0001;
        Viewer.UpdateLayout();
        var moved = PreviewImage.TranslatePoint(new Point(onImage.X * ratio, onImage.Y * ratio), Viewer);
        Viewer.ScrollToHorizontalOffset(Viewer.HorizontalOffset + moved.X - pointer.X);
        Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset + moved.Y - pointer.Y);
    }

    private void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(scale * (e.Delta > 0 ? ZoomStep : 1 / ZoomStep), e.GetPosition(Viewer));
        e.Handled = true;
    }

    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click switches between fitted and actual size.
            ZoomAt(fitted && fitScale < 1 ? 1 : fitScale, e.GetPosition(Viewer));
            e.Handled = true;
            return;
        }
        panStart = e.GetPosition(Viewer);
        panOffset = new Point(Viewer.HorizontalOffset, Viewer.VerticalOffset);
        panning = PreviewImage.CaptureMouse();
        panned = false;
        e.Handled = true;
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (!panning) return;
        var now = e.GetPosition(Viewer);
        if (Math.Abs(now.X - panStart.X) + Math.Abs(now.Y - panStart.Y) > 3) panned = true;
        Viewer.ScrollToHorizontalOffset(panOffset.X - (now.X - panStart.X));
        Viewer.ScrollToVerticalOffset(panOffset.Y - (now.Y - panStart.Y));
    }

    private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        panning = false;
        PreviewImage.ReleaseMouseCapture();
        e.Handled = true;
    }

    // Clicking outside the image closes the preview.
    private void Backdrop_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool onScrollBar = false;
        for (var node = e.OriginalSource as DependencyObject; node is not null && !onScrollBar; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            onScrollBar = node is System.Windows.Controls.Primitives.ScrollBar;
        if (!panned && !onScrollBar && e.OriginalSource is not System.Windows.Controls.Image) Close();
        panned = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
