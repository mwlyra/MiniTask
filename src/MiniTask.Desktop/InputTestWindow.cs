using System.Windows.Input;

namespace MiniTask.Desktop;

public sealed class InputTestWindow : Window
{
    private readonly ListBox events = new() { Height = 140 };
    private readonly TextBlock counts = new();
    private int down, up, moves, buttons, wheels;
    public InputTestWindow()
    {
        Style = (Style)FindResource(typeof(Window));
        Title = "MiniTask · Input test"; Width = 640; Height = 520;
        var root = new StackPanel { Margin = new(16) };
        root.Children.Add(new TextBlock { Text = "Local input test", FontSize = 20, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "Type below, scroll, or drag the square. This window shows received input only; its contents are not saved.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8) });
        root.Children.Add(new TextBox { AcceptsReturn = true, Height = 72, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var canvas = new Canvas { Height = 100, Background = Brushes.LightSteelBlue, Margin = new(3) };
        var square = new System.Windows.Shapes.Rectangle { Width = 38, Height = 38, Fill = Brushes.RoyalBlue, Cursor = Cursors.SizeAll };
        Canvas.SetLeft(square, 15); Canvas.SetTop(square, 28); canvas.Children.Add(square); root.Children.Add(canvas);
        square.MouseLeftButtonDown += (_, e) => { square.CaptureMouse(); e.Handled = true; };
        square.MouseMove += (_, e) => { if (square.IsMouseCaptured) { var p = e.GetPosition(canvas); Canvas.SetLeft(square, Math.Clamp(p.X - 19, 0, canvas.ActualWidth - 38)); Canvas.SetTop(square, Math.Clamp(p.Y - 19, 0, 62)); } };
        square.MouseLeftButtonUp += (_, _) => square.ReleaseMouseCapture();
        root.Children.Add(counts); root.Children.Add(events);
        PreviewKeyDown += (_, e) => { down++; Log($"Key down: {e.Key} · repeat {e.IsRepeat}"); };
        PreviewKeyUp += (_, e) => { up++; Log($"Key up: {e.Key}"); };
        PreviewMouseDown += (_, e) => { buttons++; Log($"Button down: {e.ChangedButton}"); };
        PreviewMouseUp += (_, e) => { buttons++; Log($"Button up: {e.ChangedButton}"); };
        PreviewMouseWheel += (_, e) => { wheels++; Log($"Wheel: {e.Delta}"); };
        PreviewMouseMove += (_, _) => { moves++; Count(); };
        var clear = new Button { Content = "Clear counters", HorizontalAlignment = HorizontalAlignment.Right };
        clear.Click += (_, _) => { events.Items.Clear(); down = up = moves = buttons = wheels = 0; Count(); }; root.Children.Add(clear);
        Content = root; Count();
    }
    private void Log(string value) { events.Items.Insert(0, value); if (events.Items.Count > 100) events.Items.RemoveAt(100); Count(); }
    private void Count() => counts.Text = $"Keys ↓ {down}  ↑ {up}     Mouse moves {moves}     Buttons {buttons}     Wheels {wheels}";
}
