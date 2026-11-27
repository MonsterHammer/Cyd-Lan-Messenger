using System.IO;
using System.Windows;
using System.Windows.Media;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Represents a playable audio or video entry from LAN transfers or user-picked folders.</summary>
public sealed class MediaItem : Observable
{
    private bool isCurrent;
    private bool isPlaying;

    public required string Path { get; init; }
    public required string Title { get; init; }
    public required string Extension { get; init; }
    public bool IsVideo { get; init; }
    public bool IsFromLan { get; init; }
    public bool IsManualFile { get; init; }
    public required string SourceLabel { get; init; }
    public long Size { get; init; }
    public DateTimeOffset AddedTime { get; init; }

    public string SizeLabel => Size > 0 ? FileTransfer.FormatBytes(Size) : "";
    public string MetaLabel => SizeLabel.Length > 0 ? $"{Extension} · {SizeLabel} · {SourceLabel}" : $"{Extension} · {SourceLabel}";
    public string KindLabel => IsVideo ? "Video" : "Audio";

    public bool IsCurrent { get => isCurrent; set => Set(ref isCurrent, value); }
    public bool IsPlaying { get => isPlaying; set => Set(ref isPlaying, value); }
}

/// <summary>Supported extension checks and lazy bounded scanning for user-selected media paths.</summary>
public static class MediaCatalog
{
    public const int MaxFilesPerFolder = 500;
    public const int MaxTotalScanned = 1500;

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".wma", ".aac", ".m4a", ".flac"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".wmv", ".avi"
    };

    public const string OpenFileFilter =
        "Media files|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.mp4;*.m4v;*.mov;*.wmv;*.avi|" +
        "Audio|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac|" +
        "Video|*.mp4;*.m4v;*.mov;*.wmv;*.avi";

    public static bool IsAudioFile(string? nameOrPath) =>
        !string.IsNullOrWhiteSpace(nameOrPath) && AudioExtensions.Contains(Path.GetExtension(nameOrPath.Trim()));

    public static bool IsVideoFile(string? nameOrPath) =>
        !string.IsNullOrWhiteSpace(nameOrPath) && VideoExtensions.Contains(Path.GetExtension(nameOrPath.Trim()));

    public static bool IsMediaFile(string? nameOrPath) => IsAudioFile(nameOrPath) || IsVideoFile(nameOrPath);

    public static MediaItem? TryCreateItem(string path, string sourceLabel, bool isFromLan, bool isManualFile, DateTimeOffset? time = null, long knownSize = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsMediaFile(path)) return null;
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }

        long size = knownSize;
        DateTimeOffset added = time ?? DateTimeOffset.MinValue;
        try
        {
            var info = new FileInfo(full);
            if (!info.Exists) return null;
            if (size <= 0) size = info.Length;
            if (added == DateTimeOffset.MinValue) added = info.LastWriteTimeUtc;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }

        string ext = Path.GetExtension(full).TrimStart('.').ToUpperInvariant();
        string title = Path.GetFileNameWithoutExtension(full);
        if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileName(full);
        return new MediaItem
        {
            Path = full,
            Title = title,
            Extension = ext,
            IsVideo = IsVideoFile(full),
            IsFromLan = isFromLan,
            IsManualFile = isManualFile,
            SourceLabel = sourceLabel,
            Size = size,
            AddedTime = added
        };
    }

    public static List<MediaItem> ScanAddedPaths(IEnumerable<string> folders, IEnumerable<string> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<MediaItem>();

        foreach (var file in files)
        {
            if (results.Count >= MaxTotalScanned) break;
            if (TryCreateItem(file, "Added", isFromLan: false, isManualFile: true) is { } item && seen.Add(item.Path))
                results.Add(item);
        }

        var enumOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            MaxRecursionDepth = 3,
            ReturnSpecialDirectories = false
        };

        foreach (var folder in folders)
        {
            if (results.Count >= MaxTotalScanned) break;
            if (string.IsNullOrWhiteSpace(folder)) continue;
            string fullDir;
            try { fullDir = Path.GetFullPath(folder); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (!Directory.Exists(fullDir)) continue;

            string label = Path.GetFileName(fullDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(label)) label = "Folder";

            int addedFromFolder = 0;
            try
            {
                foreach (var entry in Directory.EnumerateFiles(fullDir, "*", enumOptions))
                {
                    if (addedFromFolder >= MaxFilesPerFolder || results.Count >= MaxTotalScanned) break;
                    if (!IsMediaFile(entry)) continue;
                    if (TryCreateItem(entry, label, isFromLan: false, isManualFile: false) is { } item && seen.Add(item.Path))
                    {
                        results.Add(item);
                        addedFromFolder++;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }

        return results;
    }
}

/// <summary>Draws lightweight theme-tinted visualizer bars for the audio stage while playback is active.</summary>
public sealed class AudioVisualizer : FrameworkElement
{
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(AudioVisualizer),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(AudioVisualizer),
        new FrameworkPropertyMetadata(false, (d, _) => ((AudioVisualizer)d).OnPlaybackChanged()));

    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    private readonly System.Windows.Threading.DispatcherTimer timer = new(System.Windows.Threading.DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(55)
    };
    private double phase;

    public AudioVisualizer()
    {
        IsHitTestVisible = false;
        SetResourceReference(AccentBrushProperty, "Accent");
        timer.Tick += (_, _) =>
        {
            phase = (phase + .18) % (Math.PI * 100);
            InvalidateVisual();
        };
        IsVisibleChanged += (_, _) => UpdateTimer();
    }

    private void OnPlaybackChanged()
    {
        UpdateTimer();
        InvalidateVisual();
    }

    private void UpdateTimer()
    {
        bool run = IsPlaying && IsVisible && SystemParameters.ClientAreaAnimation;
        if (run && !timer.IsEnabled) timer.Start();
        else if (!run) timer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 16 || h <= 8) return;

        Color accent = AccentBrush is SolidColorBrush s ? s.Color : Color.FromRgb(0x3B, 0x7A, 0x8C);
        Brush barBrush = new SolidColorBrush(Color.FromArgb(IsPlaying ? (byte)195 : (byte)95, accent.R, accent.G, accent.B));
        barBrush.Freeze();

        const int count = 18;
        double slot = w / count;
        double barW = Math.Max(3, Math.Min(7, slot * .54));
        for (int i = 0; i < count; i++)
        {
            double norm = IsPlaying
                ? .22 + .78 * Math.Abs(Math.Sin(phase * (.85 + (i % 4) * .17) + i * .68) * Math.Cos(phase * .45 + i * .33))
                : .18 + .18 * Math.Abs(Math.Sin(i * .7));
            double bh = Math.Max(4, h * norm);
            double x = i * slot + (slot - barW) * .5;
            dc.DrawRoundedRectangle(barBrush, null, new Rect(x, h - bh, barW, bh), barW * .45, barW * .45);
        }
    }
}
