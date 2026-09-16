using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MiniTask.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly AppController controller = new();
    private readonly Action<Settings> savePreferences;
    private readonly Action<bool> playSound;
    private Settings settings;
    private readonly Button open, save, record, play, options;
    private readonly TextBlock status = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock detail = new() { FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 0, 0) };
    private readonly DispatcherTimer timer;
    private readonly TrayIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private bool dirty, closing, finished, dialogOpen;
    private string? filename;
    private string? warning;
    private string? uiError;
    public MainWindow(string? initialFile, Action<Settings>? savePreferences = null, Action<bool>? playSound = null)
    {
        this.savePreferences = savePreferences ?? (value => value.Save());
        this.playSound = playSound ?? SoundCues.Play;
        settings = Settings.Load(out warning);
        Style = (Style)FindResource(typeof(Window));
        App.ApplyTheme(settings.Theme);
        Title = "MiniTask"; Width = 284; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = settings.AlwaysOnTop;
        Icon = BrandIcon.Window;
        var root = new StackPanel { Margin = new(4, 3, 4, 3), UseLayoutRounding = true };
        var toolbar = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5 };
        open = Tool("Open", "Open a recording (Ctrl+O)\nRight-click for recent files", async () => await Open());
        save = Tool("Save", "Save recording (Ctrl+S)", async () => await Save());
        record = Tool("Rec", "Record", ToggleRecord);
        play = Tool("Play", "Play", TogglePlay);
        options = Tool("Prefs", "Playback speed, repeats and options", ShowPreferences);
        foreach (var button in new[] { open, save, record, play, options }) toolbar.Children.Add(button);
        root.Children.Add(toolbar);
        var summary = new DockPanel { Height = 19, Margin = new(3, 2, 3, 0) };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        DockPanel.SetDock(detail, Dock.Right); summary.Children.Add(detail); summary.Children.Add(status);
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        root.Children.Add(summary);
        status.MouseLeftButtonUp += (_, _) => { if (HudNotice is { } message) MessageBox.Show(this, message, "MiniTask", MessageBoxButton.OK, MessageBoxImage.Warning); };
        Content = root;
        PreviewKeyDown += async (_, e) =>
        {
            if (Busy || dialogOpen) return;
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.O) { e.Handled = true; await Open(); }
            else if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.S) { e.Handled = true; await Save(); }
        };
        trayIcon = BrandIcon.CreateTrayIcon();
        tray = new TrayIcon(trayIcon, () => Dispatcher.BeginInvoke(Restore), () => controller.Stop(), () => Dispatcher.BeginInvoke(Close));
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && settings.TrayOnMinimize && tray.Added && controller.Gate.State != RunState.Recording) Hide();
        };
        controller.RecordRequested += () => Dispatcher.BeginInvoke(ToggleRecord);
        controller.PlayRequested += () => Dispatcher.BeginInvoke(TogglePlay);
        controller.Recorded += _ => Dispatcher.BeginInvoke(() => { dirty = true; filename = null; });
        controller.RunStarted += () => Dispatcher.BeginInvoke(() => PlaySound(true));
        controller.RunFinished += () => Dispatcher.BeginInvoke(() => PlaySound(false));
        controller.Failed += message => Dispatcher.BeginInvoke(() => { uiError = message; Restore(); });
        controller.TechnicalFailure += ex => Diagnostics.Failure("engine", ex);
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        Closing += OnClosing;
        SystemEvents.UserPreferenceChanged += PreferenceChanged;
        Loaded += async (_, _) =>
        {
            try { await controller.Input.Configure(settings.Hotkeys); }
            catch (Exception ex) { warning = ex.Message; }
            if (controller.Input.SessionWarning is { } sessionWarning) warning = sessionWarning;
            if (warning is not null) MessageBox.Show(this, warning, "MiniTask shortcut / settings warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (initialFile is not null) await Open(initialFile);
            Refresh();
        };
        Refresh();
    }
    private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => App.ApplyTheme(settings.Theme));
    private Button Tool(string label, string tooltip, Action action)
    {
        var stack = new StackPanel();
        stack.Children.Add(new ToolbarGlyph(label));
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 2, 0, 0) });
        var button = new Button { Content = stack, ToolTip = tooltip, Style = (Style)FindResource("ToolbarButton") };
        ToolTipService.SetInitialShowDelay(button, 350);
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
    private static void Label(Button button, string label, bool stop)
    {
        var stack = (StackPanel)button.Content;
        if (((TextBlock)stack.Children[1]).Text == label) return;
        ((TextBlock)stack.Children[1]).Text = label;
        ((ToolbarGlyph)stack.Children[0]).Stopped = stop;
        AutomationProperties.SetName(button, label);
    }
    private bool Busy => controller.Gate.State is not (RunState.Idle or RunState.Error);
    private string? HudNotice => uiError ?? warning ?? (!Busy && !controller.Status.StartsWith("Ready", StringComparison.Ordinal) && controller.Status != "Stopped" ? controller.Status : null);
    private void Refresh()
    {
        var state = controller.Gate.State;
        bool recording = state == RunState.Recording;
        bool playing = state is RunState.Playing or RunState.Countdown;
        Label(record, recording ? "Stop" : "Rec", recording);
        Label(play, playing ? "Stop" : "Play", playing);
        record.IsEnabled = !dialogOpen && (!Busy || recording);
        play.IsEnabled = !dialogOpen && (playing || !Busy && controller.Macro?.Events.Count > 0);
        open.IsEnabled = options.IsEnabled = !Busy && !dialogOpen;
        save.IsEnabled = !Busy && !dialogOpen && controller.Macro is not null;
        UpdateHud(state);
        foreach (var button in new[] { open, save, record, play, options })
            ((StackPanel)button.Content).Children[1].Visibility = settings.ShowCaptions ? Visibility.Visible : Visibility.Collapsed;
        record.ToolTip = $"{(recording ? "Stop recording" : "Record")} ({Hotkeys.Label(settings.Hotkeys.Record)})";
        play.ToolTip = $"{(playing ? "Stop playback" : "Play")} ({Hotkeys.Label(settings.Hotkeys.Play)})\nEmergency stop: {Hotkeys.Label(settings.Hotkeys.Emergency)}";
        string trayText = recording ? "MiniTask · RECORDING" : playing ? "MiniTask · PLAYING" : "MiniTask · Ready";
        if (tray.Text != trayText) tray.Text = trayText;
        if (recording && (!IsVisible || WindowState == WindowState.Minimized)) Restore();
        if (open.ContextMenu is null)
        {
            var recents = new ContextMenu();
            foreach (var path in settings.Recent)
            {
                var item = new MenuItem { Header = path };
                item.Click += async (_, _) => await Open(path); recents.Items.Add(item);
            }
            if (recents.Items.Count == 0) recents.Items.Add(new MenuItem { Header = "No recent files", IsEnabled = false });
            open.ContextMenu = recents;
        }
    }
    private void UpdateHud(RunState state)
    {
        string time = FormatTime(controller.RecordingElapsedUs);
        string repeats = settings.Playback.Continuous ? "∞" : settings.Playback.Repetitions.ToString();
        var p = controller.Progress;
        status.Text = state switch
        {
            RunState.Recording => $"● Recording {time}",
            RunState.Countdown => $"Starting in {p?.CountdownRemaining ?? settings.Playback.CountdownSeconds}…",
            RunState.Playing => $"▶ Playing {p?.Repetition ?? 1}/{repeats}  {p?.Fraction ?? 0:P0}",
            RunState.Stopping => "Stopping…",
            _ when HudNotice is not null => "⚠ " + (warning is not null ? "Check hotkeys · click here" : "Stopped · click for details"),
            _ when controller.Status == "Stopped" => "Stopped",
            _ when controller.Macro is { } macro => $"{(dirty ? "Unsaved" : "Ready")} · {FormatTime(macro.DurationUs)}",
            _ => "Ready"
        };
        detail.Text = Busy ? $"{Hotkeys.Label(settings.Hotkeys.Emergency)} Stop" : $"{settings.Playback.Speed:0.##}× · {(settings.Playback.Continuous ? "Loop" : settings.Playback.Repetitions == 1 ? "Once" : settings.Playback.Repetitions + " times")}";
        detail.ToolTip = $"Record: {Hotkeys.Label(settings.Hotkeys.Record)}\nPlay: {Hotkeys.Label(settings.Hotkeys.Play)}\nEmergency stop: {Hotkeys.Label(settings.Hotkeys.Emergency)}";
        status.ToolTip = HudNotice ?? (controller.Macro is { } m ? $"{Path.GetFileName(filename) ?? "Unsaved recording"}\n{m.Events.Count:N0} actions · {m.DurationUs / 1_000_000d:0.0} seconds" : "Press Rec to start, then press it again to stop.");
        status.Cursor = HudNotice is not null ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
        Title = state switch { RunState.Recording => $"MiniTask — REC {time}", RunState.Playing => $"MiniTask — PLAY {p?.Repetition ?? 1}/{repeats}", RunState.Countdown => "MiniTask — Starting…", _ => dirty ? "MiniTask *" : "MiniTask" };
    }
    private static string FormatTime(long us)
    {
        var t = TimeSpan.FromMicroseconds(us);
        return t.TotalHours >= 1 ? ((int)t.TotalHours).ToString() + t.ToString("\\:mm\\:ss") : t.ToString("mm\\:ss");
    }
    private void ToggleRecord()
    {
        if (controller.Gate.State == RunState.Recording) { controller.Stop(); return; }
        if (Busy || dialogOpen) return;
        if (options.ContextMenu is { } menu) menu.IsOpen = false;
        dialogOpen = true;
        try
        {
            if (!settings.PrivacyAccepted)
            {
                MessageBox.Show(this, "Saved recordings include what you type—even passwords. Be careful when sharing them. MiniTask keeps everything on your computer.", "MiniTask · First recording", MessageBoxButton.OK, MessageBoxImage.Information);
                settings = settings with { PrivacyAccepted = true }; PersistSettings();
            }
            nint target = 0; WindowTarget? targetInfo = null;
            if (settings.CoordinateMode == CoordinateMode.Window)
            {
                var choice = SelectTarget(); if (choice is null) return;
                target = choice.Handle; targetInfo = choice.Target;
                DesktopEnvironment.Verify(target, targetInfo);
                if (!DesktopEnvironment.Activate(target)) throw new InvalidOperationException("Windows did not activate the selected window. Activate it yourself, then use the Record shortcut.");
            }
            var metadata = new Macro
            {
                Desktop = DesktopEnvironment.Bounds, Monitors = DesktopEnvironment.Monitors(), CoordinateMode = settings.CoordinateMode,
                Target = targetInfo, KeyboardLayout = DesktopEnvironment.LayoutFor(target != 0 ? target : controller.Input.LastExternalForeground), Playback = settings.Playback
            };
            uiError = null;
            controller.StartRecording(metadata, target);
        }
        catch (Exception ex) { Error(ex); }
        finally { dialogOpen = false; Refresh(); }
    }
    private void TogglePlay()
    {
        if (controller.Gate.State is RunState.Playing or RunState.Countdown) { controller.Stop(); return; }
        if (Busy || dialogOpen || controller.Macro is not { } macro) return;
        if (options.ContextMenu is { } menu) menu.IsOpen = false;
        dialogOpen = true;
        try
        {
            MacroStorage.Validate(macro);
            if (macro.Events.Any(e => e.Kind is InputKind.KeyDown or InputKind.KeyUp && new[] { settings.Hotkeys.Record, settings.Hotkeys.Play, settings.Hotkeys.Emergency }.Contains(e.VirtualKey)))
                throw new InvalidDataException("This macro uses a configured control key. Choose different hotkeys in Settings before playback.");
            if (DesktopEnvironment.Bounds != macro.Desktop || !DesktopEnvironment.Monitors().SequenceEqual(macro.Monitors))
                throw new InvalidOperationException("Display arrangement differs from the recording. Restore it or record a new macro.");
            nint target = macro.CoordinateMode == CoordinateMode.Window ? DesktopEnvironment.Resolve(macro.Target!) : controller.Input.LastExternalForeground;
            if (macro.CoordinateMode == CoordinateMode.Window)
            {
                var origin = DesktopEnvironment.ClientOrigin(target);
                foreach (var action in macro.Events.Where(e => e.Kind is not (InputKind.KeyDown or InputKind.KeyUp)))
                {
                    var p = Coordinates.Anchor(action.X, action.Y, origin.X, origin.Y);
                    Coordinates.Normalize(p.X, p.Y, macro.Desktop);
                    if (!macro.Monitors.Any(d => p.X >= d.Bounds.X && p.Y >= d.Bounds.Y && (long)p.X < (long)d.Bounds.X + d.Bounds.Width && (long)p.Y < (long)d.Bounds.Y + d.Bounds.Height))
                        throw new InvalidDataException("A window-relative mouse position falls outside a monitor. Move the target window fully onto the recorded desktop.");
                }
            }
            if (macro.KeyboardLayout != DesktopEnvironment.LayoutFor(target) && MessageBox.Show(this, "Keyboard layout differs from the recording. Physical keys may type different characters. Continue?", "Keyboard layout mismatch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (target != 0 && !DesktopEnvironment.Activate(target)) throw new InvalidOperationException("Windows refused target activation. Activate the target yourself and use the Play shortcut.");
            uiError = null;
            controller.Play(settings.Playback, macro.CoordinateMode == CoordinateMode.Window ? target : 0);
        }
        catch (Exception ex) { Error(ex); }
        finally { dialogOpen = false; Refresh(); }
    }
    private WindowChoice? SelectTarget()
    {
        var list = new ListBox { ItemsSource = DesktopEnvironment.Windows(), Margin = new(12), MinHeight = 180 };
        var accept = new Button { Content = "Use selected window", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new DockPanel { Margin = new(6) }; DockPanel.SetDock(accept, Dock.Bottom); panel.Children.Add(accept); panel.Children.Add(list);
        var dialog = new Window { Title = "MiniTask · Select a window", Owner = this, Width = 590, Height = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        accept.Click += (_, _) => { if (list.SelectedItem is not null) dialog.DialogResult = true; };
        return dialog.ShowDialog() == true ? list.SelectedItem as WindowChoice : null;
    }
    private async Task<bool> MayDiscard()
    {
        if (!dirty) return true;
        var answer = MessageBox.Show(this, "Save changes to the current macro?", "MiniTask · Unsaved recording", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && await Save();
    }
    private async Task Open(string? path = null)
    {
        if (Busy) return;
        bool previousDialog = dialogOpen; dialogOpen = true;
        try
        {
            if (!await MayDiscard()) return;
            if (path is null)
            {
                var dialog = new OpenFileDialog { Filter = "MiniTask macro (*.minitask)|*.minitask", CheckFileExists = true };
                if (dialog.ShowDialog(this) != true) return; path = dialog.FileName;
            }
            var loaded = await Task.Run(() => MacroStorage.Load(path));
            controller.Macro = loaded; filename = Path.GetFullPath(path); dirty = false; uiError = null;
            settings = settings with { Playback = loaded.Playback }; Recent(filename);
        }
        catch (Exception ex) { Error(ex); }
        finally { dialogOpen = previousDialog; Refresh(); }
    }
    private async Task<bool> Save()
    {
        if (controller.Macro is not { } macro) return true;
        bool previousDialog = dialogOpen; dialogOpen = true;
        try
        {
            string? path = filename;
            if (path is null)
            {
                var dialog = new SaveFileDialog { Filter = "MiniTask macro (*.minitask)|*.minitask", DefaultExt = ".minitask", FileName = "My macro.minitask" };
                if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName;
            }
            macro = macro with { Playback = settings.Playback, Name = Path.GetFileNameWithoutExtension(path) };
            await Task.Run(() => MacroStorage.Save(path, macro));
            controller.Macro = macro; filename = path; dirty = false; uiError = null; Recent(path); return true;
        }
        catch (Exception ex) { Error(ex); return false; }
        finally { dialogOpen = previousDialog; Refresh(); }
    }
    private void Recent(string path)
    {
        settings = settings with { Recent = new[] { path }.Concat(settings.Recent).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList() }; PersistSettings();
        open.ContextMenu = null;
    }
    private void PersistSettings() { try { savePreferences(settings); } catch (Exception ex) { Error(ex); } }
    private void PlaySound(bool starting) { if (settings.SoundCues && !closing) playSound(starting); }
    private void ShowSettings()
    {
        if (Busy || dialogOpen) return;
        dialogOpen = true;
        var dialog = new SettingsWindow(settings) { Owner = this };
        try
        {
            if (dialog.ShowDialog() == true)
            {
                settings = dialog.Result; PersistSettings();
                if (controller.Macro is not null) dirty = true;
            }
        }
        finally { dialogOpen = false; Refresh(); }
    }
    private async Task RestartElevated()
    {
        if (dirty && !await Save()) return;
        try
        {
            string exe = Environment.ProcessPath!;
            // A short-lived elevated copy waits for the mutex before starting the app.
            var info = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
            info.ArgumentList.Add("--wait-for-exit"); info.ArgumentList.Add(Environment.ProcessId.ToString());
            if (filename is not null) info.ArgumentList.Add(filename);
            Process.Start(info); Close();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Error(Exception ex) { uiError = ex.Message; Diagnostics.Failure("desktop", ex); MessageBox.Show(this, ex.Message, "MiniTask", MessageBoxButton.OK, MessageBoxImage.Warning); }
    private void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (finished) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        controller.Stop();
        await controller.WaitForStop();
        // Drain the recording-complete UI notification before prompting.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (!await MayDiscard()) { closing = false; return; }
        timer.Stop(); SystemEvents.UserPreferenceChanged -= PreferenceChanged;
        await controller.DisposeAsync(); tray.Dispose(); trayIcon.Dispose(); finished = true; Close();
    }
}
