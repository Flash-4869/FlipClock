param(
    [string]$ExePath = 'E:\ZCodeSpace\FlipClock\FlipClock.exe',
    [string]$WorkDir = 'E:\ZCodeSpace\FlipClock\shotsI',
    [int]$FrameCount = 36,
    [int]$FrameMs = 55
)
# Deterministic hero capture:
#   1. fullscreen TOPMOST dark panel covers everything (incl. any UI windows)
#   2. the clock is re-asserted as topmost AFTER the panel, so both sit in the
#      topmost band with the clock on top -> pure uniform background, real
#      rendered shadow, zero compositing
#   3. the cursor is moved out of the capture region
#   4. the capture region is verified to be pure panel color before shooting
# The screen goes dark for ~8 seconds; the panel auto-closes when done.
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class HeroKit3 {
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point p);

    public static IntPtr FindWindowByPid(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid == (uint)pid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void BringTopmost(IntPtr h) {
        // HWND_TOPMOST re-assert: moves the window to the top of the topmost band
        SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0040);
    }

    public static string ParkCursor(int w, int h) {
        Point old;
        GetCursorPos(out old);
        SetCursorPos(w - 8, h - 8);
        return old.X + "," + old.Y;
    }

    public static void RestoreCursor(string pos) {
        string[] p = pos.Split(',');
        SetCursorPos(int.Parse(p[0]), int.Parse(p[1]));
    }

    public static string Find(string path) {
        int stride, w, h;
        byte[] px = Load(path, out stride, out w, out h);
        int[] colBest = new int[w];
        for (int x = 0; x < w; x++) {
            int best = 0, run = 0, gap = 0;
            for (int y = 0; y < h; y++) {
                int o = y * stride + x * 4;
                bool wht = px[o+2] >= 235 && px[o+1] >= 235 && px[o] >= 235;
                if (wht) { run++; gap = 0; if (run > best) best = run; }
                else if (run > 0) { gap++; if (gap > 8) run = 0; }
            }
            colBest[x] = best;
        }
        List<int[]> clusters = new List<int[]>();
        int cs = -1;
        for (int x = 0; x <= w; x++) {
            bool q = x < w && colBest[x] >= 30;
            if (q && cs < 0) cs = x;
            if (!q && cs >= 0) { clusters.Add(new int[]{ cs, x - 1 }); cs = -1; }
        }
        List<object[]> glyphs = new List<object[]>();
        foreach (int[] cl in clusters) {
            if (cl[1] - cl[0] + 1 < 2) continue;
            int top = int.MaxValue, bot = -1;
            for (int x = cl[0]; x <= cl[1]; x++)
                for (int y = 0; y < h; y++) {
                    int o = y * stride + x * 4;
                    if (px[o+2] >= 235 && px[o+1] >= 235 && px[o] >= 235) {
                        if (y < top) top = y; if (y > bot) bot = y;
                    }
                }
            if (bot - top + 1 >= 28) glyphs.Add(new object[]{ cl[0], cl[1], top, bot });
        }
        if (glyphs.Count == 0) return "none";
        List<object[]> bestGroup = new List<object[]>();
        for (int i = 0; i < glyphs.Count; i++) {
            List<object[]> grp = new List<object[]>();
            for (int j = 0; j < glyphs.Count; j++) {
                object[] a = glyphs[i], b = glyphs[j];
                if (Math.Abs((int)a[2]-(int)b[2]) <= 6 && Math.Abs((int)a[3]-(int)b[3]) <= 6) grp.Add(b);
            }
            if (grp.Count > bestGroup.Count) bestGroup = grp;
        }
        int gTop = int.MaxValue, gBot = -1, rMinX = int.MaxValue, rMaxX = -1;
        foreach (object[] g in bestGroup) {
            if ((int)g[2] < gTop) gTop = (int)g[2];
            if ((int)g[3] > gBot) gBot = (int)g[3];
            if ((int)g[0] < rMinX) rMinX = (int)g[0];
            if ((int)g[1] > rMaxX) rMaxX = (int)g[1];
        }
        return rMinX + "," + gTop + "," + rMaxX + "," + gBot;
    }

    private static byte[] Load(string path, out int stride, out int width, out int height) {
        using (Bitmap bmp = new Bitmap(path)) {
            Rectangle rc = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bd = bmp.LockBits(rc, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            stride = bd.Stride; width = bmp.Width; height = bmp.Height;
            byte[] p = new byte[stride * height];
            Marshal.Copy(bd.Scan0, p, 0, p.Length);
            bmp.UnlockBits(bd);
            return p;
        }
    }

    // checks the outer ring of the image for uniformity against a sample color
    public static string VerifyRing(string path) {
        int stride, w, h;
        byte[] p = Load(path, out stride, out w, out h);
        int r0 = p[2], g0 = p[1], b0 = p[0]; // top-left pixel as reference
        int bad = 0;
        int m = 10;
        for (int y = 0; y < h; y++) {
            bool edgeRow = y < m || y >= h - m;
            for (int x = 0; x < w; x++) {
                if (!edgeRow && x >= m && x < w - m) continue;
                int o = y * stride + x * 4;
                if (Math.Abs(p[o+2]-r0) > 4 || Math.Abs(p[o+1]-g0) > 4 || Math.Abs(p[o]-b0) > 4) bad++;
            }
        }
        return "ref=" + r0 + "," + g0 + "," + b0 + " bad=" + bad;
    }
}
"@

if (Test-Path $WorkDir) { Remove-Item -Recurse -Force $WorkDir }
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

function Capture([int]$x, [int]$y, [int]$w, [int]$h, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# 1) clock with capture config
Stop-Process -Name FlipClock -Force -ErrorAction SilentlyContinue
$cfgDir = Join-Path $env:LOCALAPPDATA 'FlipClock'
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
Set-Content -Path (Join-Path $cfgDir 'settings.ini') -Value "topmost=True`nhour12=False`nshowseconds=True`nscale=1.0`nx=140`ny=120"
Start-Process -FilePath $ExePath -ArgumentList '/culture:en-US'
Start-Sleep -Milliseconds 2500
$proc = Get-Process FlipClock -ErrorAction Stop
$hwnd = [HeroKit3]::FindWindowByPid($proc.Id)
if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'ERROR: window-not-found'; exit 1 }

# 2) locate clock
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$fullPath = Join-Path $WorkDir 'full.png'
Capture $bounds.X $bounds.Y $bounds.Width $bounds.Height $fullPath
$bbox = [HeroKit3]::Find($fullPath)
if ($bbox -eq 'none') { Write-Output 'ERROR: clock-not-found'; exit 1 }
$parts = $bbox.Split(',')
$x0 = [int]$parts[0]; $y0 = [int]$parts[1]; $x1 = [int]$parts[2]; $y1 = [int]$parts[3]
$rx = [Math]::Max($bounds.X, $x0 - 55)
$ry = [Math]::Max($bounds.Y, $y0 - 45)
$rw = [Math]::Min($bounds.Width - ($rx - $bounds.X), ($x1 - $x0) + 110)
$rh = [Math]::Min($bounds.Height - ($ry - $bounds.Y), ($y1 - $y0) + 145)
Write-Output ("bbox=" + $bbox + " rect=" + $rx + "," + $ry + " " + $rw + "x" + $rh)

# 3) fullscreen topmost panel, then re-assert clock above it
$panel = New-Object System.Windows.Forms.Form
$panel.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$panel.WindowState = [System.Windows.Forms.FormWindowState]::Maximized
$panel.BackColor = [System.Drawing.Color]::FromArgb(16, 18, 27)
$panel.ShowInTaskbar = $false
$panel.TopMost = $true
$panel.Show()
$panel.Refresh()
for ($i = 0; $i - 16; $i++) {
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 40
}
[HeroKit3]::BringTopmost($hwnd)
Start-Sleep -Milliseconds 600

# 4) park the cursor away from the region
$cursorOld = [HeroKit3]::ParkCursor($bounds.Width, $bounds.Height)

try {
    # 5) verify the region ring is pure panel color before shooting
    $probe = Join-Path $WorkDir 'probe.png'
    Capture $rx $ry $rw $rh $probe
    $ring = [HeroKit3]::VerifyRing($probe)
    Write-Output ("ring-check " + $ring)
    if ($ring -match 'bad=([0-9]+)') {
        if ([int]$Matches[1] -gt 8) {
            Write-Output 'ERROR: background-not-pure-aborting'
            exit 1
        }
    }

    # 6) hero screenshot + frames
    Capture $rx $ry $rw $rh (Join-Path $WorkDir 'shot.png')
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $FrameCount; $i++) {
        Capture $rx $ry $rw $rh (Join-Path $WorkDir ("frame{0:d3}.png" -f $i))
        Start-Sleep -Milliseconds $FrameMs
    }
    $sw.Stop()
    $fps = [Math]::Round($FrameCount / $sw.Elapsed.TotalSeconds, 2)
    Write-Output ("fps=" + $fps)
} finally {
    [HeroKit3]::RestoreCursor($cursorOld)
    $panel.Close()
    $panel.Dispose()
}
Write-Output 'done'
