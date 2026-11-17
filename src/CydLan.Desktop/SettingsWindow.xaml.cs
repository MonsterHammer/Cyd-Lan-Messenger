using System.Windows;
using System.Windows.Controls;

namespace CydLan.Desktop;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel model;
    public SettingsWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        DisplayNameBox.Text = model.Settings.DisplayName;
        StatusBox.SelectedItem = StatusBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == model.Settings.Status) ?? StatusBox.Items[0];
        NotificationsBox.IsChecked = model.Settings.Notifications; MinimizedBox.IsChecked = model.Settings.StartMinimized;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text.Trim();
        if (name.Length == 0) { DisplayNameBox.Focus(); return; }
        await model.SaveProfileAsync(name, (string)((ComboBoxItem)StatusBox.SelectedItem).Tag, NotificationsBox.IsChecked == true, MinimizedBox.IsChecked == true);
        Close();
    }
}
