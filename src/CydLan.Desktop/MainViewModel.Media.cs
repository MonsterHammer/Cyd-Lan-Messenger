using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CydLan.Core;

namespace CydLan.Desktop;

public enum MediaRepeatMode { Off, All, One }

public sealed partial class MainViewModel
{
    public ObservableCollection<MediaItem> MediaItems { get; } = [];
    public ICollectionView MediaView { get; private set; } = null!;

    private readonly HashSet<string> sessionHiddenMedia = new(StringComparer.OrdinalIgnoreCase);
    private List<MediaItem> cachedScannedMedia = [];
    private bool mediaLoaded;
    private string mediaFilter = "all";
    private MediaItem? currentMedia;
    private bool isMediaPlaying;
    private double mediaPositionSeconds;
    private double mediaDurationSeconds;
    private bool mediaShuffle;
    private MediaRepeatMode mediaRepeat = MediaRepeatMode.Off;
    private double mediaVolume = .8;
    private bool mediaMuted;
    private double mediaSpeed = 1.0;

    public event Action<MediaItem>? MediaLoadRequested;
    public event Action<bool>? MediaPlayPauseRequested;
    public event Action? MediaStopRequested;
    public event Action<double>? MediaSeekRequested;
    public event Action<double>? MediaVolumeRequested;
    public event Action<double>? MediaSpeedRequested;

    public string MediaFilter
    {
        get => mediaFilter;
        set
        {
            if (!Set(ref mediaFilter, value)) return;
            MediaView.Refresh();
            Changed(nameof(NoMediaShown));
        }
    }

    public MediaItem? CurrentMedia
    {
        get => currentMedia;
        private set
        {
            if (currentMedia == value) return;
            if (currentMedia is not null) { currentMedia.IsCurrent = false; currentMedia.IsPlaying = false; }
            currentMedia = value;
            if (currentMedia is not null) currentMedia.IsCurrent = true;
            Changed();
            Changed(nameof(HasCurrentMedia));
            Changed(nameof(IsCurrentVideo));
            Changed(nameof(IsCurrentAudio));
            Changed(nameof(ShowMiniPlayer));
            Changed(nameof(MediaProgressRatio));
            if (MediaOpen) { Changed(nameof(ChatTitle)); Changed(nameof(ChatSubtitle)); }
        }
    }

    public bool HasCurrentMedia => currentMedia is not null;
    public bool IsCurrentVideo => currentMedia?.IsVideo == true;
    public bool IsCurrentAudio => currentMedia is { IsVideo: false };
    public bool ShowMiniPlayer => !MediaOpen && HasCurrentMedia;

    public bool IsMediaPlaying
    {
        get => isMediaPlaying;
        private set
        {
            if (!Set(ref isMediaPlaying, value)) return;
            if (currentMedia is not null) currentMedia.IsPlaying = value;
            Changed(nameof(IsCurrentAudioPlaying));
            Changed(nameof(MediaPlayLabel));
        }
    }

    public bool IsCurrentAudioPlaying => isMediaPlaying && IsCurrentAudio;
    public string MediaPlayLabel => isMediaPlaying ? "Pause" : "Play";

    public double MediaPositionSeconds
    {
        get => mediaPositionSeconds;
        private set
        {
            if (!Set(ref mediaPositionSeconds, Math.Max(0, value))) return;
            Changed(nameof(MediaPositionLabel));
            Changed(nameof(MediaProgressRatio));
        }
    }

    public double MediaDurationSeconds
    {
        get => mediaDurationSeconds;
        private set
        {
            if (!Set(ref mediaDurationSeconds, Math.Max(0, value))) return;
            Changed(nameof(MediaDurationLabel));
            Changed(nameof(MediaProgressRatio));
        }
    }

    public double MediaProgressRatio => mediaDurationSeconds > .05 ? Math.Clamp(mediaPositionSeconds / mediaDurationSeconds, 0, 1) : 0;
    public string MediaPositionLabel => FormatDuration(mediaPositionSeconds);
    public string MediaDurationLabel => FormatDuration(mediaDurationSeconds);

    public bool MediaShuffle { get => mediaShuffle; set => Set(ref mediaShuffle, value); }

    public MediaRepeatMode MediaRepeat
    {
        get => mediaRepeat;
        set
        {
            if (!Set(ref mediaRepeat, value)) return;
            Changed(nameof(MediaRepeatLabel));
            Changed(nameof(MediaRepeatActive));
            Changed(nameof(MediaRepeatOne));
        }
    }

    public string MediaRepeatLabel => mediaRepeat switch
    {
        MediaRepeatMode.All => "Repeat all",
        MediaRepeatMode.One => "Repeat one",
        _ => "Repeat off"
    };
    public bool MediaRepeatActive => mediaRepeat != MediaRepeatMode.Off;
    public bool MediaRepeatOne => mediaRepeat == MediaRepeatMode.One;

    public double MediaVolume
    {
        get => mediaVolume;
        set
        {
            double clamped = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : .8;
            if (!Set(ref mediaVolume, clamped)) return;
            if (clamped > 0 && mediaMuted) MediaMuted = false;
            Settings.MediaVolume = Math.Round(clamped, 2);
            if (!Preview) store?.SaveSettings(Settings);
            Changed(nameof(EffectiveMediaVolume));
            MediaVolumeRequested?.Invoke(EffectiveMediaVolume);
        }
    }

    public bool MediaMuted
    {
        get => mediaMuted;
        set
        {
            if (!Set(ref mediaMuted, value)) return;
            Changed(nameof(EffectiveMediaVolume));
            MediaVolumeRequested?.Invoke(EffectiveMediaVolume);
        }
    }

    public double EffectiveMediaVolume => mediaMuted ? 0 : mediaVolume;

    public double MediaSpeed
    {
        get => mediaSpeed;
        set
        {
            double clamped = Math.Clamp(value, .5, 2.0);
            if (!Set(ref mediaSpeed, clamped)) return;
            Changed(nameof(MediaSpeedLabel));
            MediaSpeedRequested?.Invoke(clamped);
        }
    }

    public string MediaSpeedLabel => Math.Abs(mediaSpeed - 1.0) < .01 ? "1x" : $"{mediaSpeed:0.##}x";

    public int AllMediaCount => MediaItems.Count;
    public int AudioMediaCount => MediaItems.Count(m => !m.IsVideo);
    public int VideoMediaCount => MediaItems.Count(m => m.IsVideo);
    public int LanMediaCount => MediaItems.Count(m => m.IsFromLan);
    public bool NoMediaShown => MediaView.IsEmpty;
    public bool HasAddedMediaSources => (Settings.MediaFiles?.Count ?? 0) > 0 || (Settings.MediaFolders?.Count ?? 0) > 0;

    private void InitMedia()
    {
        Settings.MediaFolders ??= [];
        Settings.MediaFiles ??= [];
        mediaVolume = double.IsFinite(Settings.MediaVolume) ? Math.Clamp(Settings.MediaVolume, 0, 1) : .8;
        MediaView = CollectionViewSource.GetDefaultView(MediaItems);
        MediaView.Filter = item => item is MediaItem media && MediaFilter switch
        {
            "audio" => !media.IsVideo,
            "video" => media.IsVideo,
            "lan" => media.IsFromLan,
            _ => true
        };
    }

    private void RefreshMediaFilterIfLoaded()
    {
        if (!mediaLoaded) return;
        MediaView.Refresh();
        Changed(nameof(NoMediaShown));
    }

    public async Task EnsureMediaLoadedAsync()
    {
        if (mediaLoaded || disposed) return;
        mediaLoaded = true;
        var folders = (Settings.MediaFolders ?? []).ToList();
        var files = (Settings.MediaFiles ?? []).ToList();
        var scanned = (folders.Count > 0 || files.Count > 0) && !Preview
            ? await Task.Run(() => MediaCatalog.ScanAddedPaths(folders, files))
            : MediaCatalog.ScanAddedPaths(folders, files);
        if (disposed) return;
        cachedScannedMedia = scanned;
        MergeMediaLibrary();
    }

    private void SyncMediaFromTransferIfLoaded(FileTransfer transfer)
    {
        if (!mediaLoaded || transfer.Folder || transfer.Inline || !MediaCatalog.IsMediaFile(transfer.Name)) return;
        if (transfer.State is not ("Received" or "Sent · receipt unavailable") || !File.Exists(transfer.Path)) return;
        sessionHiddenMedia.Remove(transfer.Path);
        MergeMediaLibrary();
    }

    private void MergeMediaLibrary()
    {
        var merged = new List<MediaItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (currentMedia is not null && File.Exists(currentMedia.Path) && seen.Add(currentMedia.Path))
            merged.Add(currentMedia);

        foreach (var t in Transfers)
        {
            if (t.Folder || t.Inline || !MediaCatalog.IsMediaFile(t.Name)) continue;
            if (t.State is not ("Received" or "Sent · receipt unavailable") || string.IsNullOrWhiteSpace(t.Path)) continue;
            if (sessionHiddenMedia.Contains(t.Path)) continue;
            string source = string.IsNullOrWhiteSpace(t.PeerName) ? "LAN" : $"LAN · {t.PeerName}";
            if (MediaCatalog.TryCreateItem(t.Path, source, isFromLan: true, isManualFile: false, t.Time, t.Size) is { } item && seen.Add(item.Path))
                merged.Add(item);
        }

        foreach (var item in cachedScannedMedia)
        {
            if (sessionHiddenMedia.Contains(item.Path)) continue;
            if (seen.Add(item.Path)) merged.Add(item);
        }

        UpdateRows(MediaItems, merged);
        MediaView.Refresh();
        NotifyMediaCountsChanged();
    }

    private void NotifyMediaCountsChanged()
    {
        Changed(nameof(AllMediaCount));
        Changed(nameof(AudioMediaCount));
        Changed(nameof(VideoMediaCount));
        Changed(nameof(LanMediaCount));
        Changed(nameof(NoMediaShown));
        Changed(nameof(HasAddedMediaSources));
    }

    public async Task AddMediaPathsAsync(IEnumerable<string> paths, bool autoPlayFirst = false)
    {
        Settings.MediaFiles ??= [];
        Settings.MediaFolders ??= [];
        var filesChanged = false;
        MediaItem? firstAdded = null;

        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string full;
            try { full = Path.GetFullPath(raw); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { continue; }

            if (Directory.Exists(full))
            {
                if (!Settings.MediaFolders.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
                {
                    Settings.MediaFolders.Add(full);
                    filesChanged = true;
                }
            }
            else if (MediaCatalog.TryCreateItem(full, "Added", isFromLan: false, isManualFile: true) is { } item)
            {
                sessionHiddenMedia.Remove(item.Path);
                if (!Settings.MediaFiles.Any(f => string.Equals(f, item.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    Settings.MediaFiles.Add(item.Path);
                    filesChanged = true;
                }
                firstAdded ??= item;
            }
        }

        if (filesChanged && !Preview) store?.SaveSettings(Settings);
        mediaLoaded = true;
        cachedScannedMedia = Preview
            ? MediaCatalog.ScanAddedPaths(Settings.MediaFolders, Settings.MediaFiles)
            : await Task.Run(() => MediaCatalog.ScanAddedPaths(Settings.MediaFolders, Settings.MediaFiles));
        if (disposed) return;

        MergeMediaLibrary();
        if (firstAdded is not null)
        {
            var match = MediaItems.FirstOrDefault(m => string.Equals(m.Path, firstAdded.Path, StringComparison.OrdinalIgnoreCase)) ?? firstAdded;
            if (autoPlayFirst || !HasCurrentMedia) PlayMedia(match);
        }
        else if (!HasCurrentMedia && MediaView.Cast<MediaItem>().FirstOrDefault() is { } firstFromFolder && autoPlayFirst)
        {
            PlayMedia(firstFromFolder);
        }
        else if (filesChanged && MediaItems.Count == 0)
        {
            Toast = "No supported media files found.";
        }
    }

    public void RemoveMediaItem(MediaItem item)
    {
        if (CurrentMedia == item) StopMedia();
        sessionHiddenMedia.Add(item.Path);
        Settings.MediaFiles ??= [];
        if (Settings.MediaFiles.RemoveAll(f => string.Equals(f, item.Path, StringComparison.OrdinalIgnoreCase)) > 0 && !Preview)
            store?.SaveSettings(Settings);
        cachedScannedMedia.RemoveAll(m => string.Equals(m.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        MediaItems.Remove(item);
        MediaView.Refresh();
        NotifyMediaCountsChanged();
    }

    public void ClearAddedMedia()
    {
        Settings.MediaFiles?.Clear();
        Settings.MediaFolders?.Clear();
        if (!Preview) store?.SaveSettings(Settings);
        if (CurrentMedia is { IsFromLan: false }) StopMedia();
        cachedScannedMedia.Clear();
        MergeMediaLibrary();
    }

    public void PlayPath(string path, string sourceLabel = "LAN", bool isFromLan = true)
    {
        if (!File.Exists(path) || !MediaCatalog.IsMediaFile(path))
        {
            Toast = "Media file unavailable";
            return;
        }
        MediaOpen = true;
        sessionHiddenMedia.Remove(path);
        var existing = MediaItems.FirstOrDefault(m => string.Equals(m.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = MediaCatalog.TryCreateItem(path, sourceLabel, isFromLan, isManualFile: !isFromLan);
            if (existing is null) { Toast = "Media file unavailable"; return; }
            if (!isFromLan)
            {
                Settings.MediaFiles ??= [];
                if (!Settings.MediaFiles.Any(f => string.Equals(f, existing.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    Settings.MediaFiles.Add(existing.Path);
                    if (!Preview) store?.SaveSettings(Settings);
                }
                cachedScannedMedia.Insert(0, existing);
            }
            MediaItems.Insert(0, existing);
            MediaView.Refresh();
            NotifyMediaCountsChanged();
        }
        PlayMedia(existing);
    }

    public void PlayMedia(MediaItem item)
    {
        if (!File.Exists(item.Path))
        {
            Toast = $"File not found · {item.Title}";
            RemoveMediaItem(item);
            return;
        }
        MediaPositionSeconds = 0;
        MediaDurationSeconds = 0;
        CurrentMedia = item;
        IsMediaPlaying = true;
        MediaLoadRequested?.Invoke(item);
    }

    public void TogglePlayPause()
    {
        if (CurrentMedia is null)
        {
            if ((MediaView.Cast<MediaItem>().FirstOrDefault() ?? MediaItems.FirstOrDefault()) is { } first)
                PlayMedia(first);
            return;
        }
        IsMediaPlaying = !IsMediaPlaying;
        MediaPlayPauseRequested?.Invoke(IsMediaPlaying);
    }

    public void StopMedia()
    {
        IsMediaPlaying = false;
        MediaPositionSeconds = 0;
        MediaDurationSeconds = 0;
        CurrentMedia = null;
        MediaStopRequested?.Invoke();
    }

    public void NextMedia(bool autoAdvance = false)
    {
        if (CurrentMedia is not { } cur)
        {
            if (MediaView.Cast<MediaItem>().FirstOrDefault() is { } first) PlayMedia(first);
            return;
        }
        if (autoAdvance && MediaRepeat == MediaRepeatMode.One)
        {
            SeekMedia(0);
            IsMediaPlaying = true;
            MediaPlayPauseRequested?.Invoke(true);
            return;
        }

        var list = MediaView.Cast<MediaItem>().ToList();
        if (list.Count == 0) list = MediaItems.ToList();
        if (list.Count == 0) { StopMedia(); return; }

        if (MediaShuffle && list.Count > 1)
        {
            var candidates = list.Where(m => m != cur).ToList();
            PlayMedia(candidates[Random.Shared.Next(candidates.Count)]);
            return;
        }

        int idx = list.IndexOf(cur);
        int next = idx + 1;
        if (next >= list.Count)
        {
            if (!autoAdvance || MediaRepeat == MediaRepeatMode.All)
                PlayMedia(list[0]);
            else
            {
                IsMediaPlaying = false;
                MediaPositionSeconds = 0;
                MediaSeekRequested?.Invoke(0);
                MediaPlayPauseRequested?.Invoke(false);
            }
            return;
        }
        PlayMedia(list[next]);
    }

    public void PreviousMedia()
    {
        if (CurrentMedia is null) return;
        if (MediaPositionSeconds > 3.0)
        {
            SeekMedia(0);
            return;
        }
        var list = MediaView.Cast<MediaItem>().ToList();
        if (list.Count == 0) list = MediaItems.ToList();
        if (list.Count == 0) return;

        int idx = list.IndexOf(CurrentMedia);
        int prev = idx <= 0 ? list.Count - 1 : idx - 1;
        PlayMedia(list[prev]);
    }

    public void SeekMedia(double seconds)
    {
        double clamped = MediaDurationSeconds > 0 ? Math.Clamp(seconds, 0, MediaDurationSeconds) : Math.Max(0, seconds);
        MediaPositionSeconds = clamped;
        MediaSeekRequested?.Invoke(clamped);
    }

    public void ToggleMediaShuffle() => MediaShuffle = !MediaShuffle;

    public void CycleMediaRepeat() => MediaRepeat = MediaRepeat switch
    {
        MediaRepeatMode.Off => MediaRepeatMode.All,
        MediaRepeatMode.All => MediaRepeatMode.One,
        _ => MediaRepeatMode.Off
    };

    public void CycleMediaSpeed() => MediaSpeed = MediaSpeed switch
    {
        < .9 => 1.0,
        < 1.2 => 1.25,
        < 1.4 => 1.5,
        < 1.9 => 2.0,
        _ => .75
    };

    internal void ReportMediaProgress(double posSeconds, double durationSeconds)
    {
        if (double.IsFinite(durationSeconds) && durationSeconds > 0)
            MediaDurationSeconds = durationSeconds;
        if (double.IsFinite(posSeconds) && posSeconds >= 0)
            MediaPositionSeconds = posSeconds;
    }

    internal void ReportMediaEnded() => NextMedia(autoAdvance: true);

    internal void ReportMediaFailed(string? detail = null)
    {
        string name = CurrentMedia?.Title ?? "media";
        StopMedia();
        Toast = string.IsNullOrWhiteSpace(detail) ? $"Unable to play {name}" : $"Unable to play {name}: {detail}";
    }

    private static string FormatDuration(double totalSeconds)
    {
        if (!double.IsFinite(totalSeconds) || totalSeconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }
}
