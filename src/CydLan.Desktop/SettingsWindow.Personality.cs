using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CydLan.Core;

namespace CydLan.Desktop;

public partial class SettingsWindow
{
    private readonly DispatcherTimer personalityMoodTimer = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private bool loadingTraits;

    private static readonly IReadOnlyList<StyleOption> PersonalityPresetOptions =
        PetTraits.Presets.Select(p => new StyleOption(string.Join(',', p.Traits), p.Name)).ToList();

    private void InitPersonalityControls(AppSettings s)
    {
        PetTraitsBox.ItemsSource = PetTraits.All;
        PetTraitPresetBox.ItemsSource = PersonalityPresetOptions;
        personalityMoodTimer.Tick += (_, _) => { personalityMoodTimer.Stop(); PetStudioPreview.SetMood(PetMood.Neutral); };
        LoadPersonality(s.PetTraits);
    }

    private void LoadPersonality(IEnumerable<string> traits)
    {
        loadingTraits = true;
        try
        {
            var set = PetTraits.Normalize(traits).ToHashSet();
            PetTraitsBox.SelectedItems.Clear();
            foreach (var option in PetTraits.All.Where(o => set.Contains(o.Value))) PetTraitsBox.SelectedItems.Add(option);
            PetTraitPresetBox.SelectedItem = null;
            UpdatePersonalitySummary();
        }
        finally { loadingTraits = false; }
    }

    private List<string> SelectedTraits() => PetTraits.Normalize(PetTraitsBox.SelectedItems.OfType<StyleOption>().Select(o => o.Value));

    private void PetTraits_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || loadingTraits) return;
        model.PreviewPetTraits(SelectedTraits());
        UpdatePersonalitySummary();
        if (e.AddedItems.OfType<StyleOption>().LastOrDefault() is { } added) PreviewTraitMood(added.Value);
    }

    private void PetTraitPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || loadingTraits || PetTraitPresetBox.SelectedItem is not StyleOption preset) return;
        LoadPersonality(preset.Value.Split(','));
        model.PreviewPetTraits(SelectedTraits());
        PreviewTraitMood(preset.Value.Split(',')[0]);
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { loadingTraits = true; PetTraitPresetBox.SelectedItem = null; loadingTraits = false; });
    }

    private void PetTraitsClear_Click(object sender, RoutedEventArgs e)
    {
        LoadPersonality([]);
        model.PreviewPetTraits([]);
    }

    private void UpdatePersonalitySummary()
    {
        var names = PetTraitsBox.SelectedItems.OfType<StyleOption>().Select(o => o.Name).ToList();
        PetTraitsSummary.Text = names.Count switch { 0 => "", 1 => names[0], _ => string.Join(" · ", names) };
        PetTraitsClearButton.Visibility = names.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Shows the studio companion making the face that goes with a trait.</summary>
    private void PreviewTraitMood(string trait)
    {
        string m = trait switch
        {
            PetTraits.Needy => PetMood.Sad,
            PetTraits.Sleepy => PetMood.Drowsy,
            PetTraits.Grumpy => PetMood.Angry,
            PetTraits.Cheerful => PetMood.Celebrate,
            PetTraits.Shy => PetMood.Shy,
            PetTraits.Clingy => PetMood.Love,
            PetTraits.Dramatic => PetMood.Fainted,
            PetTraits.Chatty => PetMood.Alert,
            PetTraits.Foodie => PetMood.Hungry,
            PetTraits.Curious => PetMood.Curious,
            PetTraits.Playful => PetMood.Dizzy,
            PetTraits.Bossy => PetMood.Angry,
            PetTraits.Sassy => PetMood.Smug,
            PetTraits.Zen => PetMood.Happy,
            _ => PetMood.Neutral
        };
        PetStudioPreview.SetMood(m);
        personalityMoodTimer.Stop();
        personalityMoodTimer.Start();
    }
}
