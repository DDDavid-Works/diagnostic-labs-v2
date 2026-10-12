# Draws the Diagnostic Labs app icon (pale green tile, green microscope, yellow specimen dot)
# and writes src\DiagnosticLabs.Wpf\Assets\app.ico (PNG frames: 16, 24, 32, 48, 64, 128, 256)
# plus preview PNGs next to it. Run: powershell -STA -File tools\Make-AppIcon.ps1
Add-Type -AssemblyName PresentationCore, WindowsBase

$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'src\DiagnosticLabs.Wpf\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null

function Brush($hex) { $b = [Windows.Media.BrushConverter]::new().ConvertFromString($hex); $b.Freeze(); $b }
$green = Brush '#2F7D4F'; $tile = Brush '#F1F7F1'; $edge = Brush '#9DB8A2'; $yellow = Brush '#E1E100'

function Render([int]$size) {
    $dv = [Windows.Media.DrawingVisual]::new()
    $dc = $dv.RenderOpen()
    $k = $size / 180.0                                   # the design lives in a 180 x 180 tile
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($k, $k))

    $edgePen = [Windows.Media.Pen]::new($edge, 3); $edgePen.Freeze()
    $dc.DrawRoundedRectangle($tile, $edgePen, [Windows.Rect]::new(1.5, 1.5, 177, 177), 40, 40)

    $dc.PushTransform([Windows.Media.TranslateTransform]::new(20, 20))   # microscope: 100-unit box at 1.4x
    $dc.PushTransform([Windows.Media.ScaleTransform]::new(1.4, 1.4))

    $pen = [Windows.Media.Pen]::new($green, 6)
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'; $pen.Freeze()
    $dc.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse('M62,84 C88,84 88,50 52,38'))   # arm
    $dc.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse('M12,66 H44'))                  # stage
    $dc.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse('M20,84 H80'))                  # base

    $dc.PushTransform([Windows.Media.RotateTransform]::new(30, 40, 30))
    $dc.DrawRoundedRectangle($green, $null, [Windows.Rect]::new(33, 10, 14, 40), 3, 3)    # tube
    $dc.DrawRoundedRectangle($green, $null, [Windows.Rect]::new(36, 2, 8, 10), 2, 2)      # eyepiece
    $dc.DrawRoundedRectangle($green, $null, [Windows.Rect]::new(37, 50, 6, 8), 1.5, 1.5)  # objective
    $dc.Pop(); $dc.Pop(); $dc.Pop()

    $dc.DrawEllipse($yellow, $null, [Windows.Point]::new(111, 64), 9, 9)                  # specimen
    $dc.Pop()
    $dc.Close()

    $bmp = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($dv)
    $enc = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = [IO.MemoryStream]::new(); $enc.Save($ms); ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($s in $sizes) { , (Render $s) }

$ico = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ico)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $outDir 'app.ico'), $ico.ToArray())
[IO.File]::WriteAllBytes((Join-Path $outDir 'app-256.png'), $frames[$sizes.Count - 1])
[IO.File]::WriteAllBytes((Join-Path $outDir 'app-32.png'), $frames[2])
[IO.File]::WriteAllBytes((Join-Path $outDir 'app-16.png'), $frames[0])
"Wrote $outDir\app.ico"
