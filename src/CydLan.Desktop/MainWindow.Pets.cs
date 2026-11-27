using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CydLan.Desktop;

/// <summary>The Pets module on the rail: hosts the arena view and the sidebar pet.</summary>
public partial class MainWindow
{
    private PetArenaView? petsView;
    private readonly DispatcherTimer sidebarPetTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private double sidebarBreath, sidebarHop;

    private void InitPets()
    {
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.PetsOpen)) return;
            if (model.PetsOpen) EnsurePetsView();
            UpdateSidebarPet();
        };
        model.Arena.PropertyChanged += (_, _) => UpdateSidebarPet();
        sidebarPetTimer.Tick += (_, _) =>
        {
            sidebarBreath = (sidebarBreath + .033 * 2.3) % (Math.PI * 20);
            if (sidebarHop > 0) { sidebarHop += .033 * 2.1; if (sidebarHop >= 1) sidebarHop = 0; }
            SidebarPetVisual.SetMotionState(sidebarBreath, 0, sidebarHop, 0, 1, false, false, false, true, false, model.Settings.Status);
        };
        IsVisibleChanged += (_, _) => UpdateSidebarPet();
        Closed += (_, _) => { sidebarPetTimer.Stop(); petsView?.Release(); };
        if (model.PetsOpen) EnsurePetsView();
    }

    private void EnsurePetsView()
    {
        if (petsView is not null) return;
        petsView = new PetArenaView(model);
        PetsHost.Content = petsView;
    }

    private void UpdateSidebarPet()
    {
        if (model.PetsOpen && IsVisible)
        {
            SidebarPetVisual.PetStyle = model.Arena.BattleLook;
            if (SystemParameters.ClientAreaAnimation && !sidebarPetTimer.IsEnabled) sidebarPetTimer.Start();
        }
        else sidebarPetTimer.Stop();
    }

    /// <summary>Opens the Pets module.</summary>
    internal void OpenPets() => model.PetsOpen = true;

    internal PetArenaView? PetsView => petsView;

    private void Pets_Click(object sender, RoutedEventArgs e) => model.PetsOpen = true;
    private void PetsCardBattle_Click(object sender, RoutedEventArgs e) => model.Arena.StartCardBattle();
    private void PetsCustomize_Click(object sender, RoutedEventArgs e) => OpenAppearanceTab("Companion");
    private void SidebarPet_Click(object sender, MouseButtonEventArgs e) { sidebarHop = .02; SidebarPetVisual.SetMood(PetMood.Laughing); _ = ResetSidebarMoodAsync(); }

    private async Task ResetSidebarMoodAsync()
    {
        await Task.Delay(1500);
        SidebarPetVisual.SetMood("");
    }
}
