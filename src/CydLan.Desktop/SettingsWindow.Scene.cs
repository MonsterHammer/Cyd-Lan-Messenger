using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CydLan.Core;
using DragDropEffects = System.Windows.DragDropEffects;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public partial class SettingsWindow
{
    private List<SceneEffectItem> sceneEffects = [];
    private string? selectedSceneItemId;
    private string sceneDragKind = "";
    private Point sceneDragStart;
    private bool sceneDragStarted;
    private bool loadingSceneInspector;

    private void InitSceneControls()
    {
        ScenePaletteBox.ItemsSource = SceneEffectCatalog.Effects;
        SceneZoneBox.ItemsSource = SceneEffectCatalog.Zones;

        SceneStage.ItemDropped += (kind, zone, x, y) => AddSceneEffect(kind, zone, x, y);
        SceneStage.ItemSelected += id => SelectSceneItem(id);
        SceneStage.ItemMoved += (id, zone, x, y) => MoveSceneItem(id, zone, x, y);
    }

    private void DisposeSceneControls()
    {
        ScenePaletteBox.ItemsSource = null;
        SceneZoneBox.ItemsSource = null;
    }

    private void LoadSceneControls(AppSettings s)
    {
        sceneEffects = SceneEffectCatalog.NormalizeList(s.SceneEffects).Select(i => i.Clone()).ToList();
        selectedSceneItemId = sceneEffects.Count > 0 ? sceneEffects[^1].Id : null;
        RefreshSceneStageAndInspector();
    }

    private void WriteSceneTo(AppSettings s) =>
        s.SceneEffects = SceneEffectCatalog.NormalizeList(sceneEffects).Select(i => i.Clone()).ToList();

    private SceneEffectItem? SelectedSceneItem =>
        selectedSceneItemId is null ? null : sceneEffects.FirstOrDefault(i => i.Id == selectedSceneItemId);

    private void ApplyScenePreview()
    {
        if (!IsInitialized || closing) return;
        model.PreviewSceneEffects(sceneEffects);
        RefreshSceneStageAndInspector();
    }

    private void RefreshSceneStageAndInspector()
    {
        if (SceneStage is null) return;
        SceneStage.SetScene(sceneEffects, selectedSceneItemId);
        SceneCountText.Text = sceneEffects.Count == 1 ? "1 item" : $"{sceneEffects.Count} items";
        ClearSceneButton.IsEnabled = sceneEffects.Count > 0;

        var selected = SelectedSceneItem;
        SceneInspectorPanel.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        if (selected is null) return;

        loadingSceneInspector = true;
        var desc = SceneEffectCatalog.DescriptorFor(selected.Kind);
        SceneSelectedLabel.Text = desc.Name;
        SceneZoneBox.SelectedValue = SceneEffectCatalog.NormalizeZone(selected.Zone);
        SceneScaleBox.Value = Math.Clamp(selected.Scale, .4, 2.4);
        SceneOpacityBox.Value = Math.Clamp(selected.Opacity, .15, 1.0);
        SceneSpeedBox.Value = Math.Clamp(selected.Speed, .2, 2.5);
        SceneRotationBox.Value = Math.Clamp(selected.Rotation, -180, 180);
        SceneFlipBox.IsChecked = selected.FlipX;
        string normColor = AppearanceOptions.NormalizeHex(selected.ColorHex);
        SceneColorPicker.ColorHex = normColor;
        SceneColorHexBox.Text = normColor;
        loadingSceneInspector = false;
    }

    public SceneEffectItem AddSceneEffect(string kind, string? zone = null, double? x = null, double? y = null)
    {
        var desc = SceneEffectCatalog.DescriptorFor(kind);
        if (sceneEffects.Count >= SceneEffectCatalog.MaxItems)
            sceneEffects.RemoveAt(0);

        double stagger = (sceneEffects.Count % 7 - 3) * 0.08;
        var item = SceneEffectCatalog.NormalizeItem(new SceneEffectItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Kind = desc.Kind,
            Zone = zone ?? SceneEffectCatalog.ZoneContent,
            X = x ?? Math.Clamp(desc.DefaultX + stagger, .06, .94),
            Y = y ?? desc.DefaultY,
            Scale = 1.0,
            Opacity = .90,
            Speed = 1.0,
            Rotation = 0,
            FlipX = false,
            ColorHex = "",
            Seed = Random.Shared.NextDouble() * 100.0
        });
        sceneEffects.Add(item);
        selectedSceneItemId = item.Id;
        ApplyScenePreview();
        return item;
    }

    private void SelectSceneItem(string? id)
    {
        selectedSceneItemId = id;
        RefreshSceneStageAndInspector();
    }

    private void MoveSceneItem(string id, string zone, double x, double y)
    {
        var item = sceneEffects.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        item.Zone = SceneEffectCatalog.NormalizeZone(zone);
        item.X = Math.Clamp(x, 0, 1);
        item.Y = Math.Clamp(y, 0, 1);
        selectedSceneItemId = id;
        ApplyScenePreview();
    }

    private void SceneCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string kind }) return;
        sceneDragKind = kind;
        sceneDragStart = e.GetPosition(this);
        sceneDragStarted = false;
    }

    private void SceneCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (string.IsNullOrEmpty(sceneDragKind) || e.LeftButton != MouseButtonState.Pressed || sceneDragStarted) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - sceneDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - sceneDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        sceneDragStarted = true;
        string kind = sceneDragKind;
        sceneDragKind = "";
        if (sender is DependencyObject source)
            DragDrop.DoDragDrop(source, kind, DragDropEffects.Copy);
    }

    private void SceneCard_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!sceneDragStarted && !string.IsNullOrEmpty(sceneDragKind) && sender is FrameworkElement { Tag: string kind })
        {
            string zone = SelectedSceneItem?.Zone ?? SceneEffectCatalog.ZoneContent;
            AddSceneEffect(kind, zone);
        }
        sceneDragKind = "";
        sceneDragStarted = false;
    }

    private void SceneZone_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loadingSceneInspector || SelectedSceneItem is not { } item || SceneZoneBox.SelectedValue is not string zone) return;
        item.Zone = SceneEffectCatalog.NormalizeZone(zone);
        ApplyScenePreview();
    }

    private void SceneProperty_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loadingSceneInspector || SelectedSceneItem is not { } item) return;
        item.Scale = SceneScaleBox.Value;
        item.Opacity = SceneOpacityBox.Value;
        item.Speed = SceneSpeedBox.Value;
        item.Rotation = SceneRotationBox.Value;
        ApplyScenePreview();
    }

    private void SceneFlip_Changed(object sender, RoutedEventArgs e)
    {
        if (loadingSceneInspector || SelectedSceneItem is not { } item) return;
        item.FlipX = SceneFlipBox.IsChecked == true;
        ApplyScenePreview();
    }

    private void SceneColorPicker_Changed(object? sender, string value)
    {
        if (loadingSceneInspector || SelectedSceneItem is not { } item) return;
        item.ColorHex = AppearanceOptions.NormalizeHex(value);
        loadingSceneInspector = true;
        SceneColorHexBox.Text = item.ColorHex;
        loadingSceneInspector = false;
        ApplyScenePreview();
    }

    private void SceneColorHex_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitSceneColorHex();
        e.Handled = true;
    }

    private void SceneColorHex_Committed(object sender, KeyboardFocusChangedEventArgs e) => CommitSceneColorHex();

    private void CommitSceneColorHex()
    {
        if (loadingSceneInspector || SelectedSceneItem is not { } item) return;
        string raw = SceneColorHexBox.Text.Trim();
        string normalized;
        if (raw.Length == 0) normalized = "";
        else if (AppearanceOptions.TryParseHex(raw, out _)) normalized = AppearanceOptions.NormalizeHex(raw);
        else
        {
            loadingSceneInspector = true;
            SceneColorHexBox.Text = item.ColorHex;
            loadingSceneInspector = false;
            return;
        }
        item.ColorHex = normalized;
        loadingSceneInspector = true;
        SceneColorHexBox.Text = normalized;
        SceneColorPicker.ColorHex = normalized;
        loadingSceneInspector = false;
        ApplyScenePreview();
    }

    private void DuplicateSceneItem_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSceneItem is not { } item) return;
        AddSceneEffect(item.Kind, item.Zone, Math.Clamp(item.X + .07, .04, .96), item.Y);
        if (SelectedSceneItem is { } copy)
        {
            copy.Scale = item.Scale;
            copy.Opacity = item.Opacity;
            copy.Speed = item.Speed;
            copy.Rotation = item.Rotation;
            copy.FlipX = item.FlipX;
            copy.ColorHex = item.ColorHex;
            ApplyScenePreview();
        }
    }

    private void DeleteSceneItem_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSceneItem is not { } item) return;
        sceneEffects.RemoveAll(i => i.Id == item.Id);
        selectedSceneItemId = sceneEffects.Count > 0 ? sceneEffects[^1].Id : null;
        ApplyScenePreview();
    }

    private void ClearScene_Click(object sender, RoutedEventArgs e)
    {
        if (sceneEffects.Count == 0) return;
        sceneEffects.Clear();
        selectedSceneItemId = null;
        ApplyScenePreview();
    }
}
