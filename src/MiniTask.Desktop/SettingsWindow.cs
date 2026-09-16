namespace MiniTask.Desktop;

public sealed class SettingsWindow : Window
{
    public Settings Result { get; private set; }
    public SettingsWindow(Settings current)
    {
        Result = current; Style = (Style)FindResource(typeof(Window));
        Title = "MiniTask · Advanced"; Icon = BrandIcon.Window; Width = 358; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new(12) };
        var mode = new ComboBox { ItemsSource = new[] { "Screen positions", "Relative to a window" }, SelectedIndex = (int)current.CoordinateMode };
        Add(panel, "New recording positions", mode);
        var gap = new TextBox { Text = current.Playback.GapMs.ToString() }; Add(panel, "Pause between repeats (ms)", gap);
        var focus = new CheckBox { Content = "Stop when the target loses focus", IsChecked = current.Playback.StopOnFocusLoss };
        var tray = new CheckBox { Content = "Minimize to the system tray", IsChecked = current.TrayOnMinimize };
        panel.Children.Add(focus); panel.Children.Add(tray);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new(0, 6, 0, 0) }; panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 12, 0, 0) };
        var ok = new Button { Content = "OK", MinWidth = 65, IsDefault = true };
        buttons.Children.Add(ok); buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 65 }); panel.Children.Add(buttons);
        ok.Click += (_, _) =>
        {
            if (!int.TryParse(gap.Text, out int milliseconds) || milliseconds is < 0 or > 3_600_000)
            { error.Text = "Enter a pause from 0 to 3,600,000 milliseconds."; error.Visibility = Visibility.Visible; gap.Focus(); gap.SelectAll(); return; }
            Result = current with { CoordinateMode = (CoordinateMode)mode.SelectedIndex, TrayOnMinimize = tray.IsChecked == true,
                Playback = current.Playback with { GapMs = milliseconds, StopOnFocusLoss = focus.IsChecked == true } };
            DialogResult = true;
        };
        Content = panel;
    }
    private static void Add(Panel parent, string label, Control control)
    {
        var row = new Grid { Margin = new(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = new(150) });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 8, 0) });
        Grid.SetColumn(control, 1); row.Children.Add(control); parent.Children.Add(row);
        System.Windows.Automation.AutomationProperties.SetName(control, label);
    }
    public const string Guide = "1. Press Rec (F8).\n2. Do the actions you want to repeat.\n3. Press Stop (F8).\n4. Press Play (F9).\n\nF10 stops playback. You can also click the active Stop button.\n\nOpen and Save use .minitask recordings. Save a recording before starting another if you want to keep it. Right-click Open for recent files. Use Prefs for speed, repeat count, shortcuts and appearance. Settings are remembered automatically.\n\nPlayback controls your mouse and keyboard. Leave them alone while it runs. Elevated apps may need MiniTask to run as administrator. Some games, secure screens and minimized windows do not accept simulated input.\n\nMore help: github.com/mwlyra/MiniTask";
}
