namespace CydLan.Desktop;

/// <summary>Coordinates the tray-controlled floating LAN pet and its compact quick-chat popup.</summary>
public sealed class FloatingPetController : IDisposable
{
    private readonly MainViewModel model;
    private readonly Action openMainWindow;
    private readonly Action? customizePet;
    private FloatingPetWindow? petWindow;
    private FloatingQuickChatWindow? quickChatWindow;
    private DateTime lastQuickChatDeactivatedUtc = DateTime.MinValue;
    private bool disposed;

    public FloatingPetWindow? PetWindow => petWindow;
    public FloatingQuickChatWindow? QuickChatWindow => quickChatWindow;

    public FloatingPetController(MainViewModel model, Action openMainWindow, Action? customizePet = null)
    {
        this.model = model;
        this.openMainWindow = openMainWindow;
        this.customizePet = customizePet;
        model.FloatingStateChanged += SyncVisibility;
        model.PetActivity += OnPetActivity;
        model.PetHopRequested += OnPetHopRequested;
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
                customizePet ?? openMainWindow);
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

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        model.FloatingStateChanged -= SyncVisibility;
        model.PetActivity -= OnPetActivity;
        model.PetHopRequested -= OnPetHopRequested;
        quickChatWindow?.Close();
        petWindow?.Close();
    }
}
