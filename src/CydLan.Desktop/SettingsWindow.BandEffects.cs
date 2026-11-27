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
    private readonly Dictionary<string, CheckBox> ambientFxCards = [];
    private readonly Dictionary<string, CheckBox> railFxCards = [];
    private readonly Dictionary<string, CheckBox> sidebarFxCards = [];

    /// <summary>Adds one selectable preview card per effect; several can be on at once.</summary>
    private void BuildEffectCards(WrapPanel panel, Dictionary<string, CheckBox> cards, IReadOnlyList<StyleOption> options,
        Slider intensity, FrameworkElement intensityPanel, string zone, Func<string, AmbientEffect> preview, bool narrow = false)
    {
        if (cards.Count > 0) return;
        foreach (var option in options)
        {
            var effect = preview(option.Value);
            BindingOperations.SetBinding(effect, AmbientEffect.IntensityProperty, new Binding(nameof(Slider.Value)) { Source = intensity });
            FrameworkElement content = narrow
                ? new Grid
                {
                    Background = (System.Windows.Media.Brush)FindResource("Canvas"),
                    Children = { new Border { Width = 48, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Background = (System.Windows.Media.Brush)FindResource("Sidebar"), Child = effect } }
                }
                : new Border { Background = (System.Windows.Media.Brush)FindResource(zone is "Title" or "Header" ? "Sidebar" : "Canvas"), Child = effect };
            string name = zone.Length > 0 ? $"{zone} {option.Name.ToLowerInvariant()}" : option.Name;
            var card = new CheckBox { Style = (Style)FindResource("EffectCard"), Tag = name, Content = content };
            System.Windows.Automation.AutomationProperties.SetName(card, name);
            card.Checked += (_, _) => SyncBandIntensity(cards, intensityPanel);
            card.Unchecked += (_, _) => SyncBandIntensity(cards, intensityPanel);
            panel.Children.Add(card);
            cards[option.Value] = card;
        }
    }

    private void BuildBandEffectCards(WrapPanel panel, Dictionary<string, CheckBox> cards, Slider intensity, FrameworkElement intensityPanel, string zone) =>
        BuildEffectCards(panel, cards, BandEffects.All, intensity, intensityPanel, zone, kind => new FxBand { Kind = kind });

    private static void SyncBandIntensity(Dictionary<string, CheckBox> cards, FrameworkElement panel) =>
        panel.Visibility = cards.Values.Any(c => c.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

    private static void LoadCards(Dictionary<string, CheckBox> cards, IReadOnlyCollection<string> selected, Slider intensity, double value, FrameworkElement panel)
    {
        foreach (var (key, card) in cards) card.IsChecked = selected.Contains(key);
        intensity.Value = double.IsFinite(value) ? Math.Clamp(value, .1, 1) : .65;
        SyncBandIntensity(cards, panel);
    }

    private static List<string> Selected(Dictionary<string, CheckBox> cards) =>
        cards.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToList();

    private void LoadBandEffects(AppSettings s)
    {
        BuildBandEffectCards(TitleEffectCards, titleFxCards, TitleFxIntensityBox, TitleFxIntensityPanel, "Title");
        BuildBandEffectCards(HeaderEffectCards, headerFxCards, HeaderFxIntensityBox, HeaderFxIntensityPanel, "Header");
        BuildEffectCards(AmbientFxCards, ambientFxCards, AmbientFxCatalog.All, AmbientFxIntensityBox, AmbientFxIntensityPanel, "", kind => new AmbientFxLayer { Kind = kind });
        BuildEffectCards(RailFxCards, railFxCards, PanelFxCatalog.All, RailFxIntensityBox, RailFxIntensityPanel, "Side nav", kind => new PanelFxLayer { Kind = kind }, narrow: true);
        BuildEffectCards(SidebarFxCards, sidebarFxCards, PanelFxCatalog.All, SidebarFxIntensityBox, SidebarFxIntensityPanel, "Side panel", kind => new PanelFxLayer { Kind = kind });
        LoadCards(titleFxCards, BandEffects.Normalize(s.TitleFx), TitleFxIntensityBox, s.TitleFxIntensity, TitleFxIntensityPanel);
        LoadCards(headerFxCards, BandEffects.Normalize(s.HeaderFx), HeaderFxIntensityBox, s.HeaderFxIntensity, HeaderFxIntensityPanel);
        LoadCards(ambientFxCards, AmbientFxCatalog.Normalize(s.AmbientFx), AmbientFxIntensityBox, s.AmbientFxIntensity, AmbientFxIntensityPanel);
        LoadCards(railFxCards, PanelFxCatalog.Normalize(s.RailFx), RailFxIntensityBox, s.RailFxIntensity, RailFxIntensityPanel);
        LoadCards(sidebarFxCards, PanelFxCatalog.Normalize(s.SidebarFx), SidebarFxIntensityBox, s.SidebarFxIntensity, SidebarFxIntensityPanel);
    }

    private void WriteBandEffects(AppSettings s)
    {
        s.TitleFx = Selected(titleFxCards);
        s.HeaderFx = Selected(headerFxCards);
        s.AmbientFx = Selected(ambientFxCards);
        s.RailFx = Selected(railFxCards);
        s.SidebarFx = Selected(sidebarFxCards);
        s.TitleFxIntensity = TitleFxIntensityBox.Value;
        s.HeaderFxIntensity = HeaderFxIntensityBox.Value;
        s.AmbientFxIntensity = AmbientFxIntensityBox.Value;
        s.RailFxIntensity = RailFxIntensityBox.Value;
        s.SidebarFxIntensity = SidebarFxIntensityBox.Value;
    }
}
