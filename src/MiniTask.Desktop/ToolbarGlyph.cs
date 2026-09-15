namespace MiniTask.Desktop;

// Original compact vector artwork, drawn on a 24-pixel grid.
public sealed class ToolbarGlyph : FrameworkElement
{
    public string Kind { get; }
    private bool stopped;
    public bool Stopped { get => stopped; set { if (stopped != value) { stopped = value; InvalidateVisual(); } } }
    public ToolbarGlyph(string kind) { Kind = kind; Width = Height = 24; SnapsToDevicePixels = true; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        void Shape(string geometry, string fill, string outline)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill));
            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(outline)), 1);
            dc.DrawGeometry(brush, pen, Geometry.Parse(geometry));
        }
        if (stopped) { Shape("M5,5 H19 V19 H5 Z", "#BB3D35", "#87312D"); return; }
        switch (Kind)
        {
            case "Open":
                Shape("M2,6 H9 L11,8 H21 V20 H2 Z", "#DBB858", "#8E7131");
                Shape("M2,11 H23 L19,20 H2 Z", "#F3D582", "#8E7131");
                break;
            case "Save":
                Shape("M3,3 H18 L21,6 V21 H3 Z", "#628BA4", "#365A70");
                Shape("M7,3 H16 V10 H7 Z", "#DAE3E6", "#365A70");
                Shape("M7,14 H17 V21 H7 Z", "#FAF7EF", "#365A70");
                Shape("M13,4 H15 V8 H13 Z", "#628BA4", "#628BA4");
                break;
            case "Rec":
                dc.DrawEllipse(new LinearGradientBrush(Color.FromRgb(231, 104, 93), Color.FromRgb(171, 40, 36), 90), new Pen(new SolidColorBrush(Color.FromRgb(134, 42, 37)), 1), new Point(12, 12), 8, 8);
                dc.DrawArcHighlight();
                break;
            case "Play":
                Shape("M6,3 L21,12 6,21 Z", "#67A36A", "#386B3B");
                Shape("M8,6 L17,12 8,12 Z", "#89BD87", "#89BD87");
                break;
            case "Prefs":
                Shape("M14,3 C18,1 23,4 21,8 L17,6 14,9 16,12 C13,14 11,13 10,12 L4,21 1,18 9,10 C6,5 10,1 14,3 Z", "#A5A5A0", "#62625E");
                break;
        }
    }
}
internal static class GlyphHighlights
{
    internal static void DrawArcHighlight(this DrawingContext dc) => dc.DrawGeometry(null,
        new Pen(new SolidColorBrush(Color.FromArgb(165, 255, 220, 212)), 1), Geometry.Parse("M6,10 C7,6 10,5 13,6"));
}
