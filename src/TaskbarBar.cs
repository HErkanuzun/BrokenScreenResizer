using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // Görev çubuğundaki bir pencere
    class TbWindow
    {
        public IntPtr Handle;
        public string Title = "";
        public bool Minimized, Flashing;
        public Bitmap Icon;
    }

    // ===================================================================================
    // Programın kendi görev çubuğu: ana olmayan bir monitörün istenen kenarına yaslanır.
    // Windows'un çubuğu gibi yer ayırır (AppBar), o monitördeki pencereleri gösterir;
    // Başlat düğmesi, saat, pencere menüsü ve yanıp sönen pencere vurgusu vardır.
    // ===================================================================================
    class SideBar : Form
    {
        public readonly TbMonitor Monitor;
        public readonly int Side;
        readonly IntPtr hmon;
        readonly float k;   // bu monitörün DPI ölçeği
        uint callbackMsg;
        bool registered;
        public bool ShowLabels = true;
        public bool Center = true;
        public event Action<IntPtr> ButtonClicked;
        public event Action<IntPtr, Point> ButtonMenu;
        public event Action<Point> BarMenu;

        List<TbWindow> wins = new List<TbWindow>();
        IntPtr active;
        int hot = -1;            // -2 başlat, -3 saat, -4 taşma, >=0 pencere
        string clockText = "", dateText = "";
        int visibleCount;        // sığan düğme sayısı (taşma varsa son yer "⋯")
        readonly Font font, fontSmall;

        public SideBar(TbMonitor m, int side)
        {
            Monitor = m; Side = side;
            hmon = TbNative.MonitorOf(m.Bounds);
            k = (float)TbNative.MonitorScale(hmon);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = BarBg;
            TopMost = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            font = new Font("Segoe UI", 12f * k, GraphicsUnit.Pixel);
            fontSmall = new Font("Segoe UI", 11f * k, GraphicsUnit.Pixel);
            Bounds = Strip(m.Bounds);
            Text = "Broken Screen Resizer";
        }

        static readonly Color BarBg = Color.FromArgb(28, 28, 30);
        static readonly Color BarLine = Color.FromArgb(52, 52, 56);
        static readonly Color BtnHover = Color.FromArgb(48, 48, 52);
        static readonly Color BtnActive = Color.FromArgb(56, 58, 64);
        static readonly Color Flash = Color.FromArgb(150, 96, 24);
        static readonly Color WinBlue = Color.FromArgb(0, 120, 212), WinBlueLight = Color.FromArgb(80, 180, 255);

        int Thick { get { return (int)Math.Round(48 * k); } }
        bool Vertical { get { return TbSide.Vertical(Side); } }
        int P(float v) { return (int)Math.Round(v * k); }

        Rectangle Strip(Rectangle s)
        {
            int t = Thick;
            switch (Side)
            {
                case TbSide.Left: return new Rectangle(s.Left, s.Top, t, s.Height);
                case TbSide.Right: return new Rectangle(s.Right - t, s.Top, t, s.Height);
                case TbSide.Top: return new Rectangle(s.Left, s.Top, s.Width, t);
                default: return new Rectangle(s.Left, s.Bottom - t, s.Width, t);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8;   // TOOLWINDOW | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        // ---------------------------------------------------------------- AppBar
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
            callbackMsg = Win.RegisterWindowMessage("BrokenScreenResizer_TaskBar");
            d.uCallbackMessage = callbackMsg;
            Win.SHAppBarMessage(Win.ABM_NEW, ref d);
            registered = true;
            SetPos();
        }

        void SetPos()
        {
            if (!registered) return;
            var d = Data();
            d.uEdge = (uint)Side;
            var r = Strip(Monitor.Bounds);
            d.rc.Left = r.Left; d.rc.Top = r.Top; d.rc.Right = r.Right; d.rc.Bottom = r.Bottom;
            Win.SHAppBarMessage(Win.ABM_QUERYPOS, ref d);
            int t = Thick;
            switch (Side)
            {
                case TbSide.Left: d.rc.Right = d.rc.Left + t; break;
                case TbSide.Right: d.rc.Left = d.rc.Right - t; break;
                case TbSide.Top: d.rc.Bottom = d.rc.Top + t; break;
                default: d.rc.Top = d.rc.Bottom - t; break;
            }
            Win.SHAppBarMessage(Win.ABM_SETPOS, ref d);
            Bounds = Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) { font.Dispose(); fontSmall.Dispose(); }
            base.Dispose(disposing);
        }

        // ---------------------------------------------------------------- içerik
        public IntPtr MonitorHandle { get { return hmon; } }

        public void SetContent(List<TbWindow> list, IntPtr fg, string clock, string date)
        {
            bool same = list.Count == wins.Count && fg == active && clock == clockText && date == dateText;
            if (same)
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i]; var b = wins[i];
                    if (a.Handle != b.Handle || a.Title != b.Title || a.Minimized != b.Minimized || a.Flashing != b.Flashing || a.Icon != b.Icon) { same = false; break; }
                }
            wins = list; active = fg; clockText = clock; dateText = date;
            if (!same) Invalidate();
        }

        // Tam ekran bir pencere bu monitörü kaplıyorsa çubuk geri çekilir (oyun ve video üstünde görünmez)
        bool hiddenForFullscreen;
        public void SetFullscreen(bool full)
        {
            if (full == hiddenForFullscreen || !IsHandleCreated) return;
            hiddenForFullscreen = full;
            TbNative.ShowWindow(Handle, full ? TbNative.SW_HIDE : TbNative.SW_SHOWNOACTIVATE);
        }

        // ---------------------------------------------------------------- yerleşim
        Rectangle StartRect()
        {
            int s = P(44), pad = P(2);
            if (Vertical) return new Rectangle((Width - s) / 2, pad, s, s);
            return new Rectangle(pad + P(4), (Height - s) / 2, s, s);
        }

        Rectangle ClockRect()
        {
            if (Vertical) return new Rectangle(0, Height - P(52), Width, P(48));
            int w = P(84);
            return new Rectangle(Width - w - P(6), 0, w, Height);
        }

        int BtnLen()
        {
            if (Vertical) return P(44);
            return ShowLabels ? P(176) : P(44);
        }

        // Pencere düğmelerinin yerleri; sığmayanlar için son yer taşma düğmesi olur
        List<Rectangle> ButtonRects(out Rectangle overflow)
        {
            overflow = Rectangle.Empty;
            var res = new List<Rectangle>();
            var st = StartRect();
            var cl = ClockRect();
            int s = P(44), gap = P(2);
            int from = Vertical ? st.Bottom + P(6) : st.Right + P(6);
            int to = Vertical ? cl.Top - P(6) : cl.Left - P(6);
            int avail = Math.Max(0, to - from);
            int n = wins.Count;
            int len = BtnLen();
            if (!Vertical && ShowLabels && n > 0)
                len = Math.Max(s, Math.Min(len, (avail - (n - 1) * gap) / n));
            int fit = Math.Max(0, (avail + gap) / (len + gap));
            bool over = n > fit;
            int shown = over ? Math.Max(0, fit - 1) : n;
            int total = (over ? shown + 1 : shown) * (len + gap) - gap;
            int start = from;
            if (Center && total > 0 && total < avail) start = Vertical ? from : Math.Max(from, (Width - total) / 2);
            for (int i = 0; i < shown; i++)
            {
                int p = start + i * (len + gap);
                res.Add(Vertical ? new Rectangle((Width - s) / 2, p, s, len) : new Rectangle(p, (Height - s) / 2, len, s));
            }
            if (over)
            {
                int p = start + shown * (len + gap);
                overflow = Vertical ? new Rectangle((Width - s) / 2, p, s, s) : new Rectangle(p, (Height - s) / 2, s, s);
            }
            visibleCount = shown;
            return res;
        }

        int HitTest(Point p)
        {
            if (StartRect().Contains(p)) return -2;
            if (ClockRect().Contains(p)) return -3;
            Rectangle ov;
            var rs = ButtonRects(out ov);
            for (int i = 0; i < rs.Count; i++) if (rs[i].Contains(p)) return i;
            if (!ov.IsEmpty && ov.Contains(p)) return -4;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int h = HitTest(e.Location);
            var screenPt = PointToScreen(e.Location);
            if (e.Button == MouseButtons.Left)
            {
                if (h == -2) PressKeys(0x5B);            // Başlat (Windows tuşu)
                else if (h == -3) PressKeys(0x5B, 0x4E);  // bildirimler ve takvim (Windows + N)
                else if (h == -4) ShowOverflow(screenPt);
                else if (h >= 0 && h < wins.Count && ButtonClicked != null) ButtonClicked(wins[h].Handle);
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (h >= 0 && h < wins.Count) { if (ButtonMenu != null) ButtonMenu(wins[h].Handle, screenPt); }
                else if (BarMenu != null) BarMenu(screenPt);
            }
        }

        void ShowOverflow(Point at)
        {
            var menu = new ContextMenuStrip();
            for (int i = visibleCount; i < wins.Count; i++)
            {
                var w = wins[i];
                var item = menu.Items.Add(w.Title, w.Icon);
                item.Click += delegate { if (ButtonClicked != null) ButtonClicked(w.Handle); };
            }
            menu.Closed += delegate { BeginInvoke((MethodInvoker)menu.Dispose); };
            menu.Show(at);
        }

        static void PressKeys(params byte[] keys)
        {
            foreach (var key in keys) TbNative.keybd_event(key, 0, 0, UIntPtr.Zero);
            for (int i = keys.Length - 1; i >= 0; i--) TbNative.keybd_event(keys[i], 0, 2, UIntPtr.Zero);
        }

        // ---------------------------------------------------------------- çizim
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BarBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // ekrana bakan kenarda ince çizgi
            using (var pen = new Pen(BarLine, Math.Max(1, P(1))))
            {
                switch (Side)
                {
                    case TbSide.Left: g.DrawLine(pen, Width - 1, 0, Width - 1, Height); break;
                    case TbSide.Right: g.DrawLine(pen, 0, 0, 0, Height); break;
                    case TbSide.Top: g.DrawLine(pen, 0, Height - 1, Width, Height - 1); break;
                    default: g.DrawLine(pen, 0, 0, Width, 0); break;
                }
            }

            // Başlat
            var st = StartRect();
            if (hot == -2) Theme.Fill(g, st, P(6), BtnHover);
            float q = P(9), gq = P(1.5f);
            float cx = st.X + st.Width / 2f, cy = st.Y + st.Height / 2f;
            using (var b = new LinearGradientBrush(new RectangleF(cx - q, cy - q, q * 2, q * 2), WinBlueLight, WinBlue, 45f))
            {
                g.FillRectangle(b, cx - q, cy - q, q - gq, q - gq);
                g.FillRectangle(b, cx + gq, cy - q, q - gq, q - gq);
                g.FillRectangle(b, cx - q, cy + gq, q - gq, q - gq);
                g.FillRectangle(b, cx + gq, cy + gq, q - gq, q - gq);
            }

            // pencereler
            Rectangle ov;
            var rs = ButtonRects(out ov);
            for (int i = 0; i < rs.Count; i++) DrawButton(g, rs[i], wins[i], i == hot);
            if (!ov.IsEmpty)
            {
                if (hot == -4) Theme.Fill(g, ov, P(6), BtnHover);
                Theme.Draw(g, "⋯", font, ov, Color.FromArgb(220, 220, 225), StringAlignment.Center, StringAlignment.Center, false);
            }

            // saat
            var cr = ClockRect();
            if (hot == -3) Theme.Fill(g, Rectangle.Inflate(cr, -P(3), -P(4)), P(6), BtnHover);
            var top = new RectangleF(cr.X, cr.Y + cr.Height / 2f - font.Height, cr.Width, font.Height + P(1));
            var bottom = new RectangleF(cr.X, cr.Y + cr.Height / 2f, cr.Width, fontSmall.Height + P(1));
            Theme.Draw(g, clockText, font, top, Color.FromArgb(235, 235, 240), StringAlignment.Center, StringAlignment.Far, false);
            Theme.Draw(g, dateText, fontSmall, bottom, Color.FromArgb(180, 182, 190), StringAlignment.Center, StringAlignment.Near, false);
        }

        void DrawButton(Graphics g, Rectangle r, TbWindow w, bool isHot)
        {
            bool act = w.Handle == active && !w.Minimized;
            var rf = Rectangle.Inflate(r, -P(1), -P(1));
            if (w.Flashing && !act) Theme.Fill(g, rf, P(6), Flash);
            else if (act) Theme.Fill(g, rf, P(6), isHot ? Theme.Mix(BtnActive, Color.White, 0.06f) : BtnActive);
            else if (isHot) Theme.Fill(g, rf, P(6), BtnHover);

            int ic = P(24);
            bool label = !Vertical && ShowLabels && r.Width >= P(90);
            var iconRect = label ? new Rectangle(r.X + P(10), r.Y + (r.Height - ic) / 2, ic, ic)
                                 : new Rectangle(r.X + (r.Width - ic) / 2, r.Y + (r.Height - ic) / 2, ic, ic);
            if (w.Icon != null) g.DrawImage(w.Icon, iconRect);
            else
            {
                Theme.Fill(g, iconRect, P(5), Color.FromArgb(70, 74, 82));
                string letter = w.Title.Length > 0 ? w.Title.Substring(0, 1).ToUpperInvariant() : "?";
                Theme.Draw(g, letter, font, iconRect, Color.White, StringAlignment.Center, StringAlignment.Center, false);
            }
            if (label)
            {
                var tr = new RectangleF(iconRect.Right + P(8), r.Y, r.Right - iconRect.Right - P(14), r.Height);
                Theme.Draw(g, w.Title, font, tr, w.Minimized ? Color.FromArgb(175, 178, 186) : Color.FromArgb(235, 235, 240),
                    StringAlignment.Near, StringAlignment.Center, false);
            }

            // çalışan pencere çizgisi (Windows 11 gibi): etkin olan uzun ve renkli
            float lw = act ? P(16) : P(6), lh = P(3);
            RectangleF line;
            if (Vertical)
            {
                float x = Side == TbSide.Left ? r.X + P(1) : r.Right - P(1) - lh;
                line = new RectangleF(x, r.Y + (r.Height - lw) / 2f, lh, lw);
            }
            else
            {
                float y = Side == TbSide.Top ? r.Y + P(1) : r.Bottom - P(1) - lh;
                line = new RectangleF(r.X + (r.Width - lw) / 2f, y, lw, lh);
            }
            Theme.Fill(g, line, lh / 2, act ? WinBlueLight : Color.FromArgb(140, 144, 152));
        }
    }

    // ===================================================================================
    // Programın çubuklarını yönetir: pencereleri listeler, monitörlere dağıtır, saati günceller,
    // ekran düzeni değişince çubukları yeniden kurar.
    // ===================================================================================
    class TaskbarManager : IDisposable
    {
        readonly List<SideBar> bars = new List<SideBar>();
        readonly Func<TaskbarPlan> planner;
        readonly Timer timer = new Timer(), rebuild = new Timer();
        readonly List<IntPtr> order = new List<IntPtr>();             // ilk görülme sırası (düğmeler yer değiştirmesin)
        readonly HashSet<IntPtr> flashing = new HashSet<IntPtr>();
        readonly Dictionary<IntPtr, Bitmap> icons = new Dictionary<IntPtr, Bitmap>();
        readonly Dictionary<IntPtr, IntPtr> iconHandles = new Dictionary<IntPtr, IntPtr>();
        readonly Dictionary<string, Bitmap> exeIcons = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        ShellHook hook;
        public bool ShowLabels = true, Center = true;
        public event Action OpenSettings;
        int tick;

        public TaskbarManager(Func<TaskbarPlan> planner)
        {
            this.planner = planner;
            timer.Interval = 250;
            timer.Tick += delegate { Refresh(); };
            rebuild.Interval = 1200;
            rebuild.Tick += delegate { rebuild.Stop(); Build(); };
        }

        public int Count { get { return bars.Count; } }

        public void Start()
        {
            Build();
            if (hook == null)
            {
                hook = new ShellHook();
                hook.Event += OnShellEvent;
            }
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            timer.Start();
        }

        public void Stop()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            timer.Stop();
            rebuild.Stop();
            CloseBars();
            if (hook != null) { hook.Dispose(); hook = null; }
        }

        public void Dispose()
        {
            Stop();
            timer.Dispose();
            rebuild.Dispose();
            foreach (var b in icons.Values) b.Dispose();
            foreach (var b in exeIcons.Values) if (b != null) b.Dispose();
            icons.Clear(); exeIcons.Clear();
        }

        void CloseBars()
        {
            foreach (var b in bars) { b.Unregister(); b.Close(); b.Dispose(); }
            bars.Clear();
        }

        // Ekran düzeni değişince (monitör takıldı, Ekran Çoğaltıcı açıldı...) biraz bekleyip yeniden kur
        void OnDisplayChanged(object sender, EventArgs e)
        {
            try
            {
                if (hook != null && hook.IsHandleCreatedSafe) hook.Invoke(() => { rebuild.Stop(); rebuild.Start(); });
            }
            catch { }
        }

        void Build()
        {
            CloseBars();
            var plan = planner();
            if (plan == null || plan.Native) return;
            for (int i = 0; i < plan.BarMonitors.Count; i++)
            {
                var bar = new SideBar(plan.BarMonitors[i], plan.BarSides[i]);
                bar.ShowLabels = ShowLabels;
                bar.Center = Center;
                bar.ButtonClicked += Toggle;
                bar.ButtonMenu += WindowMenu;
                bar.BarMenu += BarMenu;
                bar.Show();
                bars.Add(bar);
            }
            Refresh();
        }

        void OnShellEvent(int code, IntPtr h)
        {
            if (code == 0x8006) flashing.Add(h);                       // HSHELL_FLASH
            else if (code == 4 || code == 0x8004) flashing.Remove(h);  // etkinleşti
            else if (code == 2) { flashing.Remove(h); order.Remove(h); }  // kapandı
            Refresh();
        }

        static readonly string[] SkipClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

        static bool IsTaskbarWindow(IntPtr h)
        {
            if (!TbNative.IsWindowVisible(h)) return false;
            long ex = TbNative.GetWindowLong(h, TbNative.GWL_EXSTYLE);
            bool app = (ex & TbNative.WS_EX_APPWINDOW) != 0;
            if (!app)
            {
                if ((ex & TbNative.WS_EX_TOOLWINDOW) != 0 || (ex & TbNative.WS_EX_NOACTIVATE) != 0) return false;
                if (TbNative.GetWindow(h, TbNative.GW_OWNER) != IntPtr.Zero) return false;
            }
            int cloaked;
            if (TbNative.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false;   // başka masaüstü / gizli UWP
            if (TbNative.GetWindowTextLength(h) <= 0) return false;
            return Array.IndexOf(SkipClasses, TbNative.ClassOf(h)) < 0;
        }

        // Tam ekran (kenarlıksız) pencere bu monitörü tamamen kaplıyor mu?
        static bool IsFullscreen(IntPtr fg, Rectangle mon)
        {
            if (fg == IntPtr.Zero) return false;
            string cls = TbNative.ClassOf(fg);
            if (cls == "Progman" || cls == "WorkerW") return false;
            TbNative.RECT r;
            if (!TbNative.GetWindowRect(fg, out r)) return false;
            return r.Left <= mon.Left && r.Top <= mon.Top && r.Right >= mon.Right && r.Bottom >= mon.Bottom;
        }

        void Refresh()
        {
            if (bars.Count == 0) return;
            var found = new List<IntPtr>();
            TbNative.EnumWindows((h, l) => { if (IsTaskbarWindow(h)) found.Add(h); return true; }, IntPtr.Zero);
            order.RemoveAll(h => !found.Contains(h));
            foreach (var h in found) if (!order.Contains(h)) order.Add(h);
            flashing.RemoveWhere(h => !found.Contains(h));
            foreach (var h in new List<IntPtr>(icons.Keys))
                if (!found.Contains(h)) { icons[h].Dispose(); icons.Remove(h); iconHandles.Remove(h); }

            IntPtr fg = TbNative.GetForegroundWindow();
            if (fg != IntPtr.Zero) flashing.Remove(fg);
            var now = DateTime.Now;
            string clock = now.ToString("t"), date = now.ToString("d");
            bool refreshIcons = (tick++ % 8) == 0;   // simgeler ~2 sn'de bir yenilenir

            foreach (var bar in bars)
            {
                var list = new List<TbWindow>();
                foreach (var h in order)
                {
                    if (TbNative.MonitorFromWindow(h, TbNative.MONITOR_DEFAULTTONEAREST) != bar.MonitorHandle) continue;
                    list.Add(new TbWindow
                    {
                        Handle = h, Title = TbNative.TitleOf(h), Minimized = TbNative.IsIconic(h),
                        Flashing = flashing.Contains(h), Icon = IconOf(h, refreshIcons)
                    });
                }
                bar.ShowLabels = ShowLabels;
                bar.Center = Center;
                string d = TbSide.Vertical(bar.Side) ? now.ToString("dd.MM") : date;
                bar.SetContent(list, fg, clock, d);
                bar.SetFullscreen(IsFullscreen(fg, bar.Monitor.Bounds) && TbNative.MonitorFromWindow(fg, TbNative.MONITOR_DEFAULTTONEAREST) == bar.MonitorHandle);
            }
        }

        // ---------------------------------------------------------------- simgeler
        Bitmap IconOf(IntPtr h, bool refresh)
        {
            Bitmap bmp;
            if (icons.TryGetValue(h, out bmp) && !refresh) return bmp;
            IntPtr hi = WindowIcon(h);
            IntPtr old;
            if (bmp != null && iconHandles.TryGetValue(h, out old) && old == hi) return bmp;
            Bitmap made = null;
            if (hi != IntPtr.Zero)
            {
                try { using (var ic = Icon.FromHandle(hi)) made = ic.ToBitmap(); } catch { made = null; }
            }
            if (made == null)
            {
                var exe = ExeIcon(h);
                if (exe != null) made = new Bitmap(exe);
            }
            if (bmp != null) bmp.Dispose();
            if (made == null) { icons.Remove(h); iconHandles.Remove(h); return null; }
            icons[h] = made;
            iconHandles[h] = hi;
            return made;
        }

        static IntPtr WindowIcon(IntPtr h)
        {
            IntPtr r;
            foreach (int type in new[] { 1, 2, 0 })   // ICON_BIG, ICON_SMALL2, ICON_SMALL
            {
                if (TbNative.SendMessageTimeout(h, TbNative.WM_GETICON, (IntPtr)type, IntPtr.Zero, 2, 40, out r) != IntPtr.Zero && r != IntPtr.Zero) return r;
            }
            r = TbNative.GetClassLong(h, TbNative.GCLP_HICON);
            if (r != IntPtr.Zero) return r;
            return TbNative.GetClassLong(h, TbNative.GCLP_HICONSM);
        }

        // Pencere kendi simgesini vermiyorsa programın exe simgesi (UWP uygulamalarında asıl uygulamanınki)
        Bitmap ExeIcon(IntPtr h)
        {
            IntPtr target = h;
            if (TbNative.ClassOf(h) == "ApplicationFrameWindow")
            {
                IntPtr core = TbNative.FindWindowEx(h, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
                if (core != IntPtr.Zero) target = core;
            }
            uint pid;
            TbNative.GetWindowThreadProcessId(target, out pid);
            string path = TbNative.ProcessPath(pid);
            if (string.IsNullOrEmpty(path)) return null;
            Bitmap b;
            if (exeIcons.TryGetValue(path, out b)) return b;
            try { using (var ic = Icon.ExtractAssociatedIcon(path)) b = ic != null ? ic.ToBitmap() : null; }
            catch { b = null; }
            exeIcons[path] = b;
            return b;
        }

        // ---------------------------------------------------------------- eylemler
        // Tıklama: etkin pencereyse küçült, değilse öne getir
        static void Toggle(IntPtr h)
        {
            if (!TbNative.IsWindow(h)) return;
            if (TbNative.GetForegroundWindow() == h && !TbNative.IsIconic(h))
            {
                TbNative.ShowWindow(h, TbNative.SW_MINIMIZE);
                return;
            }
            Activate(h);
        }

        static void Activate(IntPtr h)
        {
            if (TbNative.IsIconic(h)) TbNative.ShowWindow(h, TbNative.SW_RESTORE);
            // Tıklama bu programa geldiği için Windows öne getirmeye izin verir; vermezse yedek yol
            if (!TbNative.SetForegroundWindow(h) || TbNative.GetForegroundWindow() != h) TbNative.SwitchToThisWindow(h, true);
        }

        void WindowMenu(IntPtr h, Point at)
        {
            var menu = new ContextMenuStrip();
            bool min = TbNative.IsIconic(h);
            menu.Items.Add(min ? L.T("Geri getir", "Restore") : L.T("Öne getir", "Bring to front"), null, delegate { Activate(h); });
            if (!min) menu.Items.Add(L.T("Simge durumuna küçült", "Minimize"), null, delegate { TbNative.ShowWindow(h, TbNative.SW_MINIMIZE); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Pencereyi kapat", "Close window"), null, delegate { TbNative.PostMessage(h, TbNative.WM_SYSCOMMAND, (IntPtr)TbNative.SC_CLOSE, IntPtr.Zero); });
            menu.Closed += delegate { if (hook != null) hook.Invoke(menu.Dispose); };
            menu.Show(at);
        }

        void BarMenu(Point at)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("Görev Yöneticisi", "Task Manager"), null, delegate { Run("taskmgr.exe"); });
            menu.Items.Add(L.T("Masaüstünü göster", "Show desktop"), null, delegate
            {
                TbNative.keybd_event(0x5B, 0, 0, UIntPtr.Zero); TbNative.keybd_event(0x44, 0, 0, UIntPtr.Zero);
                TbNative.keybd_event(0x44, 0, 2, UIntPtr.Zero); TbNative.keybd_event(0x5B, 0, 2, UIntPtr.Zero);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Görev çubuğu ayarları (Broken Screen Resizer)", "Taskbar settings (Broken Screen Resizer)"), null, delegate
            {
                var h = OpenSettings;
                if (h != null) h();
            });
            menu.Closed += delegate { if (hook != null) hook.Invoke(menu.Dispose); };
            menu.Show(at);
        }

        static void Run(string exe)
        {
            try { System.Diagnostics.Process.Start(exe); } catch { }
        }
    }

    // Pencere açıldı/kapandı/etkinleşti/yanıp sönüyor bildirimlerini alan gizli pencere
    class ShellHook : NativeWindow, IDisposable
    {
        readonly uint msg;
        public event Action<int, IntPtr> Event;

        public ShellHook()
        {
            CreateHandle(new CreateParams());
            msg = TbNative.RegisterWindowMessage("SHELLHOOK");
            TbNative.RegisterShellHookWindow(Handle);
        }

        public bool IsHandleCreatedSafe { get { return Handle != IntPtr.Zero; } }

        readonly Queue<Action> pending = new Queue<Action>();
        const int WM_APP_RUN = 0x8000 + 77;

        // Başka iş parçacığından (ör. ekran ayarı olayı) arayüz iş parçacığına iş gönderir
        public void Invoke(Action a)
        {
            lock (pending) pending.Enqueue(a);
            TbNative.PostMessage(Handle, WM_APP_RUN, IntPtr.Zero, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)msg)
            {
                var h = Event;
                if (h != null) h((int)(m.WParam.ToInt64() & 0xFFFF), m.LParam);
                return;
            }
            if (m.Msg == WM_APP_RUN)
            {
                while (true)
                {
                    Action a;
                    lock (pending) { if (pending.Count == 0) break; a = pending.Dequeue(); }
                    try { a(); } catch { }
                }
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                TbNative.DeregisterShellHookWindow(Handle);
                DestroyHandle();
            }
        }
    }
}
