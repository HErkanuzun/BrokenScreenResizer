using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // Bir kenardaki bozuk şeridi görev çubuğu gibi rezerve eden siyah pencere.
    // Birden fazla kenar bozuksa her kenar için bir tane açılır.
    class AppBarForm : Form
    {
        uint callbackMsg;
        bool registered;
        Rectangle screenBounds;
        readonly Edge edge;
        readonly int thick;

        public AppBarForm(Rectangle screenBounds, Edge edge, int thick)
        {
            this.screenBounds = screenBounds; this.edge = edge; this.thick = thick;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = FullStrip();
        }

        // Kenar boyunca tam uzunlukta şerit (Windows diğer çubuklara göre kısaltabilir)
        Rectangle FullStrip()
        {
            var s = screenBounds;
            switch (edge)
            {
                case Edge.Left: return new Rectangle(s.Left, s.Top, thick, s.Height);
                case Edge.Right: return new Rectangle(s.Right - thick, s.Top, thick, s.Height);
                case Edge.Top: return new Rectangle(s.Left, s.Top, s.Width, thick);
                default: return new Rectangle(s.Left, s.Bottom - thick, s.Width, thick);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8; // TOOLWINDOW | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        Win.APPBARDATA Data()
        {
            var d = new Win.APPBARDATA();
            d.cbSize = Marshal.SizeOf(typeof(Win.APPBARDATA));
            d.hWnd = Handle;
            return d;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var d = Data();
            callbackMsg = Win.RegisterWindowMessage("BrokenScreenResizer_AppBar");
            d.uCallbackMessage = callbackMsg;
            Win.SHAppBarMessage(Win.ABM_NEW, ref d);
            registered = true;
            SetPos();
        }

        public void Update(Rectangle screenBounds)
        {
            this.screenBounds = screenBounds;
            SetPos();
        }

        void SetPos()
        {
            if (!registered) return;
            var d = Data();
            d.uEdge = (uint)edge;
            var r = FullStrip();
            d.rc.Left = r.Left; d.rc.Top = r.Top; d.rc.Right = r.Right; d.rc.Bottom = r.Bottom;
            Win.SHAppBarMessage(Win.ABM_QUERYPOS, ref d);
            switch (edge)
            {
                case Edge.Left: d.rc.Right = d.rc.Left + thick; break;
                case Edge.Right: d.rc.Left = d.rc.Right - thick; break;
                case Edge.Top: d.rc.Bottom = d.rc.Top + thick; break;
                case Edge.Bottom: d.rc.Top = d.rc.Bottom - thick; break;
            }
            Win.SHAppBarMessage(Win.ABM_SETPOS, ref d);
            // Siyah alan her zaman ekranın o kenarındaki şeridi tam kaplasın
            Bounds = FullStrip();
        }

        public void Unregister()
        {
            if (!registered) return;
            var d = Data();
            Win.SHAppBarMessage(Win.ABM_REMOVE, ref d);
            registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (registered && m.Msg == (int)callbackMsg && m.WParam.ToInt64() == Win.ABN_POSCHANGED) SetPos();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Unregister();
            base.OnFormClosing(e);
        }
    }

    // "Yazılım modu": bozuk kenarları kapatır, fareyi ve pencereleri sağlam alanda (herhangi bir dikdörtgen) tutar.
    class Shield
    {
        readonly string gdiName;
        readonly Cuts signalCuts;   // monitör çözünürlüğüne göre
        readonly int signalW, signalH;
        Cuts cuts;                  // masaüstü koordinatlarına göre
        public bool LockCursor, FixWindows;
        Rectangle screenBounds;
        readonly List<AppBarForm> bars = new List<AppBarForm>();
        Timer timer;
        readonly uint myPid;

        public Shield(string gdiName, Cuts cuts, int signalW, int signalH, bool lockCursor, bool fixWindows)
        {
            this.gdiName = gdiName; signalCuts = cuts; this.signalW = signalW; this.signalH = signalH;
            LockCursor = lockCursor; FixWindows = fixWindows;
            myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        public static Screen FindScreen(string gdiName)
        {
            foreach (var s in Screen.AllScreens) if (s.DeviceName == gdiName) return s;
            return null;
        }

        void ReadScreen(Screen s)
        {
            screenBounds = s.Bounds;
            screens = Screen.AllScreens;
            cuts = signalW > 0 && signalH > 0 ? signalCuts.Scale(signalW, signalH, s.Bounds.Width, s.Bounds.Height) : signalCuts;
        }

        public bool Start()
        {
            var s = FindScreen(gdiName);
            if (s == null) return false;
            ReadScreen(s);
            foreach (var e in Cuts.All)
            {
                if (cuts[e] <= 0) continue;
                var bar = new AppBarForm(screenBounds, e, cuts[e]);
                bar.Show();
                bars.Add(bar);
            }
            try { InstallHook(); } catch { hook = IntPtr.Zero; }
            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            return true;
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            if (bars.Count == 0 || !bars[0].IsHandleCreated) return;
            bars[0].BeginInvoke((MethodInvoker)delegate
            {
                var s = FindScreen(gdiName);
                if (s == null) return;
                // Şerit kalınlıkları değişmez; sadece konumlar güncellenir
                screenBounds = s.Bounds;
                screens = Screen.AllScreens;
                foreach (var b in bars) b.Update(screenBounds);
            });
        }

        public void Stop()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            RemoveHook();
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            foreach (var b in bars) { b.Unregister(); b.Close(); b.Dispose(); }
            bars.Clear();
        }

        bool IsBar(IntPtr h)
        {
            foreach (var b in bars) if (b.IsHandleCreated && b.Handle == h) return true;
            return false;
        }

        void Tick()
        {
            if (LockCursor) PushCursor();
            if (FixWindows) PushForegroundWindow();
        }

        int lastX = int.MinValue, lastY = int.MinValue;

        // Fare bozuk alana girmek isterse nereye gitmeli?
        //  - sağlam alandan geliyorsa ve ötede başka monitör varsa, bozuk şeridin üstünden atlayıp o monitöre geçer
        //  - başka yerden geliyorsa (ör. komşu monitörden) sağlam alanın kenarına konur
        // Böylece monitörler arasında ölü alan hissedilmez.
        bool Redirect(int px, int py, int fromX, int fromY, out int x, out int y)
        {
            x = px; y = py;
            var S = screenBounds;
            var g = cuts.Good(S);
            if (!S.Contains(px, py) || g.Contains(px, py)) return false;
            x = Math.Max(g.Left, Math.Min(g.Right - 1, px));
            y = Math.Max(g.Top, Math.Min(g.Bottom - 1, py));
            if (g.Contains(fromX, fromY))
            {
                // Hangi bozuk şeride girdiyse o yönde ekranın dışına atla
                int bx = px, by = py;
                if (px < g.Left) bx = S.Left - 1;
                else if (px >= g.Right) bx = S.Right;
                if (py < g.Top) by = S.Top - 1;
                else if (py >= g.Bottom) by = S.Bottom;
                // köşeden çıkılıyorsa sadece bir yönde atlamayı da dene
                var tries = new[] { new Point(bx, by), new Point(bx, y), new Point(x, by) };
                foreach (var t in tries)
                {
                    if (t.X == x && t.Y == y) continue;
                    foreach (var s in screens)
                        if (s.DeviceName != gdiName && s.Bounds.Contains(t)) { x = t.X; y = t.Y; return true; }
                }
            }
            return true;
        }

        // Yedek: hook çalışmazsa zamanlayıcıyla kontrol
        void PushCursor()
        {
            Win.POINT p;
            if (!Win.GetCursorPos(out p)) return;
            int x, y;
            if (!Redirect(p.X, p.Y, lastX, lastY, out x, out y)) { lastX = p.X; lastY = p.Y; return; }
            Win.SetCursorPos(x, y);
            lastX = x; lastY = y;
        }

        // Asıl yöntem: düşük seviyeli fare kancası. Hareket olayı daha işlenmeden yakalanır,
        // böylece imleç bozuk alanda bir an bile görünmez.
        Win.LowLevelMouseProc hookProc;
        IntPtr hook = IntPtr.Zero;
        Screen[] screens = new Screen[0];

        void InstallHook()
        {
            hookProc = HookCallback;
            using (var proc = System.Diagnostics.Process.GetCurrentProcess())
            using (var mod = proc.MainModule)
                hook = Win.SetWindowsHookEx(Win.WH_MOUSE_LL, hookProc, Win.GetModuleHandle(mod.ModuleName), 0);
        }

        void RemoveHook()
        {
            if (hook != IntPtr.Zero) { Win.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && LockCursor && wParam.ToInt64() == Win.WM_MOUSEMOVE)
            {
                var info = (Win.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win.MSLLHOOKSTRUCT));
                Win.POINT cur;
                if (Win.GetCursorPos(out cur))
                {
                    int x, y;
                    if (Redirect(info.pt.X, info.pt.Y, cur.X, cur.Y, out x, out y))
                    {
                        Win.SetCursorPos(x, y);
                        lastX = x; lastY = y;
                        return new IntPtr(1);   // asıl hareketi engelle
                    }
                }
            }
            return Win.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        static readonly string[] SkipClasses = {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
            "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland"
        };

        void PushForegroundWindow()
        {
            if ((Win.GetAsyncKeyState(0x01) & 0x8000) != 0) return; // sürükleme sırasında dokunma

            IntPtr h = Win.GetForegroundWindow();
            if (h == IntPtr.Zero || IsBar(h)) return;
            uint pid;
            Win.GetWindowThreadProcessId(h, out pid);
            if (pid == myPid) return;
            if (Win.IsIconic(h) || Win.IsZoomed(h)) return;

            var sb = new StringBuilder(256);
            Win.GetClassName(h, sb, 256);
            if (Array.IndexOf(SkipClasses, sb.ToString()) >= 0) return;

            Win.RECT r;
            if (!Win.GetWindowRect(h, out r)) return;
            var S = screenBounds;
            if (!S.Contains((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2)) return;

            var G = cuts.Good(S);
            const uint flags = Win.SWP_NOZORDER | Win.SWP_NOACTIVATE;

            // Kenarlıksız tam ekran -> sağlam alana sığdır
            if (r.Left <= S.Left && r.Top <= S.Top && r.Right >= S.Right && r.Bottom >= S.Bottom)
            {
                Win.SetWindowPos(h, IntPtr.Zero, G.Left, G.Top, G.Width, G.Height, flags);
                return;
            }

            // Normal pencere bozuk şeritlere taşmışsa it (7 px: Windows'un görünmez pencere kenarı).
            // Sadece bozuk olan kenarlara bakılır; diğer kenarlardan komşu monitöre taşmak serbest.
            int x = r.Left, y = r.Top, w = r.Right - r.Left, ht = r.Bottom - r.Top;
            bool move = false;
            bool overL = cuts.L > 0 && x < G.Left - 10, overR = cuts.R > 0 && x + w > G.Right + 10;
            if (overL || overR)
            {
                w = Math.Min(w, G.Width + 14);
                if (cuts.L > 0 && x < G.Left - 10) x = G.Left - 7;
                if (cuts.R > 0 && x + w > G.Right + 10) x = G.Right + 7 - w;
                move = true;
            }
            bool overT = cuts.T > 0 && y < G.Top - 2, overB = cuts.B > 0 && y + ht > G.Bottom + 10;
            if (overT || overB)
            {
                ht = Math.Min(ht, G.Height + 7);
                if (cuts.T > 0 && y < G.Top - 2) y = G.Top;
                if (cuts.B > 0 && y + ht > G.Bottom + 10) y = G.Bottom + 7 - ht;
                move = true;
            }
            if (move) Win.SetWindowPos(h, IntPtr.Zero, x, y, w, ht, flags);
        }
    }
}
