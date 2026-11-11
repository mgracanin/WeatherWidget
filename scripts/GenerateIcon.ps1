param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Assets\Brand')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$svgPath = Join-Path $OutputDirectory 'weather-widget.svg'
$pngPath = Join-Path $OutputDirectory 'weather-widget-512.png'
$icoPath = Join-Path $OutputDirectory 'weather-widget.ico'

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="tile" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2875E8"/><stop offset="1" stop-color="#48A8FF"/></linearGradient>
    <radialGradient id="sunGlow"><stop offset="0.72" stop-color="#FFE78C" stop-opacity="0.52"/><stop offset="1" stop-color="#FFE78C" stop-opacity="0"/></radialGradient>
  </defs>
  <rect width="512" height="512" rx="112" fill="url(#tile)"/>
  <rect x="4" y="4" width="504" height="504" rx="108" fill="none" stroke="#FFFFFF" stroke-opacity="0.24" stroke-width="4"/>
  <circle cx="340" cy="156" r="94" fill="url(#sunGlow)"/>
  <circle cx="340" cy="156" r="68" fill="#FFD34E"/>
  <path d="M310 111c10-12 25-18 42-18 13 0 25 4 34 11-17-8-39-6-55 4-9 5-16 12-21 21z" fill="#FFF2AA" fill-opacity="0.8"/>
  <path d="M121 354h310c31 0 56-25 56-56 0-25-18-46-42-51 1-3 1-7 1-10 0-40-32-72-72-72-25 0-47 12-59 32-17-35-51-58-91-58-52 0-96 35-106 84-27 3-48 26-48 54 0 28 23 51 51 51z" transform="translate(0 8)" fill="#1455A8" fill-opacity="0.28"/>
  <path d="M121 354h310c31 0 56-25 56-56 0-25-18-46-42-51 1-3 1-7 1-10 0-40-32-72-72-72-25 0-47 12-59 32-17-35-51-58-91-58-52 0-96 35-106 84-27 3-48 26-48 54 0 28 23 51 51 51z" fill="#F8FBFF"/>
</svg>
'@
[System.IO.File]::WriteAllText($svgPath, $svg.Trim() + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

$cloudData = 'M121 354h310c31 0 56-25 56-56 0-25-18-46-42-51 1-3 1-7 1-10 0-40-32-72-72-72-25 0-47 12-59 32-17-35-51-58-91-58-52 0-96 35-106 84-27 3-48 26-48 54 0 28 23 51 51 51z'
$cloudGeometry = [System.Windows.Media.Geometry]::Parse($cloudData)

function New-Color([string]$Value) { return [System.Windows.Media.ColorConverter]::ConvertFromString($Value) }
function Render-IconPng([int]$Size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($Size / 512.0, $Size / 512.0))

    $tileBrush = [System.Windows.Media.LinearGradientBrush]::new()
    $tileBrush.StartPoint = [System.Windows.Point]::new(0, 0)
    $tileBrush.EndPoint = [System.Windows.Point]::new(1, 1)
    [void]$tileBrush.GradientStops.Add([System.Windows.Media.GradientStop]::new((New-Color '#2875E8'), 0))
    [void]$tileBrush.GradientStops.Add([System.Windows.Media.GradientStop]::new((New-Color '#48A8FF'), 1))
    $context.DrawRoundedRectangle($tileBrush, $null, [System.Windows.Rect]::new(0, 0, 512, 512), 112, 112)

    $edgePen = [System.Windows.Media.Pen]::new([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(61, 255, 255, 255)), 4)
    $context.DrawRoundedRectangle($null, $edgePen, [System.Windows.Rect]::new(4, 4, 504, 504), 108, 108)

    $glowBrush = [System.Windows.Media.RadialGradientBrush]::new()
    [void]$glowBrush.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromArgb(133, 255, 231, 140), 0.72))
    [void]$glowBrush.GradientStops.Add([System.Windows.Media.GradientStop]::new([System.Windows.Media.Color]::FromArgb(0, 255, 231, 140), 1))
    $context.DrawEllipse($glowBrush, $null, [System.Windows.Point]::new(340, 156), 94, 94)
    $context.DrawEllipse([System.Windows.Media.SolidColorBrush]::new((New-Color '#FFD34E')), $null, [System.Windows.Point]::new(340, 156), 68, 68)

    $shine = [System.Windows.Media.Geometry]::Parse('M310 111c10-12 25-18 42-18 13 0 25 4 34 11-17-8-39-6-55 4-9 5-16 12-21 21z')
    $context.DrawGeometry([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(204, 255, 242, 170)), $null, $shine)

    $context.PushTransform([System.Windows.Media.TranslateTransform]::new(0, 8))
    $context.DrawGeometry([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromArgb(71, 20, 85, 168)), $null, $cloudGeometry)
    $context.Pop()
    $context.DrawGeometry([System.Windows.Media.SolidColorBrush]::new((New-Color '#F8FBFF')), $null, $cloudGeometry)
    $context.Pop()
    $context.Close()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    [void]$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = foreach ($size in $sizes) { [pscustomobject]@{ Size = $size; Bytes = (Render-IconPng $size) } }
[System.IO.File]::WriteAllBytes($pngPath, (Render-IconPng 512))

$memory = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($memory)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frame.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $memory.ToArray())
$writer.Dispose()
$memory.Dispose()

Write-Output "Generated $pngPath"
Write-Output "Generated $icoPath with $($frames.Count) PNG frames ($($sizes -join ', '))"
