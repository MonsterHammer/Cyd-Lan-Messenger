using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Image = System.Windows.Controls.Image;

namespace CydLan.Desktop;

public sealed record GifFrame(BitmapSource Bitmap, int DelayMs);

public sealed class AnimatedGifSequence
{
    public IReadOnlyList<GifFrame> Frames { get; }
    public AnimatedGifSequence(IReadOnlyList<GifFrame> frames) => Frames = frames;

    public AnimatedGifSequence ToGrayscale() =>
        new(Frames.Select(f => new GifFrame(Images.ToGrayscale(f.Bitmap), f.DelayMs)).ToArray());

    public static AnimatedGifSequence? TryLoadFromFile(string path, bool grayscale = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 20 * 1024 * 1024) return null;
            return TryLoadFromBytes(File.ReadAllBytes(path), grayscale);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    public static AnimatedGifSequence? TryLoadFromBytes(byte[] bytes, bool grayscale = false)
    {
        if (!Images.HasGifSignature(bytes)) return null;
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var decoder = new GifBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count <= 1) return null;

            int logicalWidth = decoder.Frames[0].PixelWidth;
            int logicalHeight = decoder.Frames[0].PixelHeight;
            if (decoder.Metadata is BitmapMetadata rootMeta)
            {
                if (TryMetaInt(rootMeta, "/logscrdesc/Width", out int w) && w > 0) logicalWidth = w;
                if (TryMetaInt(rootMeta, "/logscrdesc/Height", out int h) && h > 0) logicalHeight = h;
            }
            if (logicalWidth <= 0 || logicalHeight <= 0 || logicalWidth > 4096 || logicalHeight > 4096) return null;

            double scale = Math.Min(1.0, 960.0 / Math.Max(logicalWidth, logicalHeight));
            int outWidth = Math.Max(1, (int)Math.Round(logicalWidth * scale));
            int outHeight = Math.Max(1, (int)Math.Round(logicalHeight * scale));

            var frames = new List<GifFrame>(Math.Min(decoder.Frames.Count, 180));
            BitmapSource? accumulated = null;
            BitmapSource? previous = null;

            int count = Math.Min(decoder.Frames.Count, 180);
            for (int i = 0; i < count; i++)
            {
                var frame = decoder.Frames[i];
                int left = 0, top = 0, delayMs = 100, disposal = 0;
                if (frame.Metadata is BitmapMetadata meta)
                {
                    TryMetaInt(meta, "/imgdesc/Left", out left);
                    TryMetaInt(meta, "/imgdesc/Top", out top);
                    if (TryMetaInt(meta, "/grctlext/Delay", out int centiseconds) && centiseconds > 0)
                        delayMs = Math.Max(20, centiseconds * 10);
                    TryMetaInt(meta, "/grctlext/Disposal", out disposal);
                }

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    if (accumulated is not null)
                        dc.DrawImage(accumulated, new Rect(0, 0, logicalWidth, logicalHeight));
                    dc.DrawImage(frame, new Rect(left, top, frame.PixelWidth, frame.PixelHeight));
                }

                var composed = new RenderTargetBitmap(logicalWidth, logicalHeight, 96, 96, PixelFormats.Pbgra32);
                composed.Render(visual);
                composed.Freeze();

                BitmapSource display = composed;
                if (scale < .999)
                {
                    var resized = new TransformedBitmap(display, new ScaleTransform(scale, scale));
                    resized.Freeze();
                    display = resized;
                }
                if (grayscale) display = Images.ToGrayscale(display);

                frames.Add(new GifFrame(display, delayMs));

                if (disposal == 2)
                {
                    var clearedVisual = new DrawingVisual();
                    var outerOnly = Geometry.Combine(
                        new RectangleGeometry(new Rect(0, 0, logicalWidth, logicalHeight)),
                        new RectangleGeometry(new Rect(left, top, frame.PixelWidth, frame.PixelHeight)),
                        GeometryCombineMode.Exclude,
                        null);
                    using (var cdc = clearedVisual.RenderOpen())
                    {
                        cdc.PushClip(outerOnly);
                        cdc.DrawImage(composed, new Rect(0, 0, logicalWidth, logicalHeight));
                        cdc.Pop();
                    }
                    var cleared = new RenderTargetBitmap(logicalWidth, logicalHeight, 96, 96, PixelFormats.Pbgra32);
                    cleared.Render(clearedVisual);
                    cleared.Freeze();
                    previous = composed;
                    accumulated = cleared;
                }
                else if (disposal == 3)
                {
                    accumulated = previous;
                }
                else
                {
                    previous = composed;
                    accumulated = composed;
                }
            }

            return frames.Count > 1 ? new AnimatedGifSequence(frames) : null;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or InvalidOperationException or FileFormatException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static bool TryMetaInt(BitmapMetadata meta, string query, out int value)
    {
        value = 0;
        try
        {
            if (meta.ContainsQuery(query) && meta.GetQuery(query) is { } raw)
            {
                value = Convert.ToInt32(raw);
                return true;
            }
        }
        catch { }
        return false;
    }
}

/// <summary>Attached GIF player for WPF Image controls that respects motion settings and unfocused animation state.</summary>
public static class AnimatedGifBehavior
{
    public static readonly DependencyProperty SequenceProperty = DependencyProperty.RegisterAttached(
        "Sequence", typeof(AnimatedGifSequence), typeof(AnimatedGifBehavior),
        new PropertyMetadata(null, OnSequenceChanged));

    public static readonly DependencyProperty FallbackSourceProperty = DependencyProperty.RegisterAttached(
        "FallbackSource", typeof(ImageSource), typeof(AnimatedGifBehavior),
        new PropertyMetadata(null, OnFallbackSourceChanged));

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller", typeof(GifPlayer), typeof(AnimatedGifBehavior), new PropertyMetadata(null));

    public static AnimatedGifSequence? GetSequence(DependencyObject obj) => (AnimatedGifSequence?)obj.GetValue(SequenceProperty);
    public static void SetSequence(DependencyObject obj, AnimatedGifSequence? value) => obj.SetValue(SequenceProperty, value);
    public static ImageSource? GetFallbackSource(DependencyObject obj) => (ImageSource?)obj.GetValue(FallbackSourceProperty);
    public static void SetFallbackSource(DependencyObject obj, ImageSource? value) => obj.SetValue(FallbackSourceProperty, value);

    private static void OnSequenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        EnsureController(image).Update(e.NewValue as AnimatedGifSequence, GetFallbackSource(image));
    }

    private static void OnFallbackSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        EnsureController(image).Update(GetSequence(image), e.NewValue as ImageSource);
    }

    private static GifPlayer EnsureController(Image image)
    {
        if (image.GetValue(ControllerProperty) is GifPlayer existing) return existing;
        var player = new GifPlayer(image);
        image.SetValue(ControllerProperty, player);
        return player;
    }

    private sealed class GifPlayer
    {
        private readonly Image image;
        private readonly DispatcherTimer timer = new(DispatcherPriority.Render);
        private AnimatedGifSequence? sequence;
        private ImageSource? fallback;
        private Window? host;
        private bool subscribed;
        private int frameIndex;

        public GifPlayer(Image image)
        {
            this.image = image;
            timer.Tick += OnTick;
            image.Loaded += (_, _) => Attach();
            image.Unloaded += (_, _) => Detach();
            image.IsVisibleChanged += (_, _) => SyncPlayback();
            if (image.IsLoaded) Attach();
        }

        public void Update(AnimatedGifSequence? next, ImageSource? fallbackSource)
        {
            fallback = fallbackSource;
            if (!ReferenceEquals(sequence, next))
            {
                sequence = next;
                frameIndex = 0;
            }
            if (sequence is null || sequence.Frames.Count <= 1)
            {
                timer.Stop();
                image.Source = fallback;
            }
            else
            {
                image.Source = sequence.Frames[Math.Clamp(frameIndex, 0, sequence.Frames.Count - 1)].Bitmap;
            }
            SyncPlayback();
        }

        private void Attach()
        {
            if (subscribed) { SyncPlayback(); return; }
            subscribed = true;
            host = Window.GetWindow(image);
            if (host is not null)
            {
                host.StateChanged += OnHostChanged;
                host.Activated += OnHostChanged;
                host.Deactivated += OnHostChanged;
                host.Closed += OnHostClosed;
            }
            SystemParameters.StaticPropertyChanged += OnSystemChanged;
            AmbientEffect.SpeedChanged += OnPolicyChanged;
            AmbientEffect.UnfocusedChanged += OnPolicyChanged;
            SyncPlayback();
        }

        private void Detach()
        {
            timer.Stop();
            if (!subscribed) return;
            subscribed = false;
            if (host is not null)
            {
                host.StateChanged -= OnHostChanged;
                host.Activated -= OnHostChanged;
                host.Deactivated -= OnHostChanged;
                host.Closed -= OnHostClosed;
            }
            host = null;
            SystemParameters.StaticPropertyChanged -= OnSystemChanged;
            AmbientEffect.SpeedChanged -= OnPolicyChanged;
            AmbientEffect.UnfocusedChanged -= OnPolicyChanged;
        }

        private void OnHostChanged(object? sender, EventArgs e) => SyncPlayback();
        private void OnHostClosed(object? sender, EventArgs e) => Detach();
        private void OnPolicyChanged() => SyncPlayback();
        private void OnSystemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
                image.Dispatcher.BeginInvoke(SyncPlayback);
        }

        private void SyncPlayback()
        {
            if (sequence is null || sequence.Frames.Count <= 1 || !image.IsVisible || !SystemParameters.ClientAreaAnimation ||
                host is not { WindowState: not WindowState.Minimized } ||
                (!host.IsActive && !AmbientEffect.AnimateWhenUnfocused))
            {
                timer.Stop();
                return;
            }
            ScheduleCurrentDelay();
            if (!timer.IsEnabled) timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (sequence is null || sequence.Frames.Count <= 1) { timer.Stop(); return; }
            frameIndex = (frameIndex + 1) % sequence.Frames.Count;
            image.Source = sequence.Frames[frameIndex].Bitmap;
            ScheduleCurrentDelay();
        }

        private void ScheduleCurrentDelay()
        {
            if (sequence is null || sequence.Frames.Count == 0) return;
            double speed = Math.Clamp(AmbientEffect.SpeedMultiplier, .4, 2.0);
            int delay = sequence.Frames[Math.Clamp(frameIndex, 0, sequence.Frames.Count - 1)].DelayMs;
            timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, Math.Round(delay / speed)));
        }
    }
}
