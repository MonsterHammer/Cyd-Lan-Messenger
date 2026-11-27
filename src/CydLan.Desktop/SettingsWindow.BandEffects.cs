using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CydLan.Core;
using CheckBox = System.Windows.Controls.CheckBox;
using Binding = System.Windows.Data.Binding;

namespace CydLan.Desktop;

public partial class SettingsWindow
{
    private readonly Dictionary<string, CheckBox> titleFxCards = [];
    private readonly Dictionary<string, CheckBox> headerFxCards = [];

    /// <summary>Adds a card per extra effect to the title bar and chat header galleries.</summary>
    private void BuildBandEffectCards(WrapPanel panel, Dictionary<string, CheckBox> cards, Slider intensity, FrameworkElement intensityPanel, string zone)
    {
        if (cards.Count > 0) return;
        foreach (var option in BandEffects.All)
        {
            var preview = new FxBand { Kind = option.Value };
            BindingOperations.SetBinding(preview, AmbientEffect.IntensityProperty, new Binding(nameof(Slider.Value)) { Source = intensity });
            var card = new CheckBox
            {
                Style = (Style)FindResource("EffectCard"),
                Tag = $"{zone} {option.Name.ToLowerInvariant()}",
                Content = new Border { Background = (System.Windows.Media.Brush)FindResource("Sidebar"), Child = preview }
            };
            System.Windows.Automation.AutomationProperties.SetName(card, $"{zone} {option.Name.ToLowerInvariant()}");
            card.Checked += (_, _) => SyncBandIntensity(cards, intensityPanel);
            card.Unchecked += (_, _) => SyncBandIntensity(cards, intensityPanel);
            panel.Children.Add(card);
            cards[option.Value] = card;
        }
    }

    private static void SyncBandIntensity(Dictionary<string, CheckBox> cards, FrameworkElement panel) =>
        panel.Visibility = cards.Values.Any(c => c.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

    private void LoadBandEffects(AppSettings s)
    {
        BuildBandEffectCards(TitleEffectCards, titleFxCards, TitleFxIntensityBox, TitleFxIntensityPanel, "Title");
        BuildBandEffectCards(HeaderEffectCards, headerFxCards, HeaderFxIntensityBox, HeaderFxIntensityPanel, "Header");
        var title = BandEffects.Normalize(s.TitleFx);
        var header = BandEffects.Normalize(s.HeaderFx);
        foreach (var (value, card) in titleFxCards) card.IsChecked = title.Contains(value);
        foreach (var (value, card) in headerFxCards) card.IsChecked = header.Contains(value);
        TitleFxIntensityBox.Value = double.IsFinite(s.TitleFxIntensity) ? Math.Clamp(s.TitleFxIntensity, .1, 1) : .65;
        HeaderFxIntensityBox.Value = double.IsFinite(s.HeaderFxIntensity) ? Math.Clamp(s.HeaderFxIntensity, .1, 1) : .65;
        SyncBandIntensity(titleFxCards, TitleFxIntensityPanel);
        SyncBandIntensity(headerFxCards, HeaderFxIntensityPanel);
    }

    private void WriteBandEffects(AppSettings s)
    {
        s.TitleFx = titleFxCards.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToList();
        s.HeaderFx = headerFxCards.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToList();
        s.TitleFxIntensity = TitleFxIntensityBox.Value;
        s.HeaderFxIntensity = HeaderFxIntensityBox.Value;
    }
}
