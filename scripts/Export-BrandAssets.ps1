# Run with Windows PowerShell -STA. Renders the same vector used by the app.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path -Parent $PSScriptRoot
$brandPath = Join-Path $projectRoot 'src/CydLan.Desktop/Brand.xaml'
$assetDirectory = Join-Path $projectRoot 'assets/brand'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$reader = [System.Xml.XmlReader]::Create($brandPath)
try { $resources = [System.Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }

function Render-BrandPng([string]$ResourceName, [int]$Size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawImage($resources[$ResourceName], [System.Windows.Rect]::new(0, 0, $Size, $Size))
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = New-Object System.IO.MemoryStream
    try { $encoder.Save($memory); return ,$memory.ToArray() } finally { $memory.Dispose() }
}

[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'cydlan-mark.png'), (Render-BrandPng 'BrandMark' 512))
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'cydlan-icon.png'), (Render-BrandPng 'BrandAppIcon' 512))
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @($sizes | ForEach-Object { ,(Render-BrandPng 'BrandAppIcon' $_) })
$icon = [System.IO.File]::Create((Join-Path $assetDirectory 'cydlan.ico'))
$writer = [System.IO.BinaryWriter]::new($icon)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $edge = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$edge); $writer.Write([byte]$edge); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
}
finally { $writer.Dispose(); $icon.Dispose() }
Write-Output "Exported SVG companions, 512px PNGs and a seven-size Windows icon to $assetDirectory"
