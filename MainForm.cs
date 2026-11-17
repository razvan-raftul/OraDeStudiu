using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Automation;
using Microsoft.Win32;

namespace OraDeStudiu
{
    /// <summary>
    /// Fereastra: taburile Azi, Pian, Keyboard, Player, Scor, Setări (Control 1–6).
    /// Scurtăturile playerului merg oriunde: F5 pornește sau pune pauză, F6 spune unde ești,
    /// F7 și F8 înapoi și înainte 5 secunde, F9 mai rar, F11 mai repede, F12 viteza normală.
    /// </summary>
    public class MainForm : Form
    {
        readonly Studiu st = new Studiu();
        readonly Player pl = new Player();
        readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        readonly TextBox stare = new TextBox { ReadOnly = true, Dock = DockStyle.Bottom, AccessibleName = "Stare", TabStop = false };
        readonly Timer ceas = new Timer { Interval = 60000 };
        bool closingForUpdate;

        // Azi
        ListBox lAzi;
        // Pian și Keyboard
        readonly Dictionary<string, ListBox> lRepFoldere = new Dictionary<string, ListBox>(), lInvFoldere = new Dictionary<string, ListBox>(),
            lRep = new Dictionary<string, ListBox>(), lInv = new Dictionary<string, ListBox>();
        readonly Dictionary<string, Label> eRep = new Dictionary<string, Label>(), eInv = new Dictionary<string, Label>();
        readonly Dictionary<string, List<string>> cRep = new Dictionary<string, List<string>>(), cInv = new Dictionary<string, List<string>>();
        // Player
        TextBox tPiesa;
        TrackBar tVolum;
        // Scor
        TextBox tScor;
        // Setări
        CheckBox cPornire;

        public MainForm()
        {
            Text = "Ora de Studiu";
            Width = 900; Height = 640;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            st.Incarca();

            Controls.Add(tabs);
            Controls.Add(stare);
            tabs.TabPages.Add(PaginaAzi());
            foreach (var i in Studiu.Instrumente) tabs.TabPages.Add(PaginaInstrument(i));
            tabs.TabPages.Add(PaginaPlayer());
            tabs.TabPages.Add(PaginaScor());
            tabs.TabPages.Add(PaginaSetari());
            tabs.SelectedIndexChanged += (s, e) => LaTab();

            ceas.Tick += (s, e) => { if (st.ZiNoua()) { ReimprospateazaAzi(); Announce("A început o zi nouă. Ai provocări noi în tabul Azi."); } };
            Shown += (s, e) =>
            {
                st.ZiNoua();
                ReimprospateazaToate();
                ceas.Start();
                var n = st.DeAratat().Count(p => !p.Facuta && string.CompareOrdinal(p.Termen, Studiu.Azi) <= 0);
                Announce(n == 0 ? "Azi nu ai provocări. Adaugă folderele cu piese în taburile Pian și Keyboard." :
                    n == 1 ? "Azi ai o provocare." : "Azi ai " + n + " provocări.");
                lAzi.Focus();
                Updater.CheckAsync(this);
            };
            FormClosing += (s, e) => { pl.Opreste(); st.Salveaza(); };
        }

        // ================================================================ ajutoare
        public void Announce(string msg, bool important = true)
        {
            stare.Text = msg;
            try
            {
                stare.AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted,
                    important ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent, msg);
            }
            catch { }
        }

        public void CloseForUpdate() { closingForUpdate = true; Close(); }

        static FlowLayoutPanel Panou(TabPage p)
        {
            var f = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            p.Controls.Add(f);
            return f;
        }

        static Label Eticheta(Control parent, string text)
        {
            var l = new Label { Text = text, AutoSize = true, Margin = new Padding(3, 10, 3, 2) };
            parent.Controls.Add(l);
            return l;
        }

        static ListBox Lista(Control parent, string nume, int inaltime = 120)
        {
            var l = new ListBox { Width = 820, Height = inaltime, AccessibleName = nume, IntegralHeight = false, HorizontalScrollbar = true };
            parent.Controls.Add(l);
            return l;
        }

        static FlowLayoutPanel RandButoane(Control parent, params Button[] b)
        {
            var r = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Width = 830, Margin = new Padding(0) };
            r.Controls.AddRange(b);
            parent.Controls.Add(r);
            return r;
        }

        static Button Buton(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
            b.Click += click;
            return b;
        }

        // ================================================================ Azi
        TabPage PaginaAzi()
        {
            var p = new TabPage("Azi");
            var f = Panou(p);
            Eticheta(f, "Provocările de azi (Enter: ascultă piesa)");
            lAzi = Lista(f, "Provocările de azi", 260);
            lAzi.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AsculataProvocarea(); } };
            RandButoane(f,
                Buton("&Ascultă piesa", (s, e) => AsculataProvocarea()),
                Buton("Am &făcut-o", (s, e) => FacutaProvocarea()),
                Buton("A&mână pe mâine (minus 5 puncte)", (s, e) => AmanaProvocarea()));
            RandButoane(f,
                Buton("Încă o provocare la &pian", (s, e) => IncaUna("Pian")),
                Buton("Încă o provocare la &keyboard", (s, e) => IncaUna("Keyboard")));
            return p;
        }

        Provocare Selectata()
        {
            var lista = lAzi.Tag as List<Provocare>;
            int i = lAzi.SelectedIndex;
            if (lista == null || i < 0 || i >= lista.Count) { Announce("Alege întâi o provocare din listă."); lAzi.Focus(); return null; }
            return lista[i];
        }

        void ReimprospateazaAzi(string pastreazaId = null)
        {
            var lista = st.DeAratat();
            int vechi = lAzi.SelectedIndex;
            lAzi.BeginUpdate();
            lAzi.Items.Clear();
            foreach (var p in lista) lAzi.Items.Add(Studiu.Text(p));
            if (lista.Count == 0) lAzi.Items.Add("Nu ai provocări acum. Adaugă folderele cu piese în taburile Pian și Keyboard sau cere încă o provocare.");
            lAzi.EndUpdate();
            lAzi.Tag = lista;
            int k = pastreazaId != null ? lista.FindIndex(x => x.Id == pastreazaId) : -1;
            if (k < 0) k = Math.Min(Math.Max(vechi, 0), lAzi.Items.Count - 1);
            if (lAzi.Items.Count > 0) lAzi.SelectedIndex = k;
        }

        void AsculataProvocarea() { var p = Selectata(); if (p != null) Asculta(p.Cale); }

        void FacutaProvocarea()
        {
            var p = Selectata(); if (p == null) return;
            if (p.Facuta) { Announce("E deja făcută."); return; }
            st.Facut(p);
            ReimprospateazaAzi(p.Id);
            if (p.Tip == "noua") { ReimprospateazaInstrument(p.Instrument); Announce("Bravo! Ai învățat-o și am pus-o în repertoriu. Plus 10 puncte. Ai " + st.S.Puncte + "."); }
            else Announce("Bravo! Plus 2 puncte. Ai " + st.S.Puncte + ".");
            ReimprospateazaScor();
        }

        void AmanaProvocarea()
        {
            var p = Selectata(); if (p == null) return;
            if (p.Facuta) { Announce("E deja făcută."); return; }
            st.Amana(p);
            ReimprospateazaAzi(p.Id);
            ReimprospateazaScor();
            Announce("Amânată pe mâine. Minus 5 puncte. Ai " + st.S.Puncte + ".");
        }

        void IncaUna(string instr)
        {
            var p = st.AdaugaRepetare(instr);
            if (p == null) { Announce("Nu mai am piese de dat la " + instr.ToLowerInvariant() + ". Adaugă un folder de repertoriu în tabul " + instr + "."); return; }
            st.Salveaza();
            ReimprospateazaAzi(p.Id);
            lAzi.Focus();
            Announce(Studiu.Text(p, false) + ".");
        }

        // ================================================================ Pian și Keyboard
        TabPage PaginaInstrument(string instr)
        {
            var p = new TabPage(instr);
            var f = Panou(p);
            string mic = instr.ToLowerInvariant();

            Eticheta(f, "Folderele cu repertoriul meu (piesele pe care le știu)");
            lRepFoldere[instr] = Lista(f, "Folderele cu repertoriul de " + mic, 70);
            RandButoane(f,
                Buton("Adaugă un folder de &repertoriu...", (s, e) => AdaugaFolder(instr, true)),
                Buton("Scoate folderul de repertoriu selectat", (s, e) => ScoateFolder(instr, true)));

            Eticheta(f, "Folderele cu piese pe care vreau să le învăț");
            lInvFoldere[instr] = Lista(f, "Folderele cu piese de învățat la " + mic, 70);
            RandButoane(f,
                Buton("Adaugă un folder cu piese de &învățat...", (s, e) => AdaugaFolder(instr, false)),
                Buton("Scoate folderul de învățat selectat", (s, e) => ScoateFolder(instr, false)));

            eRep[instr] = Eticheta(f, "Repertoriul");
            lRep[instr] = Lista(f, "Repertoriul de " + mic, 150);
            lRep[instr].KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AscultaDinLista(instr, true); } };
            RandButoane(f, Buton("Ascultă piesa din repertoriu", (s, e) => AscultaDinLista(instr, true)));

            eInv[instr] = Eticheta(f, "De învățat");
            lInv[instr] = Lista(f, "Piese de învățat la " + mic, 150);
            lInv[instr].KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AscultaDinLista(instr, false); } };
            RandButoane(f,
                Buton("Ascultă piesa de învățat", (s, e) => AscultaDinLista(instr, false)),
                Buton("O știu deja, mut-o în repertoriu", (s, e) => StiuDeja(instr)));
            return p;
        }

        void ReimprospateazaInstrument(string instr)
        {
            var lf = lRepFoldere[instr]; lf.Items.Clear(); foreach (var x in st.S.FoldereRepertoriu[instr]) lf.Items.Add(x);
            var li = lInvFoldere[instr]; li.Items.Clear(); foreach (var x in st.S.FoldereDeInvatat[instr]) li.Items.Add(x);
            cRep[instr] = st.Repertoriu(instr);
            cInv[instr] = st.DeInvatat(instr);
            Umple(lRep[instr], cRep[instr]);
            Umple(lInv[instr], cInv[instr]);
            eRep[instr].Text = "Repertoriul (" + Bucati(cRep[instr].Count) + ")";
            eInv[instr].Text = "De învățat (" + Bucati(cInv[instr].Count) + ")";
            lRep[instr].AccessibleName = "Repertoriul de " + instr.ToLowerInvariant() + ", " + Bucati(cRep[instr].Count);
            lInv[instr].AccessibleName = "Piese de învățat la " + instr.ToLowerInvariant() + ", " + Bucati(cInv[instr].Count);
        }

        static string Bucati(int n) { return n == 1 ? "o piesă" : n + (n >= 20 ? " de piese" : " piese"); }

        static void Umple(ListBox l, List<string> cai)
        {
            int vechi = l.SelectedIndex;
            l.BeginUpdate();
            l.Items.Clear();
            foreach (var c in cai) l.Items.Add(Studiu.Nume(c));
            l.EndUpdate();
            if (l.Items.Count > 0) l.SelectedIndex = Math.Min(Math.Max(vechi, 0), l.Items.Count - 1);
        }

        void AdaugaFolder(string instr, bool repertoriu)
        {
            using (var d = new FolderBrowserDialog { Description = repertoriu ? "Alege folderul cu repertoriul de " + instr.ToLowerInvariant() : "Alege folderul cu piese de învățat la " + instr.ToLowerInvariant(), ShowNewFolderButton = false })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var lista = repertoriu ? st.S.FoldereRepertoriu[instr] : st.S.FoldereDeInvatat[instr];
                if (lista.Contains(d.SelectedPath, StringComparer.OrdinalIgnoreCase)) { Announce("Folderul e deja în listă."); return; }
                lista.Add(d.SelectedPath);
                st.Salveaza();
                Cursor = Cursors.WaitCursor;
                ReimprospateazaInstrument(instr);
                Cursor = Cursors.Default;
                Announce("Am adăugat folderul " + Path.GetFileName(d.SelectedPath) + ". " +
                    (repertoriu ? "În repertoriu sunt acum " + Bucati(cRep[instr].Count) : "De învățat sunt acum " + Bucati(cInv[instr].Count)) + ".");
                // dacă azi încă nu avea ce să-ți dea la acest instrument, îți dă acum
                if (!st.S.Provocari.Any(p => p.Instrument == instr && !p.Facuta))
                {
                    if (repertoriu) st.AdaugaRepetare(instr); else st.AdaugaNoua(instr);
                    st.Salveaza();
                    ReimprospateazaAzi();
                }
            }
        }

        void ScoateFolder(string instr, bool repertoriu)
        {
            var l = repertoriu ? lRepFoldere[instr] : lInvFoldere[instr];
            var lista = repertoriu ? st.S.FoldereRepertoriu[instr] : st.S.FoldereDeInvatat[instr];
            int i = l.SelectedIndex;
            if (i < 0 || i >= lista.Count) { Announce("Alege întâi un folder din listă."); l.Focus(); return; }
            var nume = Path.GetFileName(lista[i]);
            lista.RemoveAt(i);
            st.Salveaza();
            ReimprospateazaInstrument(instr);
            Announce("Am scos folderul " + nume + " din listă. Fișierele rămân pe calculator.");
        }

        void AscultaDinLista(string instr, bool repertoriu)
        {
            var l = repertoriu ? lRep[instr] : lInv[instr];
            var c = repertoriu ? cRep[instr] : cInv[instr];
            int i = l.SelectedIndex;
            if (c == null || i < 0 || i >= c.Count) { Announce("Lista e goală sau nu e aleasă nicio piesă."); return; }
            Asculta(c[i]);
        }

        void StiuDeja(string instr)
        {
            var c = cInv[instr];
            int i = lInv[instr].SelectedIndex;
            if (c == null || i < 0 || i >= c.Count) { Announce("Alege întâi o piesă din lista De învățat."); return; }
            var nume = Studiu.Nume(c[i]);
            st.InRepertoriu(instr, c[i]);
            ReimprospateazaInstrument(instr);
            ReimprospateazaAzi();
            lInv[instr].Focus();
            Announce(nume + " e acum în repertoriu.");
        }

        // ================================================================ Player
        TabPage PaginaPlayer()
        {
            var p = new TabPage("Player");
            var f = Panou(p);
            Eticheta(f, "Piesa care se aude");
            tPiesa = new TextBox { ReadOnly = true, Width = 820, AccessibleName = "Piesa care se aude", Text = "Nicio piesă. Alege una din Azi, Pian sau Keyboard și apasă Enter." };
            f.Controls.Add(tPiesa);
            RandButoane(f,
                Buton("Pornește sau pune pauză (F5)", (s, e) => PornestePauza()),
                Buton("Unde sunt (F6)", (s, e) => SpuneUnde()),
                Buton("Înapoi 5 secunde (F7)", (s, e) => Sari(-5)),
                Buton("Înainte 5 secunde (F8)", (s, e) => Sari(5)));
            RandButoane(f,
                Buton("Mai rar (F9)", (s, e) => Viteza(-5)),
                Buton("Mai repede (F11)", (s, e) => Viteza(5)),
                Buton("Viteza normală (F12)", (s, e) => Viteza(0)),
                Buton("De la început", (s, e) => { pl.Pozitie = 0; Announce("De la început."); }));
            Eticheta(f, "Volumul");
            tVolum = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, Width = 400, Value = Math.Max(0, Math.Min(100, st.S.Volum)), AccessibleName = "Volumul" };
            tVolum.ValueChanged += (s, e) => { st.S.Volum = tVolum.Value; pl.SetVolum(tVolum.Value); };
            f.Controls.Add(tVolum);
            Eticheta(f, "Scurtături, oriunde în aplicație: F5 pornește sau pune pauză, F6 spune unde ești, F7 și F8 înapoi și înainte 5 secunde, F9 mai rar, F11 mai repede, F12 viteza normală. Viteza se schimbă fără să se schimbe tonul.").MaximumSize = new Size(820, 0);
            return p;
        }

        void Asculta(string cale)
        {
            if (!File.Exists(cale)) { Announce("Nu găsesc fișierul " + Path.GetFileName(cale) + ". Poate a fost mutat sau șters."); return; }
            try
            {
                pl.Deschide(cale, st.S.Volum);
                tPiesa.Text = Studiu.Nume(cale);
                Announce("Se aude " + Studiu.Nume(cale) + (pl.Viteza != 100 ? ", la viteza " + pl.Viteza + " la sută" : "") + ". F5 pune pauză.", false);
            }
            catch (Exception ex) { Announce("Nu pot reda piesa: " + ex.Message); }
        }

        void PornestePauza()
        {
            if (pl.Cale == null) { Announce("Nu e aleasă nicio piesă. Alege una și apasă Enter."); return; }
            bool canta = pl.Canta;
            pl.PornestePauza();
            Announce(canta ? "Pauză la " + Player.Timp(pl.Pozitie) + "." : "Pornit.", false);
        }

        void SpuneUnde()
        {
            if (pl.Cale == null) { Announce("Nu e aleasă nicio piesă."); return; }
            Announce(Player.Timp(pl.Pozitie) + " din " + Player.Timp(pl.Durata) + (pl.Viteza != 100 ? ", viteza " + pl.Viteza + " la sută" : "") + ".", false);
        }

        void Sari(double sec)
        {
            if (pl.Cale == null) { Announce("Nu e aleasă nicio piesă."); return; }
            pl.Pozitie = pl.Pozitie + sec;
            Announce(Player.Timp(pl.Pozitie), false);
        }

        void Viteza(int pas)
        {
            pl.SetViteza(pas == 0 ? 100 : pl.Viteza + pas);
            Announce(pl.Viteza == 100 ? "Viteza normală." : "Viteza " + pl.Viteza + " la sută.", false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5: PornestePauza(); return true;
                case Keys.F6: SpuneUnde(); return true;
                case Keys.F7: Sari(-5); return true;
                case Keys.F8: Sari(5); return true;
                case Keys.F9: Viteza(-5); return true;
                case Keys.F11: Viteza(5); return true;
                case Keys.F12: Viteza(0); return true;
            }
            if ((keyData & Keys.Modifiers) == Keys.Control)
            {
                var k = keyData & Keys.KeyCode;
                if (k >= Keys.D1 && k <= Keys.D9 && k - Keys.D1 < tabs.TabCount)
                {
                    tabs.SelectedIndex = k - Keys.D1;
                    var primul = PrimulControl(tabs.SelectedTab);
                    if (primul != null) primul.Focus();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static Control PrimulControl(Control c)
        {
            foreach (Control x in c.Controls)
            {
                if (x.CanSelect && x.TabStop && !(x is Label) && !(x is FlowLayoutPanel)) return x;
                var y = PrimulControl(x);
                if (y != null) return y;
            }
            return null;
        }

        // ================================================================ Scor
        TabPage PaginaScor()
        {
            var p = new TabPage("Scor");
            var f = Panou(p);
            Eticheta(f, "Scorul și istoricul");
            tScor = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 820, Height = 440, AccessibleName = "Scorul și istoricul" };
            f.Controls.Add(tScor);
            return p;
        }

        void ReimprospateazaScor()
        {
            var s = st.S;
            var text = "Puncte: " + s.Puncte + ".\r\nZile la rând: " + s.ZileLaRand + ".\r\n" +
                "La pian, în repertoriu: " + Bucati(st.Repertoriu("Pian").Count) + ". La keyboard: " + Bucati(st.Repertoriu("Keyboard").Count) + ".\r\n\r\n" +
                "Cum se câștigă: piesă nouă învățată plus 10, piesă repetată plus 2, amânare minus 5, termen depășit minus 5.\r\n\r\nIstoric:\r\n" +
                (s.Istoric.Count == 0 ? "Încă nimic." : string.Join("\r\n", s.Istoric.Take(200)));
            tScor.Text = text;
        }

        // ================================================================ Setări
        const string CheieRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

        TabPage PaginaSetari()
        {
            var p = new TabPage("Setări");
            var f = Panou(p);
            cPornire = new CheckBox { Text = "Pornește Ora de Studiu odată cu Windows (îți spune provocările zilei)", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            try { using (var k = Registry.CurrentUser.OpenSubKey(CheieRun)) cPornire.Checked = k != null && k.GetValue("Ora de Studiu") != null; } catch { }
            cPornire.CheckedChanged += (s, e) =>
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(CheieRun))
                    {
                        if (cPornire.Checked) k.SetValue("Ora de Studiu", "\"" + Application.ExecutablePath + "\"");
                        else k.DeleteValue("Ora de Studiu", false);
                    }
                    Announce(cPornire.Checked ? "Pornește odată cu Windows." : "Nu mai pornește odată cu Windows.");
                }
                catch (Exception ex) { Announce("Nu am putut schimba: " + ex.Message); }
            };
            f.Controls.Add(cPornire);
            RandButoane(f,
                Buton("Caută actualizări", (s, e) => { Announce("Caut actualizări..."); Updater.CheckAsync(this, true); }),
                Buton("Deschide folderul cu datele aplicației", (s, e) => { try { Directory.CreateDirectory(Studiu.Folder); Process.Start(Studiu.Folder); } catch { } }),
                Buton("Despre", (s, e) => MessageBox.Show(this, "Ora de Studiu, versiunea " + Updater.Current.ToString(3) + ".\r\n\r\nÎți dă în fiecare zi provocări din repertoriul tău și piese noi de învățat, la pian și la keyboard.", "Despre")));
            return p;
        }

        // ================================================================ reîmprospătare
        void LaTab()
        {
            var t = tabs.SelectedTab.Text;
            if (t == "Scor") ReimprospateazaScor();
            else if (Studiu.Instrumente.Contains(t)) ReimprospateazaInstrument(t);
            else if (t == "Azi") ReimprospateazaAzi();
        }

        void ReimprospateazaToate()
        {
            foreach (var i in Studiu.Instrumente) ReimprospateazaInstrument(i);
            ReimprospateazaAzi();
            ReimprospateazaScor();
        }
    }
}
