# Draws the prototypes' stand-in icon (crane, 2026-11-17, for crcrewso: "I need a stand-in icon for this project"):
# a teal tile with a measured beam profile in white and a calculated one in orange over it, the two curves this
# project compares. Writes dsc.png (256 px) and dsc.ico (16-256 px, PNG entries) beside this script.
# Windows PowerShell or PowerShell 7 on Windows (System.Drawing). Run: .\Make-Icon.ps1
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $s = $size / 256.0
    function P([double]$x, [double]$y) { New-Object System.Drawing.PointF ([float]($x * $s)), ([float]($y * $s)) }

    # The tile: a rounded square, teal, darker at the bottom.
    $r = 48 * $s; $w = $size - 1
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tile.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90); $tile.AddArc($w - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $tile.AddArc($w - 2 * $r, $w - 2 * $r, 2 * $r, 2 * $r, 0, 90); $tile.AddArc(0, $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $tile.CloseFigure()
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush (P 0 0), (P 0 256), ([System.Drawing.Color]::FromArgb(255, 22, 176, 190)), ([System.Drawing.Color]::FromArgb(255, 10, 104, 122))
    $g.FillPath($fill, $tile)

    # The measured profile: low tails, steep penumbrae, a flat top with small horns; filled white.
    [System.Drawing.PointF[]]$measured = @((P 26 206), (P 60 200), (P 74 190), (P 84 96), (P 96 76), (P 128 82), (P 160 76), (P 172 96), (P 182 190), (P 196 200), (P 230 206))
    $area = New-Object System.Drawing.Drawing2D.GraphicsPath
    # The area under it runs to the tile's edges (clipped to the tile), so it reads as a curve, not a shape.
    [System.Drawing.PointF[]]$wide = @((P -10 208)) + $measured + @((P 266 208))
    $area.AddCurve($wide, [float]0.35); $area.AddLine((P 266 208), (P 266 266)); $area.AddLine((P 266 266), (P -10 266)); $area.CloseFigure()
    # Pale cyan rather than white, so the tile's lower edge still shows on a white background.
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 236, 241))
    $g.SetClip($tile); $g.FillPath($white, $area)

    # The calculated profile: an orange line a little wider and flatter, so the two visibly differ.
    [System.Drawing.PointF[]]$calculated = @((P -10 204), (P 26 202), (P 58 196), (P 70 186), (P 80 100), (P 94 84), (P 128 88), (P 162 84), (P 176 100), (P 186 186), (P 198 196), (P 230 202), (P 266 204))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 140, 26)), ([float][math]::Max(1.5, 14 * $s))
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawCurve($pen, $calculated, [float]0.35)

    $g.Dispose(); $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($n in $sizes) { $b = New-IconBitmap $n; $ms = New-Object System.IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); if ($n -eq 256) { $b.Save((Join-Path $here 'dsc.png'), [System.Drawing.Imaging.ImageFormat]::Png) }; $b.Dispose(); , $ms.ToArray() }

# An .ico with PNG entries (Windows Vista and later read these): header, one directory entry per size, then the PNGs.
$out = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]; $dim = if ($n -ge 256) { 0 } else { $n }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); [System.IO.File]::WriteAllBytes((Join-Path $here 'dsc.ico'), $out.ToArray())
"wrote dsc.png and dsc.ico ($($sizes -join ', ') px) in $here"
