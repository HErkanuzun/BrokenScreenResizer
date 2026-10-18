using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EkranKurtar
{
    static class Geo
    {
        // Kırık alan
        public static Rectangle Cut(Rectangle s, Edge e, int c)
        {
            switch (e)
            {
                case Edge.Left: return new Rectangle(s.Left, s.Top, c, s.Height);
                case Edge.Right: return new Rectangle(s.Right - c, s.Top, c, s.Height);
                case Edge.Top: return new Rectangle(s.Left, s.Top, s.Width, c);
                default: return new Rectangle(s.Left, s.Bottom - c, s.Width, c);
            }
        }

        // Sağlam alan
        public static Rectangle Good(Rectangle s, Edge e, int c)
        {
            switch (e)
            {
                case Edge.Left: return new Rectangle(s.Left + c, s.Top, s.Width - c, s.Height);
                case Edge.Right: return new Rectangle(s.Left, s.Top, s.Width - c, s.Height);
                case Edge.Top: return new Rectangle(s.Left, s.Top + c, s.Width, s.Height - c);
                default: return new Rectangle(s.Left, s.Top, s.Width, s.Height - c);
            }
        }
    }

    // Kırık alanı görev çubuğu gibi rezerve eden siyah pencere
    class AppBarForm : Form
    {
        uint callbackMsg;
        bool registered;
        Rectangle screenBounds;
        Edge edge;
        int cut;

        public AppBarForm(Rectangle screenBounds, Edge edge, int cut)
        {
            this.screenBounds = screenBounds; this.edge = edge; this.cut = cut;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = Geo.Cut(screenBounds, edge, cut);
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
            callbackMsg = Win.RegisterWindowMessage("EkranKurtar_AppBar");
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
            var r = Geo.Cut(screenBounds, edge, cut);
            d.rc.Left = r.Left; d.rc.Top = r.Top; d.rc.Right = r.Right; d.rc.Bottom = r.Bottom;
            Win.SHAppBarMessage(Win.ABM_QUERYPOS, ref d);
            switch (edge)
            {
                case Edge.Left: d.rc.Right = d.rc.Left + cut; break;
                case Edge.Right: d.rc.Left = d.rc.Right - cut; break;
                case Edge.Top: d.rc.Bottom = d.rc.Top + cut; break;
                case Edge.Bottom: d.rc.Top = d.rc.Bottom - cut; break;
            }
            Win.SHAppBarMessage(Win.ABM_SETPOS, ref d);
            Bounds = new Rectangle(d.rc.Left, d.rc.Top, d.rc.Right - d.rc.Left, d.rc.Bottom - d.rc.Top);
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

    // "Yazılım modu": kırık alanı kapatır, fareyi ve pencereleri sağlam alanda tutar.
    class Shield
    {
        readonly string gdiName;
        readonly Edge edge;
        readonly int cut;
        public bool LockCursor, FixWindows;
        Rectangle screenBounds;
        AppBarForm bar;
        Timer timer;
        readonly uint myPid;

        public Shield(string gdiName, Edge edge, int cut, bool lockCursor, bool fixWindows)
        {
            this.gdiName = gdiName; this.edge = edge; this.cut = cut;
            LockCursor = lockCursor; FixWindows = fixWindows;
            myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        public static Screen FindScreen(string gdiName)
        {
            foreach (var s in Screen.AllScreens) if (s.DeviceName == gdiName) return s;
            return null;
        }

        public bool Start()
        {
            var s = FindScreen(gdiName);
            if (s == null) return false;
            screenBounds = s.Bounds;
            bar = new AppBarForm(screenBounds, edge, cut);
            bar.Show();
            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            return true;
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            if (bar == null || !bar.IsHandleCreated) return;
            bar.BeginInvoke((MethodInvoker)delegate
            {
                var s = FindScreen(gdiName);
                if (s == null) return;
                screenBounds = s.Bounds;
                bar.Update(screenBounds);
            });
        }

        public void Stop()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (bar != null) { bar.Unregister(); bar.Close(); bar.Dispose(); bar = null; }
        }

        void Tick()
        {
            if (LockCursor) PushCursor();
            if (FixWindows) PushForegroundWindow();
        }

        int lastX = int.MinValue, lastY = int.MinValue;

        // Fare kırık alana girerse:
        //  - sağlam alandan geliyorsa ve ötede başka monitör varsa, kırık alanın üstünden atlayıp o monitöre geçer
        //  - başka yerden geliyorsa (ör. komşu monitörden) sağlam alanın kenarına konur
        // Böylece monitörler arasında ölü alan hissedilmez.
        void PushCursor()
        {
            Win.POINT p;
            if (!Win.GetCursorPos(out p)) return;
            var c = Geo.Cut(screenBounds, edge, cut);
            var g = Geo.Good(screenBounds, edge, cut);
            if (!c.Contains(p.X, p.Y)) { lastX = p.X; lastY = p.Y; return; }

            int x = Math.Max(g.Left, Math.Min(g.Right - 1, p.X));
            int y = Math.Max(g.Top, Math.Min(g.Bottom - 1, p.Y));

            if (g.Contains(lastX, lastY))
            {
                int bx = p.X, by = p.Y;
                switch (edge)
                {
                    case Edge.Left: bx = c.Left - 1; break;
                    case Edge.Right: bx = c.Right; break;
                    case Edge.Top: by = c.Top - 1; break;
                    case Edge.Bottom: by = c.Bottom; break;
                }
                foreach (var s in Screen.AllScreens)
                    if (s.DeviceName != gdiName && s.Bounds.Contains(bx, by)) { x = bx; y = by; break; }
            }
            Win.SetCursorPos(x, y);
            lastX = x; lastY = y;
        }

        static readonly string[] SkipClasses = {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
            "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland"
        };

        void PushForegroundWindow()
        {
            if ((Win.GetAsyncKeyState(0x01) & 0x8000) != 0) return; // sürükleme sırasında dokunma

            IntPtr h = Win.GetForegroundWindow();
            if (h == IntPtr.Zero || (bar != null && h == bar.Handle)) return;
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

            var G = Geo.Good(S, edge, cut);
            const uint flags = Win.SWP_NOZORDER | Win.SWP_NOACTIVATE;

            // Kenarlıksız tam ekran -> sağlam alana sığdır
            if (r.Left <= S.Left && r.Top <= S.Top && r.Right >= S.Right && r.Bottom >= S.Bottom)
            {
                Win.SetWindowPos(h, IntPtr.Zero, G.Left, G.Top, G.Width, G.Height, flags);
                return;
            }

            // Normal pencere kırık alana taşmışsa it (7 px: Windows'un görünmez pencere kenarı)
            int x = r.Left, y = r.Top, w = r.Right - r.Left, ht = r.Bottom - r.Top;
            bool move = false;
            switch (edge)
            {
                case Edge.Left:
                    if (r.Left < G.Left - 10) { x = G.Left - 7; w = Math.Min(w, G.Width + 14); move = true; }
                    break;
                case Edge.Right:
                    if (r.Right > G.Right + 10)
                    {
                        w = Math.Min(w, G.Width + 14);
                        x = G.Right + 7 - w;
                        move = true;
                    }
                    break;
                case Edge.Top:
                    if (r.Top < G.Top - 2) { y = G.Top; ht = Math.Min(ht, G.Height + 7); move = true; }
                    break;
                case Edge.Bottom:
                    if (r.Bottom > G.Bottom + 10)
                    {
                        ht = Math.Min(ht, G.Height + 7);
                        y = G.Bottom + 7 - ht;
                        move = true;
                    }
                    break;
            }
            if (move) Win.SetWindowPos(h, IntPtr.Zero, x, y, w, ht, flags);
        }
    }
}
