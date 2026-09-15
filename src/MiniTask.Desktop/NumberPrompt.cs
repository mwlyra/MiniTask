using System.Globalization;

namespace MiniTask.Desktop;

internal static class NumberPrompt
{
    public static double? Show(Window owner, string title, string label, double value, double minimum, double maximum, bool integer = false)
    {
        var window = new Window { Owner = owner, Title = "MiniTask · " + title, Width = 292, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var panel = new StackPanel { Margin = new(12) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new(0, 0, 0, 5) });
        var text = new TextBox { Text = value.ToString(CultureInfo.InvariantCulture), Margin = new(0), Padding = new(4) }; panel.Children.Add(text);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 0), Visibility = Visibility.Collapsed }; panel.Children.Add(error);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 65 };
        row.Children.Add(ok); row.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 65 }); panel.Children.Add(row);
        double? result = null;
        ok.Click += (_, _) =>
        {
            if (double.TryParse(text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) && number >= minimum && number <= maximum && (!integer || number == Math.Truncate(number)))
            { result = number; window.DialogResult = true; }
            else { error.Text = $"Enter {(integer ? "a whole number" : "a number")} from {minimum} to {maximum}."; error.Visibility = Visibility.Visible; text.Focus(); text.SelectAll(); }
        };
        window.Content = panel; window.Loaded += (_, _) => { text.Focus(); text.SelectAll(); }; window.ShowDialog(); return result;
    }
}
