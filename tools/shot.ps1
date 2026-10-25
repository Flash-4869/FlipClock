param([string]$OutDir)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

function Shot([string]$name) {
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $bmp.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose()
  $bmp.Dispose()
}

Shot "shot_idle.png"
$now = Get-Date
$sleep = 1050 - $now.Millisecond
if ($sleep -lt 30) { $sleep += 1000 }
Start-Sleep -Milliseconds $sleep
Shot "shot_fold.png"
Start-Sleep -Milliseconds 200
Shot "shot_unfold.png"
Write-Output "done"
