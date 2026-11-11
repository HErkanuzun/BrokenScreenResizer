using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // "Sanal monitör modu":
    //  1) Broken Screen Resizer'ın kendi sanal ekran sürücüsüyle (driver\, bkz. VirtualDriver.cs) sağlam alan boyutunda sanal bir monitör açılır.
    //  2) Sanal monitör, fiziksel monitörün yerine konur; fiziksel monitör düzende kenara alınır.
    //  3) Sanal monitörün görüntüsü Windows Büyüteç API'siyle fiziksel monitörün sağlam kısmına 1:1 kopyalanır.
    //  4) Fare fiziksel monitörün (artık sadece ayna olan) alanına giremez.
    //  5) (v1.7) Ayna kendini denetler, beyaz kalırsa onarır; fiziksel monitöre düşen pencereler anında geri alınır.
    // ===================================================================================

    static class VNative
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY;
            public int dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        public const int ENUM_CURRENT_SETTINGS = -1, ENUM_REGISTRY_SETTINGS = -2;
        public const int DM_POSITION = 0x20, DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_DISPLAYFREQUENCY = 0x400000;
        public const int CDS_UPDATEREGISTRY = 0x1, CDS_SET_PRIMARY = 0x10, CDS_NORESET = 0x10000000;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1, DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string device, uint index, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string device, ref DEVMODE dm, IntPtr hwnd, int flags, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChangeDisplaySettingsExW")]
        public static extern int ChangeDisplaySettingsExNull(string device, IntPtr dm, IntPtr hwnd, int flags, IntPtr param);

        // Büyüteç API
        [StructLayout(LayoutKind.Sequential)]
        public struct MAGTRANSFORM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] public float[] v;
        }
        [DllImport("Magnification.dll")] public static extern bool MagInitialize();
        [DllImport("Magnification.dll")] public static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowSource(IntPtr hwnd, Win.RECT rect);
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM t);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(int exStyle, string cls, string name, int style, int x, int y, int w, int h,
                                                   IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool InvalidateRect(IntPtr h, IntPtr rect, bool erase);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

        public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, MS_SHOWMAGNIFIEDCURSOR = 0x1;

        // Ayna ve fare kancası iş parçacıkları, pencere kurtarma
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOMOVE = 0x2, SWP_ASYNCWINDOWPOS = 0x4000, QS_ALLINPUT = 0x4FF, WM_QUIT = 0x12;
        public const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        public const int DWMWA_CLOAKED = 14, SW_RESTORE = 9, SW_MAXIMIZE = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public Win.POINT pt; }
        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);

        [DllImport("user32.dll")] public static extern uint MsgWaitForMultipleObjects(uint n, IntPtr[] handles, bool waitAll, uint ms, uint mask);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

        public static DEVMODE NewDevMode()
        {
            var dm = new DEVMODE();
            dm.dmDeviceName = "";
            dm.dmFormName = "";
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return dm;
        }
    }

    // ------------------------------------------------------------------ Ekran düzeni (yedek / uygula)
    class LayoutEntry
    {
        public string Gdi = "";
        public int X, Y, W, H, Freq;
        public bool Primary, Attached;

        public Rectangle Rect { get { return new Rectangle(X, Y, W, H); } }
    }

    static class DisplayLayout
    {
        static string BackupPath { get { return Path.Combine(Settings.Dir, "layout_backup.txt"); } }

        public static List<LayoutEntry> Capture()
        {
            var list = new List<LayoutEntry>();
            var dd = new VNative.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            for (uint i = 0; VNative.EnumDisplayDevices(null, i, ref dd, 0); i++)
            {
                var e = new LayoutEntry();
                e.Gdi = dd.DeviceName;
                e.Attached = (dd.StateFlags & VNative.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
                e.Primary = (dd.StateFlags & VNative.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                if (e.Attached)
                {
                    var dm = VNative.NewDevMode();
                    if (VNative.EnumDisplaySettings(dd.DeviceName, VNative.ENUM_CURRENT_SETTINGS, ref dm))
                    {
                        e.X = dm.dmPositionX; e.Y = dm.dmPositionY;
                        e.W = dm.dmPelsWidth; e.H = dm.dmPelsHeight; e.Freq = dm.dmDisplayFrequency;
                    }
                }
                list.Add(e);
                dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            }
            return list;
        }

        public static LayoutEntry Find(List<LayoutEntry> list, string gdi)
        {
            foreach (var e in list) if (e.Gdi == gdi) return e;
            return null;
        }

        // Listeyi tek seferde uygular. Attached=false olanlar masaüstünden çıkarılır.
        public static int Apply(List<LayoutEntry> list)
        {
            int worst = 0;
            foreach (var e in list)
            {
                var dm = VNative.NewDevMode();
                int flags = VNative.CDS_UPDATEREGISTRY | VNative.CDS_NORESET;
                dm.dmFields = VNative.DM_POSITION | VNative.DM_PELSWIDTH | VNative.DM_PELSHEIGHT;
                if (e.Attached)
                {
                    dm.dmPositionX = e.X; dm.dmPositionY = e.Y;
                    dm.dmPelsWidth = e.W; dm.dmPelsHeight = e.H;
                    if (e.Freq > 1) { dm.dmDisplayFrequency = e.Freq; dm.dmFields |= VNative.DM_DISPLAYFREQUENCY; }
                    if (e.Primary) flags |= VNative.CDS_SET_PRIMARY;
                }
                int r = VNative.ChangeDisplaySettingsEx(e.Gdi, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
                if (r != 0 && worst == 0) worst = r;
            }
            int fin = VNative.ChangeDisplaySettingsExNull(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            return worst != 0 ? worst : fin;
        }

        public static string Err(int r)
        {
            switch (r)
            {
                case 0: return L.T("başarılı", "success");
                case 1: return L.T("yeniden başlatma gerekli", "restart required");
                case -1: return L.T("ekran ayarı değiştirilemedi (-1)", "display change failed (-1)");
                case -2: return L.T("bu çözünürlük desteklenmiyor (-2)", "mode not supported (-2)");
                case -5: return L.T("geçersiz parametre (-5)", "bad parameter (-5)");
                default: return L.T("hata kodu ", "error code ") + r;
            }
        }

        public static bool HasBackup { get { return File.Exists(BackupPath); } }

        public static void SaveBackup(List<LayoutEntry> list)
        {
            Directory.CreateDirectory(Settings.Dir);
            var sb = new StringBuilder();
            foreach (var e in list)
                sb.AppendLine(string.Join("|", new[] { e.Gdi, e.Attached ? "1" : "0", e.Primary ? "1" : "0",
                    e.X.ToString(), e.Y.ToString(), e.W.ToString(), e.H.ToString(), e.Freq.ToString() }));
            File.WriteAllText(BackupPath, sb.ToString());
        }

        public static List<LayoutEntry> LoadBackup()
        {
            var list = new List<LayoutEntry>();
            if (!File.Exists(BackupPath)) return list;
            foreach (var line in File.ReadAllLines(BackupPath))
            {
                var p = line.Split('|');
                if (p.Length < 8) continue;
                var e = new LayoutEntry();
                e.Gdi = p[0]; e.Attached = p[1] == "1"; e.Primary = p[2] == "1";
                int.TryParse(p[3], out e.X); int.TryParse(p[4], out e.Y);
                int.TryParse(p[5], out e.W); int.TryParse(p[6], out e.H); int.TryParse(p[7], out e.Freq);
                list.Add(e);
            }
            return list;
        }

        // Yedeği geri yükler; yedekte olmayan ama şimdi açık olan ekranları (sanal monitör) kapatır.
        public static int RestoreBackup()
        {
            var backup = LoadBackup();
            if (backup.Count == 0) return 0;
            var now = Capture();
            var apply = new List<LayoutEntry>();
            foreach (var b in backup) if (b.Attached) apply.Add(b);
            foreach (var n in now)
                if (n.Attached && Find(backup, n.Gdi) == null) { n.Attached = false; apply.Add(n); }
                else if (n.Attached)
                {
                    var b = Find(backup, n.Gdi);
                    if (b != null && !b.Attached) { b.Attached = false; apply.Add(b); }
                }
            int r = Apply(apply);
            if (r == 0) try { File.Delete(BackupPath); } catch { }
            return r;
        }
    }

    // ------------------------------------------------------------------ Ayna için ek Windows çağrıları (v1.7)
    static class MNative
    {
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr dc, int x, int y);
        public const uint CLR_INVALID = 0xFFFFFFFF;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateDC(string driver, string device, string output, IntPtr init);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);

        // Monitörün dikey senkron (yenileme) anını beklemek için
        [StructLayout(LayoutKind.Sequential)]
        public struct D3DKMT_OPENADAPTERFROMHDC { public IntPtr hDc; public uint hAdapter; public uint LuidLow; public int LuidHigh; public uint VidPnSourceId; }
        [StructLayout(LayoutKind.Sequential)]
        public struct D3DKMT_WAITFORVERTICALBLANKEVENT { public uint hAdapter; public uint hDevice; public uint VidPnSourceId; }
        [StructLayout(LayoutKind.Sequential)]
        public struct D3DKMT_CLOSEADAPTER { public uint hAdapter; }
        [DllImport("gdi32.dll")] public static extern int D3DKMTOpenAdapterFromHdc(ref D3DKMT_OPENADAPTERFROMHDC p);
        [DllImport("gdi32.dll")] public static extern int D3DKMTWaitForVerticalBlankEvent(ref D3DKMT_WAITFORVERTICALBLANKEVENT p);
        [DllImport("gdi32.dll")] public static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER p);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        public const int GWL_STYLE = -16, WS_CAPTION = 0x00C00000;
        public const uint WAIT_OBJECT_0 = 0, WAIT_TIMEOUT = 0x102;
    }

    // ------------------------------------------------------------------ Ayna penceresi (fiziksel monitörde)
    // Fiziksel monitörü kaplayan siyah pencere; içindeki her Büyüteç alt penceresi bir sanal monitörün
    // görüntüsünü monitörün bir bölgesine 1:1 çizer (bozuk ekran: tek bölge, Ekran Çoğaltıcı: birden çok).
    class MirrorForm : Form
    {
        readonly IntPtr[] mags;
        readonly Win.RECT[] sources;
        readonly Rectangle[] magRects;

        public MirrorForm(Rectangle physBounds, Rectangle[] magAreas, Rectangle[] sourceRects)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            Bounds = physBounds;
            magRects = magAreas;
            mags = new IntPtr[magAreas.Length];
            sources = new Win.RECT[magAreas.Length];
            SetSources(sourceRects);
        }

        public void SetSources(Rectangle[] rs)
        {
            for (int i = 0; i < sources.Length && i < rs.Length; i++)
            {
                var r = rs[i];
                sources[i].Left = r.Left; sources[i].Top = r.Top; sources[i].Right = r.Right; sources[i].Bottom = r.Bottom;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000 | 0x08000000 | 0x8 | 0x20; // TOOLWINDOW | LAYERED | NOACTIVATE | TOPMOST | TRANSPARENT
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            VNative.SetLayeredWindowAttributes(Handle, 0, 255, 2);
            for (int i = 0; i < magRects.Length; i++)
            {
                var r = magRects[i];
                mags[i] = VNative.CreateWindowEx(0, "Magnifier", "BrokenScreenResizerMirror" + (i + 1),
                    VNative.WS_CHILD | VNative.WS_VISIBLE | VNative.MS_SHOWMAGNIFIEDCURSOR,
                    r.X, r.Y, r.Width, r.Height, Handle, IntPtr.Zero, VNative.GetModuleHandle(null), IntPtr.Zero);
                var t = new VNative.MAGTRANSFORM();
                t.v = new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
                if (mags[i] != IntPtr.Zero) VNative.MagSetWindowTransform(mags[i], ref t);
            }
        }

        public bool Ready
        {
            get
            {
                foreach (var m in mags) if (m == IntPtr.Zero) return false;
                return mags.Length > 0;
            }
        }

        // Her karede çağrılır. false: Büyüteç kaynağı kabul etmedi (ayna bozulmuş olabilir)
        public bool Frame()
        {
            bool ok = true;
            for (int i = 0; i < mags.Length; i++)
            {
                if (mags[i] == IntPtr.Zero) { ok = false; continue; }
                if (!VNative.MagSetWindowSource(mags[i], sources[i])) ok = false;
                // Arka plan silinir: Büyüteç çizemezse ayna donmuş eski görüntüde kalmaz, beyaz görünür
                // ve sağlık denetimi bunu yakalayıp aynayı yeniden kurar.
                VNative.InvalidateRect(mags[i], IntPtr.Zero, true);
            }
            return ok;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            for (int i = 0; i < mags.Length; i++)
                if (mags[i] != IntPtr.Zero) { VNative.DestroyWindow(mags[i]); mags[i] = IntPtr.Zero; }
            base.OnHandleDestroyed(e);
        }
    }

    // ------------------------------------------------------------------ Birleştirme bekçisi (v1.9)
    // Sanal monitörün köşesinde 2×2 piksellik, neredeyse tam saydam, tıklamaları geçiren pencere.
    // Bir oyun ya da video sanal monitörü tam kaplayınca Windows masaüstünü birleştirmeyi (DWM) bırakıp
    // oyunun karelerini doğrudan ekrana verebilir ("independent flip"). Büyüteç birleştirilmiş masaüstünü
    // kopyaladığı için ayna o zaman donar ya da saniyede 1-2 kareye düşer. Üstte görünen başka bir pencere
    // olduğu sürece Windows birleştirmeye devam eder; bu pencere de onu sağlar. Gözle görülmez.
    class KeeperForm : Form
    {
        public KeeperForm(Rectangle virt)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            Bounds = new Rectangle(virt.Right - 2, virt.Bottom - 2, 2, 2);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x80000 | 0x08000000 | 0x8 | 0x20; // TOOLWINDOW | LAYERED | NOACTIVATE | TOPMOST | TRANSPARENT
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            VNative.SetLayeredWindowAttributes(Handle, 0, 1, 2);   // 1/255 saydamlık: görünmez ama Windows için "görünür"
        }
    }

    // ------------------------------------------------------------------ Sanal monitör modunu yöneten sınıf
    // Ayna çizimi, dikey senkron bekleme ve fare/pencere kancaları ayrı iş parçacıklarında çalışır;
    // ana pencere ne yaparsa yapsın ayna akıcı kalır, fare de takılmaz.
    //
    // v1.7:
    //  - Ayna monitörün yenilenmesine (VSync) göre çizilir: oyunlarda takılma/titreme azalır, gereksiz kare çizilmez.
    //  - Beyaz ekran: Büyüteç bazen (kilit ekranı, UAC, uyku, oyunun ekran modu değişimi...) görüntüyü kaybedip beyaz kalır.
    //    Ayna bunu kendisi fark edip yeniden kurulur; olmazsa programa tam onarım için haber verir.
    //  - Görünmeyen ekrana (fiziksel monitör) düşen pencereler pencere olaylarıyla anında geri alınır.
    class VirtualMirror
    {
        // Bir sanal monitör ve görüntüsünün fiziksel monitörde çizileceği yer (monitörün kendi pikselleri)
        public class Target
        {
            public string Gdi;
            public Rectangle Area;
        }

        class Rects
        {
            public Rectangle Phys;
            public Rectangle[] Virt;
            public Rectangle[] Areas;   // her sanal monitörün fiziksel monitördeki yeri (monitöre göre)
            public int Fps;
        }

        class RescueInfo
        {
            public int Count, First;
            public bool Told;
        }

        // Çalışan aynalar: gizledikleri fiziksel monitör programın arayüzünde "ekran" olarak gösterilmez
        static readonly List<VirtualMirror> live = new List<VirtualMirror>();

        public static bool IsHidden(string gdi)
        {
            lock (live) foreach (var m in live) if (m.physGdi == gdi) return true;
            return false;
        }

        readonly string physGdi;
        readonly Target[] targets;
        readonly bool fromCuts;   // bozuk ekran modu: tek hedef, yeri kesimlerden hesaplanır
        readonly Cuts cuts;
        volatile Rects rects;
        volatile bool running, layoutChanged, rebuildNow, rebuildSoon, locked, failed, vsyncActive, vsyncReopen;
        volatile IntPtr mirrorHwnd = IntPtr.Zero;
        string startError;

        Thread renderThread, vsyncThread;
        readonly ManualResetEvent renderReady = new ManualResetEvent(false);
        readonly AutoResetEvent vblank = new AutoResetEvent(false);
        HookThread hooks;
        System.Windows.Forms.Timer winTimer;

        readonly Dictionary<IntPtr, RescueInfo> rescued = new Dictionary<IntPtr, RescueInfo>();
        readonly Queue<string> notices = new Queue<string>();

        // Ayna kendini onaramadı; ekran düzeni baştan kurulmalı (arayüz iş parçacığında çağrılır)
        public event Action Failed;
        // Kullanıcıya bildirilecek durum (arayüz iş parçacığında çağrılır)
        public event Action<string> Notice;

        // Bozuk ekran modu: sağlam alan boyutundaki tek sanal monitör, monitörün sağlam alanına çizilir
        public VirtualMirror(string physGdi, string virtGdi, Cuts cuts)
        {
            this.physGdi = physGdi;
            this.targets = new[] { new Target { Gdi = virtGdi } };
            this.cuts = cuts;
            fromCuts = true;
        }

        // Ekran Çoğaltıcı: her sanal monitör, monitörün kendi bölgesine çizilir
        public VirtualMirror(string physGdi, Target[] targets)
        {
            this.physGdi = physGdi;
            this.targets = targets;
        }

        // Fiziksel monitörün (artık sadece ayna) ve sanal monitörlerin masaüstü koordinatları
        public Rectangle PhysRect { get { var r = rects; return r != null ? r.Phys : Rectangle.Empty; } }
        public Rectangle VirtRect { get { var r = rects; return r != null && r.Virt.Length > 0 ? r.Virt[0] : Rectangle.Empty; } }
        public int Count { get { return targets.Length; } }
        public string PhysGdi { get { return physGdi; } }
        public bool IsSplit { get { return !fromCuts; } }

        public string[] VirtGdis
        {
            get
            {
                var a = new string[targets.Length];
                for (int i = 0; i < a.Length; i++) a[i] = targets[i].Gdi;
                return a;
            }
        }

        // Her sanal monitörün fiziksel monitördeki yeri (monitörün sol üst köşesine göre)
        public Rectangle[] PhysAreas
        {
            get { var r = rects; return r != null ? (Rectangle[])r.Areas.Clone() : new Rectangle[0]; }
        }

        Rects ReadRects()
        {
            var now = DisplayLayout.Capture();
            var p = DisplayLayout.Find(now, physGdi);
            if (p == null || !p.Attached) return null;
            var r = new Rects();
            r.Phys = p.Rect;
            r.Virt = new Rectangle[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                var v = DisplayLayout.Find(now, targets[i].Gdi);
                if (v == null || !v.Attached) return null;
                r.Virt[i] = v.Rect;
            }
            r.Fps = Math.Max(30, Math.Min(240, p.Freq > 1 ? p.Freq : 60));
            r.Areas = Areas(r);
            return r;
        }

        // Her hedefin fiziksel monitördeki yeri (sanal monitörden büyük olamaz)
        Rectangle[] Areas(Rects r)
        {
            var a = new Rectangle[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                var want = fromCuts ? cuts.Good(new Rectangle(0, 0, r.Phys.Width, r.Phys.Height)) : targets[i].Area;
                a[i] = new Rectangle(want.X, want.Y, Math.Min(want.Width, r.Virt[i].Width), Math.Min(want.Height, r.Virt[i].Height));
            }
            return a;
        }

        public string Start()
        {
            rects = ReadRects();
            if (rects == null) return L.T("Monitörler bulunamadı.", "Monitors not found.");

            running = true;
            Perf.Boost();
            renderThread = new Thread(RenderThread);
            renderThread.IsBackground = true;
            renderThread.Priority = ThreadPriority.Highest;
            renderThread.SetApartmentState(ApartmentState.STA);
            renderThread.Start();
            if (!renderReady.WaitOne(8000) || startError != null)
            {
                string e = startError ?? L.T("Ayna penceresi oluşturulamadı.", "Could not create the mirror window.");
                Stop();
                return e;
            }

            vsyncThread = new Thread(VsyncThread);
            vsyncThread.IsBackground = true;
            vsyncThread.Priority = ThreadPriority.Highest;
            vsyncThread.Start();

            hooks = new HookThread(OnMouse, OnWindowEvent);
            hooks.Start();

            healthTimer = new System.Threading.Timer(HealthTick, null, 4000, 2000);

            // Yedek denetim: olay kaçarsa pencereler en geç 1 sn içinde geri alınır
            winTimer = new System.Windows.Forms.Timer();
            winTimer.Interval = 1000;
            winTimer.Tick += delegate { OnTimer(); };
            winTimer.Start();
            PullWindows();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerChanged;
            lock (live) live.Add(this);
            return null;
        }

        // Elle onarım (tepsi menüsü): büyüteci hemen yeniden kurar
        public void Refresh()
        {
            layoutChanged = true;
            rebuildNow = true;
        }

        // ---------------------------------------------------------------- Ayna (kendi iş parçacığı)
        readonly List<KeeperForm> keepers = new List<KeeperForm>();   // sadece çizim iş parçacığında kullanılır

        MirrorForm MakeForm(Rects r)
        {
            var f = new MirrorForm(r.Phys, r.Areas, r.Virt);
            f.Show();
            if (!f.Ready)
            {
                try { f.Close(); f.Dispose(); } catch { }
                return null;
            }
            mirrorHwnd = f.Handle;
            MakeKeepers(r);
            return f;
        }

        void MakeKeepers(Rects r)
        {
            CloseKeepers();
            foreach (var v in r.Virt)
            {
                try
                {
                    var k = new KeeperForm(v);
                    k.Show();
                    keepers.Add(k);
                }
                catch { }
            }
        }

        void CloseKeepers()
        {
            foreach (var k in keepers) { try { k.Close(); k.Dispose(); } catch { } }
            keepers.Clear();
        }

        // Büyüteci ve ayna penceresini baştan kurar (program kapatılıp açılmış gibi)
        MirrorForm Rebuild(MirrorForm old, ref bool mag)
        {
            mirrorHwnd = IntPtr.Zero;
            CloseKeepers();
            if (old != null) { try { old.Close(); old.Dispose(); } catch { } }
            if (mag) { try { VNative.MagUninitialize(); } catch { } }
            try { mag = VNative.MagInitialize(); } catch { mag = false; }
            var r = rects;
            if (!mag || r == null) return null;
            try { return MakeForm(r); } catch { return null; }
        }

        void RenderThread()
        {
            MirrorForm form = null;
            bool mag = false, hiRes = false;
            try
            {
                try { mag = VNative.MagInitialize(); } catch { mag = false; }
                if (!mag)
                {
                    startError = L.T("Windows Büyüteç API'si başlatılamadı.", "Could not start the Windows Magnification API.");
                    return;
                }
                form = MakeForm(rects);
                if (form == null)
                {
                    startError = L.T("Ayna penceresi oluşturulamadı.", "Could not create the mirror window.");
                    return;
                }
                renderReady.Set();

                var sw = Stopwatch.StartNew();
                var waitOn = new[] { vblank.SafeWaitHandle.DangerousGetHandle() };
                double next = 0, nextTop = 0, lastRebuild = -100000, rebuildAt = -1, missingSince = -1, nextMissing = 0;
                double statStart = 0, statMax = 0, statLogged = -100000;
                int skip = 0, badFrames = 0, statFrames = 0;

                while (running)
                {
                    Application.DoEvents();
                    if (!running) break;
                    double now = sw.Elapsed.TotalMilliseconds;

                    // ekran düzeni değişti ya da sanal monitör kayboldu
                    if (layoutChanged || (missingSince >= 0 && now >= nextMissing))
                    {
                        layoutChanged = false;
                        nextMissing = now + 1000;
                        var nr = ReadRects();
                        if (nr != null)
                        {
                            rects = nr;
                            missingSince = -1;
                            vsyncReopen = true;
                            // yeni düzende büyüteci baştan kur; eski kaynak geçersiz kalıp beyaz ekrana yol açabilir
                            if (rebuildAt < 0) rebuildAt = now + 1200;
                        }
                        else if (missingSince < 0) missingSince = now;
                    }
                    // sanal monitör uzun süre geri gelmedi (ör. uyku sonrası sürücü yeniden başladı): tam onarım
                    if (missingSince >= 0 && now - missingSince > 8000) { missingSince = -1; failed = true; Log.Write("mirror: virtual monitor missing for 8 s -> full repair"); }

                    if (rebuildNow) { rebuildNow = false; rebuildAt = now; }
                    if (rebuildSoon) { rebuildSoon = false; rebuildAt = now + 1000; }
                    if ((rebuildAt >= 0 && now >= rebuildAt && !locked) || (form == null && now - lastRebuild >= 3000))
                    {
                        if (now - lastRebuild >= 1500)
                        {
                            rebuildAt = -1;
                            lastRebuild = now;
                            form = Rebuild(form, ref mag);
                            Log.Write("mirror rebuilt" + (form == null ? " (FAILED)" : "") + (Perf.FullScreenApp ? " [full-screen app]" : ""));
                            lastRebuildTick = Environment.TickCount;
                            skip = 0;
                        }
                        else rebuildAt = lastRebuild + 1500;
                    }
                    if (form == null)
                    {
                        VNative.MsgWaitForMultipleObjects(0, null, false, 100, VNative.QS_ALLINPUT);
                        continue;
                    }

                    // ---- kare zamanlaması: monitörün yenilenmesine göre (yoksa zamanlayıcıyla)
                    var rs = rects;
                    double period = 1000.0 / rs.Fps;
                    bool due;
                    if (vsyncActive)
                    {
                        if (hiRes) { try { VNative.timeEndPeriod(1); } catch { } hiRes = false; }
                        uint w = VNative.MsgWaitForMultipleObjects(1, waitOn, false, (uint)Math.Ceiling(period * 3), VNative.QS_ALLINPUT);
                        due = w == MNative.WAIT_OBJECT_0 || w == MNative.WAIT_TIMEOUT;
                    }
                    else
                    {
                        if (!hiRes) { try { VNative.timeBeginPeriod(1); hiRes = true; } catch { } }
                        double t = sw.Elapsed.TotalMilliseconds;
                        if (t < next)
                        {
                            VNative.MsgWaitForMultipleObjects(0, null, false, (uint)Math.Max(1, Math.Min(50, next - t)), VNative.QS_ALLINPUT);
                            due = sw.Elapsed.TotalMilliseconds >= next;
                        }
                        else due = true;
                    }
                    if (!due || !running) continue;
                    if (locked) { next = sw.Elapsed.TotalMilliseconds + 100; continue; }   // kilit ekranındayken çizmeye gerek yok
                    if (skip > 0) { skip--; continue; }

                    double t0 = sw.Elapsed.TotalMilliseconds;
                    bool ok = form.Frame();
                    double cost = sw.Elapsed.TotalMilliseconds - t0;
                    // Çizim yavaşsa kare hızını kendiliğinden düşür (sistemi ve oyunu kilitlemesin)
                    skip = cost > period ? Math.Min(3, (int)(cost / period)) : 0;
                    next = t0 + Math.Max(period, cost * 2);

                    badFrames = ok ? 0 : badFrames + 1;
                    if (badFrames > rs.Fps * 2) { badFrames = 0; rebuildAt = t0; Log.Write("mirror: magnifier rejected the source, rebuilding"); }

                    // Kare istatistiği: ayna belirgin şekilde yavaşladıysa günlüğe yazılır (en fazla 30 sn'de bir)
                    statFrames++;
                    if (cost > statMax) statMax = cost;
                    if (t0 - statStart >= 10000)
                    {
                        double fps = statFrames * 1000.0 / (t0 - statStart);
                        if (fps < rs.Fps / 2.0 && t0 - statLogged >= 30000)
                        {
                            statLogged = t0;
                            Log.Write("mirror slow: " + fps.ToString("0.0") + " fps (target " + rs.Fps + "), slowest frame " + statMax.ToString("0") +
                                      " ms, vsync=" + vsyncActive + (Perf.FullScreenApp ? " [full-screen app]" : ""));
                        }
                        statStart = t0; statFrames = 0; statMax = 0;
                    }

                    if (t0 >= nextTop)
                    {
                        // Başka "her zaman üstte" pencereler aynanın (ve bekçilerin) üstüne çıkmasın
                        const uint topFlags = Win.SWP_NOSIZE | VNative.SWP_NOMOVE | Win.SWP_NOACTIVATE | VNative.SWP_ASYNCWINDOWPOS;
                        Win.SetWindowPos(form.Handle, VNative.HWND_TOPMOST, 0, 0, 0, 0, topFlags);
                        foreach (var k in keepers) Win.SetWindowPos(k.Handle, VNative.HWND_TOPMOST, 0, 0, 0, 0, topFlags);
                        nextTop = t0 + 1000;
                    }
                }
            }
            catch (Exception ex)
            {
                if (startError == null && !renderReady.WaitOne(0)) startError = ex.Message;
                else if (running) failed = true;   // ayna beklenmedik şekilde durdu: tam onarım
            }
            finally
            {
                if (hiRes) { try { VNative.timeEndPeriod(1); } catch { } }
                mirrorHwnd = IntPtr.Zero;
                CloseKeepers();
                if (form != null) { try { form.Close(); form.Dispose(); } catch { } }
                if (mag) { try { VNative.MagUninitialize(); } catch { } }
                renderReady.Set();
            }
        }

        // ---------------------------------------------------------------- Sağlık denetimi (iş parçacığı havuzunda)
        // Ekrandan birkaç piksel okumak birkaç ms sürebilir; çizim iş parçacığını (ve oyunu) bekletmesin diye ayrı çalışır.
        // Ayna bembeyaz ama sanal monitör değilse büyüteç görüntüyü kaybetmiştir: önce ayna yeniden kurulur,
        // iki denemede düzelmezse program ekran düzenini baştan kurar.
        System.Threading.Timer healthTimer;
        volatile int lastRebuildTick = Environment.TickCount;
        int healthBusy, whiteStrikes, repairs, quietUntil;

        void HealthTick(object state)
        {
            if (Interlocked.Exchange(ref healthBusy, 1) == 1) return;
            try
            {
                int now = Environment.TickCount;
                var rs = rects;
                if (!running || locked || rs == null || mirrorHwnd == IntPtr.Zero) return;
                if (now - lastRebuildTick < 2500 || now - quietUntil < 0) return;
                // Tam ekran oyun/video öndeyken ekrandan piksel okunmaz: her okuma ekran kartını bir an bekletir
                // (oyunda takılma) ve oyunun koyu/donuk sahneleri yanlış alarm verip aynayı oyun ortasında
                // defalarca yeniden kurdurabiliyordu. Oyundan çıkınca denetim kaldığı yerden devam eder.
                if (Perf.FullScreenApp) { whiteStrikes = 0; return; }
                if (!LooksBroken(rs)) { whiteStrikes = 0; repairs = 0; return; }
                if (++whiteStrikes < 2) return;
                whiteStrikes = 0;
                if (++repairs > 2)
                {
                    // yeniden kurmak yetmedi: program ekran düzenini baştan kursun
                    repairs = 0;
                    failed = true;
                    quietUntil = now + 30000;
                    Log.Write("health: mirror still white/black after 2 rebuilds -> full repair");
                }
                else
                {
                    rebuildNow = true;
                    Log.Write("health: mirror looks white/black -> rebuild");
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref healthBusy, 0); }
        }

        static bool NearWhite(uint c)
        {
            return (c & 0xFF) >= 250 && ((c >> 8) & 0xFF) >= 250 && ((c >> 16) & 0xFF) >= 250;
        }

        static bool NearBlack(uint c)
        {
            return (c & 0xFF) <= 4 && ((c >> 8) & 0xFF) <= 4 && ((c >> 16) & 0xFF) <= 4;
        }

        static readonly float[,] Probes = { { 0.25f, 0.25f }, { 0.75f, 0.25f }, { 0.5f, 0.5f }, { 0.25f, 0.75f }, { 0.75f, 0.75f } };

        // Aynanın 5 noktası saf beyaz (ya da saf siyah), sanal monitörün aynı noktaları değilse ayna bozulmuştur:
        // Büyüteç görüntüyü kaybedince kendi arka planını (beyaz) ya da boş görüntü (siyah) gösterir.
        // (Ekranın gerçekten beyaz/siyah olduğu durumda sanal monitör de öyledir; yanlış alarm vermez.)
        static bool LooksBroken(Rects r)
        {
            IntPtr dc = MNative.GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return false;
            try
            {
                for (int i = 0; i < r.Areas.Length; i++)
                {
                    var a = r.Areas[i];
                    if (a.Width < 40 || a.Height < 40) continue;
                    int n = Probes.GetLength(0), mirrorWhite = 0, sourceWhite = 0, mirrorBlack = 0, sourceLit = 0;
                    for (int k = 0; k < n; k++)
                    {
                        int dx = (int)(a.Width * Probes[k, 0]), dy = (int)(a.Height * Probes[k, 1]);
                        uint m = MNative.GetPixel(dc, r.Phys.X + a.X + dx, r.Phys.Y + a.Y + dy);
                        uint s = MNative.GetPixel(dc, r.Virt[i].X + dx, r.Virt[i].Y + dy);
                        if (m == MNative.CLR_INVALID || s == MNative.CLR_INVALID) return false;   // okunamadı (kilit ekranı vb.)
                        if (NearWhite(m)) mirrorWhite++;
                        if (NearWhite(s)) sourceWhite++;
                        if (NearBlack(m)) mirrorBlack++;
                        // kaynakta belirgin şekilde aydınlık nokta (koyu bir oyun sahnesi siyah sayılmasın diye eşik yüksek)
                        if ((s & 0xFF) + ((s >> 8) & 0xFF) + ((s >> 16) & 0xFF) > 90) sourceLit++;
                    }
                    if (mirrorWhite == n && sourceWhite < n) return true;
                    if (mirrorBlack == n && sourceLit >= 2) return true;
                }
            }
            catch { }
            finally { MNative.ReleaseDC(IntPtr.Zero, dc); }
            return false;
        }

        // ---------------------------------------------------------------- Dikey senkron (kendi iş parçacığı)
        // Fiziksel monitör her yenilendiğinde aynaya "yeni kare çiz" sinyali verir. Bekleme hiç gelmezse
        // (ör. monitör kapalı) ayna zamanlayıcıyla çizmeye devam eder; çizim iş parçacığı asla takılmaz.
        bool OpenVsync(out MNative.D3DKMT_OPENADAPTERFROMHDC oa)
        {
            oa = new MNative.D3DKMT_OPENADAPTERFROMHDC();
            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = MNative.CreateDC(null, physGdi, null, IntPtr.Zero);
                if (dc == IntPtr.Zero) return false;
                oa.hDc = dc;
                return MNative.D3DKMTOpenAdapterFromHdc(ref oa) == 0;
            }
            catch { return false; }
            finally { if (dc != IntPtr.Zero) MNative.DeleteDC(dc); }
        }

        static void CloseVsync(uint adapter)
        {
            try
            {
                var c = new MNative.D3DKMT_CLOSEADAPTER { hAdapter = adapter };
                MNative.D3DKMTCloseAdapter(ref c);
            }
            catch { }
        }

        void VsyncThread()
        {
            var oa = new MNative.D3DKMT_OPENADAPTERFROMHDC();
            bool open = false;
            int errors = 0;
            try
            {
                while (running)
                {
                    if (!open || vsyncReopen)
                    {
                        if (open) { CloseVsync(oa.hAdapter); open = false; }
                        vsyncReopen = false;
                        open = OpenVsync(out oa);
                        if (!open)
                        {
                            vsyncActive = false;
                            if (++errors > 5) return;
                            Thread.Sleep(1000);
                            continue;
                        }
                    }
                    var w = new MNative.D3DKMT_WAITFORVERTICALBLANKEVENT { hAdapter = oa.hAdapter, hDevice = 0, VidPnSourceId = oa.VidPnSourceId };
                    if (MNative.D3DKMTWaitForVerticalBlankEvent(ref w) != 0)
                    {
                        vsyncActive = false;
                        if (++errors > 20) { Log.Write("vsync wait unavailable, using timer"); return; }   // bu sistemde çalışmıyor: zamanlayıcı kullanılır
                        vsyncReopen = true;
                        Thread.Sleep(50);
                        continue;
                    }
                    errors = 0;
                    vsyncActive = true;
                    vblank.Set();
                }
            }
            catch { }
            finally
            {
                vsyncActive = false;
                if (open) CloseVsync(oa.hAdapter);
            }
        }

        // ---------------------------------------------------------------- Sistem olayları
        void OnDisplayChanged(object s, EventArgs e)
        {
            layoutChanged = true;
        }

        void OnSessionSwitch(object s, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteConnect:
                    locked = true;
                    break;
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.SessionLogon:
                    // kilit ekranından sonra büyüteç çoğu zaman görüntüyü kaybeder: yeniden kur
                    locked = false;
                    layoutChanged = true;
                    rebuildSoon = true;
                    break;
            }
        }

        void OnPowerChanged(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                layoutChanged = true;
                rebuildSoon = true;
            }
        }

        void OnTimer()
        {
            PullWindows();
            while (true)
            {
                string msg;
                lock (notices) { if (notices.Count == 0) break; msg = notices.Dequeue(); }
                var n = Notice;
                if (n != null) n(msg);
            }
            if (failed && running)
            {
                failed = false;
                var h = Failed;
                if (h != null) h();
            }
        }

        // ---------------------------------------------------------------- Fare (kanca iş parçacığında)
        // Fare, sadece ayna olan fiziksel monitör alanına giremez
        bool OnMouse(int x, int y)
        {
            var r = rects;
            if (r == null || !r.Phys.Contains(x, y)) return false;
            Win.POINT cur;
            if (Win.GetCursorPos(out cur) && r.Phys.Contains(cur.X, cur.Y))
                Win.SetCursorPos(r.Virt[0].X + r.Virt[0].Width / 2, r.Virt[0].Y + r.Virt[0].Height / 2);
            return true;
        }

        // ---------------------------------------------------------------- Pencereleri kurtar
        // Pencere açıldı / taşındı / öne geldi: görünmeyen ekrana düştüyse hemen geri al
        void OnWindowEvent(IntPtr h)
        {
            var r = rects;
            if (r != null && running) Rescue(h, r);
        }

        // Fiziksel monitöre (aynanın arkasına, ulaşılamayan yere) düşen tüm pencereleri sanal monitöre taşır.
        // Broken Screen Resizer'ın kendi pencereleri de dahil.
        void PullWindows()
        {
            var r = rects;
            if (r == null) return;
            VNative.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                try { Rescue(h, r); } catch { }
                return true;
            }, IntPtr.Zero);

            // kapanmış pencerelerin kayıtlarını temizle
            lock (rescued)
            {
                if (rescued.Count == 0) return;
                var dead = new List<IntPtr>();
                foreach (var k in rescued.Keys) if (!MNative.IsWindow(k)) dead.Add(k);
                foreach (var k in dead) rescued.Remove(k);
            }
        }

        static readonly string[] SkipClasses = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW" };

        void Rescue(IntPtr h, Rects all)
        {
            if (h == mirrorHwnd || !VNative.IsWindowVisible(h) || Win.IsIconic(h)) return;
            Win.RECT wr;
            if (!Win.GetWindowRect(h, out wr)) return;
            int cx = (wr.Left + wr.Right) / 2, cy = (wr.Top + wr.Bottom) / 2;
            if (!all.Phys.Contains(cx, cy)) return;

            int ex = VNative.GetWindowLong(h, VNative.GWL_EXSTYLE);
            if ((ex & VNative.WS_EX_NOACTIVATE) != 0) return;
            // araç pencereleri: başlık çubuğu olanlar (ör. yüzen paneller) taşınır, menü/ipucu gibi olanlar değil
            if ((ex & VNative.WS_EX_TOOLWINDOW) != 0 &&
                (VNative.GetWindowLong(h, MNative.GWL_STYLE) & MNative.WS_CAPTION) != MNative.WS_CAPTION) return;
            int cloaked;
            if (VNative.DwmGetWindowAttribute(h, VNative.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return;
            var cls = new StringBuilder(64);
            Win.GetClassName(h, cls, 64);
            if (Array.IndexOf(SkipClasses, cls.ToString()) >= 0) return;
            if (!Allow(h)) return;

            // Pencere monitörün hangi bölgesine düştüyse o bölgenin sanal monitörüne taşınır
            int target = 0;
            for (int i = 0; i < all.Areas.Length; i++)
                if (all.Areas[i].Contains(cx - all.Phys.X, cy - all.Phys.Y)) { target = i; break; }
            var v = all.Virt[target];

            // Asenkron çağrılar: yanıt vermeyen başka bir program Broken Screen Resizer'ı kilitleyemesin
            const uint flags = Win.SWP_NOZORDER | Win.SWP_NOACTIVATE | VNative.SWP_ASYNCWINDOWPOS;
            if (Win.IsZoomed(h))
            {
                // Büyütülmüş pencere: küçült, sanal monitöre taşı, orada yeniden büyüt
                VNative.ShowWindowAsync(h, VNative.SW_RESTORE);
                Win.SetWindowPos(h, IntPtr.Zero, v.X + v.Width / 10, v.Y + v.Height / 10,
                                 v.Width * 8 / 10, v.Height * 8 / 10, flags);
                VNative.ShowWindowAsync(h, VNative.SW_MAXIMIZE);
                return;
            }
            // Kenarlıksız tam ekran (ör. oyun) monitörü kaplıyorsa sanal monitörü tam kaplar
            int w = Math.Min(wr.Right - wr.Left, v.Width), ht = Math.Min(wr.Bottom - wr.Top, v.Height);
            int x = v.X + (v.Width - w) / 2, y = v.Y + (v.Height - ht) / 2;
            Win.SetWindowPos(h, IntPtr.Zero, x, y, w, ht, flags);
        }

        // Aynı pencere kısa sürede defalarca geri alınıyorsa (taşınamıyor ya da program onu geri itiyor)
        // denemeyi bir süre bırakır ve kullanıcıya bir kez haber verir.
        bool Allow(IntPtr h)
        {
            int now = Environment.TickCount;
            lock (rescued)
            {
                RescueInfo ri;
                if (!rescued.TryGetValue(h, out ri)) { ri = new RescueInfo { First = now }; rescued[h] = ri; }
                if (now - ri.First > 10000) { ri.First = now; ri.Count = 0; }
                ri.Count++;
                if (ri.Count <= 6) return true;
                if (!ri.Told)
                {
                    ri.Told = true;
                    var sb = new StringBuilder(256);
                    MNative.GetWindowText(h, sb, 256);
                    string title = sb.Length > 0 ? sb.ToString() : L.T("Bir pencere", "A window");
                    lock (notices)
                        notices.Enqueue(string.Format(L.T(
                            "\"{0}\" görünmeyen ekranda kaldı ve geri getirilemedi; yönetici olarak çalışıyor olabilir. Pencereyi seçip Win+Shift+← ya da → ile taşıyabilirsin.",
                            "\"{0}\" is stuck on the hidden screen and couldn't be brought back; it may be running as administrator. Select it and press Win+Shift+← or → to move it."), title));
                }
                return false;
            }
        }

        public void Stop()
        {
            running = false;
            lock (live) live.Remove(this);
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerChanged;
            if (winTimer != null) { winTimer.Stop(); winTimer.Dispose(); winTimer = null; }
            if (healthTimer != null) { healthTimer.Dispose(); healthTimer = null; }
            if (hooks != null) { hooks.Stop(); hooks = null; }
            vblank.Set();
            if (renderThread != null) { renderThread.Join(3000); renderThread = null; }
            // dikey senkron iş parçacığı bir sonraki yenilemede kendiliğinden biter (beklenmez)
            vsyncThread = null;
        }

        // ------------------------------------------------------------------ Kurulum adımları
        // Sanal monitörü fiziksel monitörün yerine koyan yeni düzeni hesaplar ve uygular.
        public static int Arrange(string physGdi, string virtGdi, int vw, int vh)
        {
            bool changed;
            return Arrange(physGdi, virtGdi, vw, vh, null, out changed);
        }

        // v1.9: baseline verilirse (programdan önceki düzenin yedeği) düzen ona göre hesaplanır, Windows'un o anki
        // haline göre değil. Açılışta Windows sanal monitörü kendi hatırladığı bir yere koymuş ya da fiziksel monitörü
        // önceki oturumdan kalma bir yerde tutuyor olabilir; o zaman o anki düzenden hesaplamak yanlış sonuç veriyordu.
        // Düzen zaten doğruysa hiçbir şey değiştirilmez (changed = false, ekran kırpışmaz).
        public static int Arrange(string physGdi, string virtGdi, int vw, int vh, List<LayoutEntry> baseline, out bool changed)
        {
            changed = false;
            var now = DisplayLayout.Capture();
            var plan = PlanVirtual(Merge(now, baseline, virtGdi), physGdi, virtGdi, vw, vh);
            if (plan == null) return -5;
            if (Matches(plan, now)) return 0;
            changed = true;
            return DisplayLayout.Apply(plan);
        }

        // Ekran düzeni planlananla aynı mı (açılıştan sonra Windows düzeni değiştirdi mi diye bakmak için)
        public static bool LayoutOk(string physGdi, string virtGdi, int vw, int vh, List<LayoutEntry> baseline)
        {
            var now = DisplayLayout.Capture();
            var plan = PlanVirtual(Merge(now, baseline, virtGdi), physGdi, virtGdi, vw, vh);
            return plan != null && Matches(plan, now);
        }

        static LayoutEntry Clone(LayoutEntry e)
        {
            return new LayoutEntry { Gdi = e.Gdi, X = e.X, Y = e.Y, W = e.W, H = e.H, Freq = e.Freq, Primary = e.Primary, Attached = e.Attached };
        }

        // Şu an bağlı olan ekranlar; yedekte açık görünenlerin yeri ve boyutu yedekten alınır
        static List<LayoutEntry> Merge(List<LayoutEntry> now, List<LayoutEntry> baseline, string virtGdi)
        {
            var list = new List<LayoutEntry>();
            foreach (var n in now)
            {
                var b = baseline != null && n.Gdi != virtGdi ? DisplayLayout.Find(baseline, n.Gdi) : null;
                list.Add(Clone(b != null && b.Attached && b.W > 0 && b.H > 0 ? b : n));
            }
            return list;
        }

        static bool Matches(List<LayoutEntry> plan, List<LayoutEntry> now)
        {
            foreach (var e in plan)
            {
                if (!e.Attached) continue;
                var n = DisplayLayout.Find(now, e.Gdi);
                if (n == null || !n.Attached || n.X != e.X || n.Y != e.Y || n.W != e.W || n.H != e.H || n.Primary != e.Primary) return false;
            }
            foreach (var n in now)
                if (n.Attached && DisplayLayout.Find(plan, n.Gdi) == null) return false;
            return true;
        }

        static List<LayoutEntry> PlanVirtual(List<LayoutEntry> now, string physGdi, string virtGdi, int vw, int vh)
        {
            var p = DisplayLayout.Find(now, physGdi);
            var v = DisplayLayout.Find(now, virtGdi);
            if (p == null || v == null || !p.Attached) return null;

            int dW = p.W - vw, dH = p.H - vh;
            var list = new List<LayoutEntry>();
            foreach (var e in now) if (e.Attached && e.Gdi != physGdi && e.Gdi != virtGdi) list.Add(e);

            // sanal monitör fiziksel monitörün yerini alır; boşluk kalmasın diye komşular kaydırılır
            v.Attached = true;
            v.W = vw; v.H = vh; v.Freq = 0;
            v.X = p.X < 0 ? p.X + dW : p.X;
            v.Y = p.Y < 0 ? p.Y + dH : p.Y;
            v.Primary = p.Primary;
            foreach (var e in list)
            {
                if (p.X >= 0 && e.X >= p.X + p.W) e.X -= dW;
                if (p.Y >= 0 && e.Y >= p.Y + p.H) e.Y -= dH;
                if (v.Primary) e.Primary = false;
            }

            // fiziksel monitör (artık sadece ayna) sanal monitörün hemen altına alınır
            int bottom = v.Y + vh;
            foreach (var e in list)
                if (e.X < v.X + p.W && e.X + e.W > v.X) bottom = Math.Max(bottom, e.Y + e.H);
            p.X = v.X;
            p.Y = bottom;
            p.Primary = false;

            list.Add(v);
            list.Add(p);

            // birincil ekran (0,0)'da olmalı
            LayoutEntry prim = null;
            foreach (var e in list) if (e.Primary) prim = e;
            if (prim != null && (prim.X != 0 || prim.Y != 0))
            {
                int ox = prim.X, oy = prim.Y;
                foreach (var e in list) { e.X -= ox; e.Y -= oy; }
            }
            return list;
        }

        // Ekran Çoğaltıcı: sanal monitörler, monitördeki bölgeleriyle aynı yerleşimde fiziksel monitörün yerini alır.
        // Böylece fare bir bölgeden komşusuna, ekranda göründüğü yerden geçer. Bölgeler monitörün içinde
        // kaldığı için diğer monitörler kaydırılmaz; fiziksel monitör (artık sadece ayna) en alta alınır.
        public static int ArrangeSplit(string physGdi, string[] virtGdis, Rectangle[] regions)
        {
            var now = DisplayLayout.Capture();
            var p = DisplayLayout.Find(now, physGdi);
            if (p == null || !p.Attached || virtGdis.Length != regions.Length || regions.Length == 0) return -5;

            var list = new List<LayoutEntry>();
            foreach (var e in now)
                if (e.Attached && e.Gdi != physGdi && Array.IndexOf(virtGdis, e.Gdi) < 0) list.Add(e);

            // birincil ekran bölündüyse en büyük bölge birincil olur
            int primary = -1;
            if (p.Primary)
            {
                long best = -1;
                for (int i = 0; i < regions.Length; i++)
                {
                    long a = (long)regions[i].Width * regions[i].Height;
                    if (a > best) { best = a; primary = i; }
                }
                foreach (var e in list) e.Primary = false;
            }

            var virt = new List<LayoutEntry>();
            for (int i = 0; i < regions.Length; i++)
            {
                var v = DisplayLayout.Find(now, virtGdis[i]);
                if (v == null) return -5;
                v.Attached = true;
                v.X = p.X + regions[i].X; v.Y = p.Y + regions[i].Y;
                v.W = regions[i].Width; v.H = regions[i].Height; v.Freq = 0;
                v.Primary = i == primary;
                virt.Add(v);
            }

            int bottom = p.Y + p.H;
            foreach (var e in list)
                if (e.X < p.X + p.W && e.X + e.W > p.X) bottom = Math.Max(bottom, e.Y + e.H);
            p.Y = bottom;
            p.Primary = false;

            list.AddRange(virt);
            list.Add(p);

            LayoutEntry prim = null;
            foreach (var e in list) if (e.Primary) prim = e;
            if (prim != null && (prim.X != 0 || prim.Y != 0))
            {
                int ox = prim.X, oy = prim.Y;
                foreach (var e in list) { e.X -= ox; e.Y -= oy; }
            }
            return DisplayLayout.Apply(list);
        }
    }
}
