using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OraDeStudiu
{
    /// <summary>
    /// Actualizare automată la pornire: citește version.txt din release-ul „latest” de pe GitHub; dacă e mai nouă,
    /// descarcă OraDeStudiu-Setup.exe într-o fereastră mică cu bară de progres (NVDA face bipurile ei)
    /// și spune doar procentul, din 10 în 10. Apoi instalează fără ferestre și aplicația se redeschide singură.
    /// </summary>
    static class Updater
    {
        const string Base = "https://github.com/razvan-raftul/OraDeStudiu/releases/download/latest/";

        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        public static void CheckAsync(MainForm form, bool manual = false)
        {
            Task.Run(() =>
            {
                try
                {
                    string text;
                    using (var w = new WebClient()) { w.Headers[HttpRequestHeader.UserAgent] = "OraDeStudiu"; text = w.DownloadString(Base + "version.txt"); }
                    Version remote;
                    if (!Version.TryParse(text.Trim(), out remote)) { if (manual) Say(form, "Nu am putut citi versiunea de pe internet."); return; }
                    var cur = Current;
                    var curCmp = new Version(cur.Major, cur.Minor, Math.Max(0, cur.Build));
                    var remCmp = new Version(remote.Major, remote.Minor, Math.Max(0, remote.Build));
                    if (remCmp <= curCmp) { if (manual) Say(form, "Ai deja ultima versiune, " + curCmp + "."); return; }
                    form.BeginInvoke((Action)(() => Download(form, remCmp)));
                }
                catch { if (manual) Say(form, "Nu am putut verifica actualizările. Verifică legătura la internet."); }
            });
        }

        static void Say(MainForm f, string m) { try { f.BeginInvoke((Action)(() => f.Announce(m))); } catch { } }

        static void Download(MainForm form, Version v)
        {
            var setup = Path.Combine(Path.GetTempPath(), "OraDeStudiu-Setup-" + v + ".exe");
            var win = new Form
            {
                Text = "Ora de Studiu", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen,
                Width = 360, Height = 110, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, TopMost = true
            };
            var bar = new ProgressBar { Left = 15, Top = 20, Width = 310, Height = 25, Minimum = 0, Maximum = 100, AccessibleName = "Actualizare" };
            win.Controls.Add(bar);
            int spus = -1;
            var w = new WebClient();
            w.Headers[HttpRequestHeader.UserAgent] = "OraDeStudiu";
            w.DownloadProgressChanged += (s, e) =>
            {
                int p = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                bar.Value = p;
                int zece = p / 10 * 10;
                if (zece > spus && zece < 100)
                {
                    spus = zece;
                    try { bar.AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other, System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent, zece + " la sută"); } catch { }
                }
            };
            w.DownloadFileCompleted += (s, e) =>
            {
                w.Dispose();
                bool ok = e.Error == null && !e.Cancelled && File.Exists(setup) && new FileInfo(setup).Length > 100000;
                if (!ok) { win.Close(); return; }
                bar.Value = 100;
                try { bar.AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other, System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent, "100 la sută"); } catch { }
                try
                {
                    Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /AUTOUPDATE") { UseShellExecute = true });
                    win.Close();
                    form.CloseForUpdate();
                }
                catch { win.Close(); }
            };
            win.Shown += (s, e) => { bar.Focus(); w.DownloadFileAsync(new Uri(Base + "OraDeStudiu-Setup.exe"), setup); };
            win.Show(form);
        }
    }
}
