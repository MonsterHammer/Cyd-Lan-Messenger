using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CydLan.Desktop;

/// <summary>Interactive miniature CydLan window stage supporting drag-and-drop placement and repositioning of SceneEffectItems.</summary>
public sealed class SceneStageCanvas : AmbientEffect
{
    private const double TitleHeight = 20;
    private const double RailWidth = 24;
    private const double SidebarWidth = 92;
    private const double HeaderHeight = 26;

    private IReadOnlyList<SceneEffectItem> items = [];
    private string? selectedId;
    private string? draggingId;
    private string? hoverDropZone;
    private Point hoverDropPoint;

    public event Action<string, string, double, double>? ItemDropped;
    public event Action<string?>? ItemSelected;
    public event Action<string, string, double, double>? ItemMoved;

    public SceneStageCanvas()
    {
        IsHitTestVisible = true;
        AllowDrop = true;
        ClipToBounds = true;
        Cursor = Cursors.Arrow;

        DragOver += OnStageDragOver;
        DragLeave += (_, _) => { hoverDropZone = null; InvalidateVisual(); };
        Drop += OnStageDrop;
        MouseLeftButtonDown += OnStageMouseDown;
        MouseMove += OnStageMouseMove;
        MouseLeftButtonUp += OnStageMouseUp;
    }

    protected override int FrameRate => 24;
    protected override bool HasActiveContent => items.Count > 0 || hoverDropZone is not null;

    public void SetScene(IReadOnlyList<SceneEffectItem> sceneItems, string? selectedItemId)
    {
        items = sceneItems;
        selectedId = selectedItemId;
        UpdateAnimation();
        InvalidateVisual();
    }

    private void OnStageDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Copy;
        var p = e.GetPosition(this);
        var (zone, _, _) = HitTestZone(p);
        hoverDropZone = zone;
        hoverDropPoint = p;
        UpdateAnimation();
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnStageDrop(object sender, DragEventArgs e)
    {
        hoverDropZone = null;
        if (e.Data.GetData(DataFormats.StringFormat) is string kind && !string.IsNullOrWhiteSpace(kind))
        {
            var (zone, nx, ny) = HitTestZone(e.GetPosition(this));
            ItemDropped?.Invoke(kind, zone, nx, ny);
            e.Handled = true;
        }
        UpdateAnimation();
        InvalidateVisual();
    }

    private void OnStageMouseDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        string? hit = HitTestItem(p);
        selectedId = hit;
        ItemSelected?.Invoke(hit);
        if (hit is not null)
        {
            draggingId = hit;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
        }
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnStageMouseMove(object sender, MouseEventArgs e)
    {
        if (draggingId is null || e.LeftButton != MouseButtonState.Pressed) return;
        var (zone, nx, ny) = HitTestZone(e.GetPosition(this));
        ItemMoved?.Invoke(draggingId, zone, nx, ny);
    }

    private void OnStageMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (draggingId is null) return;
        draggingId = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private string? HitTestItem(Point p)
    {
        double bestDist = 24;
        string? bestId = null;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var rect = RectForZone(item.Zone);
            if (rect.Width < 2 || rect.Height < 2) continue;
            double ix = rect.Left + Math.Clamp(item.X, 0, 1) * rect.Width;
            double iy = rect.Top + Math.Clamp(item.Y, 0, 1) * rect.Height;
            double dx = p.X - ix, dy = p.Y - iy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= bestDist)
            {
                bestDist = dist;
                bestId = item.Id;
            }
        }
        return bestId;
    }

    public (string Zone, double X, double Y) HitTestZone(Point p)
    {
        string[] order =
        [
            SceneEffectCatalog.ZoneTitle,
            SceneEffectCatalog.ZoneRail,
            SceneEffectCatalog.ZoneSidebar,
            SceneEffectCatalog.ZoneHeader,
            SceneEffectCatalog.ZoneContent
        ];
        foreach (string zone in order)
        {
            var r = RectForZone(zone);
            if (r.Contains(p))
            {
                double nx = r.Width > 1 ? Math.Clamp((p.X - r.Left) / r.Width, 0, 1) : .5;
                double ny = r.Height > 1 ? Math.Clamp((p.Y - r.Top) / r.Height, 0, 1) : .5;
                return (zone, nx, ny);
            }
        }
        var content = RectForZone(SceneEffectCatalog.ZoneContent);
        double cx = content.Width > 1 ? Math.Clamp((p.X - content.Left) / content.Width, 0, 1) : .5;
        double cy = content.Height > 1 ? Math.Clamp((p.Y - content.Top) / content.Height, 0, 1) : .5;
        return (SceneEffectCatalog.ZoneContent, cx, cy);
    }

    private Rect RectForZone(string zone)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return Rect.Empty;
        double bodyTop = TitleHeight;
        double bodyH = Math.Max(0, h - TitleHeight);
        double contentLeft = RailWidth + SidebarWidth;
        double rightW = Math.Max(0, w - contentLeft);
        return SceneEffectCatalog.NormalizeZone(zone) switch
        {
            SceneEffectCatalog.ZoneTitle => new Rect(0, 0, w, TitleHeight),
            SceneEffectCatalog.ZoneRail => new Rect(0, bodyTop, RailWidth, bodyH),
            SceneEffectCatalog.ZoneSidebar => new Rect(RailWidth, bodyTop, SidebarWidth, bodyH),
            SceneEffectCatalog.ZoneHeader => new Rect(contentLeft, bodyTop, rightW, HeaderHeight),
            _ => new Rect(contentLeft, bodyTop + HeaderHeight, rightW, Math.Max(0, bodyH - HeaderHeight))
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        Brush ResBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

        var lineBrush = ResBrush("Line", Brushes.Gray);
        var linePen = new Pen(lineBrush, 1);
        linePen.Freeze();
        var accentBrush = ResBrush("Accent", Brushes.DeepSkyBlue);

        var titleRect = RectForZone(SceneEffectCatalog.ZoneTitle);
        var railRect = RectForZone(SceneEffectCatalog.ZoneRail);
        var sidebarRect = RectForZone(SceneEffectCatalog.ZoneSidebar);
        var headerRect = RectForZone(SceneEffectCatalog.ZoneHeader);
        var contentRect = RectForZone(SceneEffectCatalog.ZoneContent);

        dc.DrawRectangle(ResBrush("TitleBarPreviewBackground", Brushes.DimGray), null, titleRect);
        dc.DrawRectangle(ResBrush("RailPreviewBackground", Brushes.DimGray), null, railRect);
        dc.DrawRectangle(ResBrush("SidebarPreviewBackground", Brushes.DimGray), null, sidebarRect);
        dc.DrawRectangle(ResBrush("HeaderPreviewBackground", Brushes.DimGray), null, headerRect);
        dc.DrawRectangle(ResBrush("ChatPreviewBackground", Brushes.White), null, contentRect);
        if (TryFindResource("ChatColorFinishBrush") is Brush wash)
            dc.DrawRectangle(wash, null, contentRect);

        // Subtle mock UI anchors so users can orient placement accurately.
        dc.DrawRoundedRectangle(ResBrush("SidebarRowSelected", accentBrush), null, new Rect(sidebarRect.Left + 8, sidebarRect.Top + 10, sidebarRect.Width - 16, 14), 3, 3);
        dc.DrawRoundedRectangle(ResBrush("SidebarSurface", lineBrush), null, new Rect(sidebarRect.Left + 8, sidebarRect.Top + 29, sidebarRect.Width - 16, 12), 3, 3);
        dc.DrawEllipse(accentBrush, null, new Point(headerRect.Left + 14, headerRect.Top + headerRect.Height / 2), 5, 5);
        dc.DrawRoundedRectangle(ResBrush("IncomingBubbleBackground", lineBrush), null, new Rect(contentRect.Left + 14, contentRect.Top + 16, 86, 18), 7, 7);
        dc.DrawRoundedRectangle(ResBrush("OutgoingBubbleBackground", accentBrush), null, new Rect(Math.Max(contentRect.Left + 24, contentRect.Right - 98), contentRect.Top + 42, 84, 18), 7, 7);

        // Zone dividers
        dc.DrawLine(linePen, new Point(0, TitleHeight), new Point(w, TitleHeight));
        dc.DrawLine(linePen, new Point(RailWidth, TitleHeight), new Point(RailWidth, h));
        dc.DrawLine(linePen, new Point(RailWidth + SidebarWidth, TitleHeight), new Point(RailWidth + SidebarWidth, h));
        dc.DrawLine(linePen, new Point(headerRect.Left, headerRect.Bottom), new Point(w, headerRect.Bottom));

        // Render placed items clipped to their target zone.
        double t = Time * 2.4;
        foreach (string zone in new[] { SceneEffectCatalog.ZoneContent, SceneEffectCatalog.ZoneSidebar, SceneEffectCatalog.ZoneHeader, SceneEffectCatalog.ZoneRail, SceneEffectCatalog.ZoneTitle })
        {
            var zr = RectForZone(zone);
            if (zr.Width < 2 || zr.Height < 2) continue;
            Color zoneSurface = SceneEffectRenderer.ResolveZoneSurfaceColor(this, zone);
            dc.PushClip(new RectangleGeometry(zr));
            foreach (var item in items)
            {
                if (SceneEffectCatalog.NormalizeZone(item.Zone) != zone) continue;
                var pt = new Point(zr.Left + Math.Clamp(item.X, 0, 1) * zr.Width, zr.Top + Math.Clamp(item.Y, 0, 1) * zr.Height);
                SceneEffectRenderer.DrawItem(dc, item, pt, .42, t, Tint, zoneSurface);

                if (item.Id == selectedId)
                {
                    var ringPen = new Pen(accentBrush, 1.8) { DashStyle = DashStyles.Dash };
                    ringPen.Freeze();
                    dc.DrawEllipse(null, ringPen, pt, 14, 14);
                    dc.DrawEllipse(accentBrush, null, pt, 3, 3);
                }
            }
            dc.Pop();
        }

        if (hoverDropZone is not null)
        {
            var hr = RectForZone(hoverDropZone);
            var dropPen = new Pen(accentBrush, 2) { DashStyle = DashStyles.Dash };
            dropPen.Freeze();
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(24, 59, 130, 246)), dropPen, new Rect(hr.Left + 1, hr.Top + 1, Math.Max(0, hr.Width - 2), Math.Max(0, hr.Height - 2)));
            dc.DrawEllipse(accentBrush, null, hoverDropPoint, 4, 4);
        }
    }
}
