using System;
using System.Globalization;

namespace FlipClockApp
{
    // UI strings. The repository source is kept pure ASCII, so the Chinese
    // strings are stored as unicode escapes. The app follows the Windows UI
    // language: Chinese on zh-* systems, English everywhere else.
    // The date line and AM/PM markers are formatted from the current culture,
    // so they localize themselves without any literals here.
    internal static class Strings
    {
        private static readonly bool Zh = DetectChinese();

        private static bool DetectChinese()
        {
            try
            {
                CultureInfo ui = CultureInfo.CurrentUICulture;
                return ui != null && ui.TwoLetterISOLanguageName == "zh";
            }
            catch { return false; }
        }

        private static string Pick(string zh, string en)
        {
            return Zh ? zh : en;
        }

        public static bool IsChinese { get { return Zh; } }

        public static string AppTitle { get { return Pick("\u7ffb\u9875\u65f6\u949f", "Flip Clock"); } }
        public static string SettingsTitle { get { return Pick("\u7ffb\u9875\u65f6\u949f \u8bbe\u7f6e", "Flip Clock Settings"); } }
        public static string SettingsLabel { get { return Pick("\u8bbe\u7f6e", "Settings"); } }
        public static string Settings { get { return Pick("\u8bbe\u7f6e...", "Settings..."); } }
        public static string AlwaysOnTop { get { return Pick("\u7a97\u53e3\u7f6e\u9876", "Always on top"); } }
        public static string HideClock { get { return Pick("\u9690\u85cf\u65f6\u949f", "Hide clock"); } }
        public static string ShowClock { get { return Pick("\u663e\u793a\u65f6\u949f", "Show clock"); } }
        public static string Exit { get { return Pick("\u9000\u51fa", "Exit"); } }
        public static string Size { get { return Pick("\u5927\u5c0f", "Size"); } }
        public static string General { get { return Pick("\u5e38\u89c4", "General"); } }
        public static string ShowSeconds { get { return Pick("\u663e\u793a\u79d2\u6570", "Show seconds"); } }
        public static string Hour12 { get { return Pick("12\u5c0f\u65f6\u5236", "12-hour clock"); } }
        public static string AutoStart { get { return Pick("\u5f00\u673a\u81ea\u542f\u52a8", "Start with Windows"); } }
        public static string OK { get { return Pick("\u786e\u5b9a", "OK"); } }
        public static string Apply { get { return Pick("\u5e94\u7528", "Apply"); } }
        public static string Cancel { get { return Pick("\u53d6\u6d88", "Cancel"); } }
    }
}
