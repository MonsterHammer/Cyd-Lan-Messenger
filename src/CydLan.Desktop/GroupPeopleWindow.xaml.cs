using System.Windows;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>Picks people for a new group chat, or people to add to an existing one.</summary>
public partial class GroupPeopleWindow : Window
{
    public sealed class Choice(Peer peer) : Observable
    {
        private bool selected;
        public Peer Peer { get; } = peer;
        public bool Selected { get => selected; set => Set(ref selected, value); }
    }
    private readonly List<Choice> choices;
    public string GroupName => NameBox.Text.Trim();
    public IReadOnlyList<string> SelectedIds => choices.Where(c => c.Selected).Select(c => c.Peer.Id).ToList();

    public GroupPeopleWindow(MainViewModel model, string heading, IEnumerable<string> exclude, bool askName)
    {
        InitializeComponent();
        PopupHost.EnableDrag(this, TitleBar);
        HeadingText.Text = heading; ConfirmButton.Content = askName ? "Create" : "Add";
        NamePanel.Visibility = askName ? Visibility.Visible : Visibility.Collapsed;
        var skip = exclude.ToHashSet();
        // Online people first; group invitations only reach people who are online.
        choices = model.Peers.Where(p => !skip.Contains(p.Id)).OrderByDescending(p => p.Online).ThenBy(p => p.Name).Select(p => new Choice(p)).ToList();
        PeopleList.ItemsSource = choices;
        EmptyText.Visibility = choices.Any(c => c.Peer.Online) ? Visibility.Collapsed : Visibility.Visible;
    }
    /// <summary>Selects people programmatically (tests).</summary>
    public void Select(params string[] ids) { foreach (var c in choices) c.Selected = ids.Contains(c.Peer.Id); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIds.Count == 0) return;
        DialogResult = true;
    }
}
