using Microsoft.Win32;

namespace MiniTask.Desktop;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string[] args = e.Args;
        if (args.Contains("--input-test"))
        {
            MainWindow = new InputTestWindow(); MainWindow.Show(); return;
        }
        if (args.Length >= 2 && args[0] == "--wait-for-exit" && int.TryParse(args[1], out int oldPid))
        {
            try { using var previous = System.Diagnostics.Process.GetProcessById(oldPid); if (!previous.WaitForExit(15000)) { Shutdown(1); return; } }
            catch (ArgumentException) { }
            args = args.Skip(2).ToArray();
        }
        instance = new Mutex(true, "Local\\MiniTask.Desktop", out bool created);
        if (!created) { MessageBox.Show("MiniTask is already running. Check its toolbar or tray icon.", "MiniTask"); Shutdown(); return; }
        try
        {
            var main = new MainWindow(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)));
            MainWindow = main; main.Show();
        }
        catch (Exception ex) { Diagnostics.Failure("startup", ex); MessageBox.Show(ex.Message, "MiniTask could not start"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
    public static void ApplyTheme(string theme)
    {
        bool dark = theme == "Dark";
        if (theme == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        var values = dark ? new[] { "#292928", "#353533", "#EEEEEB", "#BCBCB6", "#555550", "#43433F" }
                          : new[] { "#F0F0EC", "#FAFAF7", "#252522", "#666660", "#B9B9B1", "#E0E0D8" };
        string[] names = ["Surface", "Panel", "Ink", "Muted", "Line", "Hover"];
        for (int i = 0; i < names.Length; i++) Current.Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
        // Classic native menus remain light, like the standard file picker and combo boxes.
        Current.Resources[SystemColors.MenuBrushKey] = new SolidColorBrush(Color.FromRgb(250, 250, 247));
        Current.Resources[SystemColors.MenuTextBrushKey] = new SolidColorBrush(Color.FromRgb(37, 37, 34));
        if (SystemParameters.HighContrast)
        {
            Current.Resources["Surface"] = SystemColors.WindowBrush; Current.Resources["Panel"] = SystemColors.WindowBrush;
            Current.Resources["Ink"] = SystemColors.WindowTextBrush; Current.Resources["Muted"] = SystemColors.WindowTextBrush;
            Current.Resources["Line"] = SystemColors.WindowTextBrush; Current.Resources["Hover"] = SystemColors.HighlightBrush;
        }
    }
}
