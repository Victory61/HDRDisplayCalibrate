// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Modified 2026 by DMD61 (Victory61): the page switches Windows HDR off by itself (SDR calibration needs it off);
// guided sensor dialogs (place / remove, room light); version 12–13: steps in the order they are done (optional room light
// first); Standard / Advanced / Maximum modes with the patch counts of Calibrite PROFILER (119 / 211 / 461) and the
// estimated time; firewall rules added before the sensor is placed; no message while the sensor is on the screen
// (luminance limited automatically, profile set as the display default by the app, summary at the end); phase X of N.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>SDR calibration (video LUT) + ICC profile + install + verification, like DisplayCAL.</summary>
    public sealed class SdrPage : Page
    {
        readonly WhitePicker _wp = new();
        readonly TextBox _nits = Ui.Box("120");
        readonly ComboBox _curve = Ui.Combo(new[] { S.T("Gamma 2.2 (PC, games)", "Гамма 2.2 (ПК, ігри)"), S.T("Gamma 2.4 (dark room)", "Гамма 2.4 (темна кімната)"), "BT.1886 (TV, video)", "sRGB", "L*" });
        // DMD61: one choice only, the mode; it sets the grey-scale quality, the patch set (those of Calibrite PROFILER:
        // standard 119 read, advanced 211 and 461) and the profile type
        static readonly int[] ModePatches = { 119, 211, 461 };
        readonly ComboBox _mode = Ui.Combo(new[] { S.T("Standard (like Calibrite PROFILER)", "Стандарт (як Calibrite PROFILER)"),
                                                   S.T("Advanced", "Розширений"),
                                                   S.T("Maximum (slow)", "Максимум (довго)") }, 0);
        readonly TextBlock _modeInfo = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 430, Margin = new Thickness(12, 0, 0, 0) };
        readonly TextBlock _ambientNote = Ui.Caption(S.T("Normally leave it off. Turn it on only if the room is much darker or much brighter than an office and you want the tone curve adapted to the measured room light. It needs the room light measured first.",
                                                         "Зазвичай залишайте вимкненим. Вмикайте, лише якщо кімната значно темніша або світліша за офіс і ви хочете підлаштувати тонову криву під заміряне освітлення. Спершу треба поміряти освітлення кімнати."));
        readonly CheckBox _ambient = Ui.Check(S.T("Adjust tone curve to room light (CIECAM02, dispcal -a)", "Підлаштувати криву під освітлення кімнати (CIECAM02, dispcal -a)"), tip: S.T("Adapts the tone curve to how bright the room is (CIECAM02 viewing conditions). Useful when the room is much darker or brighter than a typical office.", "Підлаштовує тонову криву під яскравість кімнати (умови перегляду CIECAM02). Корисно, коли кімната значно темніша чи світліша за звичайний офіс."));
        readonly TextBox _lux = Ui.Box("");
        readonly CheckBox _install = Ui.Check(S.T("Install the profile for this display", "Встановити профіль для цього дисплея"), true, tip: S.T("When the profile is ready, installs it and makes it the default for this display.", "Коли профіль готовий, встановлює його й робить основним для цього дисплея."));
        readonly CheckBox _verify = Ui.Check(S.F("Verify afterwards ({0} patches)", "Перевірити після ({0} патчів)", Jobs.VerifyChart().Count), true, tip: S.F("Measures {0} patches with the new profile and shows the final accuracy.", "Міряє {0} патчів із новим профілем і показує підсумкову точність.", Jobs.VerifyChart().Count));
        readonly StackPanel _result = new();

        static readonly string[][] CurveArgs = { new[] { "-g2.2" }, new[] { "-g2.4" }, new[] { "-G2.4", "-f0" }, new[] { "-gs" }, new[] { "-gl" } };
        static readonly string[] CurveIds = { "2.2", "2.4", "bt1886", "srgb", "lstar" };

        public SdrPage() : base(S.T("SDR calibration", "SDR калібрування"),
            S.T("Grey scale and white point go into the video card LUT, the display's colours are described by an ICC profile. Run the live meter first if the monitor has RGB controls.",
                "Сіра шкала і точка білого записуються в LUT відеокарти, кольори дисплея описує ICC-профіль. Якщо в моніторі є RGB-регулятори, спершу «Живий замір»."))
        {
            // DMD61: steps in the order they are done: the room light (optional) is measured before the sensor goes on the screen
            Body.Children.Add(Ui.Guide(S.T("What to do", "Що робити"),
                S.T("Let the monitor warm up for about 30 minutes. In its menu turn off dynamic contrast, eco and other automatic picture modes.", "Дайте монітору прогрітися близько 30 хвилин. У його меню вимкніть динамічний контраст, еко та інші автоматичні режими картинки."),
                S.T("Optional, before the sensor goes on the screen: measure the room light. Put the sensor on the desk next to the screen with the diffuser over the lens and press “Measure ambient light”: it fills in the room light and a suitable white luminance. You can skip this step and type the luminance yourself.", "Необов'язково, перш ніж ставити датчик на екран: поміряйте освітлення кімнати. Покладіть датчик на стіл біля екрана з розсіювачем на лінзі й натисніть «Заміряти освітлення кімнати»: програма заповнить освітлення кімнати й відповідну яскравість білого. Цей крок можна пропустити й ввести яскравість самому."),
                S.T("If the monitor has RGB controls, get close to the target white first: Live meter → Auto OSD, or by hand (sensor on the screen).", "Якщо в моніторі є RGB-регулятори, спершу наблизьтесь до цільового білого: «Живий замір» → «Авто-OSD» або вручну (датчик на екрані)."),
                S.T("Choose the targets and the mode below and press Start calibration: the program asks for the name of the profile and where to save the report. Standard (like Calibrite PROFILER) takes about 20 minutes; the patches and the time of each mode are shown next to it. Esc cancels at any time.", "Виберіть цілі й режим нижче й натисніть «Почати калібрування»: програма запитає назву профілю й куди зберегти звіт. Стандарт (як Calibrite PROFILER) триває близько 20 хвилин; патчі й час кожного режиму показано поруч. Esc скасовує будь-коли."),
                S.T("The program then shows how to put the sensor on the screen: diffuser turned aside (lens uncovered), sensor in the centre. Windows HDR is switched off by the program for this display.", "Потім програма покаже, як поставити датчик на екран: розсіювач відведено вбік (лінза відкрита), датчик по центру. Windows HDR для цього дисплея програма вимикає сама."),
                S.T("While the sensor is on the screen no message appears: the program goes through the phases by itself (grey scale, profile, verification) and shows the progress at the bottom of the measurement screen.", "Поки датчик на екрані, жодних повідомлень не з'являється: програма сама проходить етапи (сіра шкала, профіль, перевірка) і показує хід унизу екрана заміру."),
                S.T("At the end the program says the sensor can be removed, sets the new profile as the default for this screen, saves the report and shows a summary. Do not change the screen brightness afterwards.", "Наприкінці програма скаже, що датчик можна зняти, зробить новий профіль основним для цього екрана, збереже звіт і покаже підсумок. Після цього не змінюйте яскравість екрана.")));
            Body.Children.Add(Ui.Card(Ui.H2(S.T("Targets", "Цілі")),
                Ui.Field(S.T("White point", "Точка білого"), _wp.View, tip: S.T("Target white: D65 for sRGB / Rec.709 work and games, D50 to match prints, “Native” keeps the panel's own white.", "Цільовий білий: D65 для роботи в sRGB / Rec.709 та ігор, D50 — під друк, «Рідна» лишає власний білий панелі.")),
                Ui.Field(S.T("White luminance, cd/m²", "Яскравість білого, кд/м²"), Ui.Row(_nits, Ui.Btn(S.T("Measure ambient light", "Заміряти освітлення кімнати"), FromRoom, false, "", tip: S.T("Measures the room with the diffuser and fills in a suitable white luminance.", "Міряє кімнату з розсіювачем і підставляє відповідну яскравість білого."))), S.T("Empty = native (maximum)", "Порожньо = рідна (максимум)"), tip: S.T("Target brightness of white: around 120 for a normal room, 80–100 for a dark room.", "Цільова яскравість білого: близько 120 для звичайної кімнати, 80–100 для темної.")),
                Ui.Field(S.T("Tone curve", "Тональна крива"), _curve, tip: S.T("How greys rise from black to white: 2.2 for PC and games, 2.4 or BT.1886 for video in a dark room, sRGB for the exact sRGB curve.", "Як сірі зростають від чорного до білого: 2.2 для ПК та ігор, 2.4 або BT.1886 для відео в темній кімнаті, sRGB — точна крива sRGB.")),
                Ui.Field(S.T("Room light, lx", "Освітлення кімнати, лк"), Ui.Row(_lux), S.T("Measured with the diffuser; used for the suggestion and the optional adjustment", "Міряється з розсіювачем; для підказки і необов'язкової корекції"), tip: S.T("Room illuminance used to adapt the tone curve. “Measure ambient light” fills it in.", "Освітленість кімнати для підлаштування тонової кривої. Її заповнює кнопка «Заміряти освітлення кімнати».")),
                _ambient, _ambientNote));
            Body.Children.Add(Ui.Card(Ui.H2(S.T("Quality", "Якість")),
                Ui.Field(S.T("Mode", "Режим"), Ui.Row(_mode, _modeInfo), tip: S.T("Standard uses the standard patch set of Calibrite PROFILER (119 patches); Advanced and Maximum its larger sets (211 and 461) and calibrate the grey scale more finely.", "Стандарт використовує стандартний набір патчів Calibrite PROFILER (119 патчів); Розширений і Максимум — більші набори (211 і 461) і точніше калібрують сіру шкалу.")),
                _install, _verify,
                Ui.Row(Ui.Btn(S.T("Start calibration", "Почати калібрування"), Run, true, "", tip: S.T("Runs the calibration and profiling. Keep the sensor on the screen; Esc cancels.", "Запускає калібрування і профілювання. Датчик має бути на екрані; Esc скасовує.")))));
            Body.Children.Add(_result);
            // DMD61: patches and estimated duration of the chosen mode, next to it
            _mode.SelectionChanged += (_, _) => UpdateEta();
            // the room-light adjustment has no effect without a measured room light
            _lux.TextChanged += (_, _) => UpdateAmbient();
            _verify.Checked += (_, _) => UpdateEta(); _verify.Unchecked += (_, _) => UpdateEta();
            UpdateEta();
            var rl = St.Cfg.Num("room_lux", double.NaN);
            if (!double.IsNaN(rl)) _lux.Text = rl.ToString("0.#", CultureInfo.InvariantCulture);
            UpdateAmbient();
        }

        void UpdateAmbient()
        {
            bool has = !double.IsNaN(Ui.Num(_lux, double.NaN));
            _ambient.IsEnabled = has;
            if (!has) _ambient.IsChecked = false;
        }

        int Mode => Math.Clamp(_mode.SelectedIndex, 0, 2);

        /// <summary>DMD61: patches and rough duration of the mode, from real runs with an i1Display-class probe on a laptop:
        /// the grey-scale calibration needs about 4 minutes to start plus ~5 s per reading (dark greys are slow),
        /// the profile and the verification ~1.8 s per patch, building the profile up to a few minutes.</summary>
        void UpdateEta()
        {
            int m = Mode, npatch = ModePatches[m];
            int ver = _verify.IsChecked == true ? Jobs.VerifyChart().Count : 0;
            double min = 4 + new[] { 106, 182, 416 }[m] * 5.0 / 60 + (npatch + ver) * 1.8 / 60 + new[] { 0.1, 1.5, 3 }[m];
            _modeInfo.Text = (ver > 0
                ? S.F("{0} patches for the profile + {1} for the verification", "{0} патчів для профілю + {1} для перевірки", npatch, ver)
                : S.F("{0} patches for the profile", "{0} патчів для профілю", npatch))
                + " · " + S.F("about {0:F0} minutes", "близько {0:F0} хв", min < 30 ? Math.Round(min) : Math.Round(min / 5) * 5);
        }

        bool _switching, _roomAsked;

        /// <summary>SDR calibration is done with Windows HDR off: the page switches it off by itself.</summary>
        public override void OnShow()
        {
            var d = St.Display;
            if (d == null || St.Job != null || _switching) return;
            Displays.RefreshColor(d);
            if (d.HdrOn)
            {
                // DMD61: say what is about to happen before the screen goes black; Cancel goes back to HDR calibration
                string hp = null; try { hp = ColorProfiles.Default(d, true); } catch { }
                var text = HdrOffFix.Needed(hp) ? S.T("SDR calibration is done with Windows HDR off. The program will now:\n\n1. turn Windows HDR off: the monitor may go black for a few seconds;\n2. clear the HDR colour correction that Windows keeps: if Windows asks for administrator permission, answer Yes;\n3. close and reopen itself on this page after a few seconds.\n\nDo not touch anything until the window is back.", "SDR-калібрування роблять з вимкненим HDR Windows. Зараз програма:\n\n1. вимкне HDR Windows: монітор може на кілька секунд стати чорним;\n2. скине HDR-корекцію кольору, яку залишає Windows: якщо Windows попросить дозвіл адміністратора, відповідайте «Так»;\n3. за кілька секунд закриється й знову відкриється на цій сторінці.\n\nНічого не торкайтеся, доки вікно не повернеться.") : S.T("SDR calibration is done with Windows HDR off. The program will now turn it off: the monitor may go black for a few seconds. Do not touch anything until the screen is back.", "SDR-калібрування роблять з вимкненим HDR Windows. Зараз програма його вимкне: монітор може на кілька секунд стати чорним. Нічого не торкайтеся, доки екран не повернеться.");
                if (MessageBox.Show(text, "HDRDisplayCalibrate", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
                { MainWindow.ShowPage(3); return; }
                TurnHdrOff(d);
            }
        }

        async void TurnHdrOff(DisplayTarget d)
        {
            _switching = true;
            St.Report(S.T("Turning Windows HDR off for the SDR calibration…", "Вимикаю Windows HDR для SDR-калібрування…"));
            string hdrProfile = null; try { hdrProfile = ColorProfiles.Default(d, true); } catch { }
            try { Displays.SetHdr(d, false); await System.Threading.Tasks.Task.Delay(6000); await HdrOffFix.Run(hdrProfile, 2); St.RefreshDisplays(); }   // DMD61
            catch (Exception e) { Log.Write("HDR off: " + e.Message); }
            finally { _switching = false; }
            St.Report(S.T("Windows HDR is off", "Windows HDR вимкнено"), 0);
        }

        void FromRoom() { if (!ProbeGuide.Ask(ProbeGuide.Mode.Ambient)) return; RunJob(async ct =>   // DMD61: guide for the room light
        {
            var (lux, _) = await Jobs.Ambient(ct);
            _lux.Text = lux.ToString("0.#", CultureInfo.InvariantCulture);
            double sug = lux <= 2 ? 100 : lux >= 1000 ? 220 : Cm.Interp(Math.Log10(lux), new[] { Math.Log10(2), 1, Math.Log10(50), Math.Log10(150), Math.Log10(400), 3 }, new[] { 100.0, 120, 140, 160, 185, 220 });
            _nits.Text = sug.ToString("0", CultureInfo.InvariantCulture);
            St.Report(S.F("Room {0:F1} lx → {1:F0} cd/m²", "Кімната {0:F1} лк → {1:F0} кд/м²", lux, sug), 1);
            Dispatcher.Invoke(() => MessageBox.Show(S.F("Room light measured: {0:F1} lx. Turn the diffuser aside (lens uncovered), then press Start calibration: the program shows how to put the sensor on the screen.", "Освітлення кімнати заміряно: {0:F1} лк. Відведіть розсіювач убік (лінза відкрита), потім натисніть «Почати калібрування»: програма покаже, як поставити датчик на екран.", lux), "HDRDisplayCalibrate"));
        }); }

        async void Run()
        {
            var d = St.Display;
            if (d?.ArgyllNumber is not int disp) { MessageBox.Show(S.T("Select a display first.", "Спершу виберіть дисплей.")); return; }
            if (d.HdrOn && MessageBox.Show(S.T("HDR is on for this display. SDR calibration should be done with HDR off. Continue anyway?", "Для цього дисплея увімкнено HDR. SDR-калібрування роблять з вимкненим HDR. Продовжити?"), "HDRDisplayCalibrate", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var wp = _wp.Xy(); double nits = Ui.Num(_nits, double.NaN), lux = Ui.Num(_lux, double.NaN);
            int ci = Math.Max(0, _curve.SelectedIndex);
            int qi = Mode, npatch = ModePatches[qi];
            bool verify = _verify.IsChecked == true, install = _install.IsChecked == true, ambient = _ambient.IsChecked == true;
            // DMD61: the optional room light comes before the sensor goes on the screen
            if (double.IsNaN(lux) && !_roomAsked)
            {
                var q = MessageBox.Show(S.T("Optional step: measure the room light first?\n\nYes: measure it now (sensor on the desk, diffuser over the lens); afterwards press Start calibration again.\nNo: skip it and calibrate with the luminance typed above.",
                                            "Необов'язковий крок: спершу поміряти освітлення кімнати?\n\nТак: поміряти зараз (датчик на столі, розсіювач на лінзі); потім знову натисніть «Почати калібрування».\nНі: пропустити й калібрувати з яскравістю, введеною вище."),
                                        "HDRDisplayCalibrate", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (q == MessageBoxResult.Cancel) return;
                _roomAsked = true;
                if (q == MessageBoxResult.Yes) { FromRoom(); return; }
            }
            // DMD61: everything that could ask something is done before the sensor goes on the screen: name of the
            // profile (display, date and time, so two calibrations on the same day do not overwrite each other) and report
            string proposed = string.Join("_", $"{d.Model}_{DateTime.Now:yyyy-MM-dd_HHmm}".Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
            var (name, reportDir, reportHtml) = ProfileNameDialog.Ask(proposed);
            if (!await FirewallFix.Ensure()) return;
            if (!ProbeGuide.Ask(ProbeGuide.Mode.Place, S.T("From now on no message appears until the program says the sensor can be removed. If the screen cannot reach the chosen white luminance, the program calibrates at the highest it can reach and tells you at the end.",
                                                           "Відтепер жодних повідомлень не буде, доки програма не скаже, що датчик можна зняти. Якщо екран не досягає вибраної яскравості білого, програма калібрує на максимально можливій і скаже про це наприкінці."))) return;
            // DMD61: phase X of N in the status line of the measurement screen
            int phases = 1 + (npatch > 0 ? 1 : 0) + (verify ? 1 : 0), phase = 0;
            string Phase(string what) => S.F("Phase {0} of {1}: {2}", "Етап {0} з {1}: {2}", ++phase, phases, what);
            RunJob(async ct =>
            {
                var dir = Paths.Session(name);
                var notes = new List<string>();
                string icc = null; bool installed = false;
                Report rep = null;
                // DMD61: one measurement screen from the first patch to the last, so the desktop never shows in between
                await Dispatcher.InvokeAsync(() => Jobs.Hold(true));
                try
                {
                    // 1. calibration
                    var a = St.Setup.Args(); a.Add("-v"); a.Add("-m"); a.Add("-q" + "lmh"[qi]);
                    if (wp is (double x, double y)) a.Add(string.Format(CultureInfo.InvariantCulture, "-w{0:F4},{1:F4}", x, y));
                    if (!double.IsNaN(nits)) a.Add(string.Format(CultureInfo.InvariantCulture, "-b{0:0.#}", nits));
                    a.AddRange(CurveArgs[ci]);
                    if (ambient && !double.IsNaN(lux)) a.Add(string.Format(CultureInfo.InvariantCulture, "-a{0:0.#}", lux));
                    a.Add(name);
                    if (St.Setup.OwnWindow) Argyll.LutLinear(disp);
                    // the white point correction costs some light: check the target luminance can be reached at all,
                    // otherwise dispcal keeps chasing it for hours and the white point suffers.
                    // DMD61: no question with the sensor on the screen: DDC/CI raises the brightness by itself, otherwise the
                    // target is lowered to what the screen reaches and the summary says so
                    if (!double.IsNaN(nits) && St.Setup.OwnWindow)
                    {
                        double native;
                        using (var rd = new Jobs.LiveReader(fast: true)) native = (await rd.Read(1, 1, 1, ct)).Y;
                        double reach = native * 0.95;
                        if (nits > reach)
                        {
                            bool raised = false, hasDdc;
                            using (var t = Ddc.Open(d)) hasDdc = t?.Get(Ddc.Brightness) != null;
                            if (hasDdc)
                                try
                                {
                                    St.Report(S.T("Raising monitor brightness…", "Піднімаю яскравість монітора…"));
                                    var res = await Jobs.AutoOsd(d, null, nits / 0.95, dir, ct);
                                    Log.Write("brightness for calibration: " + res);
                                    Argyll.LutLinear(disp);
                                    using (var rd = new Jobs.LiveReader(fast: true)) reach = (await rd.Read(1, 1, 1, ct)).Y * 0.95;
                                    raised = nits <= reach;
                                    if (raised) notes.Add(S.T("The monitor brightness was raised over DDC/CI to reach the target white luminance.", "Яскравість монітора піднято через DDC/CI, щоб досягти цільової яскравості білого."));
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception e) { Log.Write("DDC brightness: " + e.Message); }
                            if (!raised)
                            {
                                int bi = a.FindIndex(v => v.StartsWith("-b"));
                                a[bi] = string.Format(CultureInfo.InvariantCulture, "-b{0:0}", Math.Floor(reach));
                                notes.Add(S.F("This screen reaches about {0:F0} cd/m² after the white point correction, below the target of {1:F0} cd/m²: it was calibrated to {0:F0} cd/m². To reach the target, raise the screen brightness and calibrate again.",
                                              "Після корекції точки білого цей екран дає близько {0:F0} кд/м², менше за ціль {1:F0} кд/м²: його відкалібровано на {0:F0} кд/м². Щоб досягти цілі, підніміть яскравість екрана й відкалібруйте знову.", Math.Floor(reach), nits));
                            }
                        }
                    }
                    var (code, outp) = await Jobs.RunMeasuring("dispcal", a, dir, Phase(S.T("Grey scale calibration", "Калібрування сірої шкали")), ct);
                    if (code != 0) throw new Exception(Jobs.LastError(outp) ?? "dispcal failed");
                    var cal = Path.Combine(dir, name + ".cal");
                    // with our own patch window dispcal has no VideoLUT access and marks the curves as "not for the LUT";
                    // we load them into the LUT ourselves, so mark them as LUT curves, otherwise colprof leaves vcgt out of the profile
                    File.WriteAllText(cal, File.ReadAllText(cal).Replace("VIDEO_LUT_CALIBRATION_POSSIBLE \"NO\"", "VIDEO_LUT_CALIBRATION_POSSIBLE \"YES\""));
                    Argyll.LutLoad(disp, cal);
                    // 2. profile
                    if (npatch > 0)
                    {
                        var tg = new List<string> { "-v", "-d3", "-G", "-e4", "-B4", $"-s{Math.Clamp(npatch / 40, 5, 33)}", $"-g{Math.Clamp(npatch / 12, 9, 96)}", $"-f{npatch}", name };
                        var (tc, to) = await Argyll.RunAsync("targen", tg, dir, S.T("Patch set", "Набір патчів"), null, ct);
                        if (tc != 0) throw new Exception(Jobs.LastError(to) ?? "targen failed");
                        var ra = St.Setup.Args(); ra.Add("-v"); ra.Add(name);
                        var (rc, ro) = await Jobs.RunMeasuring("dispread", ra, dir, Phase(S.T("Profile", "Профіль")), ct);
                        if (rc != 0) throw new Exception(Jobs.LastError(ro) ?? "dispread failed");
                        // the patches were measured through these curves: attach them so the profile carries its calibration (vcgt)
                        File.AppendAllText(Path.Combine(dir, name + ".ti3"), Environment.NewLine + File.ReadAllText(cal));
                        // DMD61: Standard mode builds the profile at medium quality: much quicker to compute
                        // the profile type follows the mode: matrix for the standard set, XYZ LUT + matrix for the larger ones
                        var cp = new List<string> { "-v", qi == 0 ? "-qm" : "-qh", qi == 0 ? "-aS" : "-aX", "-D", name, "-C", "HDRDisplayCalibrate, ArgyllCMS", name };
                        var building = S.T("Building the profile: this can take a few minutes. Do not move the sensor.", "Будую профіль: це може тривати кілька хвилин. Не рухайте датчик.");
                        St.Report(building);
                        await Dispatcher.InvokeAsync(() => { Jobs.Held?.Paint(64, 64, 64); Jobs.Held?.SetStatus(building); });
                        var (cc, co) = await Argyll.RunAsync("colprof", cp, dir, S.T("Building profile", "Побудова профілю"), new Progress<Progress>(p => St.Report(building)), ct);
                        if (cc != 0) throw new Exception(Jobs.LastError(co) ?? "colprof failed");
                        icc = Path.Combine(dir, name + ".icm");
                        if (install)
                        {
                            Argyll.InstallProfile(disp, icc); Log.Write($"installed {icc}");
                            // DMD61: dispwin uses the old Windows method, which the Settings list of Windows 11 does not show:
                            // add the profile there too and make it the display default, as the HDR calibration does
                            try { ColorProfiles.Associate(d, Path.GetFileName(icc), false); installed = true; }
                            catch (Exception e) { Log.Write("SDR profile association: " + e.Message); }
                            Argyll.LutLoad(disp, icc);
                        }
                    }
                    // 3. verification, straight after the profile: no window in between
                    if (verify)
                    {
                        var m = await Jobs.Measure(Jobs.VerifyChart(), dir, "verify", Phase(S.T("Verification", "Перевірка")), ct);
                        rep = Report.Build(m, CurveIds[ci], wp ?? m.First(p => p.Is(1, 1, 1)).Xyz.Xy);
                    }
                }
                finally { await Dispatcher.InvokeAsync(() => Jobs.Hold(false)); }
                St.Report(S.T("SDR calibration done", "SDR калібрування завершено") + (icc != null ? " · " + Path.GetFileName(icc) : ""), 1);
                // DMD61: only now the sensor can be removed; then a summary of what was done
                Dispatcher.Invoke(() => ProbeGuide.Ask(ProbeGuide.Mode.Remove));
                Dispatcher.Invoke(() =>
                {
                    _result.Children.Clear();
                    var sum = new List<UIElement> { Ui.H2(S.T("Summary", "Підсумок")) };
                    if (icc != null && install)
                        sum.Add(Ui.Text(installed
                            ? S.F("Profile {0} installed and set as the default for this screen (Settings → Display → Color profile).", "Профіль {0} встановлено й зроблено основним для цього екрана (Параметри → Дисплей → Профіль кольору).", Path.GetFileNameWithoutExtension(icc))
                            : S.F("Profile {0} installed, but Windows did not accept it as the default: in Settings → Display → Color management press “Add profile” and choose it.", "Профіль {0} встановлено, але Windows не прийняла його як основний: у Параметри → Дисплей → Керування кольором натисніть «Додати профіль» і виберіть його.", Path.GetFileNameWithoutExtension(icc))));
                    foreach (var n in notes) sum.Add(Ui.Text(n));
                    sum.Add(Ui.Text(S.T("Do not change the screen brightness from now on: the calibration was measured at the current brightness. Turn off automatic brightness and power-saving dimming if the screen has them.", "Відтепер не змінюйте яскравість екрана: калібрування заміряно на поточній яскравості. Вимкніть автоматичну яскравість і енергозберігальне затемнення, якщо вони є.")));
                    if (icc != null && install)
                        sum.Add(Ui.Row(Ui.Btn(S.T("Check in Windows Settings", "Перевірити в Параметрах Windows"), ColorProfiles.OpenDisplaySettings, false, "")));
                    _result.Children.Add(Ui.Card(sum.ToArray()));
                    if (rep != null)
                        _result.Children.Add(Ui.Card(ReportView.Build(rep, CurveIds[ci], wp ?? rep.WhiteXy, S.T("Result", "Результат")),
                            Ui.Caption(S.T("Files: ", "Файли: ") + dir)));
                    // DMD61: the report is saved automatically, as at the end of the HDR calibration
                    var file = Path.Combine(reportDir, name + "_report" + (reportHtml ? ".html" : ".pdf"));
                    bool saved = ReportExport.SaveAuto(file, reportHtml, S.T("SDR calibration", "SDR калібрування") + " — " + name, _result.Children.Cast<UIElement>().ToList());
                    _result.Children.Add(Ui.Card(saved
                        ? (UIElement)Ui.Row(Ui.Text(S.T("Report saved: ", "Звіт збережено: ") + file), Ui.Btn(S.T("Open report", "Відкрити звіт"), () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true }); } catch { } }, false, ""))
                        : Ui.Text(S.T("The report could not be saved: use Export report on Verify & report.", "Не вдалося зберегти звіт: скористайтеся «Експортувати звіт» на сторінці «Перевірка і звіт»."))));
                    _result.Children.Add(PatternsPage.Suggestion());   // DMD61: suggest the independent check by eye
                });
            });
        }
    }
}
