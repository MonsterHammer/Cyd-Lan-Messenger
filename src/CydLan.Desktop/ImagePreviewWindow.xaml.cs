using System.Windows;
using System.Windows.Media.Imaging;

namespace CydLan.Desktop;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(string fileName, BitmapSource image)
    {
        InitializeComponent();
        Title = ImageName.Text = fileName;
        PreviewImage.Source = image;
        PopupHost.EnableDrag(this, TitleBar);
    }
}
