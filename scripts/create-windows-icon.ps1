$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$output = Join-Path (Get-Location) 'assets\icon.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngImages = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
  $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $graphics.ScaleTransform($size / 64.0, $size / 64.0)

  $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 52, 66))
  $rounded = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $rounded.AddArc(0, 0, 32, 32, 180, 90)
  $rounded.AddArc(32, 0, 32, 32, 270, 90)
  $rounded.AddArc(32, 32, 32, 32, 0, 90)
  $rounded.AddArc(0, 32, 32, 32, 90, 90)
  $rounded.CloseFigure()
  $graphics.FillPath($background, $rounded)

  $shield = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $shield.StartFigure()
  $shield.AddLine(32, 9, 51, 16)
  $shield.AddLine(51, 16, 51, 30)
  $shield.AddBezier(51, 30, 51, 42, 44, 51, 32, 57)
  $shield.AddBezier(32, 57, 20, 51, 13, 42, 13, 30)
  $shield.AddLine(13, 30, 13, 16)
  $shield.AddLine(13, 16, 32, 9)
  $shield.CloseFigure()
  $shieldPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 139, 213, 177), 2.8)
  $shieldPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $graphics.DrawPath($shieldPen, $shield)

  $pulse = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $pulse.StartFigure()
  $pulse.AddLine(17, 33, 27, 33)
  $pulse.AddLine(27, 33, 32, 23)
  $pulse.AddLine(32, 23, 39, 42)
  $pulse.AddLine(39, 42, 44, 33)
  $pulse.AddLine(44, 33, 49, 33)
  $pulsePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 217, 239, 255), 3)
  $pulsePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pulsePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pulsePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $graphics.DrawPath($pulsePen, $pulse)

  $pngStream = [System.IO.MemoryStream]::new()
  $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngImages.Add($pngStream.ToArray())

  $pngStream.Dispose()
  $pulsePen.Dispose()
  $pulse.Dispose()
  $shieldPen.Dispose()
  $shield.Dispose()
  $rounded.Dispose()
  $background.Dispose()
  $graphics.Dispose()
  $bitmap.Dispose()
}

$directoryLength = 6 + 16 * $sizes.Count
$offset = $directoryLength
$stream = [System.IO.File]::Create($output)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
  $size = $sizes[$i]
  $image = $pngImages[$i]
  $dimension = if ($size -eq 256) { [byte]0 } else { [byte]$size }
  $writer.Write($dimension)
  $writer.Write($dimension)
  $writer.Write([byte]0)
  $writer.Write([byte]0)
  $writer.Write([UInt16]1)
  $writer.Write([UInt16]32)
  $writer.Write([UInt32]$image.Length)
  $writer.Write([UInt32]$offset)
  $offset += $image.Length
}

foreach ($image in $pngImages) { $writer.Write($image) }
$writer.Dispose()
$stream.Dispose()
Write-Output "Created $output"

