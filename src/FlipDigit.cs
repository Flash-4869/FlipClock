using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlipClockApp
{
    // HTC Sense style flip digit card.
    // Structure: static top half (switches to the new digit immediately),
    // static bottom half (keeps the old digit until the flap lands),
    // a top flap showing the old digit folding down, and a bottom flap
    // showing the new digit unfolding. The fold is simulated with ScaleY
    // around the fold line plus a darkening overlay that sells the motion.
    internal class FlipDigit : Grid
    {
        private const double FoldMs = 190;        // top flap fold duration
        private const double UnfoldDelayMs = 165; // delay before the bottom flap opens
        private const double UnfoldMs = 230;      // bottom flap unfold duration

        private Action<char> setTopStatic;
        private Action<char> setTopFlap;
        private Action<char> setBottomStatic;
        private Action<char> setBottomFlap;
        private UIElement topFlap;
        private UIElement bottomFlap;
        private UIElement topShade;
        private UIElement bottomShade;
        private ScaleTransform topScale;
        private ScaleTransform bottomScale;
        private char shown;    // digit currently displayed
        private char target;   // digit being flipped to
        private bool animating;
        private int gen;       // animation generation, drops stale Completed callbacks

        public FlipDigit(double w, double h, double radius, double fontSize, FontFamily font)
        {
            Width = w;
            Height = h;
            Margin = new Thickness(5, 0, 5, 0);
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions.Add(new RowDefinition());

            double glyphShift = ComputeCenterShift(font, fontSize);

            Brush topBg = MakeBrush("#FF3A3A3A", "#FF272727");
            Brush bottomBg = MakeBrush("#FF1F1F1F", "#FF2D2D2D");
            CornerRadius crTop = new CornerRadius(radius, radius, 0, 0);
            CornerRadius crBottom = new CornerRadius(0, 0, radius, radius);

            Border shadeTmp;

            Border ts = MakeHalf(true, crTop, topBg, h, fontSize, font, glyphShift, out setTopStatic, out shadeTmp);
            Grid.SetRow(ts, 0);
            Children.Add(ts);

            Border bs = MakeHalf(false, crBottom, bottomBg, h, fontSize, font, glyphShift, out setBottomStatic, out shadeTmp);
            Grid.SetRow(bs, 1);
            Children.Add(bs);

            // fixed shadow under the fold line, gives the card a groove
            Border cast = new Border
            {
                Height = 9,
                VerticalAlignment = VerticalAlignment.Top,
                Background = MakeBrush("#3F000000", "#00000000"),
                IsHitTestVisible = false
            };
            Grid.SetRow(cast, 1);
            Children.Add(cast);

            Border topShadeBorder;
            Border tf = MakeHalf(true, crTop, topBg, h, fontSize, font, glyphShift, out setTopFlap, out topShadeBorder);
            topScale = new ScaleTransform(1, 1);
            tf.RenderTransform = topScale;
            tf.RenderTransformOrigin = new Point(0.5, 1.0); // fold line at the flap bottom
            topFlap = tf;
            topShade = topShadeBorder;
            Grid.SetRow(tf, 0);
            Children.Add(tf);
            tf.Visibility = Visibility.Collapsed;

            Border bottomShadeBorder;
            Border bf = MakeHalf(false, crBottom, bottomBg, h, fontSize, font, glyphShift, out setBottomFlap, out bottomShadeBorder);
            bottomScale = new ScaleTransform(1, 0);
            bf.RenderTransform = bottomScale;
            bf.RenderTransformOrigin = new Point(0.5, 0.0); // fold line at the flap top
            bottomFlap = bf;
            bottomShade = bottomShadeBorder;
            Grid.SetRow(bf, 1);
            Children.Add(bf);
            bf.Visibility = Visibility.Collapsed;

            // the fold line itself
            Border fold = new Border
            {
                Height = 1.5,
                Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            Grid.SetRow(fold, 0);
            Grid.SetRowSpan(fold, 2);
            Children.Add(fold);
        }

        // Builds half a card. top=true renders the upper half of the digit
        // (content aligned to the top and clipped), otherwise the lower half.
        private Border MakeHalf(bool top, CornerRadius cr, Brush bg, double cardH, double fs, FontFamily font,
                                double glyphShift, out Action<char> setText, out Border shade)
        {
            Border b = new Border { Background = bg, CornerRadius = cr };

            Grid clip = new Grid { ClipToBounds = true };
            Grid full = new Grid { Height = cardH };
            full.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;

            TextBlock tb = new TextBlock
            {
                Text = "0",
                FontSize = fs,
                FontFamily = font,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (glyphShift != 0) tb.RenderTransform = new TranslateTransform(0, glyphShift);
            full.Children.Add(tb);
            clip.Children.Add(full);

            shade = new Border
            {
                Background = Brushes.Black,
                CornerRadius = cr,
                Opacity = 0,
                IsHitTestVisible = false
            };

            Grid wrap = new Grid();
            wrap.Children.Add(clip);
            wrap.Children.Add(shade);
            b.Child = wrap;

            setText = delegate(char c) { tb.Text = c.ToString(); };
            return b;
        }

        // Digit glyphs sit low inside a font's line box (the space below the
        // baseline reserved for descenders), so centering the TextBlock by its
        // layout box sinks the digit. The shift below is derived from the real
        // baseline/line height and the digit height, moving the glyph up to the
        // optical center of the card. Negative means "move up". Digit height is
        // about 0.72 em for common sans-serif faces (Segoe UI Black included).
        private const double DigitCapEm = 0.72;

        private static double ComputeCenterShift(FontFamily family, double fontSize)
        {
            try
            {
                Typeface tf = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                FormattedText ft = new FormattedText("0", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, tf, 100.0, Brushes.Black, 1.0);
                double baseline = ft.Baseline; // top of the line box to the baseline
                double lineH = ft.Height;      // line box height
                if (lineH <= 0 || baseline <= 0 || baseline > lineH) return -0.055 * fontSize;

                double glyphCenter = baseline - DigitCapEm * 100.0 / 2.0;
                double shiftEm = (lineH / 2.0 - glyphCenter) / 100.0;
                if (shiftEm > 0.2) shiftEm = 0.2;
                if (shiftEm < -0.2) shiftEm = -0.2;
                return shiftEm * fontSize;
            }
            catch { }
            return -0.055 * fontSize;
        }

        // Show a digit initially, without animation.
        public void Initialize(char c)
        {
            gen++;
            animating = false;
            shown = c;
            target = c;
            StopAnimations();
            topScale.ScaleY = 1;
            bottomScale.ScaleY = 1;
            topShade.Opacity = 0;
            bottomShade.Opacity = 0;
            topFlap.Visibility = Visibility.Collapsed;
            bottomFlap.Visibility = Visibility.Collapsed;
            setTopStatic(c);
            setBottomStatic(c);
            setTopFlap(c);
            setBottomFlap(c);
        }

        // Flip to a new digit. If a flip is still running it is completed
        // instantly first.
        public void Set(char c)
        {
            if (animating)
            {
                if (c == target) return;
                CompleteNow();
            }
            if (c == shown) return;

            target = c;
            animating = true;
            int myGen = ++gen;

            setTopFlap(shown);   // falling flap shows the old digit's top half
            setTopStatic(c);     // static top half switches immediately (hidden behind the flap)
            setBottomFlap(c);    // unfolding flap shows the new digit's bottom half
            // the static bottom half keeps the old digit until the flap lands

            StopAnimations();
            topScale.ScaleY = 1;
            bottomScale.ScaleY = 0; // flap starts folded away, invisible
            topShade.Opacity = 0;
            bottomShade.Opacity = 0;
            topFlap.Visibility = Visibility.Visible;
            bottomFlap.Visibility = Visibility.Visible;

            QuadraticEase easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            QuadraticEase easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation fold = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(FoldMs))) { EasingFunction = easeIn };
            topScale.BeginAnimation(ScaleTransform.ScaleYProperty, fold);
            DoubleAnimation foldShade = new DoubleAnimation(0, 0.55, new Duration(TimeSpan.FromMilliseconds(FoldMs))) { EasingFunction = easeIn };
            topShade.BeginAnimation(UIElement.OpacityProperty, foldShade);

            DoubleAnimation unfold = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(UnfoldMs)))
            {
                BeginTime = TimeSpan.FromMilliseconds(UnfoldDelayMs),
                EasingFunction = easeOut
            };
            unfold.Completed += new EventHandler(delegate(object s, EventArgs e)
            {
                if (myGen == gen && animating)
                {
                    shown = target;
                    animating = false;
                    setBottomStatic(shown);
                    topFlap.Visibility = Visibility.Collapsed;
                    bottomFlap.Visibility = Visibility.Collapsed;
                }
            });
            bottomScale.BeginAnimation(ScaleTransform.ScaleYProperty, unfold);
            DoubleAnimation unfoldShade = new DoubleAnimation(0.55, 0, new Duration(TimeSpan.FromMilliseconds(UnfoldMs)))
            {
                BeginTime = TimeSpan.FromMilliseconds(UnfoldDelayMs),
                EasingFunction = easeOut
            };
            bottomShade.BeginAnimation(UIElement.OpacityProperty, unfoldShade);
        }

        // Finish the running animation instantly, jumping to its target state.
        private void CompleteNow()
        {
            StopAnimations();
            topScale.ScaleY = 1;
            bottomScale.ScaleY = 1;
            topShade.Opacity = 0;
            bottomShade.Opacity = 0;
            shown = target;
            animating = false;
            setTopStatic(shown);
            setBottomStatic(shown);
            topFlap.Visibility = Visibility.Collapsed;
            bottomFlap.Visibility = Visibility.Collapsed;
        }

        private void StopAnimations()
        {
            topScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            bottomScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            topShade.BeginAnimation(UIElement.OpacityProperty, null);
            bottomShade.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private static Brush MakeBrush(string c0, string c1)
        {
            LinearGradientBrush b = new LinearGradientBrush();
            b.StartPoint = new Point(0, 0);
            b.EndPoint = new Point(0, 1);
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c0), 0));
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), 1));
            return b;
        }
    }
}
