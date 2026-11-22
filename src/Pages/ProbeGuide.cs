// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Added 2026 by DMD61 (Victory61): guided sensor dialogs (place / check / remove), modelled on the
// step-by-step prompts of commercial calibration software, so the HDR wizard can be followed without a manual;
// room light measurement (sensor on the desk, diffuser over the lens); version 12: clearer three-step room-light picture.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>
    /// Modal dialogs that tell the user when and how to put the colorimeter on the screen,
    /// remind them not to move it between steps, and tell them when it can be removed.
    /// </summary>
    public sealed class ProbeGuide : Window
    {
        public enum Mode { Place, Check, Remove, Ambient }

        /// <summary>Shows the dialog for the given moment; returns false if the user cancelled.</summary>
        public static bool Ask(Mode mode, string extra = null)
        {
            var w = new ProbeGuide(mode, extra);
            return w.ShowDialog() == true;
        }

        ProbeGuide(Mode mode, string extra)
        {
            Title = "HDRDisplayCalibrate"; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Owner = Application.Current.MainWindow; Topmost = true;
            if (S.Rtl) FlowDirection = FlowDirection.RightToLeft;

            var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20), MaxWidth = 680 };
            Button ok, cancel = null;

            if (mode == Mode.Place)
            {
                body.Children.Add(Ui.H2(S.T("Place the sensor on the screen", "Поставте датчик на екран")));
                body.Children.Add(Diagram());
                body.Children.Add(Numbered(
                    S.T("Tilt the screen slightly back.", "Злегка відхиліть екран назад."),
                    S.T("Turn the lens cover (diffuser) aside so the lens is uncovered: the sensor cannot measure a screen through it.", "Поверніть кришку об'єктива (розсіювач) убік, щоб відкрити лінзу: крізь неї датчик не може міряти екран."),
                    S.T("Put the sensor flat against the glass in the centre of the screen.", "Притуліть датчик до скла по центру екрана."),
                    S.T("Hang the cable over the top edge with the counterweight behind the monitor, so the sensor stays still.", "Перекиньте кабель через верхній край, противагу — за монітор, щоб датчик не рухався."),
                    S.T("Leave it there until the app tells you it can be removed.", "Залиште його там, доки програма не скаже, що його можна зняти.")));
                body.Children.Add(Note(S.T("During each measurement the screen turns black, grey, white and then coloured for a few minutes. That is normal: do not touch the mouse, keyboard or sensor until the step is done.",
                                           "Під час кожного заміру екран на кілька хвилин стає чорним, сірим, білим, а потім кольоровим. Це нормально: не торкайтеся миші, клавіатури й датчика, доки крок не завершиться.")));
                if (!string.IsNullOrEmpty(extra)) body.Children.Add(Note(extra));   // DMD61: page-specific note (SDR calibration)
                ok = Ui.Btn(S.T("Sensor in place — start", "Датчик на місці — почати"), () => { DialogResult = true; }, true);
                cancel = Ui.Btn(S.T("Cancel", "Скасувати"), () => { DialogResult = false; });
            }
            else if (mode == Mode.Check)
            {
                body.Children.Add(Ui.H2(S.T("Is the sensor still in the centre of the screen?", "Датчик досі по центру екрана?")));
                body.Children.Add(Diagram());
                body.Children.Add(Ui.Text(S.T("Do not move it between steps: every step must be measured at the same spot. Keep the Settings window away from the centre.",
                                              "Не рухайте його між кроками: усі кроки треба міряти в тому самому місці. Тримайте вікно Параметрів подалі від центру.")));
                if (!string.IsNullOrEmpty(extra)) body.Children.Add(Note(extra));
                body.Children.Add(Note(S.T("The measurement takes a few minutes: do not touch anything until the step is done.",
                                           "Замір триває кілька хвилин: нічого не торкайтеся, доки крок не завершиться.")));
                ok = Ui.Btn(S.T("Yes, continue", "Так, продовжити"), () => { DialogResult = true; }, true);
                cancel = Ui.Btn(S.T("Cancel", "Скасувати"), () => { DialogResult = false; });
            }
            else if (mode == Mode.Ambient)
            {
                body.Children.Add(Ui.H2(S.T("Measure the room light", "Заміряйте освітлення кімнати")));
                body.Children.Add(AmbientDiagram());
                body.Children.Add(Numbered(
                    S.T("Take the sensor off the screen.", "Зніміть датчик з екрана."),
                    S.T("Turn the lens cover (white diffuser) over the lens: the opposite of when the screen is measured.", "Поверніть кришку (білий розсіювач) на лінзу — навпаки, ніж під час заміру екрана."),
                    S.T("Stand the sensor on the desk in front of the monitor, with the diffuser facing you (towards where you sit).", "Поставте датчик на стіл перед монітором розсіювачем до себе (туди, де ви сидите)."),
                    S.T("Do not cover it with your hand or body, and keep the room light as you normally have it when you use the PC.", "Не затуляйте його рукою чи тілом і залиште світло в кімнаті таким, як зазвичай під час роботи за ПК.")));
                body.Children.Add(Note(S.T("After the measurement, turn the diffuser aside again before measuring the screen.", "Після заміру знову поверніть розсіювач убік, перш ніж міряти екран.")));
                ok = Ui.Btn(S.T("Sensor ready — measure", "Датчик готовий — заміряти"), () => { DialogResult = true; }, true);
                cancel = Ui.Btn(S.T("Cancel", "Скасувати"), () => { DialogResult = false; });
            }
            else
            {
                body.Children.Add(Ui.H2(S.T("Measurement finished — you can remove the sensor", "Замір завершено — датчик можна зняти")));
                body.Children.Add(Ui.Text(S.T("Turn the lens cover back over the lens to protect it.", "Поверніть кришку назад на лінзу, щоб захистити її.")));
                if (!string.IsNullOrEmpty(extra)) body.Children.Add(Note(extra));
                ok = Ui.Btn("OK", () => { DialogResult = true; }, true);
            }

            ok.IsDefault = true; ok.MinWidth = 120;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            if (cancel != null) { cancel.IsCancel = true; cancel.MinWidth = 100; cancel.Margin = new Thickness(0, 0, 8, 0); buttons.Children.Add(cancel); }
            ok.Margin = new Thickness(0);
            buttons.Children.Add(ok);
            body.Children.Add(buttons);
            Content = body;
        }

        static UIElement Numbered(params string[] lines)
        {
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            for (int i = 0; i < lines.Length; i++)
            {
                var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new TextBlock { Text = (i + 1) + ".", Foreground = Ui.Accent, FontWeight = FontWeights.SemiBold });
                var t = Ui.Text(lines[i]); Grid.SetColumn(t, 1); g.Children.Add(t);
                list.Children.Add(g);
            }
            return list;
        }

        static UIElement Note(string text) => new Border
        {
            BorderBrush = Ui.Warm, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 6, 6, 6), Margin = new Thickness(0, 8, 0, 0),
            Child = Ui.Text(text)
        };

        /// <summary>Room light, three pictures (DMD61, redrawn to be read at a glance): 1) the lens uncovered, as for the screen,
        /// with the arrow showing how the diffuser turns; 2) the diffuser over the lens, ready; 3) the sensor standing on the desk
        /// in front of the monitor, diffuser facing the user, under the normal room light.</summary>
        static UIElement AmbientDiagram()
        {
            var c = new Canvas { Width = 660, Height = 250, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            void At(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); }
            var panel = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));
            var body = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A));
            var arm = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            var ok = new SolidColorBrush(Color.FromRgb(0x4C, 0xC3, 0x6A));
            TextBlock Num(string n) => new() { Text = n, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Ui.Accent };
            TextBlock Label(string t, Brush b, double w) => new() { Text = t, FontSize = 12, Foreground = b, Width = w, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            // the sensor seen from the front: rounded body, round lens, cable below
            void Sensor(double x, double y, bool covered)
            {
                c.Children.Add(new Line { X1 = x + 45, Y1 = y + 110, X2 = x + 45, Y2 = y + 140, Stroke = Ui.Muted, StrokeThickness = 3 });   // cable
                At(new Rectangle { Width = 90, Height = 110, Fill = body, Stroke = Ui.Muted, StrokeThickness = 2, RadiusX = 16, RadiusY = 16 }, x, y);
                At(new Ellipse { Width = 50, Height = 50, Fill = Brushes.Black, Stroke = Ui.Muted, StrokeThickness = 2 }, x + 20, y + 30);   // lens
                if (covered) At(new Ellipse { Width = 58, Height = 58, Fill = Brushes.White, Stroke = arm, StrokeThickness = 3 }, x + 16, y + 26);   // diffuser over the lens
            }

            // 1. lens uncovered; the diffuser on its arm beside it, arrow: turn it over the lens
            At(new Rectangle { Width = 200, Height = 205, Fill = panel, RadiusX = 6, RadiusY = 6 }, 0, 0);
            At(Num("1"), 10, 6);
            Sensor(30, 40, false);
            At(new Rectangle { Width = 46, Height = 14, Fill = arm, RadiusX = 7, RadiusY = 7 }, 112, 88);                                   // arm
            At(new Ellipse { Width = 50, Height = 50, Fill = Brushes.White, Opacity = 0.9, Stroke = arm, StrokeThickness = 3 }, 138, 70);   // diffuser aside
            c.Children.Add(new Path { Data = Geometry.Parse("M 168,62 C 160,22 100,20 82,52"), Stroke = Ui.Warm, StrokeThickness = 4 });
            c.Children.Add(new Path { Data = Geometry.Parse("M 74,46 L 92,46 L 80,64 Z"), Fill = Ui.Warm });
            At(Label(S.T("turn the white diffuser over the lens", "поверніть білий розсіювач на лінзу"), Ui.Warm, 190), 5, 182);

            // 2. diffuser over the lens: ready
            At(new Rectangle { Width = 170, Height = 205, Fill = panel, RadiusX = 6, RadiusY = 6 }, 215, 0);
            At(Num("2"), 225, 6);
            Sensor(255, 40, true);
            At(new Ellipse { Width = 30, Height = 30, Fill = ok }, 330, 30);
            c.Children.Add(new Path { Data = Geometry.Parse("M 337,45 L 343,52 L 354,38"), Stroke = Brushes.White, StrokeThickness = 3.5 });
            At(Label(S.T("lens covered: ready", "лінза закрита: готово"), ok, 160), 220, 182);

            // 3. on the desk in front of the monitor, diffuser facing you, normal room light
            At(new Rectangle { Width = 260, Height = 205, Fill = panel, RadiusX = 6, RadiusY = 6 }, 400, 0);
            At(Num("3"), 410, 6);
            // room lamp with light rays
            At(new Path { Data = Geometry.Parse("M 0,14 L 10,0 L 30,0 L 40,14 Z"), Fill = Ui.Warm }, 600, 10);
            foreach (var (x2, y2) in new[] { (590.0, 50.0), (620.0, 56.0), (650.0, 50.0) })
                c.Children.Add(new Line { X1 = 620, Y1 = 26, X2 = x2, Y2 = y2, Stroke = Ui.Warm, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 2, 2 } });
            At(new Rectangle { Width = 150, Height = 95, Stroke = Ui.Muted, StrokeThickness = 4, RadiusX = 4, RadiusY = 4 }, 425, 30);   // monitor
            At(new Rectangle { Width = 10, Height = 26, Fill = Ui.Muted }, 495, 125);
            At(new Rectangle { Width = 60, Height = 6, Fill = Ui.Muted }, 470, 150);
            At(new Rectangle { Width = 240, Height = 6, Fill = body }, 410, 156);                                                      // desk
            At(new Rectangle { Width = 40, Height = 48, Fill = body, Stroke = Ui.Accent, StrokeThickness = 2, RadiusX = 8, RadiusY = 8 }, 585, 108);   // sensor standing
            At(new Ellipse { Width = 26, Height = 26, Fill = Brushes.White }, 592, 116);                                               // diffuser facing you
            c.Children.Add(new Path { Data = Geometry.Parse("M 585,128 C 560,128 550,120 540,150"), Stroke = Ui.Muted, StrokeThickness = 3 });   // cable
            c.Children.Add(new Line { X1 = 605, Y1 = 166, X2 = 605, Y2 = 178, Stroke = Ui.Accent, StrokeThickness = 3 });               // "towards you"
            c.Children.Add(new Path { Data = Geometry.Parse("M 598,176 L 612,176 L 605,184 Z"), Fill = Ui.Accent });
            At(Label(S.T("on the desk in front of the monitor, diffuser facing you", "на столі перед монітором, розсіювачем до вас"), Ui.Accent, 250), 405, 186);
            return c;
        }

        /// <summary>Front and side view: sensor in the centre, diffuser open, cable over the top, counterweight behind.</summary>
        static UIElement Diagram()
        {
            var c = new Canvas { Width = 640, Height = 225, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            void At(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); }
            TextBlock L(string t, Brush b) => new() { Text = t, FontSize = 12, Foreground = b };

            // front view
            At(L(S.T("Front", "Спереду"), Ui.Muted), 105, 0);
            At(new Rectangle { Width = 240, Height = 150, Stroke = Ui.Muted, StrokeThickness = 3, RadiusX = 4, RadiusY = 4 }, 10, 20);
            At(new Rectangle { Width = 8, Height = 30, Fill = Ui.Muted }, 126, 170);
            At(new Rectangle { Width = 70, Height = 6, Fill = Ui.Muted }, 95, 200);
            c.Children.Add(new Line { X1 = 130, Y1 = 20, X2 = 130, Y2 = 79, Stroke = Ui.Accent, StrokeThickness = 2 });
            At(new Ellipse { Width = 34, Height = 34, Fill = Brushes.Black, Stroke = Ui.Accent, StrokeThickness = 3 }, 113, 78);
            At(new TextBlock { Text = S.T("sensor in the centre", "датчик по центру"), FontSize = 12, Foreground = Ui.Warm, Width = 230, TextAlignment = TextAlignment.Center }, 15, 118);
            At(new TextBlock { Text = S.T("cable over the top", "кабель через верх"), FontSize = 12, Foreground = Ui.Muted, Width = 110, TextAlignment = TextAlignment.Right }, 14, 30);

            // side view (screen leaning back: the top is further right than the bottom)
            At(L(S.T("Side", "Збоку"), Ui.Muted), 385, 0);
            c.Children.Add(new Line { X1 = 470, Y1 = 22, X2 = 455, Y2 = 170, Stroke = Ui.Muted, StrokeThickness = 8 });
            c.Children.Add(new Line { X1 = 456, Y1 = 170, X2 = 466, Y2 = 200, Stroke = Ui.Muted, StrokeThickness = 5 });
            At(new Rectangle { Width = 60, Height = 6, Fill = Ui.Muted }, 436, 200);
            At(new Rectangle { Width = 22, Height = 22, Fill = Brushes.Black, Stroke = Ui.Accent, StrokeThickness = 2 }, 437, 84);
            c.Children.Add(new Line { X1 = 448, Y1 = 84, X2 = 448, Y2 = 12, Stroke = Ui.Accent, StrokeThickness = 2 });
            c.Children.Add(new Line { X1 = 448, Y1 = 12, X2 = 505, Y2 = 12, Stroke = Ui.Accent, StrokeThickness = 2 });
            c.Children.Add(new Line { X1 = 505, Y1 = 12, X2 = 505, Y2 = 110, Stroke = Ui.Accent, StrokeThickness = 2 });
            At(new Rectangle { Width = 16, Height = 26, Fill = Ui.Muted }, 497, 110);
            c.Children.Add(new Line { X1 = 437, Y1 = 106, X2 = 417, Y2 = 124, Stroke = Ui.Warm, StrokeThickness = 4 });
            At(new TextBlock { Text = S.T("lens cover turned aside", "кришка лінзи вбік"), FontSize = 12, Foreground = Ui.Warm, Width = 170, TextAlignment = TextAlignment.Right }, 242, 120);
            At(L(S.T("counterweight behind", "противага позаду"), Ui.Muted), 520, 115);
            return c;
        }
    }
}
