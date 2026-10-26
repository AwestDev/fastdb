# One-time download of the map engine files (needs internet). Safe to run again: existing files are kept.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$dest = Join-Path $PSScriptRoot 'src\FastDbExplorer.Wpf\Assets\Map'
$fonts = Join-Path $dest 'fonts\NotoSans'
New-Item -ItemType Directory -Force $fonts | Out-Null

Write-Host '[1/3] MapLibre GL JS ...'
foreach ($f in 'maplibre-gl.js', 'maplibre-gl.css') {
    if (-not (Test-Path (Join-Path $dest $f))) {
        Invoke-WebRequest "https://unpkg.com/maplibre-gl@5/dist/$f" -OutFile (Join-Path $dest $f)
    }
}

Write-Host '[2/3] Label fonts (Latin, Cyrillic, Arabic/Persian) ...'
$ranges = '0-255', '256-511', '512-767', '768-1023', '1024-1279', '1536-1791', '1792-2047', '8192-8447', '8448-8703',
          '64256-64511', '64512-64767', '65024-65279', '65280-65535'
foreach ($r in $ranges) {
    $file = Join-Path $fonts "$r.pbf"
    if (Test-Path $file) { continue }
    try {
        Invoke-WebRequest "https://fonts.openmaptiles.org/Noto%20Sans%20Regular/$r.pbf" -OutFile $file
    } catch {
        Remove-Item $file -ErrorAction SilentlyContinue
        Write-Host "  skipped range $r"
        if ($r -eq '0-255') { throw }   # without the basic Latin range no label can be drawn
    }
}

Write-Host '[3/3] Persian/Arabic text shaping plugin ...'
$rtl = Join-Path $dest 'mapbox-gl-rtl-text.min.js'
if (-not (Test-Path $rtl)) {
    try { Invoke-WebRequest 'https://unpkg.com/@mapbox/mapbox-gl-rtl-text@0.2.3/mapbox-gl-rtl-text.min.js' -OutFile $rtl }
    catch { Write-Host '  skipped (labels still work, Persian letters would just not be joined)' }
}
Write-Host "Done: $dest"
