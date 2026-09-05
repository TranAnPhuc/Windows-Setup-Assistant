# Run with Windows PowerShell (WPF is built in); no external image libraries.
# The SVG is the single source of truth. Supports the simple rect/path/circle
# primitives used by AppIcon.svg; unexpected elements fail rather than disappear.
[CmdletBinding()]
param([string]$PreviewDirectory)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$assetDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/WindowsSetupAssistant.App/Assets'))
$svgPath = Join-Path $assetDirectory 'AppIcon.svg'
$icoPath = Join-Path $assetDirectory 'AppIcon.ico'
$readerSettings = New-Object System.Xml.XmlReaderSettings
$readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$reader = [System.Xml.XmlReader]::Create($svgPath, $readerSettings)
try {
    $svg = New-Object System.Xml.XmlDocument
    $svg.Load($reader)
} finally { $reader.Dispose() }

function Get-Number($element, $name, $fallback = 0) {
    if (!$element.HasAttribute($name)) { return [double]$fallback }
    return [double]::Parse($element.GetAttribute($name), [Globalization.CultureInfo]::InvariantCulture)
}
function Get-Brush($value) {
    if (!$value -or $value -eq 'none') { return $null }
    return (New-Object System.Windows.Media.BrushConverter).ConvertFromInvariantString($value)
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    try {
        $scale = $size / 256.0
        $drawing.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))
        foreach ($element in $svg.DocumentElement.ChildNodes) {
            if ($element -isnot [System.Xml.XmlElement]) { continue }
            if ($element.LocalName -in @('title', 'desc')) { continue }
            $fill = Get-Brush $element.GetAttribute('fill')
            $pen = $null
            if ($element.HasAttribute('stroke')) {
                $pen = New-Object System.Windows.Media.Pen((Get-Brush $element.GetAttribute('stroke')), (Get-Number $element 'stroke-width' 1))
                if ($element.GetAttribute('stroke-linecap') -eq 'round') {
                    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
                    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
                }
                if ($element.GetAttribute('stroke-linejoin') -eq 'round') {
                    $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
                }
            }
            switch ($element.LocalName) {
                'rect' {
                    $rect = New-Object System.Windows.Rect((Get-Number $element 'x'), (Get-Number $element 'y'), (Get-Number $element 'width'), (Get-Number $element 'height'))
                    $radius = Get-Number $element 'rx'
                    $drawing.DrawRoundedRectangle($fill, $pen, $rect, $radius, $radius)
                }
                'path' { $drawing.DrawGeometry($fill, $pen, [System.Windows.Media.Geometry]::Parse($element.GetAttribute('d'))) }
                'circle' {
                    $center = New-Object System.Windows.Point((Get-Number $element 'cx'), (Get-Number $element 'cy'))
                    $radius = Get-Number $element 'r'
                    $drawing.DrawEllipse($fill, $pen, $center, $radius, $radius)
                }
                default { throw "Unsupported SVG element: $($element.LocalName)" }
            }
        }
        $drawing.Pop()
    } finally { $drawing.Close() }
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    try {
        $encoder.Save($stream)
        $images += ,$stream.ToArray()
    } finally { $stream.Dispose() }
}

# ICO directory + one transparent PNG image per required size (Windows 10+).
$output = [IO.File]::Create($icoPath)
$writer = New-Object IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
if ($PreviewDirectory) {
    $previewPath = [IO.Path]::GetFullPath($PreviewDirectory)
    [IO.Directory]::CreateDirectory($previewPath) | Out-Null
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        [IO.File]::WriteAllBytes((Join-Path $previewPath "icon-$($sizes[$i]).png"), $images[$i])
    }
}
Write-Output "Generated $icoPath (16, 24, 32, 48, 64, 128, 256 px; transparent PNG frames)."
