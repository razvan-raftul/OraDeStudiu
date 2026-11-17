using System;

namespace OraDeStudiu
{
    /// <summary>
    /// Redarea melodiilor prin Windows Media Player (vine cu Windows), fără fereastra lui.
    /// Viteza se schimbă fără să se schimbe tonul (WMP întinde sunetul, nu-l accelerează).
    /// </summary>
    public class Player
    {
        dynamic wmp;
        public string Cale { get; private set; }
        public int Viteza { get; private set; }   // procente, 50–150

        public Player() { Viteza = 100; }

        public bool Disponibil
        {
            get
            {
                if (wmp != null) return true;
                try
                {
                    var t = Type.GetTypeFromProgID("WMPlayer.OCX");
                    if (t == null) return false;
                    wmp = Activator.CreateInstance(t);
                    wmp.settings.autoStart = false;
                    return true;
                }
                catch { wmp = null; return false; }
            }
        }

        public void Deschide(string cale, int volum)
        {
            if (!Disponibil) throw new InvalidOperationException("Windows Media Player nu e disponibil pe acest calculator.");
            wmp.controls.stop();
            wmp.URL = cale;
            Cale = cale;
            wmp.settings.volume = volum;
            wmp.settings.rate = Viteza / 100.0;
            wmp.controls.play();
        }

        /// <summary>3 = cântă, 2 = pauză, 1 = oprit, 8 = s-a terminat.</summary>
        int Stare { get { try { return wmp == null ? 0 : (int)wmp.playState; } catch { return 0; } } }
        public bool Canta { get { return Stare == 3; } }

        public void PornestePauza()
        {
            if (wmp == null || Cale == null) return;
            if (Canta) wmp.controls.pause();
            else { wmp.settings.rate = Viteza / 100.0; wmp.controls.play(); }
        }

        public void Opreste() { if (wmp != null) try { wmp.controls.stop(); } catch { } }

        public double Pozitie
        {
            get { try { return wmp == null ? 0 : (double)wmp.controls.currentPosition; } catch { return 0; } }
            set { try { if (wmp != null) wmp.controls.currentPosition = Math.Max(0, Math.Min(Durata, value)); } catch { } }
        }

        public double Durata { get { try { return wmp == null || wmp.currentMedia == null ? 0 : (double)wmp.currentMedia.duration; } catch { return 0; } } }

        public void SetViteza(int procente)
        {
            Viteza = Math.Max(50, Math.Min(150, procente));
            try { if (wmp != null) wmp.settings.rate = Viteza / 100.0; } catch { }
        }

        public void SetVolum(int v) { try { if (wmp != null) wmp.settings.volume = v; } catch { } }

        public static string Timp(double sec)
        {
            sec = Math.Max(0, Math.Floor(sec));
            int m = (int)(sec / 60), s = (int)(sec % 60);
            return m + ":" + s.ToString("00");
        }
    }
}
