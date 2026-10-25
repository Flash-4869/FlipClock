using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace FlipClockApp
{
    // Standalone settings page. Nothing takes effect until the user presses
    // OK or Apply, so the clock never jumps around while the slider moves.
    // Shown modally (ShowDialog); the main window cannot be touched while open,
    // which keeps the two in sync.
    internal class SettingsWindow : Window
    {
        public delegate void ApplyHandler();
        public ApplyHandler ApplyRequested;

        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        private static readonly Brush HintBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));

        private readonly Slider scaleSlider;
        private readonly TextBlock scalePercent;
        private readonly CheckBox chkSeconds;
        private readonly CheckBox chkTop;
        private readonly CheckBox chkHour12;
        private readonly CheckBox chkAuto;

        public SettingsWindow(bool topmost, bool hour12, bool showSeconds, double scale, bool autoStart)
        {
            Title = Strings.SettingsTitle;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = 360;
            ShowInTaskbar = false; // modal dialog, no taskbar button
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Border root = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(16),
                Padding = new Thickness(22, 16, 22, 20),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 22, ShadowDepth = 4, Direction = 270, Opacity = 0.6, Color = Colors.Black
                }
            };
            Content = root;

            StackPanel sp = new StackPanel();
            root.Child = sp;

            // title row: label + close button
            Grid titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock title = new TextBlock
            {
                Text = Strings.SettingsLabel,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 0);
            Button btnClose = new Button
            {
                Content = "\u00D7",
                Width = 28,
                Height = 24,
                FontSize = 14,
                Foreground = HintBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnClose.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Close(); });
            Grid.SetColumn(btnClose, 1);
            titleRow.Children.Add(title);
            titleRow.Children.Add(btnClose);
            sp.Children.Add(titleRow);

            sp.Children.Add(MakeLine(new Thickness(0, 12, 0, 12)));

            // size: the slider only previews a percentage inside this dialog;
            // the clock itself is resized by the owner when OK/Apply is pressed
            Grid sizeRow = new Grid();
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition());
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lblSize = new TextBlock { Text = Strings.Size, FontSize = 12, Foreground = HintBrush };
            scalePercent = new TextBlock { Text = "", FontSize = 12, Foreground = HintBrush };
            Grid.SetColumn(lblSize, 0);
            Grid.SetColumn(scalePercent, 1);
            sizeRow.Children.Add(lblSize);
            sizeRow.Children.Add(scalePercent);
            sp.Children.Add(sizeRow);

            scaleSlider = new Slider
            {
                Minimum = Settings.ScaleMin,
                Maximum = Settings.ScaleMax,
                SmallChange = 0.05,
                LargeChange = 0.1,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                Value = scale,
                Margin = new Thickness(0, 8, 0, 0)
            };
            scaleSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(
                delegate(object s, RoutedPropertyChangedEventArgs<double> e) { UpdatePercent(); });
            sp.Children.Add(scaleSlider);

            // general
            TextBlock lblGeneral = new TextBlock
            {
                Text = Strings.General,
                FontSize = 12,
                Foreground = HintBrush,
                Margin = new Thickness(0, 18, 0, 2)
            };
            sp.Children.Add(lblGeneral);

            chkSeconds = MakeCheck(Strings.ShowSeconds, showSeconds);
            chkTop = MakeCheck(Strings.AlwaysOnTop, topmost);
            chkHour12 = MakeCheck(Strings.Hour12, hour12);
            chkAuto = MakeCheck(Strings.AutoStart, autoStart);
            sp.Children.Add(chkSeconds);
            sp.Children.Add(chkTop);
            sp.Children.Add(chkHour12);
            sp.Children.Add(chkAuto);

            sp.Children.Add(MakeLine(new Thickness(0, 16, 0, 12)));

            // button row: OK / Apply / Cancel
            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button btnOk = MakeButton(Strings.OK, true);
            btnOk.IsDefault = true;
            btnOk.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Apply(); Close(); });
            Button btnApply = MakeButton(Strings.Apply, false);
            btnApply.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Apply(); });
            Button btnCancel = MakeButton(Strings.Cancel, false);
            btnCancel.IsCancel = true;
            btnCancel.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Close(); });
            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnApply);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);

            UpdatePercent();

            // drag by the empty areas (check boxes, slider and buttons handle
            // their own mouse events and are not affected)
            MouseLeftButtonDown += new MouseButtonEventHandler(delegate(object s, MouseButtonEventArgs e)
            {
                try { DragMove(); } catch { }
            });
        }

        public bool TopmostOn { get { return chkTop.IsChecked == true; } }
        public bool Hour12On { get { return chkHour12.IsChecked == true; } }
        public bool ShowSecondsOn { get { return chkSeconds.IsChecked == true; } }
        public bool AutoStartOn { get { return chkAuto.IsChecked == true; } }
        public double ScaleValue { get { return scaleSlider.Value; } }

        private void UpdatePercent()
        {
            scalePercent.Text =
                ((int)Math.Round(scaleSlider.Value * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "%";
        }

        private void Apply()
        {
            if (ApplyRequested != null) ApplyRequested();
        }

        private static CheckBox MakeCheck(string text, bool value)
        {
            return new CheckBox
            {
                Content = text,
                IsChecked = value,
                Foreground = TextBrush,
                FontSize = 13,
                Margin = new Thickness(0, 9, 0, 0),
                Cursor = Cursors.Hand
            };
        }

        private static Rectangle MakeLine(Thickness margin)
        {
            return new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                Margin = margin
            };
        }

        private static Button MakeButton(string text, bool primary)
        {
            Button b = new Button
            {
                Content = text,
                Width = 76,
                Height = 31,
                FontSize = 13,
                Foreground = TextBrush,
                Cursor = Cursors.Hand,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Color bg = primary ? Color.FromRgb(0x3B, 0x69, 0xB8) : Color.FromRgb(0x2F, 0x2F, 0x2F);
            Color bgHover = primary ? Color.FromRgb(0x47, 0x79, 0xCC) : Color.FromRgb(0x3C, 0x3C, 0x3C);

            ControlTemplate tpl = new ControlTemplate(typeof(Button));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            bd.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
            bd.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;

            Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            Setter hovSet = new Setter(Border.BackgroundProperty, new SolidColorBrush(bgHover));
            hovSet.TargetName = "bd";
            hover.Setters.Add(hovSet);
            tpl.Triggers.Add(hover);

            b.Template = tpl;
            return b;
        }
    }
}
