using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlipClockApp
{
    // System tray icon. The main window has no taskbar button, so this is the
    // control surface: right-click menu (hide/show, always-on-top, settings,
    // exit) and left-click to toggle visibility.
    internal class TrayIconService
    {
        private readonly NotifyIcon ni;
        private readonly IntPtr hIcon;
        private readonly ToolStripMenuItem miToggle;
        private readonly ToolStripMenuItem miTopmost;
        private DateTime lastLeftClick = DateTime.MinValue;

        public TrayIconService(Action toggleVisibility, Action toggleTopmost, Action openSettings, Action exitApp,
                               Func<bool> isWindowVisible, Func<bool> isTopmost)
        {
            hIcon = BuildIconHandle();
            Icon icon = hIcon != IntPtr.Zero ? Icon.FromHandle(hIcon) : SystemIcons.Application;

            ni = new NotifyIcon();
            ni.Text = Strings.AppTitle;
            ni.Icon = icon;
            ni.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();

            miToggle = new ToolStripMenuItem(Strings.HideClock);
            miToggle.Click += new EventHandler(delegate(object s, EventArgs e) { toggleVisibility(); });
            menu.Items.Add(miToggle);

            miTopmost = new ToolStripMenuItem(Strings.AlwaysOnTop);
            miTopmost.Click += new EventHandler(delegate(object s, EventArgs e) { toggleTopmost(); });
            menu.Items.Add(miTopmost);

            ToolStripMenuItem miCfg = new ToolStripMenuItem(Strings.Settings);
            miCfg.Click += new EventHandler(delegate(object s, EventArgs e) { openSettings(); });
            menu.Items.Add(miCfg);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miExit = new ToolStripMenuItem(Strings.Exit);
            miExit.Click += new EventHandler(delegate(object s, EventArgs e) { exitApp(); });
            menu.Items.Add(miExit);

            // refresh text and check state right before the menu shows
            menu.Opening += new CancelEventHandler(delegate(object s, CancelEventArgs e)
            {
                miToggle.Text = isWindowVisible() ? Strings.HideClock : Strings.ShowClock;
                miTopmost.Checked = isTopmost();
            });

            ni.ContextMenuStrip = menu;

            // left-click toggles visibility; swallow the second click of a
            // double-click so the state does not bounce back
            ni.MouseClick += new MouseEventHandler(delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                DateTime now = DateTime.Now;
                double sinceLast = (now - lastLeftClick).TotalMilliseconds;
                lastLeftClick = now;
                if (sinceLast >= SystemInformation.DoubleClickTime) toggleVisibility();
            });
        }

        public void Dispose()
        {
            ni.Visible = false;
            ni.Dispose();
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }

        // Draws a miniature flip card at runtime (dark rounded card, white 12,
        // fold line), so no .ico file is needed for the tray.
        private static IntPtr BuildIconHandle()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = TextRenderingHint.AntiAlias;

                        Rectangle rc = new Rectangle(2, 3, 28, 26);
                        using (LinearGradientBrush bg = new LinearGradientBrush(
                            rc, Color.FromArgb(64, 64, 64), Color.FromArgb(34, 34, 34), LinearGradientMode.Vertical))
                        using (GraphicsPath path = RoundRect(rc, 6))
                        {
                            g.FillPath(bg, path);
                        }

                        using (Font font = new Font("Segoe UI Black", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
                        using (SolidBrush white = new SolidBrush(Color.FromArgb(245, 245, 245)))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            g.DrawString("12", font, white, new RectangleF(2, 3, 28, 26), sf);
                        }

                        using (Pen fold = new Pen(Color.FromArgb(130, 0, 0, 0), 1.6f))
                        {
                            g.DrawLine(fold, 3f, 16f, 29f, 16f);
                        }
                    }
                    return bmp.GetHicon();
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        private static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            int d = rad * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
