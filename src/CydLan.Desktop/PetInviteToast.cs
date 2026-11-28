using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace CydLan.Desktop;

/// <summary>Corner card for an incoming battle challenge while the arena is closed.</summary>
internal sealed class PetInviteToast : Window
{
    private static readonly List<PetInviteToast> openToasts = [];
    private readonly MainViewModel model;
    private readonly PetInvite invite;
    private readonly FloatingPetVisual pet = new() { Width = 64, Height = 58, Margin = new Thickness(0, 0, 10, 0) };
    private readonly DispatcherTimer anim = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private double breath, hop;

    public static void Show(MainViewModel model, PetInvite invite)
    {
        foreach (var old in openToasts.Where(t => t.invite.PeerId == invite.PeerId).ToList()) old.Close();
        var toast = new PetInviteToast(model, invite);
        openToasts.Add(toast);
        toast.Closed += (_, _) => openToasts.Remove(toast);
        toast.Present();
    }

    public static void CloseAll()
    {
        foreach (var t in openToasts.ToList()) t.Close();
    }

    private PetInviteToast(MainViewModel model, PetInvite invite)
    {
        this.model = model;
        this.invite = invite;
        Title = "Battle challenge";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = 330;
        SizeToContent = SizeToContent.Height;
        SetResourceReference(StyleProperty, typeof(Window));
        SourceInitialized += (_, _) => PopupHost.MakeToolWindow(this);

        pet.PetStyle = invite.Appearance;
        pet.SetMood(PetMood.Fired);

        var name = new TextBlock { Text = invite.PeerName, FontWeight = FontWeights.SemiBold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis };
        var summary = new TextBlock { Text = (invite.IsTrade ? "Trade · " : "Battle · ") + invite.Summary, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var decline = new Button { Content = "Decline", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0) };
        decline.SetResourceReference(StyleProperty, "Secondary");
        decline.Click += async (_, _) => { Close(); await model.Arena.DeclineAsync(invite); };
        var accept = new Button { Content = invite.IsTrade ? "Trade" : "Battle", Padding = new Thickness(14, 4, 14, 4) };
        accept.SetResourceReference(StyleProperty, "Primary");
        System.Windows.Automation.AutomationProperties.SetName(accept, (invite.IsTrade ? "Accept trade from " : "Accept battle from ") + invite.PeerName);
        accept.Click += async (_, _) => { Close(); await model.Arena.AcceptAsync(invite); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(decline); buttons.Children.Add(accept);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(name); text.Children.Add(summary); text.Children.Add(buttons);
        var row = new DockPanel();
        DockPanel.SetDock(pet, Dock.Left);
        row.Children.Add(pet); row.Children.Add(text);
        var card = new Border { Padding = new Thickness(12, 10, 12, 12), BorderThickness = new Thickness(1.4), Child = row };
        card.SetResourceReference(Border.BackgroundProperty, "Sidebar");
        card.SetResourceReference(Border.BorderBrushProperty, "Accent");
        card.SetResourceReference(Border.CornerRadiusProperty, "PanelCorner");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(card, System.Windows.Automation.AutomationLiveSetting.Assertive);
        Content = card;

        anim.Tick += (_, _) =>
        {
            if (DateTime.UtcNow > invite.ExpiresUtc || !model.Arena.Invites.Contains(invite)) { Close(); return; }
            breath = (breath + .033 * 2.3) % (Math.PI * 20);
            if (hop > 0) { hop += .033 * 2.1; if (hop >= 1) hop = 0; }
            else if (Random.Shared.Next(70) == 0) hop = .02;
            pet.SetMotionState(breath, 0, hop, 0, 1, false, true, false, true, false, "chat");
        };
        Closed += (_, _) => anim.Stop();
    }

    private void Present()
    {
        Show();
        UpdateLayout();
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 16;
        Top = work.Bottom - ActualHeight - 16 - openToasts.Count(t => t != this && t.IsVisible) * (ActualHeight + 8);
        anim.Start();
        if (model.ShouldPlaySound) System.Media.SystemSounds.Asterisk.Play();
    }
}
