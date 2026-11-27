using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>An image or file waiting in the composer until the message is sent.</summary>
public sealed class Attachment
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; init; }
    public bool IsFolder { get; init; }
    /// <summary>Decoded thumbnail when the file is a readable image; null for other files.</summary>
    public ImageSource? Thumbnail { get; init; }
    public bool IsImage => Thumbnail is not null;
    public string SizeLabel => IsFolder ? "Folder" : FileTransfer.FormatBytes(Size);

    public static Attachment From(string path)
    {
        if (Directory.Exists(path))
            return new Attachment { Path = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)), IsFolder = true };
        var info = new FileInfo(path);
        return new Attachment { Path = info.FullName, Size = info.Length, Thumbnail = LanService.IsInlineImageName(info.Name) ? Images.Load(info.FullName, 160) : null };
    }
}

public static class Images
{
    private static readonly object CacheGate = new();
    private static readonly Dictionary<(string Path, long Ticks, long Length, int Width, int Height), WeakReference<BitmapImage>> WeakCache = [];

    /// <summary>Loads an image without locking the file; returns null when it is missing or not a decodable image.</summary>
    public static BitmapImage? Load(string path, int decodeWidth, int decodeHeight = 0)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;
            var key = (info.FullName, info.LastWriteTimeUtc.Ticks, info.Length, decodeWidth, decodeHeight);
            lock (CacheGate)
            {
                if (WeakCache.TryGetValue(key, out var existing) && existing.TryGetTarget(out var hit)) return hit;
            }
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodeWidth;
            image.DecodePixelHeight = decodeHeight;
            image.UriSource = new Uri(info.FullName);
            image.EndInit();
            image.Freeze();
            lock (CacheGate)
            {
                if (WeakCache.Count >= 128)
                {
                    foreach (var dead in WeakCache.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray())
                        WeakCache.Remove(dead);
                    if (WeakCache.Count >= 192) WeakCache.Clear();
                }
                WeakCache[key] = new WeakReference<BitmapImage>(image);
            }
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException or FileFormatException)
        { return null; }
    }

    internal static BitmapImage? LoadChatBackground(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            const int maxSide = 4096;
            if (frame.PixelWidth >= frame.PixelHeight) return Load(path, Math.Min(frame.PixelWidth, maxSide));
            return Load(path, 0, Math.Min(frame.PixelHeight, maxSide));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException or System.Runtime.InteropServices.COMException)
        { return null; }
    }

    internal static BitmapImage? LoadChatBackgroundFromBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        try
        {
            using var probe = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth > 4096 || frame.PixelHeight > 4096) return null;

            const int maxSide = 4096;
            using var ms = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (frame.PixelWidth >= frame.PixelHeight) image.DecodePixelWidth = Math.Min(frame.PixelWidth, maxSide);
            else image.DecodePixelHeight = Math.Min(frame.PixelHeight, maxSide);
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException or InvalidOperationException or FileFormatException or System.Runtime.InteropServices.COMException)
        { return null; }
    }

    internal static BitmapSource ToGrayscale(BitmapSource source)
    {
        var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        var bgra = new FormatConvertedBitmap(gray, PixelFormats.Pbgra32, null, 0);
        bgra.Freeze();
        return bgra;
    }

    internal static bool HasGifSignature(ReadOnlySpan<byte> data) =>
        data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8));

    internal static string DetectImageExtension(ReadOnlySpan<byte> data)
    {
        if (HasGifSignature(data)) return ".gif";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M') return ".bmp";
        return ".png";
    }

    internal static string SaveBytes(byte[] bytes, string extension, string directory)
    {
        Directory.CreateDirectory(directory);
        string ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        var path = System.IO.Path.Combine(directory, $"bg-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}{ext}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Center-crops an image to a square, scales it to 96 px and saves it as a new PNG; null when the file is not a usable image.</summary>
    public static string? SaveProfilePicture(string source, string directory)
    {
        if (Load(source, 512) is not { PixelWidth: > 0, PixelHeight: > 0 } image) return null;
        int side = Math.Min(image.PixelWidth, image.PixelHeight);
        var square = new CroppedBitmap(image, new System.Windows.Int32Rect((image.PixelWidth - side) / 2, (image.PixelHeight - side) / 2, side, side));
        var scaled = new TransformedBitmap(square, new System.Windows.Media.ScaleTransform(96d / side, 96d / side));
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, $"profile-{Guid.NewGuid():N}.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(scaled));
        using (var file = File.Create(path)) encoder.Save(file);
        return path;
    }

    /// <summary>Saves a bitmap (for example a pasted screenshot) as PNG and returns the new file path.</summary>
    public static string SavePng(BitmapSource bitmap, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, $"Screenshot {DateTime.Now:yyyy-MM-dd HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
        return path;
    }
}
