using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>Coordinates the tray-controlled floating LAN pet, its compact quick-chat popup, and reminder delivery.</summary>
public sealed class FloatingPetController : IDisposable
{
    private readonly MainViewModel model;
    private readonly Action openMainWindow;
    private readonly Action? customizePet;
    private readonly Action<string?> openReminders;
    private FloatingPetWindow? petWindow;
    private FloatingQuickChatWindow? quickChatWindow;
    private ReminderToastWindow? reminderToast;
    private DateTime lastQuickChatDeactivatedUtc = DateTime.MinValue;
    private bool disposed;

    public FloatingPetWindow? PetWindow => petWindow;
    public FloatingQuickChatWindow? QuickChatWindow => quickChatWindow;
    internal ReminderToastWindow? ReminderToast => reminderToast;

    public FloatingPetController(MainViewModel model, Action openMainWindow, Action? customizePet = null, Action<string?>? openReminders = null)
    {
        this.model = model;
        this.openMainWindow = openMainWindow;
        this.customizePet = customizePet;
        this.openReminders = openReminders ?? (_ => openMainWindow());
        model.FloatingStateChanged += SyncVisibility;
        model.PetActivity += OnPetActivity;
        model.PetHopRequested += OnPetHopRequested;
        model.ReminderDue += OnReminderDue;
        model.ReminderResolved += OnReminderResolved;
        model.PeerCameOnline += OnPeerCameOnline;
        model.Arena.PetAction += OnArenaAction;
        model.Arena.PetLine += OnArenaLine;
        model.Arena.InviteReceived += OnInvite;
        model.Arena.BattleStarted += OnBattleStarted;
    }

    private void OpenArena()
    {
        openMainWindow();
        model.PetsOpen = true;
    }

    private void OnArenaAction(string action, string? food)
    {
        if (!disposed && model.FloatingLanEnabled && petWindow is { IsVisible: true }) petWindow.PlayAction(action, food);
    }

    private void OnArenaLine(string line)
    {
        if (!disposed && model.FloatingLanEnabled && petWindow is { IsVisible: true }) petWindow.ShowLine(line);
    }

    private void OnInvite(PetInvite invite)
    {
        if (disposed) return;
        if (model.PetsOpen) return;
        if (model.FloatingLanEnabled && petWindow is { IsVisible: true })
        {
            petWindow.PlayAction("surprise");
            petWindow.ShowLine($"{invite.PeerName} wants to battle!");
        }
        PetInviteToast.Show(model, invite);
    }

    private void OnBattleStarted(PetBattleSession session)
    {
        if (disposed) return;
        OpenArena();
    }

    public void SyncVisibility()
    {
        if (disposed) return;
        if (model.FloatingLanEnabled)
        {
            petWindow ??= new FloatingPetWindow(
                model,
                ToggleQuickChat,
                openMainWindow,
                () => quickChatWindow?.IsVisible == true,
                customizePet ?? openMainWindow,
                openReminders,
                OpenArena);
            if (!petWindow.IsVisible) petWindow.Show();
        }
        else
        {
            if (quickChatWindow?.IsVisible == true) quickChatWindow.Hide();
            if (petWindow?.IsVisible == true) petWindow.Hide();
        }
    }

    public void ToggleQuickChat()
    {
        if (disposed) return;
        if (quickChatWindow?.IsVisible == true)
        {
            quickChatWindow.Hide();
            return;
        }
        // Clicking the pet deactivates an open quick-chat popup right before MouseLeftButtonUp fires.
        if ((DateTime.UtcNow - lastQuickChatDeactivatedUtc).TotalMilliseconds < 250)
            return;

        if (petWindow is null) SyncVisibility();
        if (petWindow is null) return;

        quickChatWindow ??= new FloatingQuickChatWindow(
            model,
            openMainWindow,
            () => lastQuickChatDeactivatedUtc = DateTime.UtcNow);
        quickChatWindow.ShowNear(petWindow.PetScreenRectDip());
    }

    private void OnPetActivity(string sender, string preview, string? peerId)
    {
        if (disposed || !model.FloatingLanEnabled) return;
        if (petWindow is { IsVisible: true })
            petWindow.ReactToIncoming(sender, preview, peerId);
    }

    private void OnPetHopRequested()
    {
        if (disposed || !model.FloatingLanEnabled) return;
        if (petWindow is { IsVisible: true })
            petWindow.TriggerHop();
    }

    private void OnPeerCameOnline(string name)
    {
        if (disposed || !model.FloatingLanEnabled) return;
        if (petWindow is { IsVisible: true }) petWindow.ReactToPeerOnline(name);
    }

    private void OnReminderDue(ReminderAlert alert)
    {
        if (disposed) return;
        if (model.ShouldPlaySound)
        {
            if (alert.Reminder.Priority == "urgent" || alert.Nag >= 2) System.Media.SystemSounds.Exclamation.Play();
            else if (alert.Reminder.Priority != "gentle" || alert.Nag == 0) System.Media.SystemSounds.Asterisk.Play();
        }
        if (model.FloatingLanEnabled && petWindow is { IsVisible: true })
        {
            petWindow.ShowReminder(alert);
            return;
        }
        if (reminderToast is null)
        {
            reminderToast = new ReminderToastWindow(model, openReminders);
            reminderToast.Closed += (_, _) => reminderToast = null;
        }
        var voice = new PetMind();
        voice.SetTraits(model.PetTraitList);
        var line = voice.ReminderLine(alert.Reminder.Title, alert.Nag, alert.Overdue, alert.Reminder.Priority);
        reminderToast.Present(alert, line);
    }

    private void OnReminderResolved(Reminder reminder, ReminderOutcome outcome)
    {
        if (disposed) return;
        petWindow?.ResolveReminder(reminder, outcome);
        if (reminderToast?.Alert?.Reminder.Id == reminder.Id) reminderToast.Close();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        model.FloatingStateChanged -= SyncVisibility;
        model.PetActivity -= OnPetActivity;
        model.PetHopRequested -= OnPetHopRequested;
        model.ReminderDue -= OnReminderDue;
        model.ReminderResolved -= OnReminderResolved;
        model.PeerCameOnline -= OnPeerCameOnline;
        model.Arena.PetAction -= OnArenaAction;
        model.Arena.PetLine -= OnArenaLine;
        model.Arena.InviteReceived -= OnInvite;
        model.Arena.BattleStarted -= OnBattleStarted;
        PetInviteToast.CloseAll();
        reminderToast?.Close();
        quickChatWindow?.Close();
        petWindow?.Close();
    }
}
