using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CydLan.Core;

namespace CydLan.Desktop;

public partial class SettingsWindow
{
    private readonly DispatcherTimer petStudioTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private List<SavedCompanion> savedCompanions = [];
    private string petBodyHex = "", petAccentHex = "", petDetailHex = "";
    private double studioBreath, studioBlink, studioBlinkElapsed, studioNextBlink = 2.6, studioHop;
    private bool studioBlinkDown, studioHovered, studioExcited;
    private DateTime studioExcitedUntil = DateTime.MinValue;

    private void InitCompanionControls(AppSettings s)
    {
        savedCompanions = (s.SavedCompanions ?? []).Select(p => new SavedCompanion { Id = p.Id, Name = p.Name, Code = p.Code }).ToList();
        PetPresetBox.ItemsSource = FloatingPetStyle.Presets;
        PetBodyBox.ItemsSource = FloatingPetStyle.Bodies;
        PetPatternBox.ItemsSource = FloatingPetStyle.Patterns;
        PetEarsBox.ItemsSource = FloatingPetStyle.Ears;
        PetFeetBox.ItemsSource = FloatingPetStyle.Feet;
        PetEyesBox.ItemsSource = FloatingPetStyle.Eyes;
        PetBrowsBox.ItemsSource = FloatingPetStyle.Brows;
        PetMouthBox.ItemsSource = FloatingPetStyle.Mouths;
        PetCheeksBox.ItemsSource = FloatingPetStyle.Cheeks;
        PetGlassesBox.ItemsSource = FloatingPetStyle.Glasses;
        PetHatBox.ItemsSource = FloatingPetStyle.Hats;
        PetOutfitBox.ItemsSource = FloatingPetStyle.Outfits;
        PetBackBox.ItemsSource = FloatingPetStyle.Backs;
        PetPropBox.ItemsSource = FloatingPetStyle.Props;
        PetVibeBox.ItemsSource = FloatingPetStyle.Vibes;
        PetAuraBox.ItemsSource = FloatingPetStyle.Auras;
        PetBodyColorBox.ItemsSource = FloatingPetStyle.BodySwatches;
        PetAccentColorBox.ItemsSource = FloatingPetStyle.AccentSwatches;
        PetDetailColorBox.ItemsSource = FloatingPetStyle.DetailSwatches;

        PetFloatingBox.IsChecked = model.FloatingLanEnabled;
        PetRoamBox.IsChecked = model.FloatingLanRoam;
        RefreshSavedCompanions();

        petStudioTimer.Tick += (_, _) => StepCompanionStage();
        IsVisibleChanged += (_, _) => UpdateCompanionTimerState();
    }

    private void DisposeCompanionControls()
    {
        petStudioTimer.Stop();
        SavedPetBox.ItemsSource = null;
        PetPresetBox.ItemsSource = null;
        PetBodyBox.ItemsSource = null;
        PetPatternBox.ItemsSource = null;
        PetEarsBox.ItemsSource = null;
        PetFeetBox.ItemsSource = null;
        PetEyesBox.ItemsSource = null;
        PetBrowsBox.ItemsSource = null;
        PetMouthBox.ItemsSource = null;
        PetCheeksBox.ItemsSource = null;
        PetGlassesBox.ItemsSource = null;
        PetHatBox.ItemsSource = null;
        PetOutfitBox.ItemsSource = null;
        PetBackBox.ItemsSource = null;
        PetPropBox.ItemsSource = null;
        PetVibeBox.ItemsSource = null;
        PetAuraBox.ItemsSource = null;
        PetBodyColorBox.ItemsSource = null;
        PetAccentColorBox.ItemsSource = null;
        PetDetailColorBox.ItemsSource = null;
    }

    private void LoadCompanionControls(AppSettings s) => LoadCompanionAppearance(PetAppearance.From(s));

    private void LoadCompanionAppearance(PetAppearance p)
    {
        PetBodyBox.SelectedValue = p.Body;
        PetPatternBox.SelectedValue = p.Pattern;
        PetEarsBox.SelectedValue = p.Ears;
        PetFeetBox.SelectedValue = p.Feet;
        PetEyesBox.SelectedValue = p.Eyes;
        PetBrowsBox.SelectedValue = p.Brows;
        PetMouthBox.SelectedValue = p.Mouth;
        PetCheeksBox.SelectedValue = p.Cheeks;
        PetGlassesBox.SelectedValue = p.Glasses;
        PetHatBox.SelectedValue = p.Hat;
        PetOutfitBox.SelectedValue = p.Outfit;
        PetBackBox.SelectedValue = p.Back;
        PetPropBox.SelectedValue = p.Prop;
        PetVibeBox.SelectedValue = p.Vibe;
        PetAuraBox.SelectedValue = p.Aura;
        PetSizeBox.Value = FloatingPetStyle.ClampSize(p.Size);
        SetColorChannel(PetBodyColorBox, PetBodyHexBox, ref petBodyHex, p.BodyColor);
        SetColorChannel(PetAccentColorBox, PetAccentHexBox, ref petAccentHex, p.AccentColor);
        SetColorChannel(PetDetailColorBox, PetDetailHexBox, ref petDetailHex, p.DetailColor);
        SyncMatchedPreset(p);
        UpdateCompanionSummary(p);
    }

    private PetAppearance BuildCurrentPetAppearance() => new(
        FloatingPetStyle.NormalizeBody(PetBodyBox.SelectedValue as string),
        FloatingPetStyle.NormalizeEars(PetEarsBox.SelectedValue as string),
        FloatingPetStyle.NormalizeEyes(PetEyesBox.SelectedValue as string),
        FloatingPetStyle.NormalizeMouth(PetMouthBox.SelectedValue as string),
        FloatingPetStyle.NormalizeCheeks(PetCheeksBox.SelectedValue as string),
        FloatingPetStyle.NormalizeGlasses(PetGlassesBox.SelectedValue as string),
        FloatingPetStyle.NormalizeHat(PetHatBox.SelectedValue as string),
        FloatingPetStyle.NormalizeOutfit(PetOutfitBox.SelectedValue as string),
        FloatingPetStyle.NormalizeBack(PetBackBox.SelectedValue as string),
        FloatingPetStyle.NormalizeProp(PetPropBox.SelectedValue as string),
        FloatingPetStyle.NormalizeFeet(PetFeetBox.SelectedValue as string),
        FloatingPetStyle.NormalizePattern(PetPatternBox.SelectedValue as string),
        FloatingPetStyle.NormalizeVibe(PetVibeBox.SelectedValue as string),
        petBodyHex,
        petAccentHex,
        petDetailHex,
        FloatingPetStyle.ClampSize(PetSizeBox.Value),
        FloatingPetStyle.NormalizeBrows(PetBrowsBox.SelectedValue as string),
        FloatingPetStyle.NormalizeAura(PetAuraBox.SelectedValue as string));

    private void WriteCompanionTo(AppSettings s) => FloatingPetStyle.Apply(BuildCurrentPetAppearance(), s);

    private void SyncMatchedPreset(PetAppearance current)
    {
        var hit = FloatingPetStyle.Presets.FirstOrDefault(x => x.Appearance == current);
        PetPresetBox.SelectedValue = hit?.Name;
    }

    private void UpdateCompanionSummary(PetAppearance current)
    {
        if (PetComboSummary is null) return;
        string countLabel = FloatingPetStyle.CombinationCount >= 1_000_000_000
            ? $"{FloatingPetStyle.CombinationCount / 1_000_000_000}B+ styles"
            : $"{FloatingPetStyle.CombinationCount:N0}+ styles";
        PetComboSummary.Text = $"{countLabel} · {Math.Round(current.Size * 100):0}%";
    }

    private void ApplyCompanionPreview()
    {
        if (!IsInitialized || closing) return;
        var current = BuildCurrentPetAppearance();
        PetStudioPreview.PetStyle = current;
        model.PreviewPetAppearance(current);
        SyncMatchedPreset(current);
        UpdateCompanionSummary(current);
        RenderCompanionStage();
    }

    private void TriggerCompanionHop()
    {
        studioHop = .02;
        studioExcited = true;
        studioExcitedUntil = DateTime.UtcNow.AddSeconds(1.8);
        model.RequestPetHop();
        RenderCompanionStage();
    }

    private void PetOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        ApplyCompanionPreview();
    }

    private void PetSize_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        ApplyCompanionPreview();
    }

    private void PetPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        if (PetPresetBox.SelectedItem is not PetPreset preset) return;
        applyingAppearance = true;
        LoadCompanionAppearance(preset.Appearance);
        applyingAppearance = false;
        ApplyCompanionPreview();
        TriggerCompanionHop();
    }

    private void PetRandomize_Click(object sender, RoutedEventArgs e)
    {
        applyingAppearance = true;
        LoadCompanionAppearance(FloatingPetStyle.Randomize());
        applyingAppearance = false;
        ApplyCompanionPreview();
        TriggerCompanionHop();
    }

    private void PetReset_Click(object sender, RoutedEventArgs e)
    {
        applyingAppearance = true;
        LoadCompanionAppearance(PetAppearance.Default);
        applyingAppearance = false;
        ApplyCompanionPreview();
        TriggerCompanionHop();
    }

    private void PetHop_Click(object sender, RoutedEventArgs e) => TriggerCompanionHop();
    private void PetStage_Click(object sender, MouseButtonEventArgs e) => TriggerCompanionHop();
    private void PetStage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { studioHovered = true; RenderCompanionStage(); }
    private void PetStage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { studioHovered = false; RenderCompanionStage(); }

    private void RefreshSavedCompanions(string? selectId = null)
    {
        SavedPetBox.ItemsSource = null;
        SavedPetBox.ItemsSource = savedCompanions.ToList();
        if (selectId is not null) SavedPetBox.SelectedValue = selectId;
        else if (savedCompanions.Count > 0) SavedPetBox.SelectedIndex = 0;
        bool hasAny = savedCompanions.Count > 0;
        LoadSavedPetButton.IsEnabled = hasAny;
        DeleteSavedPetButton.IsEnabled = hasAny;
    }

    private List<SavedCompanion> GetSavedCompanionsForSave() =>
        savedCompanions.Select(p => new SavedCompanion { Id = p.Id, Name = p.Name, Code = p.Code }).ToList();

    private void SavePet_Click(object sender, RoutedEventArgs e)
    {
        CommitCompanionHexInputs();
        string name = SavedPetNameBox.Text.Trim();
        if (name.Length == 0) name = $"Companion {savedCompanions.Count + 1}";
        string code = BuildCurrentPetAppearance().ToWireCode();
        var existing = savedCompanions.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        string selectedId;
        if (existing is not null)
        {
            existing.Name = name;
            existing.Code = code;
            selectedId = existing.Id;
        }
        else
        {
            var entry = new SavedCompanion { Name = name, Code = code };
            savedCompanions.Add(entry);
            selectedId = entry.Id;
        }
        SavedPetNameBox.Text = "";
        RefreshSavedCompanions(selectedId);
        PetStatusText.Text = "Saved";
        TriggerCompanionHop();
    }

    private void LoadSavedPet_Click(object sender, RoutedEventArgs e)
    {
        if (SavedPetBox.SelectedItem is not SavedCompanion saved) return;
        string code = CompanionProfileCodec.SanitizeWireCode(saved.Code);
        if (PetAppearance.FromWireCode(code) is not { } parsed) { PetStatusText.Text = "Invalid code"; return; }
        applyingAppearance = true;
        LoadCompanionAppearance(parsed);
        applyingAppearance = false;
        ApplyCompanionPreview();
        TriggerCompanionHop();
        PetStatusText.Text = "Loaded";
    }

    private void DeleteSavedPet_Click(object sender, RoutedEventArgs e)
    {
        if (SavedPetBox.SelectedItem is not SavedCompanion saved) return;
        savedCompanions.RemoveAll(p => p.Id == saved.Id);
        RefreshSavedCompanions();
        PetStatusText.Text = "Deleted";
    }

    private void CopyPetCode_Click(object sender, RoutedEventArgs e)
    {
        CommitCompanionHexInputs();
        string code = BuildCurrentPetAppearance().ToWireCode();
        PetCodeBox.Text = code;
        try { System.Windows.Clipboard.SetText(code); } catch (System.Runtime.InteropServices.ExternalException) { }
        PetStatusText.Text = "Copied";
    }

    private void ImportPetCode_Click(object sender, RoutedEventArgs e)
    {
        string code = CompanionProfileCodec.SanitizeWireCode(PetCodeBox.Text);
        if (PetAppearance.FromWireCode(code) is not { } parsed) { PetStatusText.Text = "Invalid code"; return; }
        applyingAppearance = true;
        LoadCompanionAppearance(parsed);
        applyingAppearance = false;
        ApplyCompanionPreview();
        TriggerCompanionHop();
        PetStatusText.Text = "Imported";
    }

    private void PetFloating_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.UpdateFloatingState(enabled: PetFloatingBox.IsChecked == true);
    }

    private void PetRoam_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.UpdateFloatingState(roam: PetRoamBox.IsChecked == true);
    }

    private void PetCategory_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || PetBodyPanel is null || sender is not FrameworkElement { Tag: string cat }) return;
        PetBodyPanel.Visibility = cat == "Body" ? Visibility.Visible : Visibility.Collapsed;
        PetFacePanel.Visibility = cat == "Face" ? Visibility.Visible : Visibility.Collapsed;
        PetGearPanel.Visibility = cat == "Gear" ? Visibility.Visible : Visibility.Collapsed;
        PetColorsPanel.Visibility = cat == "Colors" ? Visibility.Visible : Visibility.Collapsed;
        PetMotionPanel.Visibility = cat == "Motion" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PetColor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        if (sender is not System.Windows.Controls.ListBox box || box.SelectedValue is not string value) return;
        applyingAppearance = true;
        if (box == PetBodyColorBox) { petBodyHex = value; PetBodyHexBox.Text = value; }
        else if (box == PetAccentColorBox) { petAccentHex = value; PetAccentHexBox.Text = value; }
        else if (box == PetDetailColorBox) { petDetailHex = value; PetDetailHexBox.Text = value; }
        applyingAppearance = false;
        ApplyCompanionPreview();
    }

    private void PetHex_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitPetHex(sender);
        e.Handled = true;
    }

    private void PetHex_Committed(object sender, KeyboardFocusChangedEventArgs e) => CommitPetHex(sender);

    private void CommitCompanionHexInputs()
    {
        CommitPetHex(PetBodyHexBox);
        CommitPetHex(PetAccentHexBox);
        CommitPetHex(PetDetailHexBox);
    }

    private void CommitPetHex(object sender)
    {
        if (!IsInitialized || closing || applyingAppearance || sender is not System.Windows.Controls.TextBox box) return;
        string raw = box.Text.Trim();
        string normalized;
        if (raw.Length == 0) normalized = "";
        else if (AppearanceOptions.TryParseHex(raw, out _)) normalized = AppearanceOptions.NormalizeHex(raw);
        else
        {
            applyingAppearance = true;
            box.Text = box == PetBodyHexBox ? petBodyHex : box == PetAccentHexBox ? petAccentHex : petDetailHex;
            applyingAppearance = false;
            return;
        }
        applyingAppearance = true;
        if (box == PetBodyHexBox) SetColorChannel(PetBodyColorBox, PetBodyHexBox, ref petBodyHex, normalized);
        else if (box == PetAccentHexBox) SetColorChannel(PetAccentColorBox, PetAccentHexBox, ref petAccentHex, normalized);
        else if (box == PetDetailHexBox) SetColorChannel(PetDetailColorBox, PetDetailHexBox, ref petDetailHex, normalized);
        applyingAppearance = false;
        ApplyCompanionPreview();
    }

    private void UpdateCompanionTimerState()
    {
        bool active = !closing && IsVisible && AppearancePage.IsVisible && AppearanceCompanionSection.IsVisible && SystemParameters.ClientAreaAnimation;
        if (active)
        {
            if (!petStudioTimer.IsEnabled) petStudioTimer.Start();
        }
        else
        {
            petStudioTimer.Stop();
            if (!closing) RenderCompanionStage();
        }
    }

    private void StepCompanionStage()
    {
        const double dt = .033;
        studioBreath = (studioBreath + dt * 2.3) % (Math.PI * 20);
        if (studioExcited && DateTime.UtcNow >= studioExcitedUntil) studioExcited = false;
        if (studioHop > 0)
        {
            studioHop += dt * 2.1;
            if (studioHop >= 1.0) studioHop = 0;
        }
        studioBlinkElapsed += dt;
        if (studioBlink == 0 && studioBlinkElapsed >= studioNextBlink)
        {
            studioBlinkElapsed = 0;
            studioBlinkDown = true;
            studioBlink = .25;
            studioNextBlink = 2.4 + Random.Shared.NextDouble() * 2.2;
        }
        else if (studioBlink > 0)
        {
            studioBlink += (studioBlinkDown ? 1 : -1) * dt * 11.0;
            if (studioBlink >= 1.0) { studioBlink = 1.0; studioBlinkDown = false; }
            else if (studioBlink <= 0) studioBlink = 0;
        }
        RenderCompanionStage();
    }

    private void RenderCompanionStage()
    {
        if (PetStudioPreview is null) return;
        bool sleepy = PetStudioPreview.PetStyle.Vibe == "sleepy" && !studioHovered && !studioExcited;
        PetStudioPreview.SetMotionState(
            studioBreath,
            studioBlink,
            studioHop,
            0,
            1,
            studioHovered,
            studioExcited,
            sleepy,
            online: model.OnlineCount > 0 || model.Settings.Status == "chat",
            roaming: false,
            model.Settings.Status);
    }
}
