// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Added 2026 by DMD61 (Victory61): brightness check before the SDR calibration. When the white of the screen is far
// from the chosen luminance and the monitor cannot be adjusted over DDC/CI, a small window in a corner of the screen
// (away from the sensor) shows the white measured live while the user changes the brightness with the monitor or
// keyboard keys; the value turns green when it is right.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Pages
{
    public sealed class BrightnessCheck : Window
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        static readonly IntPtr HwndTopmost = new(-1);
        const uint SWP_NOSIZE = 0x0001, SWP_SHOWWINDOW = 0x0040;

        readonly DisplayTarget _d;
        readonly double _target;
        readonly TextBlock _value = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) };
        readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
        readonly Button _ok;

        /// <summary>true = continue, false = continue anyway, null = still waiting.</summary>
        public bool? Result { get; private set; }
        public bool Cancelled { get; private set; }

        /// <summary>The white is right when what remains after the white point correction (~95 %) is between
        /// 97 % and 115 % of the target.</summary>
        public static bool Good(double white, double target) => white * 0.95 >= target * 0.97 && white * 0.95 <= target * 1.15;

        public BrightnessCheck(DisplayTarget d, double target)
        {
            _d = d; _target = target;
            Title = "HDRDisplayCalibrate"; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual; Left = -32000; Top = -32000; Topmost = true; ShowInTaskbar = false;
            if (S.Rtl) FlowDirection = FlowDirection.RightToLeft;
            _ok = Ui.Btn(S.T("Continue", "Продовжити"), () => { Result = true; Close(); }, true);
            _ok.IsEnabled = false;
            var anyway = Ui.Btn(S.T("Continue anyway", "Все одно продовжити"), () => { Result = false; Close(); }, false);
            var cancel = Ui.Btn(S.T("Cancel", "Скасувати"), () => { Cancelled = true; Close(); }, false);
            var body = Ui.Stack(
                Ui.H2(S.T("Screen brightness", "Яскравість екрана")),
                Ui.Text(S.T("Do not touch the sensor. Change the brightness with the monitor buttons, the keyboard keys or the Windows settings.", "Не торкайтеся датчика. Змінюйте яскравість кнопками монітора, клавішами клавіатури або в параметрах Windows.")),
                _value, _hint,
                Ui.Row(_ok, anyway, cancel));
            body.Margin = new Thickness(20, 16, 20, 16);
            Content = body;
            Closing += (_, _) => { if (Result == null) Cancelled = true; };
            SourceInitialized += (_, _) =>
            {
                // top-left corner of the screen being calibrated, far from the sensor in the centre
                var (x, y, _, _) = _d.Bounds;
                SetWindowPos(new WindowInteropHelper(this).Handle, HwndTopmost, x + 40, y + 40, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW);
            };
            Update(double.NaN);
        }

        /// <summary>Shows the last white reading; the value turns green and Continue is enabled when it is right.</summary>
        public void Update(double white)
        {
            if (double.IsNaN(white))
            {
                _value.Text = S.T("Measuring…", "Вимірюю…"); _value.Foreground = Ui.Muted;
                _hint.Text = ""; return;
            }
            bool good = Good(white, _target);
            _value.Text = S.F("White now: {0:F0} cd/m² · target {1:F0} cd/m²", "Білий зараз: {0:F0} кд/м² · ціль {1:F0} кд/м²", white, _target);
            _value.Foreground = good ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC3, 0x6A)) : Ui.Warm;
            _hint.Text = good
                ? S.T("Good: press Continue.", "Добре: натисніть «Продовжити».")
                : white * 0.95 > _target * 1.15
                    ? S.T("Too bright: lower the screen brightness until the value turns green. A screen much brighter than the target loses contrast (lighter black) and grey levels.", "Занадто яскраво: зменшуйте яскравість екрана, доки значення не стане зеленим. Екран, значно яскравіший за ціль, втрачає контраст (світліший чорний) і рівні сірого.")
                    : S.T("Too dark: raise the screen brightness until the value turns green. If it cannot go higher, press Continue anyway: the program calibrates at the highest luminance it reaches.", "Занадто темно: збільшуйте яскравість екрана, доки значення не стане зеленим. Якщо більше не можна, натисніть «Все одно продовжити»: програма калібрує на максимальній досяжній яскравості.");
            _ok.IsEnabled = good;
        }
    }
}
