using System.Windows;
using System.Windows.Threading;

namespace CydLan.Desktop;

internal partial class MessageNotificationWindow : Window
{
    private readonly Action open;
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromSeconds(6) };

    public MessageNotificationWindow(Action open)
    {
        InitializeComponent();
        this.open = open;
        expiry.Tick += (_, _) => Close();
        Closed += (_, _) => expiry.Stop();
    }

    public void Present(string sender, string message)
    {
        SenderName.Text = sender;
        MessageText.Text = message.ReplaceLineEndings(" ");
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
        if (!IsVisible) Show();
        expiry.Stop();
        expiry.Start();
    }

    private void Open_Click(object sender, RoutedEventArgs e) { Close(); open(); }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
}
