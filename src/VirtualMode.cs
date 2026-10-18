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

namespace EkranKurtar
{
    // ===================================================================================
    // "Sanal monitör modu":
    //  1) Ekran Kurtar'ın kendi sanal ekran sürücüsüyle (driver\, bkz. VirtualDriver.cs) sağlam alan boyutunda sanal bir monitör açılır.
    //  2) Sanal monitör, fiziksel monitörün yerine konur; fiziksel monitör düzende kenara alınır.
    //  3) Sanal monitörün görüntüsü Windows Büyüteç API'siyle fiziksel monitörün sağlam kısmına 1:1 kopyalanır.
    //  4) Fare fiziksel monitörün (artık sadece ayna olan) alanına giremez.
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

    // ------------------------------------------------------------------ Ayna penceresi (fiziksel monitörde)
    class MirrorForm : Form
    {
        IntPtr mag = IntPtr.Zero;
        Win.RECT source;
        Rectangle magRect;

        public MirrorForm(Rectangle physBounds, Rectangle magArea, Rectangle sourceRect)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            Bounds = physBounds;
            magRect = magArea;
            SetSource(sourceRect);
        }

        public void SetSource(Rectangle r)
        {
            source.Left = r.Left; source.Top = r.Top; source.Right = r.Right; source.Bottom = r.Bottom;
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
            mag = VNative.CreateWindowEx(0, "Magnifier", "EkranKurtarMirror",
                VNative.WS_CHILD | VNative.WS_VISIBLE | VNative.MS_SHOWMAGNIFIEDCURSOR,
                magRect.X, magRect.Y, magRect.Width, magRect.Height, Handle, IntPtr.Zero, VNative.GetModuleHandle(null), IntPtr.Zero);
            var t = new VNative.MAGTRANSFORM();
            t.v = new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            if (mag != IntPtr.Zero) VNative.MagSetWindowTransform(mag, ref t);
        }

        public bool Ready { get { return mag != IntPtr.Zero; } }

        // Her karede çağrılır
        public void Frame()
        {
            if (mag == IntPtr.Zero) return;
            VNative.MagSetWindowSource(mag, source);
            VNative.InvalidateRect(mag, IntPtr.Zero, true);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (mag != IntPtr.Zero) { VNative.DestroyWindow(mag); mag = IntPtr.Zero; }
            base.OnHandleDestroyed(e);
        }
    }

    // ------------------------------------------------------------------ Sanal monitör modunu yöneten sınıf
    // Ayna çizimi ve fare kancası ayrı iş parçacıklarında çalışır; ana pencere ne yaparsa yapsın
    // ayna akıcı kalır, fare de takılmaz.
    class VirtualMirror
    {
        class Rects
        {
            public Rectangle Phys, Virt;
            public int Fps;
        }

        readonly string physGdi, virtGdi;
        readonly Edge edge;
        readonly int cut;
        volatile Rects rects;
        volatile bool running, layoutChanged;
        volatile IntPtr mirrorHwnd = IntPtr.Zero;
        string startError;

        Thread renderThread, hookThread;
        readonly ManualResetEvent renderReady = new ManualResetEvent(false), hookReady = new ManualResetEvent(false);
        uint hookThreadId;
        Win.LowLevelMouseProc hookProc;
        IntPtr hook = IntPtr.Zero;

        System.Windows.Forms.Timer winTimer;

        public VirtualMirror(string physGdi, string virtGdi, Edge edge, int cut)
        {
            this.physGdi = physGdi; this.virtGdi = virtGdi; this.edge = edge; this.cut = cut;
        }

        // Fiziksel monitörün (artık sadece ayna) ve sanal monitörün masaüstü koordinatları
        public Rectangle PhysRect { get { var r = rects; return r != null ? r.Phys : Rectangle.Empty; } }
        public Rectangle VirtRect { get { var r = rects; return r != null ? r.Virt : Rectangle.Empty; } }

        Rects ReadRects()
        {
            var now = DisplayLayout.Capture();
            var p = DisplayLayout.Find(now, physGdi);
            var v = DisplayLayout.Find(now, virtGdi);
            if (p == null || v == null || !p.Attached || !v.Attached) return null;
            var r = new Rects();
            r.Phys = p.Rect;
            r.Virt = v.Rect;
            r.Fps = Math.Max(30, Math.Min(144, p.Freq > 1 ? p.Freq : 60));
            return r;
        }

        public string Start()
        {
            rects = ReadRects();
            if (rects == null) return L.T("Monitörler bulunamadı.", "Monitors not found.");

            running = true;
            renderThread = new Thread(RenderThread);
            renderThread.IsBackground = true;
            renderThread.Priority = ThreadPriority.AboveNormal;
            renderThread.SetApartmentState(ApartmentState.STA);
            renderThread.Start();
            if (!renderReady.WaitOne(8000) || startError != null)
            {
                string e = startError ?? L.T("Ayna penceresi oluşturulamadı.", "Could not create the mirror window.");
                Stop();
                return e;
            }

            hookThread = new Thread(HookThread);
            hookThread.IsBackground = true;
            hookThread.Priority = ThreadPriority.Highest;
            hookThread.Start();
            hookReady.WaitOne(3000);

            winTimer = new System.Windows.Forms.Timer();
            winTimer.Interval = 200;
            winTimer.Tick += delegate { PullWindows(); };
            winTimer.Start();
            PullWindows();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            return null;
        }

        // ---------------------------------------------------------------- Ayna (kendi iş parçacığı)
        void RenderThread()
        {
            MirrorForm form = null;
            bool mag = false;
            try
            {
                try { mag = VNative.MagInitialize(); } catch { mag = false; }
                if (!mag)
                {
                    startError = L.T("Windows Büyüteç API'si başlatılamadı.", "Could not start the Windows Magnification API.");
                    return;
                }
                var r = rects;
                var good = Geo.Good(new Rectangle(0, 0, r.Phys.Width, r.Phys.Height), edge, cut);
                var area = new Rectangle(good.X, good.Y, Math.Min(good.Width, r.Virt.Width), Math.Min(good.Height, r.Virt.Height));
                form = new MirrorForm(r.Phys, area, r.Virt);
                form.Show();
                if (!form.Ready)
                {
                    startError = L.T("Ayna penceresi oluşturulamadı.", "Could not create the mirror window.");
                    return;
                }
                mirrorHwnd = form.Handle;
                renderReady.Set();

                try { VNative.timeBeginPeriod(1); } catch { }
                var sw = Stopwatch.StartNew();
                double next = 0, nextTop = 0;
                while (running)
                {
                    Application.DoEvents();
                    if (!running) break;

                    if (layoutChanged)
                    {
                        layoutChanged = false;
                        var nr = ReadRects();
                        if (nr != null)
                        {
                            rects = nr;
                            form.Bounds = nr.Phys;
                            form.SetSource(nr.Virt);
                        }
                    }

                    double now = sw.Elapsed.TotalMilliseconds;
                    if (now >= next)
                    {
                        form.Frame();
                        double cost = sw.Elapsed.TotalMilliseconds - now;
                        // Çizim yavaşsa kare hızını kendiliğinden düşür (sistemi kilitlemesin)
                        next = now + Math.Max(1000.0 / rects.Fps, cost * 2);
                    }
                    if (now >= nextTop)
                    {
                        // Başka "her zaman üstte" pencereler aynanın üstüne çıkmasın
                        Win.SetWindowPos(form.Handle, VNative.HWND_TOPMOST, 0, 0, 0, 0,
                            Win.SWP_NOSIZE | VNative.SWP_NOMOVE | Win.SWP_NOACTIVATE | VNative.SWP_ASYNCWINDOWPOS);
                        nextTop = now + 1000;
                    }

                    int wait = (int)Math.Max(0, Math.Min(50, next - sw.Elapsed.TotalMilliseconds));
                    VNative.MsgWaitForMultipleObjects(0, null, false, (uint)wait, VNative.QS_ALLINPUT);
                }
            }
            catch (Exception ex)
            {
                if (startError == null && !renderReady.WaitOne(0)) startError = ex.Message;
            }
            finally
            {
                try { VNative.timeEndPeriod(1); } catch { }
                mirrorHwnd = IntPtr.Zero;
                if (form != null) { try { form.Close(); form.Dispose(); } catch { } }
                if (mag) { try { VNative.MagUninitialize(); } catch { } }
                renderReady.Set();
            }
        }

        void OnDisplayChanged(object s, EventArgs e)
        {
            layoutChanged = true;
        }

        // ---------------------------------------------------------------- Fare kancası (kendi iş parçacığı)
        void HookThread()
        {
            hookThreadId = VNative.GetCurrentThreadId();
            hookProc = Hook;
            try
            {
                using (var pr = Process.GetCurrentProcess())
                using (var mod = pr.MainModule)
                    hook = Win.SetWindowsHookEx(Win.WH_MOUSE_LL, hookProc, Win.GetModuleHandle(mod.ModuleName), 0);
            }
            catch { hook = IntPtr.Zero; }
            hookReady.Set();
            if (hook == IntPtr.Zero) return;

            VNative.MSG msg;
            while (VNative.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                VNative.TranslateMessage(ref msg);
                VNative.DispatchMessage(ref msg);
            }
            Win.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }

        // Fare, sadece ayna olan fiziksel monitör alanına giremez
        IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt64() == Win.WM_MOUSEMOVE)
            {
                var r = rects;
                var info = (Win.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win.MSLLHOOKSTRUCT));
                if (r != null && r.Phys.Contains(info.pt.X, info.pt.Y))
                {
                    Win.POINT cur;
                    if (Win.GetCursorPos(out cur) && r.Phys.Contains(cur.X, cur.Y))
                        Win.SetCursorPos(r.Virt.X + r.Virt.Width / 2, r.Virt.Y + r.Virt.Height / 2);
                    return new IntPtr(1);
                }
            }
            return Win.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // ---------------------------------------------------------------- Pencereleri kurtar
        // Fiziksel monitöre (aynanın arkasına, ulaşılamayan yere) düşen tüm pencereleri sanal monitöre taşır.
        // Ekran Kurtar'ın kendi pencereleri de dahil.
        void PullWindows()
        {
            var r = rects;
            if (r == null) return;
            IntPtr mirror = mirrorHwnd;
            VNative.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                try { Rescue(h, mirror, r); } catch { }
                return true;
            }, IntPtr.Zero);
        }

        static void Rescue(IntPtr h, IntPtr mirror, Rects r)
        {
            if (h == mirror || !VNative.IsWindowVisible(h) || Win.IsIconic(h)) return;
            int ex = VNative.GetWindowLong(h, VNative.GWL_EXSTYLE);
            if ((ex & (VNative.WS_EX_TOOLWINDOW | VNative.WS_EX_NOACTIVATE)) != 0) return;
            int cloaked;
            if (VNative.DwmGetWindowAttribute(h, VNative.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return;
            var cls = new StringBuilder(64);
            Win.GetClassName(h, cls, 64);
            string c = cls.ToString();
            if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c == "Progman" || c == "WorkerW") return;

            Win.RECT wr;
            if (!Win.GetWindowRect(h, out wr)) return;
            int cx = (wr.Left + wr.Right) / 2, cy = (wr.Top + wr.Bottom) / 2;
            if (!r.Phys.Contains(cx, cy)) return;

            // Asenkron çağrılar: yanıt vermeyen başka bir program Ekran Kurtar'ı kilitleyemesin
            const uint flags = Win.SWP_NOZORDER | Win.SWP_NOACTIVATE | VNative.SWP_ASYNCWINDOWPOS;
            if (Win.IsZoomed(h))
            {
                // Büyütülmüş pencere: küçült, sanal monitöre taşı, orada yeniden büyüt
                VNative.ShowWindowAsync(h, VNative.SW_RESTORE);
                Win.SetWindowPos(h, IntPtr.Zero, r.Virt.X + r.Virt.Width / 10, r.Virt.Y + r.Virt.Height / 10,
                                 r.Virt.Width * 8 / 10, r.Virt.Height * 8 / 10, flags);
                VNative.ShowWindowAsync(h, VNative.SW_MAXIMIZE);
                return;
            }
            int w = Math.Min(wr.Right - wr.Left, r.Virt.Width), ht = Math.Min(wr.Bottom - wr.Top, r.Virt.Height);
            int x = r.Virt.X + (r.Virt.Width - w) / 2, y = r.Virt.Y + (r.Virt.Height - ht) / 2;
            Win.SetWindowPos(h, IntPtr.Zero, x, y, w, ht, flags);
        }

        public void Stop()
        {
            running = false;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (winTimer != null) { winTimer.Stop(); winTimer.Dispose(); winTimer = null; }
            if (hookThread != null)
            {
                if (hookThreadId != 0) VNative.PostThreadMessage(hookThreadId, VNative.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                hookThread.Join(2000);
                hookThread = null;
            }
            if (renderThread != null) { renderThread.Join(3000); renderThread = null; }
        }

        // ------------------------------------------------------------------ Kurulum adımları
        // Sanal monitörü fiziksel monitörün yerine koyan yeni düzeni hesaplar ve uygular.
        public static int Arrange(string physGdi, string virtGdi, int vw, int vh)
        {
            var now = DisplayLayout.Capture();
            var p = DisplayLayout.Find(now, physGdi);
            var v = DisplayLayout.Find(now, virtGdi);
            if (p == null || v == null || !p.Attached) return -5;

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
            return DisplayLayout.Apply(list);
        }
    }
}
