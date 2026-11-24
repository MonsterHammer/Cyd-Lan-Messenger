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
    public string SizeLabel => IsFolder ? "Folder" : Size < 1024 * 1024 ? $"{Size / 1024d:0.#} KB" : $"{Size / (1024d * 1024):0.#} MB";

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
    /// <summary>Loads an image without locking the file; returns null when it is missing or not a decodable image.</summary>
    public static BitmapImage? Load(string path, int decodeWidth, int decodeHeight = 0)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodeWidth;
            image.DecodePixelHeight = decodeHeight;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
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
            const int maxSide = 1600;
            if (frame.PixelWidth >= frame.PixelHeight) return Load(path, Math.Min(frame.PixelWidth, maxSide));
            return Load(path, 0, Math.Min(frame.PixelHeight, maxSide));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException or System.Runtime.InteropServices.COMException)
        { return null; }
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
