using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace FlipClockApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplyCultureOverride();
            System.Windows.Forms.Application.EnableVisualStyles(); // themed tray menu
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            app.Run(new MainWindow());
        }

        // Optional start argument: /culture:en-US overrides the UI language
        // and date format for this run.
        private static void ApplyCultureOverride()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i].Length > 9 &&
                        args[i].StartsWith("/culture:", StringComparison.OrdinalIgnoreCase))
                    {
                        CultureInfo c = CultureInfo.GetCultureInfo(args[i].Substring(9));
                        Thread.CurrentThread.CurrentCulture = c;
                        Thread.CurrentThread.CurrentUICulture = c;
                        CultureInfo.DefaultThreadCurrentCulture = c;
                        CultureInfo.DefaultThreadCurrentUICulture = c;
                    }
                }
            }
            catch { }
        }
    }

    // Persisted to %LOCALAPPDATA%\FlipClock\settings.ini
    internal class Settings
    {
        public const double ScaleMin = 0.4;
        public const double ScaleMax = 1.6;
        public const double ScaleDefault = 0.8;

        public bool Topmost = false;
        public bool Hour12 = false;
        public bool ShowSeconds = true;
        public double Scale = ScaleDefault;
        public double? X = null;
        public double? Y = null;

        private static string FilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipClock", "settings.ini");
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string p = FilePath();
                if (!File.Exists(p)) return s;
                string[] lines = File.ReadAllLines(p);
                for (int li = 0; li < lines.Length; li++)
                {
                    string line = lines[li];
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    string v = line.Substring(i + 1).Trim();
                    double d;
                    bool b;
                    if (k == "topmost" && bool.TryParse(v, out b)) s.Topmost = b;
                    else if (k == "hour12" && bool.TryParse(v, out b)) s.Hour12 = b;
                    else if (k == "showseconds" && bool.TryParse(v, out b)) s.ShowSeconds = b;
                    else if (k == "scale" && double.TryParse(v, out d)) s.Scale = d;
                    else if (k == "x" && double.TryParse(v, out d)) s.X = d;
                    else if (k == "y" && double.TryParse(v, out d)) s.Y = d;
                }
                if (s.Scale < ScaleMin) s.Scale = ScaleMin;
                if (s.Scale > ScaleMax) s.Scale = ScaleMax;
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string p = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                string[] lines = new string[]
                {
                    "topmost=" + Topmost,
                    "hour12=" + Hour12,
                    "showseconds=" + ShowSeconds,
                    "scale=" + Scale.ToString("0.##"),
                    "x=" + (X.HasValue ? X.Value.ToString("0.#") : ""),
                    "y=" + (Y.HasValue ? Y.Value.ToString("0.#") : "")
                };
                File.WriteAllLines(p, lines);
            }
            catch { }
        }
    }

    internal class MainWindow : Window
    {
        private const double CardW = 96.0;
        private const double CardH = 136.0;
        private const double CardRadius = 9.0;
        private const double DigitFontSize = 118.0;
        private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string RunValueName = "FlipClock";

        private static readonly FontFamily DigitFontFamily = new FontFamily("Segoe UI Black, Arial Black, Segoe UI");
        private static readonly FontFamily TextFontFamily = new FontFamily("Segoe UI Light, Segoe UI");

        private readonly Settings cfg;
        private readonly FlipDigit[] digits = new FlipDigit[6];
        private readonly TextBlock dateText;
        private readonly TextBlock ampmText;
        private readonly FrameworkElement ampmHost;
        private readonly FrameworkElement colon1;
        private readonly FrameworkElement colon2;
        private readonly StackPanel contentPanel;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly TrayIconService tray;
        private string lastTime = null;
        private string lastDate = null;

        private const int GwlExStyle = -20;
        private const int WsExToolWindow = 0x80;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            cfg = Settings.Load();

            Title = Strings.AppTitle;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false; // no taskbar button; the tray icon is the control surface
            Topmost = cfg.Topmost;

            // ---- clock row ----
            DropShadowEffect clockShadow = new DropShadowEffect
            {
                BlurRadius = 24, ShadowDepth = 5, Direction = 270, Opacity = 0.55, Color = Colors.Black
            };
            StackPanel clockRow = new StackPanel { Orientation = Orientation.Horizontal, Effect = clockShadow };

            ampmText = new TextBlock
            {
                FontFamily = TextFontFamily,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE2)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 2, 12),
                Text = ""
            };
            StackPanel host = new StackPanel { Width = 40, VerticalAlignment = VerticalAlignment.Bottom };
            host.Children.Add(ampmText);
            ampmHost = host;

            for (int i = 0; i < 6; i++)
            {
                digits[i] = new FlipDigit(CardW, CardH, CardRadius, DigitFontSize, DigitFontFamily);
            }
            colon1 = MakeColon();
            colon2 = MakeColon();

            clockRow.Children.Add(ampmHost);
            clockRow.Children.Add(digits[0]);
            clockRow.Children.Add(digits[1]);
            clockRow.Children.Add(colon1);
            clockRow.Children.Add(digits[2]);
            clockRow.Children.Add(digits[3]);
            clockRow.Children.Add(colon2);
            clockRow.Children.Add(digits[4]);
            clockRow.Children.Add(digits[5]);

            // ---- date line ----
            DropShadowEffect textShadow = new DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 2, Direction = 270, Opacity = 0.5, Color = Colors.Black
            };
            dateText = new TextBlock
            {
                FontFamily = TextFontFamily,
                FontSize = 19,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDA, 0xDA, 0xDA)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Effect = textShadow,
                Text = ""
            };

            contentPanel = new StackPanel();
            contentPanel.Children.Add(clockRow);
            contentPanel.Children.Add(dateText);
            contentPanel.LayoutTransform = new ScaleTransform(cfg.Scale, cfg.Scale);

            // outer margin leaves room for the drop shadows
            Grid root = new Grid { Margin = new Thickness(32) };
            root.Children.Add(contentPanel);
            Content = root;

            SetSecondsVisible(cfg.ShowSeconds);
            InitTime();
            BuildMenu();
            tray = new TrayIconService(ToggleVisibility, ToggleTopmost, OpenSettingsFromTray, ExitApp,
                delegate { return IsVisible; }, delegate { return Topmost; });

            MouseLeftButtonDown += new MouseButtonEventHandler(OnDragStart);

            if (cfg.X.HasValue && cfg.Y.HasValue && PositionVisible(cfg.X.Value, cfg.Y.Value))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = cfg.X.Value;
                Top = cfg.Y.Value;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // /settings (legacy alias /sizepanel): open the settings window on launch
            bool openSettingsAtStart = false;
            string[] cmdArgs = Environment.GetCommandLineArgs();
            for (int i = 1; i < cmdArgs.Length; i++)
            {
                if (string.Equals(cmdArgs[i], "/settings", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cmdArgs[i], "/sizepanel", StringComparison.OrdinalIgnoreCase))
                {
                    openSettingsAtStart = true;
                    break;
                }
            }

            Loaded += new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                RunClock(cts.Token);
                if (openSettingsAtStart) OpenSettings();
            });
            Closing += new System.ComponentModel.CancelEventHandler(OnClosing);
        }

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        // ---- tray control entry points ----
        private void ToggleVisibility()
        {
            if (IsVisible) Hide();
            else { Show(); Activate(); }
        }

        private void ToggleTopmost()
        {
            Topmost = !Topmost;
            cfg.Topmost = Topmost;
        }

        private void OpenSettingsFromTray()
        {
            if (!IsVisible) Show(); // bring the clock out so changes are visible
            OpenSettings();
        }

        private void ExitApp()
        {
            Close();
        }

        // remove the window from Alt-Tab (WS_EX_TOOLWINDOW)
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                SetWindowLong(h, GwlExStyle, GetWindowLong(h, GwlExStyle) | WsExToolWindow);
            }
            catch { }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cts.Cancel();
            if (tray != null) tray.Dispose();
            cfg.X = double.IsNaN(Left) ? (double?)null : Left;
            cfg.Y = double.IsNaN(Top) ? (double?)null : Top;
            cfg.Save();
        }

        // ---- settings window ----
        private void OpenSettings()
        {
            SettingsWindow dlg = new SettingsWindow(
                cfg.Topmost, cfg.Hour12, cfg.ShowSeconds, cfg.Scale, GetAutoStartEnabled());
            dlg.Owner = this;
            dlg.Topmost = Topmost; // keep the dialog above a topmost clock
            dlg.ApplyRequested = delegate { ApplySettings(dlg); };
            dlg.ShowDialog();
        }

        private void ApplySettings(SettingsWindow d)
        {
            bool layoutChanged = (d.Hour12On != cfg.Hour12) || (d.ShowSecondsOn != cfg.ShowSeconds);

            Topmost = d.TopmostOn;
            cfg.Topmost = d.TopmostOn;
            cfg.Hour12 = d.Hour12On;
            cfg.ShowSeconds = d.ShowSecondsOn;
            cfg.Scale = d.ScaleValue;
            contentPanel.LayoutTransform = new ScaleTransform(cfg.Scale, cfg.Scale);
            ampmHost.Visibility = ShowAmpm();
            SetSecondsVisible(cfg.ShowSeconds);
            if (d.AutoStartOn != GetAutoStartEnabled()) SetAutoStartEnabled(d.AutoStartOn);
            if (layoutChanged) lastTime = null;
            UpdateTime(DateTime.Now);
            cfg.Save();
        }

        // Ticks aligned to the system clock once per second; no cumulative drift.
        private async void RunClock(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    DateTime now = DateTime.Now;
                    UpdateTime(now);
                    int wait = 1020 - now.Millisecond;
                    try { await Task.Delay(wait, token); }
                    catch (OperationCanceledException) { return; }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void UpdateTime(DateTime now)
        {
            string ts = TimeString(now, cfg.Hour12);
            if (ts != lastTime)
            {
                for (int i = 0; i < 6; i++) digits[i].Set(ts[i]);
                if (cfg.ShowSeconds) PulseColons(); // colons stay static when seconds are hidden
                lastTime = ts;
            }
            string ds = DateString(now);
            if (ds != lastDate)
            {
                dateText.Text = ds;
                lastDate = ds;
            }
            string ap = AmPmText(now);
            if (ampmText.Text != ap) ampmText.Text = ap;
        }

        private void InitTime()
        {
            ampmHost.Visibility = ShowAmpm();
            DateTime now = DateTime.Now;
            string ts = TimeString(now, cfg.Hour12);
            for (int i = 0; i < 6; i++) digits[i].Initialize(ts[i]);
            lastTime = ts;
            lastDate = DateString(now);
            dateText.Text = lastDate;
            ampmText.Text = AmPmText(now);
        }

        // The AM/PM label is only meaningful when the culture provides markers.
        private Visibility ShowAmpm()
        {
            bool hasMarkers = CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator.Length > 0;
            return (cfg.Hour12 && hasMarkers) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string AmPmText(DateTime now)
        {
            System.Globalization.DateTimeFormatInfo f = CultureInfo.CurrentCulture.DateTimeFormat;
            return now.Hour < 12 ? f.AMDesignator : f.PMDesignator;
        }

        private void SetSecondsVisible(bool visible)
        {
            Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
            digits[4].Visibility = v;
            digits[5].Visibility = v;
            colon2.Visibility = v;
        }

        private static string TimeString(DateTime now, bool hour12)
        {
            int h = now.Hour;
            if (hour12)
            {
                h = h % 12;
                if (h == 0) h = 12;
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:d2}{1:d2}{2:d2}", h, now.Minute, now.Second);
        }

        // Date formatted from the current culture's own long-date pattern, so the
        // text localizes itself (e.g. Chinese pattern includes CJK year/month/day
        // markers from the OS culture data). Chinese long dates carry no weekday,
        // so it is appended there.
        private static string DateString(DateTime now)
        {
            CultureInfo ci = CultureInfo.CurrentCulture;
            string pattern = ci.DateTimeFormat.LongDatePattern;
            if (Strings.IsChinese) pattern = pattern + " dddd";
            return now.ToString(pattern, ci);
        }

        private FrameworkElement MakeColon()
        {
            Grid g = new Grid { Width = 28, Height = CardH };
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            for (int r = 0; r < 2; r++)
            {
                Border dot = new Border
                {
                    Width = 11,
                    Height = 11,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 9, 0, 9)
                };
                Grid.SetRow(dot, r);
                g.Children.Add(dot);
            }
            return g;
        }

        // colons breathe once per second
        private void PulseColons()
        {
            FrameworkElement[] cols = new FrameworkElement[] { colon1, colon2 };
            for (int i = 0; i < cols.Length; i++)
            {
                DoubleAnimationUsingKeyFrames a = new DoubleAnimationUsingKeyFrames();
                a.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
                a.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
                a.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));
                a.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(950))));
                cols[i].BeginAnimation(UIElement.OpacityProperty, a);
            }
        }

        private void BuildMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem miCfg = new MenuItem { Header = Strings.Settings };
            miCfg.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { OpenSettings(); });

            MenuItem miTop = new MenuItem { Header = Strings.AlwaysOnTop, IsCheckable = true };
            miTop.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { ToggleTopmost(); });

            MenuItem miHide = new MenuItem { Header = Strings.HideClock };
            miHide.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { ToggleVisibility(); });

            MenuItem miExit = new MenuItem { Header = Strings.Exit };
            miExit.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Close(); });

            menu.Items.Add(miCfg);
            menu.Items.Add(miTop);
            menu.Items.Add(miHide);
            menu.Items.Add(new Separator());
            menu.Items.Add(miExit);

            // refresh the check state every time the menu opens (topmost may
            // have been toggled from the tray menu or the settings dialog)
            menu.Opened += new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                miTop.IsChecked = Topmost;
            });

            ContextMenu = menu;
        }

        // auto-start via the per-user Run key
        private static bool GetAutoStartEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(RunValueName);
                    return v is string && ((string)v).Length > 0;
                }
            }
            catch { return false; }
        }

        private static void SetAutoStartEnabled(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (k == null) return;
                    if (enable)
                    {
                        string p = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (!string.IsNullOrEmpty(p)) k.SetValue(RunValueName, "\"" + p + "\"");
                    }
                    else
                    {
                        k.DeleteValue(RunValueName, false);
                    }
                }
            }
            catch { }
        }

        // never restore to a position outside the current virtual screen
        private bool PositionVisible(double x, double y)
        {
            double vl = SystemParameters.VirtualScreenLeft;
            double vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth;
            double vh = SystemParameters.VirtualScreenHeight;
            return x > vl - 700 && x < vl + vw - 120 && y > vt - 100 && y < vt + vh - 120;
        }
    }
}
