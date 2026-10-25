param([string]$PngPath)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public static class DigitMeasure5 {
  private static bool IsWhite(byte[] p, int stride, int x, int y) {
    int o = y * stride + x * 4;
    return p[o + 2] >= 235 && p[o + 1] >= 235 && p[o] >= 235;
  }

  public static string Measure(string path) {
    using (Bitmap bmp = new Bitmap(path)) {
      Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
      BitmapData bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      int stride = bd.Stride; int w = bmp.Width; int h = bmp.Height;
      byte[] p = new byte[stride * h];
      Marshal.Copy(bd.Scan0, p, 0, p.Length);
      bmp.UnlockBits(bd);

      // per-column longest white run; bridge gaps <= 8px (fold line + cast shadow band crossing digits)
      int[] colBest = new int[w];
      for (int x = 0; x < w; x++) {
        int best = 0, run = 0, gap = 0;
        for (int y = 0; y < h; y++) {
          if (IsWhite(p, stride, x, y)) { run++; gap = 0; if (run > best) best = run; }
          else if (run > 0) { gap++; if (gap > 8) run = 0; }
        }
        colBest[x] = best;
      }

      // cluster qualifying columns (run >= 30)
      List<int[]> clusters = new List<int[]>();
      int cs = -1;
      for (int x = 0; x <= w; x++) {
        bool q = x < w && colBest[x] >= 30;
        if (q && cs < 0) cs = x;
        if (!q && cs >= 0) { clusters.Add(new int[] { cs, x - 1 }); cs = -1; }
      }

      // per-cluster white vertical extent; keep clusters at least 28px tall
      List<object[]> glyphs = new List<object[]>();
      foreach (int[] cl in clusters) {
        if (cl[1] - cl[0] + 1 < 2) continue;
        int top = int.MaxValue, bot = -1;
        for (int x = cl[0]; x <= cl[1]; x++) {
          for (int y = 0; y < h; y++) {
            if (IsWhite(p, stride, x, y)) { if (y < top) top = y; if (y > bot) bot = y; }
          }
        }
        if (bot - top + 1 >= 28) glyphs.Add(new object[] { cl[0], cl[1], top, bot });
      }
      StringBuilder dbg = new StringBuilder();
      dbg.AppendLine("clusters=" + clusters.Count + " glyphs=" + glyphs.Count);

      if (glyphs.Count == 0) return dbg.ToString() + "no-glyph-found";

      // group glyphs by similar vertical extent; keep largest group (the digit row)
      List<object[]> bestGroup = new List<object[]>();
      for (int i = 0; i < glyphs.Count; i++) {
        List<object[]> grp = new List<object[]>();
        for (int j = 0; j < glyphs.Count; j++) {
          object[] a = glyphs[i], b = glyphs[j];
          if (Math.Abs((int)a[2] - (int)b[2]) <= 6 && Math.Abs((int)a[3] - (int)b[3]) <= 6) grp.Add(b);
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

      // fold line = row with most near-black pixels within the digit row x-range
      int foldY = -1, foldCnt = -1;
      for (int y = gTop + 5; y <= gBot - 5; y++) {
        int cnt = 0;
        for (int x = rMinX; x <= rMaxX; x++) {
          int o = y * stride + x * 4;
          if (p[o + 2] < 28 && p[o + 1] < 28 && p[o] < 28) cnt++;
        }
        if (cnt > foldCnt) { foldCnt = cnt; foldY = y; }
      }
      if (foldY < 0) return dbg.ToString() + "no-fold-found";

      double center = (gTop + gBot) / 2.0;
      double off = center - foldY;
      StringBuilder sb = new StringBuilder();
      sb.Append(dbg);
      sb.AppendLine("glyphGroupCount=" + bestGroup.Count + " x=" + rMinX + ".." + rMaxX + " glyphH=" + (gBot - gTop + 1));
      sb.AppendLine("glyphY=" + gTop + ".." + gBot + " center=" + center.ToString("F1"));
      sb.AppendLine("foldY=" + foldY + " dark=" + foldCnt);
      sb.AppendLine("offset=" + off.ToString("F1") + "px (positive=digit low, negative=digit high, ideal=0)");
      return sb.ToString();
    }
  }
}
"@

[DigitMeasure5]::Measure($PngPath)
