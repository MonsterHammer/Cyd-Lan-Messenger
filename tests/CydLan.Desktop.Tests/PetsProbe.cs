using System.IO;
using System.Windows;
using System.Windows.Controls;
using CydLan.Core;
using CydLan.Desktop;

internal static partial class Program
{
    /// <summary>Measures CPU in each Pets screen with a copy of a real settings file (see --pets-probe &lt;settings.json&gt;).</summary>
    private static async Task RunPetsProbeAsync(string settingsPath)
    {
        string dir = Path.Combine(Path.GetTempPath(), "cydlan-pets-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.Copy(settingsPath, Path.Combine(dir, "settings.json"));
        await using var model = new MainViewModel(preview: false, localStore: new LocalStore(dir));
        var window = new MainWindow(model) { Left = 40, Top = 40, Width = 1240, Height = 780, ShowActivated = false };
        window.Show();
        await Task.Delay(1500);
        Console.WriteLine($"chats idle      {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        model.PetsOpen = true;
        await Task.Delay(800);
        Console.WriteLine($"pets lobby      {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        var view = window.PetsView!;
        var anim = (System.Windows.Threading.DispatcherTimer)typeof(PetArenaView).GetField("anim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
        var side = (System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("sidebarPetTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
        anim.Stop();
        Console.WriteLine($"lobby no anim   {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        side.Stop();
        Console.WriteLine($"lobby no timers {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        anim.Start(); side.Start();
        var pet = (FrameworkElement)view.FindName("MyPetVisual");
        pet.Visibility = Visibility.Collapsed;
        Console.WriteLine($"lobby pet hid   {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        pet.Visibility = Visibility.Visible;
        FindAll<Button>(window).First(b => b.Content as string == "Card battle" && b.IsVisible).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(800);
        Console.WriteLine($"card battle     {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        await Task.Delay(4000);
        Console.WriteLine($"card battle +8s {await BusyFractionAsync(TimeSpan.FromSeconds(4)):P0}");
        CaptureElement(window, Path.GetFullPath("artifacts/pets-probe.png"));
        model.Arena.LeaveCardBattle();
        window.Close();
    }
}
