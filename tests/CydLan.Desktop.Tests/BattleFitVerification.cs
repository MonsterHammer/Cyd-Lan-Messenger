using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CydLan.Desktop;

internal static partial class Program
{
    /// <summary>The card battle (stage, hand and result) fits inside the Pets module from the smallest window up.</summary>
    private static async Task RunBattleFitVerificationAsync()
    {
        string artifacts = Path.GetFullPath("artifacts");
        Directory.CreateDirectory(artifacts);
        foreach (var (w, h) in new[] { (800.0, 560.0), (1600.0, 1000.0) })
        {
            await using var model = new MainViewModel(preview: true);
            var window = new MainWindow(model) { Left = 40, Top = 40, Width = w, Height = h, ShowActivated = false };
            window.Show();
            await Task.Delay(300);
            ((Button)window.FindName("PetsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(300);
            // Each lobby tab fits the width; nothing runs off to the side.
            var lobbyView = window.PetsView!;
            foreach (var tab in new[] { "World", "Pet", "Deck", "Shop" })
            {
                ((System.Windows.Controls.Primitives.ToggleButton)lobbyView.FindName(tab + "TabButton")).IsChecked = true;
                await Task.Delay(250);
                var scroller = (ScrollViewer)lobbyView.FindName(tab + "Tab");
                Check(scroller.IsVisible && scroller.ExtentWidth <= scroller.ViewportWidth + 1 && scroller.ViewportWidth > 300,
                    $"{w}x{h}: the {tab} tab fits ({scroller.ExtentWidth:0} in {scroller.ViewportWidth:0}).");
                CaptureElement(window, Path.Combine(artifacts, $"lobby-{tab.ToLowerInvariant()}-{w}x{h}.png"));
            }
            ((System.Windows.Controls.Primitives.ToggleButton)lobbyView.FindName("WorldTabButton")).IsChecked = true;
            FindAll<Button>(window).First(b => b.Content as string == "Card battle" && b.IsVisible && b.Name == "").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(900);
            var view = window.PetsView!;
            var stage = (FrameworkElement)view.FindName("Stage");
            var hand = (Panel)view.FindName("HandPanel");
            Rect stageBox = new(0, 0, stage.ActualWidth, stage.ActualHeight);
            foreach (var name in new[] { "LeftPet", "RightPet" })
            {
                var pet = (FrameworkElement)view.FindName(name);
                Rect box = pet.TransformToAncestor(stage).TransformBounds(new Rect(0, 0, pet.ActualWidth, pet.ActualHeight));
                box.Inflate(-1, -1);
                Check(stageBox.Contains(box), $"{w}x{h}: {name} fits the stage ({box} in {stageBox}).");
            }
            var last = (FrameworkElement)hand.Children[^1];
            double right = last.TransformToAncestor(hand).Transform(new Point(last.ActualWidth, 0)).X;
            Check(right <= view.HandRoom + 1, $"{w}x{h}: every card in a {hand.Children.Count}-card hand shows ({right:0} of {view.HandRoom:0}).");
            // Queue affordable cards into the top tray and verify the tray fits inside the stage too.
            var battlePanel = (FrameworkElement)view.FindName("BattlePanel");
            while (hand.Children.OfType<Border>().FirstOrDefault(b => b.Opacity >= 1) is { } card)
                view.DropCardAt((int)card.Tag, fromQueue: false, new Point(battlePanel.ActualWidth * .75, 60));
            await Task.Delay(250);
            var tray = (FrameworkElement)view.FindName("QueueTray");
            if (tray.IsVisible)
            {
                Rect qbox = tray.TransformToVisual(stage).TransformBounds(new Rect(0, 0, tray.ActualWidth, tray.ActualHeight));
                qbox.Inflate(-1, -1);
                Check(stageBox.Contains(qbox), $"{w}x{h}: the queued cards tray fits the stage ({qbox} in {stageBox}).");
            }
            CaptureElement(window, Path.Combine(artifacts, $"battle-fit-{w}x{h}.png"));

            model.Arena.LeaveCardBattle();
            await Task.Delay(600);
            var result = (FrameworkElement)view.FindName("ResultCard");
            Check(result.IsVisible, $"{w}x{h}: the result shows.");
            Rect rbox = result.TransformToAncestor(stage).TransformBounds(new Rect(0, 0, result.ActualWidth, result.ActualHeight));
            rbox.Inflate(-1, -1);
            Check(stageBox.Contains(rbox), $"{w}x{h}: the result card fits the stage ({rbox} in {stageBox}).");
            CaptureElement(window, Path.Combine(artifacts, $"battle-fit-result-{w}x{h}.png"));
            model.Arena.DismissCardBattle();
            window.Close();
        }
        Console.WriteLine("PASS  Battle fits the window");
    }
}
