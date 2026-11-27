using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>Expanded profile modal displaying avatar, bio, optional CydLan companion character, and available LAN info.</summary>
public partial class PeerProfileWindow : Window
{
    private readonly Peer peer;
    private readonly MainViewModel model;
    private readonly PetAppearance? peerCompanion;
    private readonly DispatcherTimer animTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private double breath, blink, blinkElapsed, nextBlink = 2.6, hop;
    private bool blinkDown, stageHovered, stageExcited;
    private DateTime excitedUntil = DateTime.MinValue;

    public PeerProfileWindow(Peer peer, MainViewModel model)
    {
        InitializeComponent();
        this.peer = peer;
        this.model = model;
        PopupHost.EnableDrag(this, TitleBar);
        SourceInitialized += (_, _) => PopupHost.RoundCorners(this);

        AvatarInitialsText.Text = peer.Initials;
        AvatarPhoto.Source = peer.AvatarPath.Length > 0 ? Images.Load(peer.AvatarPath, 256) : null;

        DisplayNameText.Text = peer.Name;
        FullNameText.Text = peer.FullName;
        FullNameText.Visibility = peer.HasFullName ? Visibility.Visible : Visibility.Collapsed;

        PresenceText.Text = peer.Presence;
        PresenceDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, peer.Online ? peer.Presence switch
        {
            "Away" or "Busy" => "Away",
            "Do not disturb" => "Danger",
            _ => "Online"
        } : "Faint");

        if (peer.HasAbout)
        {
            BioSection.Visibility = Visibility.Visible;
            BioText.Text = peer.About;
        }
        else
        {
            BioSection.Visibility = Visibility.Collapsed;
        }

        // Only CydLan peers carrying a valid companion code show the Character showcase;
        // legacy LAN Messenger peers cleanly show only their available fields.
        peerCompanion = LanService.IsCydLanVersion(peer.Version)
            ? PetAppearance.FromWireCode(peer.PetCode)
            : null;
        if (peerCompanion is not null)
        {
            CompanionSection.Visibility = Visibility.Visible;
            PeerPetVisual.PetStyle = peerCompanion;
            UseCompanionButton.IsEnabled = peerCompanion != model.PetAppearance;
            RenderPet();
            if (SystemParameters.ClientAreaAnimation)
            {
                animTimer.Tick += (_, _) => StepAnimation();
                animTimer.Start();
            }
        }
        else
        {
            CompanionSection.Visibility = Visibility.Collapsed;
        }

        AddressText.Text = peer.Address;
        AddressGroup.Visibility = string.IsNullOrWhiteSpace(peer.Address) ? Visibility.Collapsed : Visibility.Visible;
        ClientText.Text = peer.Version;
        ClientGroup.Visibility = string.IsNullOrWhiteSpace(peer.Version) ? Visibility.Collapsed : Visibility.Visible;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Closed += (_, _) =>
        {
            animTimer.Stop();
            AvatarPhoto.Source = null;
        };
    }

    private void StepAnimation()
    {
        const double dt = .033;
        breath = (breath + dt * 2.3) % (Math.PI * 20);
        if (stageExcited && DateTime.UtcNow >= excitedUntil) stageExcited = false;
        if (hop > 0)
        {
            hop += dt * 2.1;
            if (hop >= 1.0) hop = 0;
        }
        blinkElapsed += dt;
        if (blink == 0 && blinkElapsed >= nextBlink)
        {
            blinkElapsed = 0;
            blinkDown = true;
            blink = .25;
            nextBlink = 2.4 + Random.Shared.NextDouble() * 2.2;
        }
        else if (blink > 0)
        {
            blink += (blinkDown ? 1 : -1) * dt * 11.0;
            if (blink >= 1.0) { blink = 1.0; blinkDown = false; }
            else if (blink <= 0) blink = 0;
        }
        RenderPet();
    }

    private void RenderPet()
    {
        if (peerCompanion is null) return;
        bool sleepy = (!peer.Online || peer.Status == "dnd" || peerCompanion.Vibe == "sleepy") && !stageHovered && !stageExcited;
        PeerPetVisual.SetMotionState(
            breath, blink, hop, 0, 1,
            stageHovered, stageExcited, sleepy,
            peer.Online, roaming: false, peer.Status);
    }

    private void CompanionStage_Click(object sender, MouseButtonEventArgs e)
    {
        hop = .02;
        stageExcited = true;
        excitedUntil = DateTime.UtcNow.AddSeconds(1.8);
        RenderPet();
    }

    private void CompanionStage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { stageHovered = true; RenderPet(); }
    private void CompanionStage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { stageHovered = false; RenderPet(); }

    private async void UseCompanion_Click(object sender, RoutedEventArgs e)
    {
        if (peerCompanion is null) return;
        UseCompanionButton.IsEnabled = false;
        await model.SavePreferencesAsync(s => FloatingPetStyle.Apply(peerCompanion, s));
        model.RequestPetHop();
        hop = .02;
        stageExcited = true;
        excitedUntil = DateTime.UtcNow.AddSeconds(1.8);
        RenderPet();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
