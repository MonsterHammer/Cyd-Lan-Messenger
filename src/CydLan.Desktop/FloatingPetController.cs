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
                openReminders);
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
        reminderToast?.Close();
        quickChatWindow?.Close();
        petWindow?.Close();
    }
}
