using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Windows düzenlemeleri (v1.6): görev çubuğunu her monitörde istenen kenara yaslar.
    //
    // Windows 11'in kendi görev çubuğu tek bir konum alır ve bu konum tüm monitörlere uygulanır
    // ("Görev çubuğu konumu" ayarı, Advanced\TaskbarLocation). Monitör başına ayrı kenar yoktur.
    // Bu yüzden:
    //   - ana monitör her zaman Windows'un kendi çubuğunu kullanır (tepsi, Başlat, saat orada kalır);
    //     kenarı Windows'un ayarıyla değiştirilir
    //   - diğer monitörler ana monitörle aynı kenardaysa hepsinde Windows'un çubuğu kalır ("yalnız Windows")
    //   - farklı kenar isteyen monitör varsa Windows'un "görev çubuğunu tüm ekranlarda göster" ayarı kapatılır
    //     ve diğer monitörlere programın kendi çubuğu (TaskbarManager) çizilir ("karma")
    // Önceki Windows değerleri saklanır; Geri al, acil sıfırlama ve kaldırma onları geri yazar.
    // ===================================================================================

    // Kenar kodları AppBar kodlarıyla aynı; None = bu monitörde çubuk yok (sadece ana olmayan monitörler)
    static class TbSide
    {
        public const int Left = 0, Top = 1, Right = 2, Bottom = 3, None = 4;
        public static readonly int[] Order = { Bottom, Top, Left, Right, None };   // arayüzdeki sıra

        public static bool Vertical(int s) { return s == Left || s == Right; }
        public static bool Valid(int s) { return s >= Left && s <= None; }

        public static string Name(int s)
        {
            switch (s)
            {
                case Left: return L.T("Sol", "Left");
                case Top: return L.T("Üst", "Top");
                case Right: return L.T("Sağ", "Right");
                case None: return L.T("Yok", "None");
                default: return L.T("Alt", "Bottom");
            }
        }
    }

    class TbMonitor
    {
        public string Key = "";     // monitörün kalıcı yolu (ayar anahtarı)
        public string Gdi = "";     // \\.\DISPLAYn
        public string Name = "";
        public Rectangle Bounds;    // masaüstü koordinatları (fiziksel piksel)
        public bool Primary;
        public int Number;          // arayüzde gösterilen sıra (1'den)
    }

    static class TaskbarLayout
    {
        // "anahtar=kenar|anahtar=kenar" (anahtar monitör yoludur; '=' ve '|' içermez)
        public static Dictionary<string, int> Parse(string s)
        {
            var d = new Dictionary<string, int>();
            foreach (var part in (s ?? "").Split('|'))
            {
                int i = part.LastIndexOf('=');
                int v;
                if (i <= 0 || !int.TryParse(part.Substring(i + 1), out v) || !TbSide.Valid(v)) continue;
                d[part.Substring(0, i)] = v;
            }
            return d;
        }

        public static string Format(Dictionary<string, int> d)
        {
            var keys = new List<string>(d.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var k in keys)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(k).Append('=').Append(d[k]);
            }
            return sb.ToString();
        }

        // Etkin monitörler, masaüstündeki yerleriyle (soldan sağa, yukarıdan aşağı numaralanır)
        public static List<TbMonitor> Monitors()
        {
            var list = new List<TbMonitor>();
            List<MonitorInfo> infos;
            try { infos = DisplayConfig.GetMonitors(); } catch { infos = new List<MonitorInfo>(); }
            foreach (var s in Screen.AllScreens)
            {
                var m = new TbMonitor { Gdi = s.DeviceName, Bounds = s.Bounds, Primary = s.Primary, Key = s.DeviceName, Name = s.DeviceName.Replace(@"\\.\", "") };
                foreach (var mi in infos)
                    if (mi.GdiName == s.DeviceName)
                    {
                        if (!string.IsNullOrEmpty(mi.DevicePath)) m.Key = mi.DevicePath;
                        if (!string.IsNullOrEmpty(mi.Name)) m.Name = mi.Name;
                    }
                list.Add(m);
            }
            list.Sort((a, b) => a.Bounds.X != b.Bounds.X ? a.Bounds.X.CompareTo(b.Bounds.X) : a.Bounds.Y.CompareTo(b.Bounds.Y));
            for (int i = 0; i < list.Count; i++) list[i].Number = i + 1;
            return list;
        }

        // Ayarlanmamış monitör ana monitörün kenarını alır
        public static int SideOf(Dictionary<string, int> sides, TbMonitor m, int primarySide)
        {
            int v;
            if (sides != null && sides.TryGetValue(m.Key, out v) && TbSide.Valid(v))
                return m.Primary && v == TbSide.None ? TbSide.Bottom : v;
            return m.Primary ? TbSide.Bottom : primarySide;
        }

        public static int PrimarySide(List<TbMonitor> mons, Dictionary<string, int> sides)
        {
            foreach (var m in mons) if (m.Primary) return SideOf(sides, m, TbSide.Bottom);
            return TbSide.Bottom;
        }
    }

    // Ne yapılacağı: ana monitörün kenarı, yalnız Windows mu, programın çizeceği çubuklar
    class TaskbarPlan
    {
        public int PrimarySide = TbSide.Bottom;
        public bool Native = true;
        public readonly List<TbMonitor> BarMonitors = new List<TbMonitor>();
        public readonly List<int> BarSides = new List<int>();
        public int Hidden;   // çubuksuz bırakılan monitör sayısı

        public static TaskbarPlan Make(List<TbMonitor> mons, Dictionary<string, int> sides)
        {
            var p = new TaskbarPlan();
            p.PrimarySide = TaskbarLayout.PrimarySide(mons, sides);
            foreach (var m in mons)
                if (!m.Primary && TaskbarLayout.SideOf(sides, m, p.PrimarySide) != p.PrimarySide) p.Native = false;
            if (p.Native) return p;
            foreach (var m in mons)
            {
                if (m.Primary) continue;
                int s = TaskbarLayout.SideOf(sides, m, p.PrimarySide);
                if (s == TbSide.None) { p.Hidden++; continue; }
                p.BarMonitors.Add(m);
                p.BarSides.Add(s);
            }
            return p;
        }
    }

    // Windows'un görev çubuğu ayarları (kullanıcıya özel, HKCU)
    static class TaskbarTweaks
    {
        const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string LocationValue = "TaskbarLocation", MultiValue = "MMTaskbarEnabled", AlignValue = "TaskbarAl";
        public const int NotSaved = -2, Absent = -1;

        public static int Read(string name)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Adv))
                {
                    var v = k == null ? null : k.GetValue(name);
                    return v is int ? (int)v : Absent;
                }
            }
            catch { return Absent; }
        }

        // v < 0: değeri sil (Windows varsayılanı)
        public static void Write(string name, int v)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Adv))
                {
                    if (v < 0) k.DeleteValue(name, false);
                    else k.SetValue(name, v, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        // Görev çubuğu ayarı değişti: Explorer yeniden başlatılmadan uygular
        public static void Notify()
        {
            IntPtr r;
            TbNative.SendMessageTimeout((IntPtr)0xffff, 0x1A, IntPtr.Zero, "TraySettings", 2, 3000, out r);
        }

        // Simge hizası: 0 sol, 1 orta (Windows 11 varsayılanı)
        public static int CurrentAlign()
        {
            int v = Read(AlignValue);
            return v == 0 ? 0 : 1;
        }

        // Ana görev çubuğunun şu an hangi kenarda olduğu (-1: bulunamadı)
        public static int PrimaryEdgeNow()
        {
            IntPtr h = TbNative.FindWindow("Shell_TrayWnd", null);
            TbNative.RECT r;
            if (h == IntPtr.Zero || !TbNative.GetWindowRect(h, out r)) return -1;
            var s = Screen.PrimaryScreen.Bounds;
            int w = r.Right - r.Left, ht = r.Bottom - r.Top;
            if (w < ht) return Math.Abs(r.Left - s.Left) <= Math.Abs(s.Right - r.Right) ? TbSide.Left : TbSide.Right;
            return Math.Abs(r.Top - s.Top) <= Math.Abs(s.Bottom - r.Bottom) ? TbSide.Top : TbSide.Bottom;
        }

        static bool WaitEdge(int edge, int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (PrimaryEdgeNow() == edge) return true;
                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
            }
            return PrimaryEdgeNow() == edge;
        }

        // İlk uygulamada Windows'un önceki değerlerini saklar (Geri al bunlara döner)
        public static void SaveOriginals(Settings cfg)
        {
            if (cfg.TbOrigLoc != NotSaved) return;
            cfg.TbOrigLoc = Read(LocationValue);
            cfg.TbOrigMM = Read(MultiValue);
            cfg.TbOrigAl = Read(AlignValue);
        }

        // Planı Windows ayarlarına yazar. Dönüş: null = tamam, değilse kullanıcıya gösterilecek uyarı.
        public static string Apply(TaskbarPlan p, int align, Settings cfg)
        {
            SaveOriginals(cfg);
            Write(AlignValue, align);
            Write(MultiValue, p.Native ? cfg.TbOrigMM : 0);
            int before = PrimaryEdgeNow();
            Write(LocationValue, p.PrimarySide);
            Notify();
            // Explorer henüz açılmadıysa (ör. Windows açılışı) doğrulanamaz; ayar Explorer açılınca okunur
            if (before < 0 || before == p.PrimarySide || WaitEdge(p.PrimarySide, 2500)) return null;

            // Bu Windows sürümü ana çubuğu taşımayı desteklemiyor (özellik henüz gelmemiş): eski konuma dön
            Write(LocationValue, cfg.TbOrigLoc);
            Notify();
            return L.T("Bu Windows sürümü görev çubuğunu alta dışında bir yere taşımayı henüz desteklemiyor (\"Görev çubuğu konumu\" ayarı Windows Update ile kademeli geliyor). " +
                       "Ana monitördeki çubuk yerinde kaldı; diğer monitörlerdeki ayarlar uygulandı.",
                       "This version of Windows can't move the taskbar away from the bottom yet (the \"Taskbar position\" setting is rolling out through Windows Update). " +
                       "The taskbar on the main monitor stayed where it was; the settings for the other monitors were applied.");
        }

        // Program kapanırken: diğer monitörlerde Windows'un çubuğu geri gelsin (açılınca yeniden uygulanır)
        public static void Suspend(Settings cfg)
        {
            if (cfg.TbOrigLoc == NotSaved) return;
            if (Read(MultiValue) == cfg.TbOrigMM) return;
            Write(MultiValue, cfg.TbOrigMM);
            Notify();
        }

        // Her şeyi programdan önceki haline döndürür
        public static void Restore(Settings cfg)
        {
            if (cfg.TbOrigLoc == NotSaved) return;
            Write(LocationValue, cfg.TbOrigLoc);
            Write(MultiValue, cfg.TbOrigMM);
            Write(AlignValue, cfg.TbOrigAl);
            Notify();
            cfg.TbOrigLoc = cfg.TbOrigMM = cfg.TbOrigAl = NotSaved;
        }
    }

    static class TbNative
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern IntPtr GetWindowLong32(IntPtr h, int i);
        public static long GetWindowLong(IntPtr h, int i) { return (IntPtr.Size == 8 ? GetWindowLongPtr64(h, i) : GetWindowLong32(h, i)).ToInt64(); }
        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] static extern IntPtr GetClassLongPtr64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "GetClassLongW")] static extern IntPtr GetClassLong32(IntPtr h, int i);
        public static IntPtr GetClassLong(IntPtr h, int i) { return IntPtr.Size == 8 ? GetClassLongPtr64(h, i) : GetClassLong32(h, i); }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool alt);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dx, out uint dy);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);

        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20, GCLP_HICON = -14, GCLP_HICONSM = -34;
        public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
        public const uint GW_OWNER = 4, MONITOR_DEFAULTTONEAREST = 2, WM_GETICON = 0x7F, WM_SYSCOMMAND = 0x112;
        public const int SW_MINIMIZE = 6, SW_RESTORE = 9, SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
        public const int SC_CLOSE = 0xF060;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;

        public static double MonitorScale(IntPtr mon)
        {
            try
            {
                uint dx, dy;
                if (GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx > 0) return dx / 96.0;
            }
            catch { }
            return Theme.S > 0 ? Theme.S : 1.0;
        }

        public static IntPtr MonitorOf(Rectangle r)
        {
            var p = new POINT { X = r.X + r.Width / 2, Y = r.Y + r.Height / 2 };
            return MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        }

        public static string ClassOf(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            return sb.ToString();
        }

        public static string TitleOf(IntPtr h)
        {
            int n = GetWindowTextLength(h);
            if (n <= 0) return "";
            var sb = new StringBuilder(n + 2);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string ProcessPath(uint pid)
        {
            IntPtr hp = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (hp == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(hp, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally { CloseHandle(hp); }
        }
    }
}
