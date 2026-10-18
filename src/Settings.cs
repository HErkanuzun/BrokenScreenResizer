using System;
using System.Drawing;
using System.IO;

namespace BrokenScreenResizer
{
    enum Edge { Left = 0, Top = 1, Right = 2, Bottom = 3 }   // değerler AppBar kenar kodlarıyla aynı
    enum Mode { Driver = 0, Software = 1, Virtual = 2, Split = 3 }   // Driver: v0.6 ve öncesinden kalan ayarı tanımak için; Split: Ekran Çoğaltıcı (v1.5)

    // Monitörün dört kenarından gizlenen (bozuk) şeritler, piksel cinsinden.
    // Kalan dikdörtgen "sağlam alan"dır; böylece ekranın herhangi bir dikdörtgen bölgesi kullanılabilir.
    struct Cuts
    {
        public int L, T, R, B;

        public Cuts(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }

        public static readonly Edge[] All = { Edge.Left, Edge.Top, Edge.Right, Edge.Bottom };

        public bool Any { get { return L > 0 || T > 0 || R > 0 || B > 0; } }
        public int H { get { return L + R; } }   // yatayda toplam
        public int V { get { return T + B; } }   // dikeyde toplam

        public int this[Edge e]
        {
            get
            {
                switch (e)
                {
                    case Edge.Left: return L;
                    case Edge.Top: return T;
                    case Edge.Right: return R;
                    default: return B;
                }
            }
            set
            {
                switch (e)
                {
                    case Edge.Left: L = value; break;
                    case Edge.Top: T = value; break;
                    case Edge.Right: R = value; break;
                    default: B = value; break;
                }
            }
        }

        public static bool IsHorizontal(Edge e) { return e == Edge.Left || e == Edge.Right; }

        // Sağlam alan
        public Rectangle Good(Rectangle s)
        {
            return new Rectangle(s.Left + L, s.Top + T, Math.Max(1, s.Width - L - R), Math.Max(1, s.Height - T - B));
        }

        // Bir kenardaki bozuk şerit (köşeler sol/sağ şeritlere dahil)
        public Rectangle Strip(Rectangle s, Edge e)
        {
            switch (e)
            {
                case Edge.Left: return new Rectangle(s.Left, s.Top, L, s.Height);
                case Edge.Right: return new Rectangle(s.Right - R, s.Top, R, s.Height);
                case Edge.Top: return new Rectangle(s.Left + L, s.Top, s.Width - L - R, T);
                default: return new Rectangle(s.Left + L, s.Bottom - B, s.Width - L - R, B);
            }
        }

        // Monitör çözünürlüğünden masaüstü ölçeğine çevirir (ör. ölçekleme farkı)
        public Cuts Scale(int fromW, int fromH, int toW, int toH)
        {
            fromW = Math.Max(1, fromW); fromH = Math.Max(1, fromH);
            return new Cuts(L * toW / fromW, T * toH / fromH, R * toW / fromW, B * toH / fromH);
        }

        public override string ToString() { return L + "," + T + "," + R + "," + B; }

        public static bool TryParse(string s, out Cuts c)
        {
            c = new Cuts();
            var p = (s ?? "").Split(',');
            int l, t, r, b;
            if (p.Length != 4 || !int.TryParse(p[0], out l) || !int.TryParse(p[1], out t) ||
                !int.TryParse(p[2], out r) || !int.TryParse(p[3], out b)) return false;
            c = new Cuts(Math.Max(0, l), Math.Max(0, t), Math.Max(0, r), Math.Max(0, b));
            return true;
        }

        public static bool operator ==(Cuts a, Cuts b) { return a.L == b.L && a.T == b.T && a.R == b.R && a.B == b.B; }
        public static bool operator !=(Cuts a, Cuts b) { return !(a == b); }
        public override bool Equals(object o) { return o is Cuts && (Cuts)o == this; }
        public override int GetHashCode() { return L ^ (T << 8) ^ (R << 16) ^ (B << 24); }
    }

    class Settings
    {
        public string MonitorPath = "";
        public Cuts Cuts;
        public Edge Focus = Edge.Left;   // arayüzde düzenlenen kenar
        public Mode Mode = Mode.Software;
        public bool Active = false;
        public Mode ActiveMode = Mode.Driver;
        public bool LockCursor = true;
        public bool FixWindows = true;
        public string Lang = "";
        public bool DriverWarp = false;

        // Ekran Çoğaltıcı (v1.5): bir monitörü birden fazla sanal monitöre böler
        public int Tab = 0;                        // 0 = bozuk ekran, 1 = Ekran Çoğaltıcı
        public string SplitMonitor = "";           // bölünen monitörün yolu
        public string SplitRegions = "";           // "x,y,w,h;..." monitör pikseli cinsinden
        public float SplitDiag = 0;                // elle girilen köşegen (inç); 0 = monitör bilgisinden

        public const string AppName = "BrokenScreenResizer";
        public const string LegacyName = "EkranKurtar";   // v0.6 öncesi adı

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        // Eski adla (Ekran Kurtar) kaydedilmiş ayarları yeni klasöre kopyalar
        static void MigrateLegacy()
        {
            try
            {
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyName);
                if (Directory.Exists(Dir) || !Directory.Exists(old)) return;
                Directory.CreateDirectory(Dir);
                foreach (var f in Directory.GetFiles(old))
                    File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), false);
            }
            catch { }
        }

        public static Settings Load()
        {
            var s = new Settings();
            MigrateLegacy();
            try
            {
                if (!File.Exists(FilePath)) return s;
                int edge = -1, cut = 0;
                bool hasCuts = false;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    int n;
                    Cuts c;
                    switch (k)
                    {
                        case "monitor": s.MonitorPath = v; break;
                        case "cuts": if (Cuts.TryParse(v, out c)) { s.Cuts = c; hasCuts = true; } break;
                        case "focus": if (int.TryParse(v, out n) && n >= 0 && n <= 3) s.Focus = (Edge)n; break;
                        // eski biçim (v0.6 öncesi): tek kenar + miktar
                        case "edge": if (int.TryParse(v, out n) && n >= 0 && n <= 3) edge = n; break;
                        case "cut": if (int.TryParse(v, out n) && n >= 0) cut = n; break;
                        // sürücü modu v0.7'de kaldırıldı: yazılım moduna geçilir
                        case "mode": s.Mode = ParseMode(v, Mode.Software); if (s.Mode == Mode.Driver || s.Mode == Mode.Split) s.Mode = Mode.Software; break;
                        case "active": s.Active = v == "1"; break;
                        case "activeMode": s.ActiveMode = ParseMode(v, Mode.Driver); break;
                        case "driverWarp": s.DriverWarp = v == "1"; break;
                        case "lockCursor": s.LockCursor = v == "1"; break;
                        case "fixWindows": s.FixWindows = v == "1"; break;
                        case "lang": s.Lang = v; break;
                        case "tab": if (int.TryParse(v, out n) && (n == 0 || n == 1)) s.Tab = n; break;
                        case "splitMonitor": s.SplitMonitor = v; break;
                        case "splitRegions": s.SplitRegions = v; break;
                        case "splitDiag":
                            float d;
                            if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) && d >= 0 && d < 1000) s.SplitDiag = d;
                            break;
                    }
                }
                if (!hasCuts && edge >= 0)
                {
                    s.Cuts[(Edge)edge] = cut;
                    s.Focus = (Edge)edge;
                }
            }
            catch { }
            return s;
        }

        static Mode ParseMode(string v, Mode def)
        {
            int n;
            return int.TryParse(v, out n) && n >= 0 && n <= 3 ? (Mode)n : def;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[] {
                    "monitor=" + MonitorPath,
                    "cuts=" + Cuts,
                    "focus=" + (int)Focus,
                    "mode=" + (int)Mode,
                    "active=" + (Active ? "1" : "0"),
                    "activeMode=" + (int)ActiveMode,
                    "lockCursor=" + (LockCursor ? "1" : "0"),
                    "fixWindows=" + (FixWindows ? "1" : "0"),
                    "lang=" + Lang,
                    "driverWarp=" + (DriverWarp ? "1" : "0"),
                    "tab=" + Tab,
                    "splitMonitor=" + SplitMonitor,
                    "splitRegions=" + SplitRegions,
                    "splitDiag=" + SplitDiag.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }
            catch { }
        }
    }
}
