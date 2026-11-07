# Draws the app icon (a usage gauge) and packs it into a multi-size .ico file.
param([Parameter(Mandatory)][string]$OutFile)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-GaugePng([int]$Size) {
    $bitmap = New-Object Drawing.Bitmap $Size, $Size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'

    $radius = [float]($Size * 0.22)
    $d = $radius * 2
    $max = [float]($Size - 1)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($max - $d, 0, $d, $d, 270, 90)
    $path.AddArc($max - $d, $max - $d, $d, $d, 0, 90)
    $path.AddArc(0, $max - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $background = New-Object Drawing.Drawing2D.LinearGradientBrush(
        (New-Object Drawing.Point 0, 0), (New-Object Drawing.Point 0, $Size),
        [Drawing.Color]::FromArgb(58, 50, 46), [Drawing.Color]::FromArgb(26, 24, 23))
    $g.FillPath($background, $path)

    # Thicker strokes at small sizes keep the gauge readable.
    $stroke = [float][Math]::Max(2.5, $Size * 0.13)
    $inset = [float]($Size * 0.18 + $stroke / 2)
    $arc = [float]($Size - 2 * $inset)
    $track = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(84, 76, 72)), $stroke
    $value = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(222, 110, 73)), $stroke
    foreach ($pen in @($track, $value)) {
        $pen.StartCap = 'Round'
        $pen.EndCap = 'Round'
    }
    $g.DrawArc($track, $inset, $inset, $arc, $arc, 135, 270)
    $g.DrawArc($value, $inset, $inset, $arc, $arc, 135, 180)

    $center = [float]($Size / 2)
    $needle = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(245, 240, 235)), ([float][Math]::Max(1.5, $Size * 0.06))
    $needle.StartCap = 'Round'
    $needle.EndCap = 'Round'
    $length = $arc * 0.3
    $angle = 315 * [Math]::PI / 180
    $g.DrawLine($needle, $center, $center, [float]($center + $length * [Math]::Cos($angle)), [float]($center + $length * [Math]::Sin($angle)))

    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose(); $path.Dispose(); $background.Dispose()
    $track.Dispose(); $value.Dispose(); $needle.Dispose()
    , $stream.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) { , (New-GaugePng $size) }

$file = [IO.File]::Create($OutFile)
$writer = New-Object IO.BinaryWriter $file
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Dispose()
