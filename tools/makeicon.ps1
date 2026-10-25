param([string]$OutPath)
# Generates app.ico: a miniature flip card drawn at 16/32/48/256 px, packed
# into an ICO container with PNG entries (supported since Windows Vista).
Add-Type -AssemblyName System.Drawing

function Draw-Card([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    $x = [Math]::Max(1, [int]($s * 0.0625))
    $y = [Math]::Max(1, [int]($s * 0.09))
    $w = $s - 2 * $x
    $h = $s - $y - [int]($s * 0.0625)
    $rc = New-Object System.Drawing.Rectangle($x, $y, $w, $h)

    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rc,
        [System.Drawing.Color]::FromArgb(64, 64, 64),
        [System.Drawing.Color]::FromArgb(34, 34, 34),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $rad = [Math]::Max(2, [int]($s * 0.1875))
    $d = $rad * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rc.X, $rc.Y, $d, $d, 180, 90)
    $path.AddArc($rc.Right - $d, $rc.Y, $d, $d, 270, 90)
    $path.AddArc($rc.Right - $d, $rc.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rc.X, $rc.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bg, $path)
    $bg.Dispose()
    $path.Dispose()

    $fontSize = [Math]::Max(6.0, [double]([int]($s * 0.40625)))
    $font = New-Object System.Drawing.Font("Segoe UI Black", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 245, 245))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("12", $font, $white, (New-Object System.Drawing.RectangleF($rc.X, $rc.Y, $rc.Width, $rc.Height)), $sf)
    $font.Dispose()
    $white.Dispose()
    $sf.Dispose()

    $foldY = $rc.Y + $rc.Height / 2.0
    $foldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(130, 0, 0, 0), [Math]::Max(1.0, $s / 20.0))
    $g.DrawLine($foldPen, [float]($rc.X + 1), [float]$foldY, [float]($rc.Right - 1), [float]$foldY)
    $foldPen.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$blobs = @()
foreach ($s in $sizes) {
    $bmp = Draw-Card $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $blobs += ,@($s, $ms.ToArray())
    $ms.Dispose()
}

$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$blobs.Count)
$offset = 6 + 16 * $blobs.Count
$entries = @()
foreach ($b in $blobs) {
    $s = $b[0]
    $data = $b[1]
    $entries += ,@($s, $data)
    $dim = 0
    if ($s -lt 256) { $dim = $s }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($e in $entries) {
    $bw.Write($e[1])
}
$bw.Flush()
$bw.Close()
$fs.Dispose()
Write-Output ("icon written: " + $OutPath)
