namespace MiniTask.Desktop;

internal sealed class LicenseWindow : Window
{
    public LicenseWindow()
    {
        Title = "MiniTask · Licenses"; Icon = BrandIcon.Window;
        Width = 640; Height = 460; MinWidth = 360; MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new DockPanel { Margin = new(12) };
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, MinWidth = 70, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        var assembly = typeof(LicenseWindow).Assembly;
        var notices = new List<string>();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("MiniTask.Licenses.", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            notices.Add(reader.ReadToEnd());
        }
        var text = new TextBox { Text = string.Join("\n\n────────────────────────\n\n", notices), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        System.Windows.Automation.AutomationProperties.SetName(text, "MiniTask and third-party license notices");
        panel.Children.Add(text); Content = panel;
    }
}

public sealed partial class MainWindow
{
    private void ShowLicenses()
    {
        if (Busy || dialogOpen) return;
        dialogOpen = true;
        try { new LicenseWindow { Owner = this }.ShowDialog(); }
        finally { dialogOpen = false; Refresh(); }
    }
}
