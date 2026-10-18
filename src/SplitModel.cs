using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Microsoft.Win32;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Ekran Çoğaltıcı (v1.5): tek bir büyük monitörü (ör. 4K televizyon) birden fazla sanal monitöre böler.
    // Her bölge Windows'ta ayrı bir monitördür; görüntüsü monitörün o bölgesine 1:1 kopyalanır.
    // Bu dosya: bölgelerin saklanması, şablonlar, kalite / piksel yoğunluğu hesabı ve monitörün fiziksel boyutu.
    // ===================================================================================
    static class SplitLayout
    {
        public const int MaxScreens = 8;          // sürücünün sınırı (protokol 2)
        public const int Grid = 8;                // bölge kenarları 8 pikselin katlarına oturur
        public const int MinW = 640, MinH = 360;  // bir sanal ekranın en küçük boyutu

        // "3840x2160|x,y,w,h;x,y,w,h" (monitör boyutu + bölgeler)
        public static string Format(Size monitor, IList<Rectangle> regions)
        {
            var parts = new List<string>();
            foreach (var r in regions) parts.Add(r.X + "," + r.Y + "," + r.Width + "," + r.Height);
            return monitor.Width + "x" + monitor.Height + "|" + string.Join(";", parts.ToArray());
        }

        // Kaydedilen bölgeleri okur; monitör boyutu değiştiyse orantılı olarak ölçekler
        public static List<Rectangle> Parse(string s, Size monitor)
        {
            var list = new List<Rectangle>();
            if (string.IsNullOrEmpty(s) || monitor.Width <= 0 || monitor.Height <= 0) return list;
            var halves = s.Split('|');
            if (halves.Length != 2) return list;
            var sz = halves[0].Split('x');
            int fw, fh;
            if (sz.Length != 2 || !int.TryParse(sz[0], out fw) || !int.TryParse(sz[1], out fh) || fw <= 0 || fh <= 0) return list;
            foreach (var item in halves[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = item.Split(',');
                int x, y, w, h;
                if (p.Length != 4 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) ||
                    !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) continue;
                list.Add(Rectangle.FromLTRB(
                    SnapX(x * monitor.Width / fw, monitor.Width), SnapX(y * monitor.Height / fh, monitor.Height),
                    SnapX((x + w) * monitor.Width / fw, monitor.Width), SnapX((y + h) * monitor.Height / fh, monitor.Height)));
                if (list.Count == MaxScreens) break;
            }
            return Valid(list, monitor) ? list : new List<Rectangle>();
        }

        // Kenarı 8'in katına oturtur; monitörün kenarına çok yakınsa kenara yapıştırır
        public static int SnapX(int v, int max)
        {
            if (v <= 0) return 0;
            if (v >= max - Grid / 2) return max;
            return Math.Min(max, (int)Math.Round(v / (double)Grid) * Grid);
        }

        public static bool Overlaps(IList<Rectangle> list)
        {
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    if (list[i].IntersectsWith(list[j])) return true;
            return false;
        }

        // Uygulanabilir mi: monitörün içinde, üst üste binmeyen, yeterince büyük bölgeler
        public static bool Valid(IList<Rectangle> list, Size monitor)
        {
            if (list.Count == 0 || list.Count > MaxScreens) return false;
            var screen = new Rectangle(Point.Empty, monitor);
            foreach (var r in list)
                if (!screen.Contains(r) || r.Width < MinW || r.Height < MinH) return false;
            return !Overlaps(list);
        }

        // Sanal monitör boyutu çift sayı olmalı (bazı ekran kartları tek sayıyı kabul etmez)
        public static Rectangle Even(Rectangle r)
        {
            return new Rectangle(r.X, r.Y, r.Width & ~1, r.Height & ~1);
        }

        // Bölgelerin birbirine değip değmediği (değmeyen bölgeye fare geçemez)
        public static bool Connected(IList<Rectangle> list)
        {
            if (list.Count <= 1) return true;
            var seen = new bool[list.Count];
            var stack = new Stack<int>();
            stack.Push(0); seen[0] = true;
            int count = 1;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                for (int j = 0; j < list.Count; j++)
                    if (!seen[j] && Touch(list[i], list[j])) { seen[j] = true; count++; stack.Push(j); }
            }
            return count == list.Count;
        }

        static bool Touch(Rectangle a, Rectangle b)
        {
            bool vertical = (a.Right == b.Left || b.Right == a.Left) && a.Top < b.Bottom && b.Top < a.Bottom;
            bool horizontal = (a.Bottom == b.Top || b.Bottom == a.Top) && a.Left < b.Right && b.Left < a.Right;
            return vertical || horizontal;
        }

        // Boş alandaki en büyük dikdörtgen (yeni ekran için); kenar adayları bölgelerin kenarlarıdır
        public static Rectangle LargestFree(IList<Rectangle> list, Size monitor)
        {
            var xs = new SortedSet<int> { 0, monitor.Width };
            var ys = new SortedSet<int> { 0, monitor.Height };
            foreach (var r in list) { xs.Add(r.Left); xs.Add(r.Right); ys.Add(r.Top); ys.Add(r.Bottom); }
            var X = new List<int>(xs); var Y = new List<int>(ys);
            Rectangle best = Rectangle.Empty;
            long bestA = 0;
            for (int a = 0; a < X.Count; a++)
                for (int b = a + 1; b < X.Count; b++)
                    for (int c = 0; c < Y.Count; c++)
                        for (int d = c + 1; d < Y.Count; d++)
                        {
                            var r = Rectangle.FromLTRB(X[a], Y[c], X[b], Y[d]);
                            long area = (long)r.Width * r.Height;
                            if (area <= bestA || r.Width < MinW || r.Height < MinH) continue;
                            bool free = true;
                            foreach (var o in list) if (o.IntersectsWith(r)) { free = false; break; }
                            if (free) { best = r; bestA = area; }
                        }
            return best;
        }
    }

    // ------------------------------------------------------------------ Şablonlar
    class SplitTemplate
    {
        public readonly string Id;
        public readonly RectangleF[] Cells;   // 0..1 arası oranlar
        readonly string tr, en;

        public SplitTemplate(string id, string tr, string en, params RectangleF[] cells)
        {
            Id = id; this.tr = tr; this.en = en; Cells = cells;
        }

        public string Name { get { return L.T(tr, en); } }
        public int Count { get { return Cells.Length; } }

        // Oranları monitör piksellerine çevirir; ortak kenarlar aynı değere yuvarlanır, böylece bölgeler tam bitişir
        public List<Rectangle> Apply(Size m)
        {
            var list = new List<Rectangle>();
            foreach (var c in Cells)
                list.Add(Rectangle.FromLTRB(
                    SplitLayout.SnapX((int)Math.Round(c.Left * m.Width), m.Width), SplitLayout.SnapX((int)Math.Round(c.Top * m.Height), m.Height),
                    SplitLayout.SnapX((int)Math.Round(c.Right * m.Width), m.Width), SplitLayout.SnapX((int)Math.Round(c.Bottom * m.Height), m.Height)));
            return list;
        }

        // Her bölge en küçük sanal ekran boyutuna sığıyor mu?
        public bool Fits(Size m)
        {
            foreach (var r in Apply(m)) if (r.Width < SplitLayout.MinW || r.Height < SplitLayout.MinH) return false;
            return true;
        }

        static RectangleF R(float x, float y, float w, float h) { return new RectangleF(x, y, w, h); }

        static RectangleF[] Grid(int cols, int rows)
        {
            var cells = new List<RectangleF>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    cells.Add(R(c / (float)cols, r / (float)rows, 1f / cols, 1f / rows));
            return cells.ToArray();
        }

        const float Third = 1f / 3f;

        public static readonly SplitTemplate[] All =
        {
            new SplitTemplate("2v", "2 · yan yana", "2 · side by side", Grid(2, 1)),
            new SplitTemplate("2h", "2 · üst üste", "2 · stacked", Grid(1, 2)),
            new SplitTemplate("3v", "3 · sütun", "3 · columns", Grid(3, 1)),
            new SplitTemplate("1+2", "1 büyük + 2", "1 large + 2", R(0, 0, 0.5f, 1), R(0.5f, 0, 0.5f, 0.5f), R(0.5f, 0.5f, 0.5f, 0.5f)),
            new SplitTemplate("2x2", "2 × 2", "2 × 2", Grid(2, 2)),
            new SplitTemplate("1+3", "1 büyük + 3", "1 large + 3", R(0, 0, 0.5f, 1), R(0.5f, 0, 0.5f, Third), R(0.5f, Third, 0.5f, Third), R(0.5f, 2 * Third, 0.5f, Third)),
            new SplitTemplate("3+2", "3 + 2", "3 + 2", R(0, 0, Third, 0.5f), R(Third, 0, Third, 0.5f), R(2 * Third, 0, Third, 0.5f), R(0, 0.5f, 0.5f, 0.5f), R(0.5f, 0.5f, 0.5f, 0.5f)),
            new SplitTemplate("3x2", "3 × 2", "3 × 2", Grid(3, 2)),
            new SplitTemplate("4x2", "4 × 2", "4 × 2", Grid(4, 2)),
        };
    }

    // ------------------------------------------------------------------ Kalite ve öneriler
    // Bir sanal ekranın kalitesi iki şeye bağlıdır:
    //   - piksel sayısı (ne kadar çalışma alanı var)
    //   - fiziksel boyutu, yani monitörün piksel yoğunluğuna (PPI) göre bölgenin kaç inç olduğu (yazılar okunur mu)
    // Monitörün boyutu bilinmiyorsa sadece piksel sayısına bakılır.
    static class SplitAdvisor
    {
        public const int TooSmall = 0, Usable = 1, Good = 2, Sharp = 3;

        static readonly double[] MinMp = { 0, 1.2, 1.9, 3.5 };     // megapiksel
        static readonly double[] MinInch = { 0, 10, 14, 20 };      // köşegen (inç)

        public static int Quality(Size px, double ppi)
        {
            double mp = (double)px.Width * px.Height / 1e6;
            double inch = ppi > 0 ? Math.Sqrt((double)px.Width * px.Width + (double)px.Height * px.Height) / ppi : double.MaxValue;
            for (int q = Sharp; q >= Usable; q--)
                if (mp >= MinMp[q] - 1e-9 && inch >= MinInch[q] - 1e-9) return q;
            return TooSmall;
        }

        public static string QualityName(int q)
        {
            switch (q)
            {
                case Sharp: return L.T("Keskin", "Sharp");
                case Good: return L.T("İyi", "Good");
                case Usable: return L.T("Kullanılabilir", "Usable");
                default: return L.T("Çok küçük", "Too small");
            }
        }

        public static Color QualityColor(int q)
        {
            switch (q)
            {
                case Sharp: return Theme.Ok;
                case Good: return Theme.Accent;
                case Usable: return Theme.Warn;
                default: return Theme.Danger;
            }
        }

        public static double Inches(Size px, double ppi)
        {
            return ppi > 0 ? Math.Sqrt((double)px.Width * px.Width + (double)px.Height * px.Height) / ppi : 0;
        }

        // Bir şablonun en zayıf bölgesinin kalitesi
        public static int TemplateQuality(SplitTemplate t, Size m, double ppi)
        {
            int q = Sharp;
            foreach (var r in t.Apply(m)) q = Math.Min(q, Quality(r.Size, ppi));
            return q;
        }

        public class Pick
        {
            public int Count;              // ekran sayısı (1 = bölme)
            public SplitTemplate Template; // null = bütün ekran
            public int Quality;
            public Size Smallest;
        }

        // n ekran için en iyi şablon (en zayıf bölgesi en kaliteli olan; eşitse en büyük en küçük bölge)
        public static Pick Best(int n, Size m, double ppi)
        {
            if (n == 1) return new Pick { Count = 1, Quality = Quality(m, ppi), Smallest = m };
            Pick best = null;
            foreach (var t in SplitTemplate.All)
            {
                if (t.Count != n || !t.Fits(m)) continue;
                var cells = t.Apply(m);
                int q = Sharp;
                Size small = m;
                foreach (var r in cells)
                {
                    q = Math.Min(q, Quality(r.Size, ppi));
                    if ((long)r.Width * r.Height < (long)small.Width * small.Height) small = r.Size;
                }
                // yatay monitörde "yan yana", dikeyde "üst üste" önce gelsin
                if (best == null || q > best.Quality ||
                    (q == best.Quality && (long)small.Width * small.Height > (long)best.Smallest.Width * best.Smallest.Height) ||
                    (q == best.Quality && small == best.Smallest && Prefer(t, m)))
                    best = new Pick { Count = n, Template = t, Quality = q, Smallest = small };
            }
            return best;
        }

        static bool Prefer(SplitTemplate t, Size m)
        {
            return m.Width >= m.Height ? t.Id == "2v" : t.Id == "2h";
        }

        // Üç öneri: kalite kaybı olmadan (keskin), önerilen (iyi), en fazla (kullanılabilir).
        // Her biri o kalitede kalabilen en çok ekran sayısıdır; hiçbiri yoksa 1 (bölme önerilmez).
        public static Pick[] Recommend(Size m, double ppi)
        {
            var res = new Pick[3];
            int[] want = { Sharp, Good, Usable };
            for (int k = 0; k < 3; k++)
            {
                Pick found = null;
                for (int n = 1; n <= SplitLayout.MaxScreens; n++)
                {
                    var p = Best(n, m, ppi);
                    if (p != null && p.Quality >= want[k]) found = p;
                }
                res[k] = found ?? Best(1, m, ppi);
            }
            return res;
        }
    }

    // ------------------------------------------------------------------ Monitörün fiziksel boyutu (EDID)
    static class MonitorSize
    {
        // Monitörün kendi bildirdiği fiziksel boyut (mm). Bilinmiyorsa ya da tutarsızsa Size.Empty.
        // devicePath: \\?\DISPLAY#MSI3EA2#5&c66988b&1&UID37120#{e6f07b5f-...}
        public static Size Read(string devicePath, Size pixels)
        {
            try
            {
                if (string.IsNullOrEmpty(devicePath)) return Size.Empty;
                string p = devicePath.StartsWith(@"\\?\") ? devicePath.Substring(4) : devicePath;
                var parts = p.Split('#');
                if (parts.Length < 3) return Size.Empty;
                string key = @"SYSTEM\CurrentControlSet\Enum\" + parts[0] + "\\" + parts[1] + "\\" + parts[2] + @"\Device Parameters";
                byte[] e;
                using (var k = Registry.LocalMachine.OpenSubKey(key))
                    e = k == null ? null : k.GetValue("EDID") as byte[];
                if (e == null || e.Length < 128) return Size.Empty;

                // ilk ayrıntılı zamanlamadaki boyut (mm) daha hassastır; yoksa temel bloktaki cm değeri
                int wmm = e[66] | ((e[68] & 0xF0) << 4), hmm = e[67] | ((e[68] & 0x0F) << 8);
                int wcm = e[21], hcm = e[22];
                Size mm = Size.Empty;
                if (Plausible(wmm, hmm, pixels)) mm = new Size(wmm, hmm);
                else if (Plausible(wcm * 10, hcm * 10, pixels)) mm = new Size(wcm * 10, hcm * 10);
                return mm;
            }
            catch { return Size.Empty; }
        }

        // Ölçü, piksel oranıyla uyumlu ve 10"-150" arasında mı (bazı televizyonlar uydurma ya da 0 bildirir)
        static bool Plausible(int wmm, int hmm, Size px)
        {
            if (wmm <= 0 || hmm <= 0 || px.Width <= 0 || px.Height <= 0) return false;
            double inch = Math.Sqrt((double)wmm * wmm + (double)hmm * hmm) / 25.4;
            if (inch < 10 || inch > 150) return false;
            double a = (double)wmm / hmm, b = (double)px.Width / px.Height;
            return Math.Abs(a - b) / b < 0.12;
        }

        public static double Diagonal(Size mm)
        {
            return mm.IsEmpty ? 0 : Math.Sqrt((double)mm.Width * mm.Width + (double)mm.Height * mm.Height) / 25.4;
        }

        public static double Ppi(Size px, double inches)
        {
            return inches > 0 ? Math.Sqrt((double)px.Width * px.Width + (double)px.Height * px.Height) / inches : 0;
        }

        public static string Inch(double v)
        {
            return v.ToString(v >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture) + "″";
        }
    }
}
