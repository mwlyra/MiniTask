namespace MiniTask.Desktop;

public sealed partial class MainWindow
{
    private void ShowPreferences()
    {
        if (Busy || dialogOpen) return;
        options.ContextMenu = BuildPreferencesMenu();
        options.ContextMenu.PlacementTarget = options;
        options.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        options.ContextMenu.IsOpen = true;
    }
    private ContextMenu BuildPreferencesMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string name, Action? action = null, bool? selected = null)
        {
            var item = new MenuItem { Header = name };
            if (selected.HasValue) { item.IsCheckable = true; item.IsChecked = selected.Value; }
            if (action is not null) item.Click += (_, e) => { e.Handled = true; action(); };
            return item;
        }
        var speed = Item("Playback speed");
        foreach (double value in new[] { .25, .5, 1, 2, 4 })
            speed.Items.Add(Item(value == 1 ? "Normal speed (1×)" : $"{value:0.##}×", () => SetPlayback(settings.Playback with { Speed = value }), settings.Playback.Speed == value));
        speed.Items.Add(new Separator());
        speed.Items.Add(Item("Custom speed…", () => PromptNumber("Playback speed", "Playback speed (×)", settings.Playback.Speed, .05, 20, false, n => SetPlayback(settings.Playback with { Speed = n }))));
        menu.Items.Add(speed);
        var repeat = Item("Repeat playback");
        foreach (int value in new[] { 1, 5, 10 })
            repeat.Items.Add(Item(value == 1 ? "Play once" : $"{value} times", () => SetPlayback(settings.Playback with { Repetitions = value, Continuous = false }), !settings.Playback.Continuous && settings.Playback.Repetitions == value));
        repeat.Items.Add(Item("Set repeat count…", () => PromptNumber("Repeat playback", "Number of times", settings.Playback.Repetitions, 1, 1_000_000, true, n => SetPlayback(settings.Playback with { Repetitions = (int)n, Continuous = false }))));
        repeat.Items.Add(new Separator());
        repeat.Items.Add(Item("Continuous playback", () => SetPlayback(settings.Playback with { Continuous = !settings.Playback.Continuous }), settings.Playback.Continuous));
        menu.Items.Add(repeat);
        var countdown = Item("Start delay");
        foreach (int seconds in new[] { 0, 1, 3, 5 }) countdown.Items.Add(Item(seconds == 0 ? "Start immediately" : $"{seconds} seconds", () => SetPlayback(settings.Playback with { CountdownSeconds = seconds }), settings.Playback.CountdownSeconds == seconds));
        countdown.Items.Add(Item("Custom delay…", () => PromptNumber("Start delay", "Seconds before playback", settings.Playback.CountdownSeconds, 0, 60, true, n => SetPlayback(settings.Playback with { CountdownSeconds = (int)n }))));
        menu.Items.Add(countdown);
        menu.Items.Add(new Separator());
        var keys = Item("Hotkeys");
        foreach (var binding in new[] { ("Record", 1, settings.Hotkeys.Record), ("Play", 2, settings.Hotkeys.Play), ("Emergency stop", 3, settings.Hotkeys.Emergency) })
        {
            var group = Item($"{binding.Item1} ({Hotkeys.Label(binding.Item3)})");
            foreach (int key in Enumerable.Range(0x75, 19))
            {
                int value = key;
                group.Items.Add(Item(Hotkeys.Label(value), async () => await SetHotkey(binding.Item2, value), binding.Item3 == value));
            }
            keys.Items.Add(group);
        }
        menu.Items.Add(keys);
        menu.Items.Add(Item("Always on top", () => { settings = settings with { AlwaysOnTop = !settings.AlwaysOnTop }; Topmost = settings.AlwaysOnTop; PersistSettings(); }, settings.AlwaysOnTop));
        menu.Items.Add(Item("Show button captions", () => { settings = settings with { ShowCaptions = !settings.ShowCaptions }; PersistSettings(); Refresh(); }, settings.ShowCaptions));
        var theme = Item("Appearance");
        foreach (string value in new[] { "Light", "Dark", "System" }) theme.Items.Add(Item(value, () => { settings = settings with { Theme = value }; App.ApplyTheme(value); PersistSettings(); }, settings.Theme == value));
        menu.Items.Add(theme);
        menu.Items.Add(Item("Advanced…", ShowSettings));
        menu.Items.Add(new Separator());
        var recent = Item("Recent recordings");
        foreach (string path in settings.Recent) recent.Items.Add(Item(Path.GetFileName(path), async () => await Open(path)));
        recent.IsEnabled = recent.Items.Count > 0; menu.Items.Add(recent);
        menu.Items.Add(Item("Clear recording…", async () => await ClearRecording()));
        var tools = Item("Tools");
        tools.Items.Add(Item("Input-test window", () =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Arguments = "--input-test" }); }
            catch (Exception ex) { Error(ex); }
        }));
        tools.Items.Add(Item("Restart as administrator…", async () => await RestartElevated())); menu.Items.Add(tools);
        menu.Items.Add(Item("Help", () => MessageBox.Show(this, SettingsWindow.Guide, "MiniTask · Quick help", MessageBoxButton.OK, MessageBoxImage.Information)));
        return menu;
    }
    private void SetPlayback(PlaybackOptions value)
    {
        if (Busy) return;
        MacroStorage.ValidateOptions(value);
        settings = settings with { Playback = value };
        if (controller.Macro is not null) dirty = true;
        PersistSettings(); Refresh();
    }
    private void PromptNumber(string title, string label, double current, double min, double max, bool integer, Action<double> commit)
    {
        if (Busy || dialogOpen) return;
        dialogOpen = true;
        try { if (NumberPrompt.Show(this, title, label, current, min, max, integer) is { } number) commit(number); }
        finally { dialogOpen = false; Refresh(); }
    }
    private async Task SetHotkey(int action, int key)
    {
        if (Busy || dialogOpen) return;
        dialogOpen = true;
        try
        {
            var hotkeys = action switch { 1 => settings.Hotkeys with { Record = key }, 2 => settings.Hotkeys with { Play = key }, _ => settings.Hotkeys with { Emergency = key } };
            await controller.Input.Configure(hotkeys); settings = settings with { Hotkeys = hotkeys }; warning = null; PersistSettings();
        }
        catch (Exception ex) { Error(ex); }
        finally { dialogOpen = false; Refresh(); }
    }
    private async Task ClearRecording()
    {
        if (Busy || dialogOpen) return;
        dialogOpen = true;
        try { if (await MayDiscard()) { controller.Macro = null; filename = null; dirty = false; uiError = null; } }
        finally { dialogOpen = false; Refresh(); }
    }
}
