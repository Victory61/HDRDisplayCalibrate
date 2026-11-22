// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Added 2026 by DMD61 (Victory61): ArgyllCMS serves the measurement patches through a small web server, so the first
// time each tool runs Windows asks whether it may communicate on networks. The question appears in the middle of the
// screen, under the sensor. The app adds the firewall rules itself before the sensor is placed, so it never appears.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Pages
{
    public static class FirewallFix
    {
        static readonly string[] Tools = { "dispcal", "dispread", "spotread" };
        static string RuleName(string tool) => "HDRDisplayCalibrate ArgyllCMS " + tool;

        static int Netsh(string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("netsh", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                p.StandardOutput.ReadToEnd(); p.WaitForExit(15000);
                return p.ExitCode;
            }
            catch (Exception e) { Log.Write("netsh: " + e.Message); return -1; }
        }

        /// <summary>True when the rules for the tools in this ArgyllCMS folder are already there.</summary>
        static bool Present(string bin)
        {
            foreach (var t in Tools)
                if (Netsh($"advfirewall firewall show rule name=\"{RuleName(t)}\" verbose") != 0) return false;
            return AppState.I.Cfg.Str("firewall_bin") == bin;
        }

        /// <summary>Called by "--firewall-rules &lt;bin&gt;" (elevated). The rules block connections from other computers:
        /// the patch server is only used by this app on the same PC (loopback), which the firewall does not filter.
        /// With a rule present Windows no longer asks.</summary>
        public static void AddRules(string bin)
        {
            foreach (var t in Tools)
            {
                var exe = Path.Combine(bin, t + ".exe");
                Netsh($"advfirewall firewall delete rule name=\"{RuleName(t)}\"");
                int code = Netsh($"advfirewall firewall add rule name=\"{RuleName(t)}\" dir=in action=block program=\"{exe}\" enable=yes profile=any");
                Log.Write($"firewall rule {t}: {code}");
            }
        }

        /// <summary>Before the sensor is placed: makes sure Windows will not ask about the firewall during the measurement.
        /// Returns false only if the user cancels.</summary>
        public static async Task<bool> Ensure()
        {
            var st = AppState.I;
            if (!st.Setup.OwnWindow) return true;
            string bin = Argyll.Bin;
            if (string.IsNullOrEmpty(bin)) return true;
            if (await Task.Run(() => Present(bin))) return true;
            st.Report(S.T("Adding the firewall rules for ArgyllCMS, so Windows does not ask during the measurement…", "Додаю правила брандмауера для ArgyllCMS, щоб Windows не питала під час заміру…"));
            bool ok = await Task.Run(() =>
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath, $"--firewall-rules \"{bin}\"") { UseShellExecute = true, Verb = "runas" });
                    p?.WaitForExit(30000);
                    foreach (var t in Tools)
                        if (Netsh($"advfirewall firewall show rule name=\"{RuleName(t)}\" verbose") != 0) return false;
                    return true;
                }
                catch (Exception e) { Log.Write("firewall rules: " + e.Message); return false; }
            });
            if (ok) { st.Cfg.Set("firewall_bin", bin); st.Cfg.Save(); st.Report(S.T("Firewall rules for ArgyllCMS added", "Правила брандмауера для ArgyllCMS додано"), 0); return true; }
            // no permission: say it now, before the sensor goes on the screen
            return MessageBox.Show(S.T("The firewall rules for ArgyllCMS could not be added (no administrator permission).\n\nDuring the measurement Windows may therefore ask whether \"dispcal\" or \"dispread\" may communicate on networks. The measurement works with either answer: press Cancel. The question appears in the middle of the screen; answer it without moving the sensor if you can.",
                                       "Не вдалося додати правила брандмауера для ArgyllCMS (немає дозволу адміністратора).\n\nТому під час заміру Windows може запитати, чи можна «dispcal» або «dispread» працювати в мережі. Замір працює за будь-якої відповіді: натисніть «Скасувати». Запитання з'являється посередині екрана; якщо можете, відповідайте, не рухаючи датчик."),
                                   "HDRDisplayCalibrate", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK;
        }
    }
}
