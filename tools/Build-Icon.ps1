param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
$brandFolder = Join-Path $projectRoot 'assets/brand'
[xml]$svg = Get-Content (Join-Path $brandFolder 'minitask.svg') -Raw
$drawing = [System.Windows.Media.DrawingGroup]::new()
foreach ($shape in $svg.DocumentElement.ChildNodes) {
    if ($shape.LocalName -ne 'path') { continue }
    $brush = $null
    $pen = $null
    if ($shape.fill -and $shape.fill -ne 'none') { $brush = [System.Windows.Media.BrushConverter]::new().ConvertFromInvariantString($shape.fill) }
    if ($shape.stroke) {
        $strokeBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromInvariantString($shape.stroke)
        $pen = [System.Windows.Media.Pen]::new($strokeBrush, [double]$shape.'stroke-width')
        $pen.StartLineCap = $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    }
    $drawing.Children.Add([System.Windows.Media.GeometryDrawing]::new($brush, $pen, [System.Windows.Media.Geometry]::Parse($shape.d)))
}
$drawing.Freeze()
function Render-Brand([int]$Size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($Size / 64.0, $Size / 64.0))
    $context.DrawDrawing($drawing)
    $context.Pop()
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    try { $encoder.Save($stream); return ,$stream.ToArray() }
    finally { $stream.Dispose() }
}
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @($sizes | ForEach-Object { ,(Render-Brand $_) })
$iconPath = Join-Path $projectRoot 'src/MiniTask.Desktop/MiniTask.ico'
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
}
finally { $writer.Dispose(); $file.Dispose() }
[System.IO.File]::WriteAllBytes((Join-Path $brandFolder 'minitask-1024.png'), (Render-Brand 1024))
[System.IO.File]::WriteAllBytes((Join-Path $brandFolder 'minitask-256.png'), $frames[-1])
Write-Output "Generated MiniTask.ico ($($sizes -join ', ') px) and branding PNGs from minitask.svg."
