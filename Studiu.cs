using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace OraDeStudiu
{
    /// <summary>O provocare: o piesă de repetat (la keyboard uneori în altă tonalitate) sau o piesă nouă de învățat.</summary>
    public class Provocare
    {
        public string Id { get; set; }
        public string Instrument { get; set; }   // "Pian" sau "Keyboard"
        public string Tip { get; set; }          // "repeta" sau "noua"
        public string Cale { get; set; }
        public string Tonalitate { get; set; }   // doar la keyboard, la repetare; null = tonalitatea piesei
        public string Data { get; set; }         // ziua în care a fost dată (yyyy-MM-dd)
        public string Termen { get; set; }       // până când (yyyy-MM-dd)
        public bool Facuta { get; set; }
        public string ZiuaFacuta { get; set; }
    }

    /// <summary>Tot ce se ține minte, în %APPDATA%\Ora de Studiu\date.json.</summary>
    public class Stare
    {
        public Dictionary<string, List<string>> FoldereRepertoriu { get; set; }
        public Dictionary<string, List<string>> FoldereDeInvatat { get; set; }
        public Dictionary<string, List<string>> Invatate { get; set; }      // piese învățate în aplicație, trecute în repertoriu
        public Dictionary<string, string> UltimaRepetata { get; set; }
        public List<Provocare> Provocari { get; set; }
        public int Puncte { get; set; }
        public int ZileLaRand { get; set; }
        public string UltimaZiActiva { get; set; }
        public string UltimaZiGenerata { get; set; }
        public List<string> Istoric { get; set; }
        public int Volum { get; set; }

        public Stare()
        {
            FoldereRepertoriu = new Dictionary<string, List<string>>();
            FoldereDeInvatat = new Dictionary<string, List<string>>();
            Invatate = new Dictionary<string, List<string>>();
            UltimaRepetata = new Dictionary<string, string>();
            Provocari = new List<Provocare>();
            Istoric = new List<string>();
            Volum = 80;
        }
    }

    /// <summary>Logica aplicației: piesele din foldere, provocările zilei, scorul.</summary>
    public class Studiu
    {
        public static readonly string[] Instrumente = { "Pian", "Keyboard" };
        public static readonly string[] Tonalitati = { "do", "do diez", "re", "mi bemol", "mi", "fa", "fa diez", "sol", "la bemol", "la", "si bemol", "si" };
        static readonly string[] Extensii = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".mid", ".midi" };
        const int PuncteInvatata = 10, PuncteRepetata = 2, PuncteAmanare = -5, PuncteTermenDepasit = -5;

        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ora de Studiu");
        static readonly string Fisier = Path.Combine(Folder, "date.json");

        public Stare S = new Stare();
        readonly Random rnd = new Random();

        public static string Azi { get { return DateTime.Today.ToString("yyyy-MM-dd"); } }
        static string Zi(DateTime d) { return d.ToString("yyyy-MM-dd"); }
        static DateTime DinZi(string s) { DateTime d; return DateTime.TryParse(s, out d) ? d.Date : DateTime.Today; }

        // ------------------------------------------------------------ salvare
        public void Incarca()
        {
            try
            {
                if (File.Exists(Fisier))
                {
                    var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var s = js.Deserialize<Stare>(File.ReadAllText(Fisier, Encoding.UTF8));
                    if (s != null) S = s;
                }
            }
            catch { }
            if (S.FoldereRepertoriu == null) S.FoldereRepertoriu = new Dictionary<string, List<string>>();
            if (S.FoldereDeInvatat == null) S.FoldereDeInvatat = new Dictionary<string, List<string>>();
            if (S.Invatate == null) S.Invatate = new Dictionary<string, List<string>>();
            if (S.UltimaRepetata == null) S.UltimaRepetata = new Dictionary<string, string>();
            if (S.Provocari == null) S.Provocari = new List<Provocare>();
            if (S.Istoric == null) S.Istoric = new List<string>();
            foreach (var i in Instrumente)
            {
                if (!S.FoldereRepertoriu.ContainsKey(i)) S.FoldereRepertoriu[i] = new List<string>();
                if (!S.FoldereDeInvatat.ContainsKey(i)) S.FoldereDeInvatat[i] = new List<string>();
                if (!S.Invatate.ContainsKey(i)) S.Invatate[i] = new List<string>();
            }
        }

        public void Salveaza()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var tmp = Fisier + ".tmp";
                File.WriteAllText(tmp, js.Serialize(S), Encoding.UTF8);
                if (File.Exists(Fisier)) File.Replace(tmp, Fisier, null); else File.Move(tmp, Fisier);
            }
            catch { }
        }

        // ------------------------------------------------------------ piesele
        static IEnumerable<string> FisiereDin(IEnumerable<string> foldere)
        {
            foreach (var f in foldere)
            {
                IEnumerable<string> toate;
                try { toate = Directory.EnumerateFiles(f, "*.*", SearchOption.AllDirectories).ToList(); }
                catch { continue; }
                foreach (var x in toate)
                    if (Extensii.Contains(Path.GetExtension(x).ToLowerInvariant())) yield return x;
            }
        }

        /// <summary>Repertoriul: piesele din folderele de repertoriu, plus cele învățate în aplicație.</summary>
        public List<string> Repertoriu(string instr)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rez = new List<string>();
            foreach (var f in FisiereDin(S.FoldereRepertoriu[instr]).Concat(S.Invatate[instr].Where(File.Exists)))
                if (set.Add(f)) rez.Add(f);
            return rez.OrderBy(Nume, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>De învățat: piesele din folderele de învățat care nu sunt deja în repertoriu.</summary>
        public List<string> DeInvatat(string instr)
        {
            var rep = new HashSet<string>(Repertoriu(instr), StringComparer.OrdinalIgnoreCase);
            var repNume = new HashSet<string>(rep.Select(Nume), StringComparer.CurrentCultureIgnoreCase);
            return FisiereDin(S.FoldereDeInvatat[instr]).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(f => !rep.Contains(f) && !repNume.Contains(Nume(f)))
                .OrderBy(Nume, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static string Nume(string cale) { return Path.GetFileNameWithoutExtension(cale); }

        // ------------------------------------------------------------ ziua
        /// <summary>La prima pornire din zi (sau la miezul nopții): termenele depășite și provocările noi.</summary>
        public bool ZiNoua()
        {
            var azi = Azi;
            if (S.UltimaZiGenerata == azi) return false;

            // ce s-a făcut în zilele trecute nu mai apare
            S.Provocari.RemoveAll(p => p.Facuta && p.ZiuaFacuta != azi);
            // termen depășit: minus 5 o dată, termenul trece pe azi
            foreach (var p in S.Provocari.Where(p => !p.Facuta && string.CompareOrdinal(p.Termen, azi) < 0).ToList())
            {
                Puncte(PuncteTermenDepasit, "Termen depășit: " + Text(p, false));
                p.Termen = azi;
            }
            // zilele la rând se rup dacă ieri nu s-a făcut nimic
            if (S.UltimaZiActiva != null && S.UltimaZiActiva != azi && S.UltimaZiActiva != Zi(DateTime.Today.AddDays(-1))) S.ZileLaRand = 0;

            foreach (var instr in Instrumente)
            {
                int repetari = rnd.NextDouble() < 0.3 ? 2 : 1;
                for (int k = 0; k < repetari; k++) AdaugaRepetare(instr);
                bool areNoua = S.Provocari.Any(p => p.Instrument == instr && p.Tip == "noua" && !p.Facuta);
                if (!areNoua && rnd.NextDouble() < 0.5) AdaugaNoua(instr);
            }
            S.UltimaZiGenerata = azi;
            Salveaza();
            return true;
        }

        /// <summary>O piesă din repertoriu de repetat; la keyboard, cam jumătate din ori, într-o tonalitate aleasă de program.</summary>
        public Provocare AdaugaRepetare(string instr)
        {
            var deja = new HashSet<string>(S.Provocari.Where(x => !x.Facuta || x.ZiuaFacuta == Azi).Select(x => x.Cale), StringComparer.OrdinalIgnoreCase);
            string ieri; S.UltimaRepetata.TryGetValue(instr, out ieri);
            var lista = Repertoriu(instr).Where(f => !deja.Contains(f)).ToList();
            if (lista.Count > 1 && ieri != null) lista.RemoveAll(f => string.Equals(f, ieri, StringComparison.OrdinalIgnoreCase));
            if (lista.Count == 0) return null;
            var p = new Provocare
            {
                Id = Guid.NewGuid().ToString("N"), Instrument = instr, Tip = "repeta", Cale = lista[rnd.Next(lista.Count)],
                Data = Azi, Termen = Azi,
                Tonalitate = instr == "Keyboard" && rnd.NextDouble() < 0.5 ? Tonalitati[rnd.Next(Tonalitati.Length)] : null
            };
            S.Provocari.Add(p);
            return p;
        }

        /// <summary>O piesă nouă din folderele de învățat; termen o săptămână la pian, trei zile la keyboard.</summary>
        public Provocare AdaugaNoua(string instr)
        {
            var deja = new HashSet<string>(S.Provocari.Select(x => x.Cale), StringComparer.OrdinalIgnoreCase);
            var lista = DeInvatat(instr).Where(f => !deja.Contains(f)).ToList();
            if (lista.Count == 0) return null;
            var p = new Provocare
            {
                Id = Guid.NewGuid().ToString("N"), Instrument = instr, Tip = "noua", Cale = lista[rnd.Next(lista.Count)],
                Data = Azi, Termen = Zi(DateTime.Today.AddDays(instr == "Pian" ? 6 : 2))
            };
            S.Provocari.Add(p);
            return p;
        }

        // ------------------------------------------------------------ ce faci cu o provocare
        public void Facut(Provocare p)
        {
            if (p.Facuta) return;
            p.Facuta = true; p.ZiuaFacuta = Azi;
            if (p.Tip == "noua")
            {
                if (!S.Invatate[p.Instrument].Contains(p.Cale, StringComparer.OrdinalIgnoreCase)) S.Invatate[p.Instrument].Add(p.Cale);
                Puncte(PuncteInvatata, "Învățată: " + Nume(p.Cale) + " (" + p.Instrument.ToLowerInvariant() + ")");
            }
            else
            {
                S.UltimaRepetata[p.Instrument] = p.Cale;
                Puncte(PuncteRepetata, "Repetată: " + Nume(p.Cale) + " (" + p.Instrument.ToLowerInvariant() + (p.Tonalitate != null ? ", cu tonica pe " + p.Tonalitate : "") + ")");
            }
            Activ();
            Salveaza();
        }

        public void Amana(Provocare p)
        {
            if (p.Facuta) return;
            var t = DinZi(p.Termen);
            if (t < DateTime.Today) t = DateTime.Today;
            p.Termen = Zi(t.AddDays(1));
            Puncte(PuncteAmanare, "Amânată: " + Nume(p.Cale) + " (" + p.Instrument.ToLowerInvariant() + ")");
            Salveaza();
        }

        /// <summary>O piesă de învățat pe care o știi deja: trece direct în repertoriu, fără puncte.</summary>
        public void InRepertoriu(string instr, string cale)
        {
            if (!S.Invatate[instr].Contains(cale, StringComparer.OrdinalIgnoreCase)) S.Invatate[instr].Add(cale);
            S.Provocari.RemoveAll(p => p.Tip == "noua" && !p.Facuta && string.Equals(p.Cale, cale, StringComparison.OrdinalIgnoreCase));
            Salveaza();
        }

        void Activ()
        {
            var azi = Azi;
            if (S.UltimaZiActiva == azi) return;
            S.ZileLaRand = S.UltimaZiActiva == Zi(DateTime.Today.AddDays(-1)) ? S.ZileLaRand + 1 : 1;
            S.UltimaZiActiva = azi;
        }

        void Puncte(int n, string ce)
        {
            S.Puncte += n;
            S.Istoric.Insert(0, DateTime.Now.ToString("d MMMM, HH:mm", new System.Globalization.CultureInfo("ro-RO")) + ". " + ce + ". " + (n > 0 ? "Plus " + n : "Minus " + (-n)) + " puncte.");
            if (S.Istoric.Count > 500) S.Istoric.RemoveRange(500, S.Istoric.Count - 500);
        }

        /// <summary>Provocările de arătat: ce nu e făcut, plus ce s-a făcut azi.</summary>
        public List<Provocare> DeAratat()
        {
            var azi = Azi;
            return S.Provocari.Where(p => !p.Facuta || p.ZiuaFacuta == azi)
                .OrderBy(p => p.Facuta).ThenBy(p => Array.IndexOf(Instrumente, p.Instrument)).ThenBy(p => p.Tip == "noua").ToList();
        }

        static readonly string[] ZileSapt = { "duminică", "luni", "marți", "miercuri", "joi", "vineri", "sâmbătă" };

        public static string Text(Provocare p, bool cuStare = true)
        {
            var sb = new StringBuilder(p.Instrument + ", ");
            if (p.Tip == "noua")
            {
                var t = DinZi(p.Termen);
                string termen = t <= DateTime.Today ? "azi" : t == DateTime.Today.AddDays(1) ? "mâine" : ZileSapt[(int)t.DayOfWeek];
                sb.Append("piesă nouă de învățat, până " + (termen == "azi" || termen == "mâine" ? termen : "" + termen) + ": " + Nume(p.Cale));
            }
            else if (p.Tonalitate != null) sb.Append("de repetat în altă tonalitate, cu tonica pe " + p.Tonalitate + ": " + Nume(p.Cale));
            else sb.Append("de repetat: " + Nume(p.Cale));
            if (cuStare && p.Facuta) sb.Append(". Făcută");
            else if (cuStare && p.Tip == "repeta" && string.CompareOrdinal(p.Termen, Azi) > 0) sb.Append(". Amânată pe mâine");
            return sb.ToString();
        }
    }
}
