using System.Windows.Media.Imaging;

namespace MiniTask.Desktop;

internal static class BrandIcon
{
    private static readonly Uri Resource = new("pack://application:,,,/MiniTask;component/MiniTask.ico");
    public static ImageSource Window { get; } = BitmapFrame.Create(Resource);
    public static System.Drawing.Icon CreateTrayIcon()
    {
        using var stream = Application.GetResourceStream(Resource).Stream;
        using var icon = new System.Drawing.Icon(stream, new System.Drawing.Size(GetSystemMetrics(49), GetSystemMetrics(50)));
        return (System.Drawing.Icon)icon.Clone();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
