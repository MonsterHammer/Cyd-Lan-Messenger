using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CydLan.Core;
using Microsoft.Win32;

namespace CydLan.Desktop;

public partial class MainWindow
{
    private readonly DispatcherTimer mediaProgressTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private bool seekingMedia;

    private void InitMediaController()
    {
        mediaProgressTimer.Tick += (_, _) => SyncMediaPosition();
        MediaPlayerElement.Volume = model.EffectiveMediaVolume;
        MediaPlayerElement.SpeedRatio = model.MediaSpeed;

        model.MediaLoadRequested += OnMediaLoadRequested;
        model.MediaPlayPauseRequested += OnMediaPlayPauseRequested;
        model.MediaStopRequested += OnMediaStopRequested;
        model.MediaSeekRequested += OnMediaSeekRequested;
        model.MediaVolumeRequested += v => MediaPlayerElement.Volume = v;
        model.MediaSpeedRequested += s => MediaPlayerElement.SpeedRatio = s;

        Closed += (_, _) =>
        {
            mediaProgressTimer.Stop();
            MediaPlayerElement.Stop();
            MediaPlayerElement.Close();
            MediaPlayerElement.Source = null;
        };
    }

    private void OnMediaLoadRequested(MediaItem item)
    {
        mediaProgressTimer.Stop();
        try
        {
            MediaPlayerElement.Stop();
            MediaPlayerElement.Close();
            MediaPlayerElement.Source = new Uri(item.Path, UriKind.Absolute);
            MediaPlayerElement.Volume = model.EffectiveMediaVolume;
            MediaPlayerElement.SpeedRatio = model.MediaSpeed;
            MediaPlayerElement.Play();
            mediaProgressTimer.Start();
        }
        catch (Exception e) when (e is UriFormatException or InvalidOperationException or IOException)
        {
            model.ReportMediaFailed(e.Message);
        }
    }

    private void OnMediaPlayPauseRequested(bool play)
    {
        if (MediaPlayerElement.Source is null && model.CurrentMedia is { } cur)
        {
            OnMediaLoadRequested(cur);
            if (!play) MediaPlayerElement.Pause();
            return;
        }
        if (play)
        {
            MediaPlayerElement.Play();
            mediaProgressTimer.Start();
        }
        else
        {
            MediaPlayerElement.Pause();
            mediaProgressTimer.Stop();
            SyncMediaPosition();
        }
    }

    private void OnMediaStopRequested()
    {
        mediaProgressTimer.Stop();
        MediaPlayerElement.Stop();
        MediaPlayerElement.Close();
        MediaPlayerElement.Source = null;
    }

    private void OnMediaSeekRequested(double seconds)
    {
        if (MediaPlayerElement.Source is null) return;
        MediaPlayerElement.Position = TimeSpan.FromSeconds(Math.Max(0, seconds));
    }

    private void SyncMediaPosition()
    {
        if (seekingMedia || MediaPlayerElement.Source is null) return;
        double pos = MediaPlayerElement.Position.TotalSeconds;
        double dur = MediaPlayerElement.NaturalDuration.HasTimeSpan
            ? MediaPlayerElement.NaturalDuration.TimeSpan.TotalSeconds
            : model.MediaDurationSeconds;
        model.ReportMediaProgress(pos, dur);
    }

    private void MediaPlayer_Opened(object sender, RoutedEventArgs e) => SyncMediaPosition();
    private void MediaPlayer_Ended(object sender, RoutedEventArgs e)
    {
        mediaProgressTimer.Stop();
        model.ReportMediaEnded();
    }
    private void MediaPlayer_Failed(object sender, ExceptionRoutedEventArgs e)
    {
        mediaProgressTimer.Stop();
        model.ReportMediaFailed(e.ErrorException?.Message);
    }

    private void Media_Click(object sender, RoutedEventArgs e) => model.MediaOpen = true;
    private void MediaFilter_Checked(object sender, RoutedEventArgs e) { if (model is not null) model.MediaFilter = (string)((FrameworkElement)sender).Tag; }

    private async void AddMediaFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add music or video",
            CheckFileExists = true,
            Multiselect = true,
            Filter = MediaCatalog.OpenFileFilter
        };
        if (picker.ShowDialog(this) == true)
            await model.AddMediaPathsAsync(picker.FileNames, autoPlayFirst: true);
    }

    private async void AddMediaFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Add media folder" };
        if (picker.ShowDialog(this) == true)
            await model.AddMediaPathsAsync([picker.FolderName]);
    }

    private void ClearMediaAdded_Click(object sender, RoutedEventArgs e) => model.ClearAddedMedia();
    private void MediaPlayPause_Click(object sender, RoutedEventArgs e) => model.TogglePlayPause();
    private void MediaStop_Click(object sender, RoutedEventArgs e) => model.StopMedia();
    private void MediaPrev_Click(object sender, RoutedEventArgs e) => model.PreviousMedia();
    private void MediaNext_Click(object sender, RoutedEventArgs e) => model.NextMedia();
    private void MediaShuffle_Click(object sender, RoutedEventArgs e) => model.ToggleMediaShuffle();
    private void MediaRepeat_Click(object sender, RoutedEventArgs e) => model.CycleMediaRepeat();
    private void MediaSpeed_Click(object sender, RoutedEventArgs e) => model.CycleMediaSpeed();
    private void MediaMute_Click(object sender, RoutedEventArgs e) => model.MediaMuted = !model.MediaMuted;
    private void MediaVideoStage_Click(object sender, MouseButtonEventArgs e) { if (model.HasCurrentMedia) model.TogglePlayPause(); }

    private void MediaSeek_PreviewMouseDown(object sender, MouseButtonEventArgs e) => seekingMedia = true;
    private void MediaSeek_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!seekingMedia) return;
        seekingMedia = false;
        model.SeekMedia(MediaSeekSlider.Value);
    }
    private void MediaSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!seekingMedia || model is null) return;
        model.ReportMediaProgress(e.NewValue, model.MediaDurationSeconds);
    }

    private void MediaItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item }) model.PlayMedia(item);
    }

    private void MediaPlayItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item }) model.PlayMedia(item);
    }

    private void MediaRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item }) model.RemoveMediaItem(item);
    }

    private void MediaRevealItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item } && File.Exists(item.Path))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
    }

    private void MediaSendToChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MediaItem item } || !File.Exists(item.Path)) return;
        model.MediaOpen = false;
        if (!model.CanAttach)
        {
            model.Toast = "Select an online contact first";
            return;
        }
        if (model.AddAttachment(item.Path) is { } problem) model.Toast = problem;
        else Composer.Focus();
    }

    private void PlayFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileTransfer transfer }) return;
        if (transfer.Path.Length == 0 || !File.Exists(transfer.Path)) { model.Toast = "Attachment not found"; return; }
        model.PlayPath(transfer.Path, string.IsNullOrWhiteSpace(transfer.PeerName) ? "LAN" : $"LAN · {transfer.PeerName}", isFromLan: true);
    }
}
