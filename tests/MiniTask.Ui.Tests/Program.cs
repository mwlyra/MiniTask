using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using MiniTask.Desktop;
using MiniTask.Windows;

namespace MiniTask.Ui.Tests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        string output = Path.Combine(root, "artifacts", "ui-checks"); Directory.CreateDirectory(output);
        var xaml = XDocument.Load(Path.Combine(root, "src", "MiniTask.Desktop", "App.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = new XElement(ns + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"), xaml.Root!.Element(ns + "Application.Resources")!.Elements());
        var app = new Application { Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString()) };
        Settings? savedPreferences = null;
        var window = new MainWindow(null, value => savedPreferences = value); app.MainWindow = window;
        // Use separate controls so an already-running user instance keeps its shortcuts/settings.
        var settingsField = typeof(MainWindow).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)!;
        settingsField.SetValue(window, new Settings { Hotkeys = new(0x85, 0x86, 0x87) });
        int failed = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                settingsField.SetValue(window, new Settings());
                var rootPanel = (StackPanel)window.Content;
                var toolbar = (System.Windows.Controls.Primitives.UniformGrid)rootPanel.Children[0];
                if (toolbar.Children.Count != 5) throw new Exception("Expected exactly five toolbar actions.");
                foreach (Button button in toolbar.Children)
                    if (string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button))) throw new Exception("Toolbar action lacks accessible name.");
                if (window.ActualWidth > 290 || window.ActualHeight > 120) throw new Exception("Toolbar exceeds compact footprint.");
                Console.WriteLine($"Compact window: {window.ActualWidth:0} × {window.ActualHeight:0} device-independent pixels.");
                var refresh = typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var field = typeof(MainWindow).GetField("controller", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var controller = (AppController)field.GetValue(window)!;
                var buildMenu = typeof(MainWindow).GetMethod("BuildPreferencesMenu", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var commands = (ContextMenu)buildMenu.Invoke(window, null)!;
                var speedMenu = commands.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Playback speed");
                speedMenu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "2×").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (savedPreferences?.Playback.Speed != 2) throw new Exception("Speed menu did not save the chosen value.");
                var repeatMenu = commands.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Repeat playback");
                repeatMenu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Continuous playback").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (savedPreferences?.Playback.Continuous != true) throw new Exception("Continuous repeat menu failed.");
                repeatMenu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "5 times").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (savedPreferences?.Playback.Continuous != false || savedPreferences.Playback.Repetitions != 5) throw new Exception("Finite repetition menu failed.");
                commands.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Show button captions").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (savedPreferences?.ShowCaptions != false || ((StackPanel)((Button)toolbar.Children[0]).Content).Children[1].Visibility != Visibility.Collapsed) throw new Exception("Caption menu did not update the toolbar.");
                settingsField.SetValue(window, new Settings()); refresh.Invoke(window, null);
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    App.ApplyTheme(theme); window.UpdateLayout();
                    foreach (double scale in new[] { 1d, 1.5d, 2d }) Render(window, Path.Combine(output, $"toolbar-{theme.ToLowerInvariant()}-{scale * 100:0}.png"), scale);
                    var prefs = (ContextMenu)buildMenu.Invoke(window, null)!;
                    if (!prefs.Items.OfType<MenuItem>().Any(m => (string)m.Header == "Repeat playback")) throw new Exception("Repeat menu missing.");
                    prefs.PlacementTarget = (Button)toolbar.Children[4]; prefs.IsOpen = true; prefs.UpdateLayout();
                    RenderElement(prefs, prefs.Background, Path.Combine(output, $"prefs-{theme.ToLowerInvariant()}.png"), 1);
                    prefs.IsOpen = false;
                    var settings = new SettingsWindow(new()) { Owner = window }; settings.Show(); settings.UpdateLayout();
                    if (settings.ActualHeight > 300) throw new Exception("Advanced dialog is too tall.");
                    Render(settings, Path.Combine(output, $"advanced-{theme.ToLowerInvariant()}.png"), 1); settings.Close();
                }
                App.ApplyTheme("Light");
                controller.Gate.TryBegin(MiniTask.Core.RunState.Recording); refresh.Invoke(window, null);
                if (System.Windows.Automation.AutomationProperties.GetName((Button)toolbar.Children[2]) != "Stop" || ((Button)toolbar.Children[3]).IsEnabled) throw new Exception("Recording controls are ambiguous.");
                Render(window, Path.Combine(output, "toolbar-recording.png"), 2); controller.Gate.Finish();
                controller.Gate.TryBegin(MiniTask.Core.RunState.Playing);
                settingsField.SetValue(window, new Settings { Playback = new(Repetitions: 5) });
                typeof(AppController).GetProperty("Progress")!.SetValue(controller, new MiniTask.Core.PlaybackProgress(2, .42));
                refresh.Invoke(window, null);
                if (System.Windows.Automation.AutomationProperties.GetName((Button)toolbar.Children[3]) != "Stop" || ((Button)toolbar.Children[2]).IsEnabled) throw new Exception("Playback controls are ambiguous.");
                Render(window, Path.Combine(output, "toolbar-playing.png"), 2); controller.Gate.Finish();
                settingsField.SetValue(window, new Settings { Hotkeys = new(0x85, 0x86, 0x87), ShowCaptions = false });
                refresh.Invoke(window, null); window.UpdateLayout(); Render(window, Path.Combine(output, "toolbar-icons-only.png"), 2);
                settingsField.SetValue(window, new Settings { Hotkeys = new(0x85, 0x86, 0x87) }); refresh.Invoke(window, null);
                var input = new InputTestWindow(); input.Show(); input.UpdateLayout(); Render(input, Path.Combine(output, "input-test-dark.png"), 1); input.Close();
                Console.WriteLine("PASS compact toolbar; speed/repeat/caption menu actions and persistence; advanced dialog; recording/playback Stop states; light/dark rendering at 100%, 150%, 200%.");
                Console.WriteLine("UI images: " + output);
            }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL UI: " + ex); }
            finally { window.Close(); }
        };
        timer.Start(); app.Run(window); return failed == 0 ? 0 : 1;
    }
    private static void Render(Window window, string path, double scale)
    {
        window.UpdateLayout();
        var element = (FrameworkElement)window.Content;
        RenderElement(element, window.Background, path, scale);
    }
    private static void RenderElement(FrameworkElement element, Brush background, string path, double scale)
    {
        element.UpdateLayout();
        double width = element.ActualWidth + element.Margin.Left + element.Margin.Right;
        double height = element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(background, null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(element), null, new Rect(element.Margin.Left, element.Margin.Top, element.ActualWidth, element.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
}
