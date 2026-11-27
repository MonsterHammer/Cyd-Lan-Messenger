using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CydLan.Core;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace CydLan.Desktop;

/// <summary>Corner card for a due reminder when the desktop companion is hidden.</summary>
internal sealed class ReminderToastWindow : Window
{
    private readonly MainViewModel model;
    private readonly Action<string?> open;
    private readonly TextBlock titleText = new() { FontWeight = FontWeights.SemiBold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock whenText = new() { FontSize = 11, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock noteText = new() { FontSize = 12, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Glyph icon = new() { Size = 16 };
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromMinutes(2) };
    private ReminderAlert? alert;

    public ReminderAlert? Alert => alert;

    public ReminderToastWindow(MainViewModel model, Action<string?> open)
    {
        this.model = model;
        this.open = open;
        Title = "CydLan reminder";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = 344;
        SizeToContent = SizeToContent.Height;
        SetResourceReference(StyleProperty, typeof(Window));
        SourceInitialized += (_, _) => PopupHost.MakeToolWindow(this);
        expiry.Tick += (_, _) => Close();
        Closed += (_, _) => expiry.Stop();

        whenText.SetResourceReference(TextBlock.ForegroundProperty, "Faint");
        noteText.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        icon.SetResourceReference(ForegroundProperty, "Accent");

        var disc = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Child = icon, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 10, 0) };
        disc.SetResourceReference(Border.BackgroundProperty, "AccentSoft");

        var snooze = new Button { Content = "Snooze", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0) };
        snooze.SetResourceReference(StyleProperty, "Secondary");
        AutomationNamed(snooze, "Snooze reminder 10 minutes");
        snooze.Click += (_, _) => { if (alert is { Test: false } a) model.SnoozeReminder(a.Reminder.Id, 10); Close(); };
        var done = new Button { Content = "Done", Padding = new Thickness(14, 4, 14, 4) };
        done.SetResourceReference(StyleProperty, "Primary");
        AutomationNamed(done, "Mark reminder done");
        done.Click += (_, _) => { if (alert is { Test: false } a) model.CompleteReminder(a.Reminder.Id); Close(); };
        var openButton = new Button { Content = "Open", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0) };
        openButton.SetResourceReference(StyleProperty, "Secondary");
        openButton.Click += (_, _) => { open(alert?.Reminder.Id); Close(); };
        var close = new Button { Width = 26, Height = 26, Content = new Glyph { Size = 12, Data = (Geometry)System.Windows.Application.Current.FindResource("IconClose") }, VerticalAlignment = VerticalAlignment.Top };
        close.SetResourceReference(StyleProperty, "Icon");
        AutomationNamed(close, "Dismiss reminder");
        close.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(openButton); buttons.Children.Add(snooze); buttons.Children.Add(done);
        var text = new StackPanel();
        text.Children.Add(titleText); text.Children.Add(whenText); text.Children.Add(noteText); text.Children.Add(buttons);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1); Grid.SetColumn(close, 2);
        grid.Children.Add(disc); grid.Children.Add(text); grid.Children.Add(close);
        var card = new Border { Padding = new Thickness(14, 12, 10, 12), BorderThickness = new Thickness(1.4), Child = grid, Margin = new Thickness(0, 0, 0, 0) };
        card.SetResourceReference(Border.BackgroundProperty, "Sidebar");
        card.SetResourceReference(Border.BorderBrushProperty, "Accent");
        card.SetResourceReference(Border.CornerRadiusProperty, "PanelCorner");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(card, System.Windows.Automation.AutomationLiveSetting.Assertive);
        Content = card;
    }

    private static void AutomationNamed(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);

    public void Present(ReminderAlert value, string line)
    {
        alert = value;
        var r = value.Reminder;
        titleText.Text = string.IsNullOrWhiteSpace(r.Title) ? "Reminder" : r.Title;
        whenText.Text = (value.Overdue ? "Missed · " : "") + ReminderSchedule.DescribeTime(r);
        noteText.Text = r.Note.Length > 0 ? r.Note : line;
        icon.Data = System.Windows.Application.Current.TryFindResource(ReminderIcons.Resource(r.Icon)) as Geometry;
        if (!IsVisible) Show();
        UpdateLayout();
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 16;
        Top = work.Bottom - ActualHeight - 16;
        expiry.Stop();
        expiry.Start();
    }
}
