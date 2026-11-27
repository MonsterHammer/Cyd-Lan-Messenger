using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>Cuts 32×32 card icons out of the pixel icon sheets in Assets/cardart. Missing sheets give no art.</summary>
public static class CardArt
{
    public const int Cell = 32, Columns = 8, Rows = 6;
    private static readonly Dictionary<string, BitmapSource?> Sheets = [];
    private static readonly Dictionary<string, ImageSource?> Icons = [];
    private static readonly Dictionary<string, (System.Windows.Media.Color Main, System.Windows.Media.Color Core)> Palettes = [];

    public static ImageSource? For(PetCard card) => For(card.Art);

    /// <summary>Extracts the dominant vivid accent and luminous core colors directly from a card's 32×32 pixel icon.</summary>
    public static (System.Windows.Media.Color Main, System.Windows.Media.Color Core) Palette(PetCard card)
    {
        if (Palettes.TryGetValue(card.Id, out var cached)) return cached;
        var fallback = card.Element.Length > 0
            ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PetGame.ElementOf(card.Element).Color)
            : System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0);
        var core = BlendWhite(fallback, .55);
        if (For(card) is BitmapSource bmp)
        {
            try
            {
                var conv = bmp.Format == PixelFormats.Bgra32 ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
                var buf = new byte[stride * h];
                conv.CopyPixels(buf, stride, 0);
                // Bucket pixels into 12 hue sectors weighted by saturation × brightness so outlines/backgrounds don't wash out the icon's true hue.
                double[] weight = new double[12], rs = new double[12], gs = new double[12], bs = new double[12];
                double bestBrightW = 0;
                byte brR = 255, brG = 255, brB = 255;
                for (int i = 0; i < buf.Length; i += 4)
                {
                    byte b = buf[i], g = buf[i + 1], r = buf[i + 2], a = buf[i + 3];
                    if (a < 180) continue;
                    int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    int delta = max - min;
                    if (max < 55 || delta < 28) continue;
                    double sat = delta / (double)max;
                    double val = max / 255.0;
                    double hue = max == r ? 60 * (((g - b) / (double)delta) % 6)
                        : max == g ? 60 * ((b - r) / (double)delta + 2)
                        : 60 * ((r - g) / (double)delta + 4);
                    if (hue < 0) hue += 360;
                    int bucket = Math.Clamp((int)(hue / 30), 0, 11);
                    double wgt = sat * sat * val * val;
                    weight[bucket] += wgt; rs[bucket] += r * wgt; gs[bucket] += g * wgt; bs[bucket] += b * wgt;
                    double bw = val * val * (0.4 + sat);
                    if (bw > bestBrightW) { bestBrightW = bw; brR = r; brG = g; brB = b; }
                }
                int top = 0;
                for (int k = 1; k < 12; k++) if (weight[k] > weight[top]) top = k;
                if (weight[top] > 2.5)
                {
                    var main = System.Windows.Media.Color.FromRgb(
                        (byte)Math.Clamp(rs[top] / weight[top], 0, 255),
                        (byte)Math.Clamp(gs[top] / weight[top], 0, 255),
                        (byte)Math.Clamp(bs[top] / weight[top], 0, 255));
                    var hi = bestBrightW > 0 ? BlendWhite(System.Windows.Media.Color.FromRgb(brR, brG, brB), .45) : BlendWhite(main, .55);
                    Palettes[card.Id] = (main, hi);
                    return (main, hi);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { }
        }
        Palettes[card.Id] = (fallback, core);
        return (fallback, core);
    }

    private static System.Windows.Media.Color BlendWhite(System.Windows.Media.Color c, double t) =>
        System.Windows.Media.Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));

    public static ImageSource? For(string art)
    {
        if (Icons.TryGetValue(art, out var cached)) return cached;
        ImageSource? icon = null;
        var (sheet, index) = new PetCard("", "", 0, "", Art: art).ArtCell;
        if (index is >= 0 and < Columns * Rows && Sheet(sheet) is { } source)
        {
            var crop = new CroppedBitmap(source, new Int32Rect(index % Columns * Cell, index / Columns * Cell, Cell, Cell));
            crop.Freeze();
            icon = crop;
        }
        Icons[art] = icon;
        return icon;
    }

    private static BitmapSource? Sheet(string name)
    {
        if (Sheets.TryGetValue(name, out var cached)) return cached;
        BitmapSource? sheet = null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/CydLan;component/Assets/cardart/{name}.png", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            sheet = image;
        }
        catch (Exception e) when (e is System.IO.IOException or UriFormatException or NotSupportedException or InvalidOperationException) { }
        Sheets[name] = sheet;
        return sheet;
    }

    /// <summary>A crisp (nearest-neighbour) image of a card's icon with curved corners, or null when the art isn't installed.</summary>
    public static System.Windows.Controls.Image? Image(PetCard card, double size)
    {
        if (For(card) is not { } source) return null;
        double radius = Math.Max(5, Math.Round(size * .2));
        var clip = new RectangleGeometry(new Rect(0, 0, size, size), radius, radius);
        clip.Freeze();
        var image = new System.Windows.Controls.Image { Source = source, Width = size, Height = size, Stretch = Stretch.Uniform, Clip = clip };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
    }
}
