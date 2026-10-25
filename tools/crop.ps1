param([string]$SrcPath, [string]$OutDir)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile($SrcPath)
$h = $src.Height
$w = $src.Width

# bottom taskbar strip (height 70)
$y1 = $h - 70
$strip = New-Object System.Drawing.Bitmap($w, 70)
$g = [System.Drawing.Graphics]::FromImage($strip)
$g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $w, 70)), (New-Object System.Drawing.Rectangle(0, $y1, $w, 70)), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$strip.Save((Join-Path $OutDir "taskbar_strip.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$strip.Dispose()

# bottom-right tray corner (500x90)
$x2 = $w - 500
$y2 = $h - 90
$corner = New-Object System.Drawing.Bitmap(500, 90)
$g2 = [System.Drawing.Graphics]::FromImage($corner)
$g2.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, 500, 90)), (New-Object System.Drawing.Rectangle($x2, $y2, 500, 90)), [System.Drawing.GraphicsUnit]::Pixel)
$g2.Dispose()
$corner.Save((Join-Path $OutDir "tray_corner.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$corner.Dispose()

# top-left clock region (700x300)
$topleft = New-Object System.Drawing.Bitmap(700, 300)
$g3 = [System.Drawing.Graphics]::FromImage($topleft)
$g3.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, 700, 300)), (New-Object System.Drawing.Rectangle(0, 0, 700, 300)), [System.Drawing.GraphicsUnit]::Pixel)
$g3.Dispose()
$topleft.Save((Join-Path $OutDir "clock_topleft.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$topleft.Dispose()

$src.Dispose()
Write-Output "cropped"
