using System.Windows;
using CydLan.Core;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Transparent overlay laid exactly over the main window for placing scene effects on the real layout.</summary>
internal sealed class SceneScreenEditor : Window
{
    private static readonly string[] ZonePriority =
    [
        SceneEffectCatalog.ZoneTitle, SceneEffectCatalog.ZoneHeader, SceneEffectCatalog.ZoneRail,
        SceneEffectCatalog.ZoneSidebar, SceneEffectCatalog.ZoneContent
    ];

    private readonly MainWindow host;
    private readonly SettingsWindow settings;
    private readonly SceneScreenOverlay overlay;
    private readonly Border palette;
    private string? draggingId;
    private Point? cardPressPoint;
    private string? cardPressKind;

    public SceneScreenEditor(MainWindow host, SettingsWindow settings)
    {
        this.host = host;
        this.settings = settings;
        Owner = host;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        Title = "Edit scene";

        overlay = new SceneScreenOverlay(this) { Focusable = true };
        overlay.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
        overlay.MouseMove += Overlay_MouseMove;
        overlay.MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
        overlay.MouseRightButtonUp += Overlay_MouseRightButtonUp;

        var hint = new Border
        {
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Right-click an effect to change its settings", FontSize = 11.5, FontWeight = FontWeights.SemiBold }
        };
        hint.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
        ((TextBlock)hint.Child).SetResourceReference(TextBlock.ForegroundProperty, "Accent");

        var done = new Button { Content = "Done", Padding = new Thickness(14, 5, 14, 5), VerticalAlignment = VerticalAlignment.Center };
        done.SetResourceReference(StyleProperty, "Primary");
        System.Windows.Automation.AutomationProperties.SetName(done, "Done editing scene");
        done.Click += (_, _) => Close();

        var effectsToggle = new Button
        {
            Content = "Effects",
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        effectsToggle.SetResourceReference(StyleProperty, "Secondary");
        System.Windows.Automation.AutomationProperties.SetName(effectsToggle, "Effects");

        palette = BuildPalette();
        effectsToggle.Click += (_, _) =>
            palette.Visibility = palette.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

        overlay.AllowDrop = true;
        overlay.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) && ZoneAt(e.GetPosition(overlay)) is not null
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        overlay.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.StringFormat) is string kind && ZoneAt(e.GetPosition(overlay)) is { } hit)
                settings.AddSceneEffect(kind, hit.Zone, hit.X, hit.Y);
            e.Handled = true;
        };

        var bar = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 7, 7, 7),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 44, 0, 0),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = .3 },
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { effectsToggle, hint, done } }
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Surface");
        bar.SetResourceReference(Border.BorderBrushProperty, "Line");

        Content = new Grid
        {
            Children =
            {
                overlay,
                new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Children = { bar, palette }
                }
            }
        };

        settings.SceneChanged += OnSceneChanged;
        host.SizeChanged += OnHostMoved;
        host.LocationChanged += OnHostMoved;
        host.StateChanged += OnHostMoved;
        Loaded += (_, _) => { MatchHost(); overlay.Focus(); };
        Closed += (_, _) =>
        {
            settings.SceneChanged -= OnSceneChanged;
            host.SizeChanged -= OnHostMoved;
            host.LocationChanged -= OnHostMoved;
            host.StateChanged -= OnHostMoved;
        };
        PreviewKeyDown += OnPreviewKeyDown;
        MatchHost();
    }

    internal IReadOnlyList<SceneEffectItem> Items => settings.SceneItems;
    internal string? SelectedId => settings.SelectedSceneId;

    private void OnSceneChanged() => overlay.InvalidateVisual();
    private void OnHostMoved(object? sender, EventArgs e) => Dispatcher.BeginInvoke(MatchHost);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (settings.IsSceneInspectorOpen) settings.CloseSceneInspector();
        else Close();
        e.Handled = true;
    }

    /// <summary>Covers the main window's client area exactly, so overlay coordinates equal main window coordinates.</summary>
    private void MatchHost()
    {
        if (PresentationSource.FromVisual(host) is not { CompositionTarget: { } target }) return;
        var topLeft = target.TransformFromDevice.Transform(host.PointToScreen(new Point(0, 0)));
        Left = topLeft.X;
        Top = topLeft.Y;
        Width = Math.Max(1, host.ActualWidth);
        Height = Math.Max(1, host.ActualHeight);
        overlay.InvalidateVisual();
    }

    /// <summary>Visible zone rectangles from the main window's live scene layers.</summary>
    internal Dictionary<string, Rect> ZoneRects()
    {
        var rects = new Dictionary<string, Rect>();
        foreach (var layer in FindLayers(host))
        {
            if (!layer.IsVisible || layer.ActualWidth < 4 || layer.ActualHeight < 4) continue;
            string zone = SceneEffectCatalog.NormalizeZone(layer.Zone);
            try
            {
                rects[zone] = layer.TransformToAncestor(host).TransformBounds(new Rect(0, 0, layer.ActualWidth, layer.ActualHeight));
            }
            catch (InvalidOperationException) { }
        }
        return rects;
    }

    private static IEnumerable<SceneEffectsLayer> FindLayers(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is SceneEffectsLayer layer) yield return layer;
            foreach (var nested in FindLayers(child)) yield return nested;
        }
    }

    internal (string Zone, double X, double Y)? ZoneAt(Point p)
    {
        var rects = ZoneRects();
        foreach (string zone in ZonePriority)
        {
            if (rects.TryGetValue(zone, out var r) && r.Contains(p))
                return (zone, Math.Clamp((p.X - r.Left) / r.Width, 0, 1), Math.Clamp((p.Y - r.Top) / r.Height, 0, 1));
        }
        return null;
    }

    internal Point? ItemCenter(SceneEffectItem item, Dictionary<string, Rect> rects) =>
        rects.TryGetValue(SceneEffectCatalog.NormalizeZone(item.Zone), out var r)
            ? new Point(r.Left + Math.Clamp(item.X, 0, 1) * r.Width, r.Top + Math.Clamp(item.Y, 0, 1) * r.Height)
            : null;

    internal static double HandleRadius(SceneEffectItem item) => Math.Clamp(22 * item.Scale, 14, 90);

    internal string? HitTestItem(Point p)
    {
        var rects = ZoneRects();
        string? best = null;
        double bestDist = double.MaxValue;
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (ItemCenter(item, rects) is not { } c) continue;
            double d = (p - c).Length;
            if (d <= HandleRadius(item) && d < bestDist) { best = item.Id; bestDist = d; }
        }
        return best;
    }

    /// <summary>Adds an effect at the centre of the chat area (or the first visible zone).</summary>
    internal SceneEffectItem AddEffect(string kind)
    {
        var desc = SceneEffectCatalog.DescriptorFor(kind);
        var rects = ZoneRects();
        string zone = rects.ContainsKey(SceneEffectCatalog.ZoneContent) ? SceneEffectCatalog.ZoneContent : rects.Keys.FirstOrDefault() ?? SceneEffectCatalog.ZoneContent;
        return settings.AddSceneEffect(desc.Kind, zone, desc.DefaultX, desc.DefaultY);
    }

    internal void MoveItemTo(string id, Point p)
    {
        if (ZoneAt(p) is { } hit) settings.MoveSceneItemTo(id, hit.Zone, hit.X, hit.Y);
    }

    internal void OpenSettingsFor(string id)
    {
        settings.SelectSceneItemById(id);
        settings.OpenSceneInspector(overlay);
    }

    private Border BuildPalette()
    {
        var cards = new WrapPanel { Margin = new Thickness(8, 8, 3, 3) };
        foreach (var desc in SceneEffectCatalog.Effects)
            cards.Children.Add(BuildCard(desc));
        var panel = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Width = 5 * 99 + 30,
            MaxHeight = 236,
            Margin = new Thickness(0, 6, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = .3 },
            Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = cards }
        };
        panel.SetResourceReference(Border.BackgroundProperty, "Surface");
        panel.SetResourceReference(Border.BorderBrushProperty, "Line");
        System.Windows.Automation.AutomationProperties.SetName(panel, "Scene effects palette");
        return panel;
    }

    private Border BuildCard(SceneEffectDescriptor desc)
    {
        var label = new TextBlock { Text = desc.Name, FontSize = 10, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        var labelBar = new Border { Padding = new Thickness(6, 0, 6, 0), BorderThickness = new Thickness(0, 1, 0, 0), Child = label };
        labelBar.SetResourceReference(Border.BackgroundProperty, "Sidebar");
        labelBar.SetResourceReference(Border.BorderBrushProperty, "Line");
        Grid.SetRow(labelBar, 1);
        var grid = new Grid { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = new GridLength(19) } } };
        grid.Children.Add(new SceneEffectPreview { Kind = desc.Kind });
        grid.Children.Add(labelBar);
        var card = new Border
        {
            Width = 94,
            Height = 50,
            Margin = new Thickness(0, 0, 5, 5),
            BorderThickness = new Thickness(1.5),
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            Tag = desc.Kind,
            Child = grid
        };
        card.SetResourceReference(Border.CornerRadiusProperty, "ControlCorner");
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        card.SetResourceReference(Border.BackgroundProperty, "Sidebar");
        Rounded.SetRadius(card, 8);
        System.Windows.Automation.AutomationProperties.SetName(card, desc.Name);
        card.PreviewMouseLeftButtonDown += (_, e) => { cardPressPoint = e.GetPosition(this); cardPressKind = desc.Kind; e.Handled = true; };
        card.PreviewMouseMove += (_, e) =>
        {
            if (cardPressPoint is not { } start || cardPressKind is null || e.LeftButton != MouseButtonState.Pressed) return;
            if ((e.GetPosition(this) - start).Length < 5) return;
            string kind = cardPressKind;
            cardPressPoint = null;
            cardPressKind = null;
            DragDrop.DoDragDrop(card, new DataObject(DataFormats.StringFormat, kind), DragDropEffects.Copy);
        };
        card.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (cardPressKind is { } kind) AddEffect(kind);
            cardPressPoint = null;
            cardPressKind = null;
            e.Handled = true;
        };
        card.MouseEnter += (_, _) => card.SetResourceReference(Border.BorderBrushProperty, "Accent");
        card.MouseLeave += (_, _) => card.SetResourceReference(Border.BorderBrushProperty, "Line");
        return card;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        settings.CloseSceneInspector();
        var p = e.GetPosition(overlay);
        string? hit = HitTestItem(p);
        settings.SelectSceneItemById(hit);
        if (hit is not null)
        {
            draggingId = hit;
            overlay.CaptureMouse();
            overlay.Cursor = Cursors.SizeAll;
        }
        overlay.Focus();
        e.Handled = true;
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(overlay);
        if (draggingId is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            MoveItemTo(draggingId, p);
            return;
        }
        overlay.Cursor = HitTestItem(p) is null ? Cursors.Arrow : Cursors.Hand;
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (draggingId is null) return;
        draggingId = null;
        overlay.ReleaseMouseCapture();
        overlay.Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void Overlay_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HitTestItem(e.GetPosition(overlay)) is not { } hit) return;
        OpenSettingsFor(hit);
        e.Handled = true;
    }
}

/// <summary>Draws zone outlines and selection handles for each placed effect.</summary>
internal sealed class SceneScreenOverlay : FrameworkElement
{
    private readonly SceneScreenEditor editor;

    public SceneScreenOverlay(SceneScreenEditor editor) => this.editor = editor;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), null, new Rect(RenderSize));
        Brush accent = TryFindResource("Accent") as Brush ?? Brushes.DeepSkyBlue;
        Color accentColor = accent is SolidColorBrush s ? s.Color : Colors.DeepSkyBlue;
        var rects = editor.ZoneRects();

        var zonePen = new Pen(new SolidColorBrush(Color.FromArgb(70, accentColor.R, accentColor.G, accentColor.B)), 1) { DashStyle = DashStyles.Dash };
        foreach (var r in rects.Values)
            dc.DrawRectangle(null, zonePen, new Rect(r.Left + .5, r.Top + .5, Math.Max(0, r.Width - 1), Math.Max(0, r.Height - 1)));

        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb(150, accentColor.R, accentColor.G, accentColor.B)), 1.2) { DashStyle = DashStyles.Dash };
        var selectedPen = new Pen(accent, 2);
        var dot = new SolidColorBrush(accentColor);
        foreach (var item in editor.Items)
        {
            if (editor.ItemCenter(item, rects) is not { } c) continue;
            bool selected = item.Id == editor.SelectedId;
            double r = SceneScreenEditor.HandleRadius(item);
            dc.DrawEllipse(selected ? new SolidColorBrush(Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B)) : null,
                selected ? selectedPen : handlePen, c, r, r);
            dc.DrawEllipse(dot, null, c, selected ? 3.2 : 2.2, selected ? 3.2 : 2.2);
        }
    }
}
