using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // ------------------------------------------------------------------ Tema
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(22, 24, 29);
        public static readonly Color Card = Color.FromArgb(31, 34, 40);
        public static readonly Color Field = Color.FromArgb(40, 44, 52);
        public static readonly Color FieldHover = Color.FromArgb(50, 55, 64);
        public static readonly Color Border = Color.FromArgb(52, 57, 66);
        public static readonly Color Text = Color.FromArgb(234, 237, 242);
        public static readonly Color Sub = Color.FromArgb(142, 150, 163);
        public static readonly Color Accent = Color.FromArgb(46, 196, 176);
        public static readonly Color AccentHover = Color.FromArgb(70, 214, 195);
        public static readonly Color OnAccent = Color.FromArgb(8, 30, 27);
        public static readonly Color Danger = Color.FromArgb(240, 86, 91);
        public static readonly Color Warn = Color.FromArgb(245, 165, 36);
        public static readonly Color Ok = Color.FromArgb(61, 214, 140);

        public static float S = 1f;   // DPI ölçeği
        public static int P(float v) { return (int)Math.Round(v * S); }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Dim(Color c) { return Mix(c, Bg, 0.55f); }

        // Küçük asma kilit simgesi (cx: sol kenar, cy: dikey orta, size: yükseklik)
        public static void Padlock(Graphics g, float x, float cy, float size, Color c)
        {
            float bw = size * 0.85f, bh = size * 0.6f, by = cy - size / 2 + size * 0.4f;
            using (var pen = new Pen(c, Math.Max(1.2f, size * 0.13f)))
                g.DrawArc(pen, x + bw * 0.2f, cy - size / 2, bw * 0.6f, size * 0.62f, 180, 180);
            using (var pen = new Pen(c, Math.Max(1.2f, size * 0.13f)))
            {
                g.DrawLine(pen, x + bw * 0.2f, cy - size / 2 + size * 0.31f, x + bw * 0.2f, by);
                g.DrawLine(pen, x + bw * 0.8f, cy - size / 2 + size * 0.31f, x + bw * 0.8f, by);
            }
            Fill(g, new RectangleF(x, by, bw, bh), size * 0.12f, c);
        }

        // "Sisli" kilit görünümü: ayar uygulanmışken değiştirilemeyen öğelerin üstüne çekilen buğu.
        // Sadece şeklin içine çizilir (köşelerde dikdörtgen leke kalmasın).
        public static void Fog(Graphics g, RectangleF r, float rad)
        {
            using (var path = Round(r, rad))
            {
                var st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var b = new LinearGradientBrush(r, Color.FromArgb(150, 58, 64, 76), Color.FromArgb(110, 40, 44, 54), 90f))
                    g.FillRectangle(b, r);
                using (var pen = new Pen(Color.FromArgb(22, 210, 220, 235), P(1.5f)))
                    for (float d = -r.Height; d < r.Width + r.Height; d += P(9))
                        g.DrawLine(pen, r.X + d, r.Bottom, r.X + d + r.Height, r.Y);
                g.Restore(st);
            }
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static void Draw(Graphics g, string s, Font f, RectangleF r, Color c, StringAlignment h, StringAlignment v, bool wrap)
        {
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(c))
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                if (!wrap) sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(s ?? "", f, b, r, sf);
            }
        }

        public static Graphics Prep(PaintEventArgs e, Color bg)
        {
            var g = e.Graphics;
            g.Clear(bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            return g;
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        // Windows 10/11 koyu başlık çubuğu
        public static void DarkTitle(Form f)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            }
            catch { }
        }

        public static Image Logo()
        {
            try
            {
                using (var st = typeof(Theme).Assembly.GetManifestResourceStream("BrokenScreenResizer.logo.png"))
                    if (st != null) return new Bitmap(st);
            }
            catch { }
            return AppIcon(48).ToBitmap();
        }

        static Icon appIcon;
        public static Icon AppIcon(int size)
        {
            try
            {
                using (var s = typeof(Theme).Assembly.GetManifestResourceStream("BrokenScreenResizer.app.ico"))
                    if (s != null) return new Icon(s, size, size);
            }
            catch { }
            if (appIcon == null)
            {
                try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch { appIcon = SystemIcons.Application; }
            }
            return appIcon;
        }
    }

    // ------------------------------------------------------------------ Ortak taban
    // Titremeden kaydırılabilen panel (ana pencerenin duyarlı sayfası için)
    class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    class Ctl : Control
    {
        protected bool hover, pressed;

        public Ctl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected Color C(Color c) { return Enabled ? c : Theme.Dim(c); }

        // Ayar uygulanmışken kilitli (sisli) görünür
        bool fogged;
        public bool Fogged { get { return fogged; } set { if (fogged == value) return; fogged = value; Invalidate(); } }
        protected void FogOver(Graphics g, float rad)
        {
            if (fogged) Theme.Fog(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), rad);
        }
    }

    // ------------------------------------------------------------------ Düğme
    enum BtnKind { Primary, Secondary, Ghost, DangerGhost }

    class FlatButton : Ctl
    {
        public BtnKind Kind = BtnKind.Secondary;

        public FlatButton(string text, BtnKind kind) { Text = text; Kind = kind; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fg = Theme.Text;
            switch (Kind)
            {
                case BtnKind.Primary:
                    Theme.Fill(g, r, Theme.P(8), C(pressed ? Theme.Mix(Theme.Accent, Color.Black, 0.15f) : hover ? Theme.AccentHover : Theme.Accent));
                    fg = Theme.OnAccent;
                    break;
                case BtnKind.Secondary:
                    Theme.Fill(g, r, Theme.P(8), C(hover ? Theme.FieldHover : Theme.Field));
                    Theme.Stroke(g, r, Theme.P(8), C(Theme.Border), 1);
                    break;
                case BtnKind.Ghost:
                    if (hover && Enabled) Theme.Fill(g, r, Theme.P(6), Theme.Field);
                    fg = Theme.Sub;
                    break;
                case BtnKind.DangerGhost:
                    if (hover && Enabled) Theme.Fill(g, r, Theme.P(6), Theme.Mix(Theme.Danger, Theme.Bg, 0.85f));
                    fg = Theme.Danger;
                    break;
            }
            if (!Enabled) fg = Theme.Mix(fg, Kind == BtnKind.Primary ? Theme.Dim(Theme.Accent) : Theme.Bg, 0.45f);
            var f = Kind == BtnKind.Primary ? new Font(Font, FontStyle.Bold) : Font;
            Theme.Draw(g, Text, f, new RectangleF(0, 0, Width, Height), fg, StringAlignment.Center, StringAlignment.Center, false);
            if (f != Font) f.Dispose();
            FogOver(g, Theme.P(8));
        }
    }

    // ------------------------------------------------------------------ Segmentli seçici
    class Seg : Ctl
    {
        string[] items = new string[0];
        int selected = -1;
        int hot = -1;
        public event Action<int> Changed;

        public string[] Items { get { return items; } set { items = value ?? new string[0]; Invalidate(); } }
        public int Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        RectangleF Item(int i)
        {
            float w = (Width - Theme.P(6)) / (float)Math.Max(1, items.Length);
            return new RectangleF(Theme.P(3) + i * w, Theme.P(3), w, Height - Theme.P(6));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < items.Length; i++) if (Item(i).Contains(e.Location)) h = i;
            if (h != hot) { hot = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled) return;
            for (int i = 0; i < items.Length; i++)
                if (Item(i).Contains(e.Location) && i != selected)
                {
                    selected = i;
                    Invalidate();
                    var h = Changed;
                    if (h != null) h(i);
                }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.P(9), Theme.Card);
            Theme.Stroke(g, r, Theme.P(9), Theme.Border, 1);
            for (int i = 0; i < items.Length; i++)
            {
                var ir = Item(i);
                Color fg = C(Theme.Sub);
                if (i == selected)
                {
                    Theme.Fill(g, ir, Theme.P(7), C(Theme.Accent));
                    fg = C(Theme.OnAccent);
                }
                else if (i == hot && Enabled)
                {
                    Theme.Fill(g, ir, Theme.P(7), Theme.Field);
                    fg = Theme.Text;
                }
                var tr = new RectangleF(ir.X + Theme.P(6), ir.Y, ir.Width - Theme.P(12), ir.Height);
                var f = i == selected ? new Font(Font, FontStyle.Bold) : Font;
                Theme.Draw(g, items[i], f, tr, fg, StringAlignment.Center, StringAlignment.Center, false);
                if (f != Font) f.Dispose();
            }
            FogOver(g, Theme.P(9));
        }
    }

    // ------------------------------------------------------------------ Kaydırıcı
    class FlatSlider : Ctl
    {
        int min, max = 100, val;
        bool drag;
        public event Action<int> ValueChanged;

        public FlatSlider()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public int Minimum { get { return min; } set { min = value; Invalidate(); } }
        public int Maximum { get { return max; } set { max = Math.Max(min + 1, value); Invalidate(); } }
        public int Value
        {
            get { return val; }
            set { val = Math.Max(min, Math.Min(max, value)); Invalidate(); }
        }

        float TrackL { get { return Theme.P(10); } }
        float TrackR { get { return Width - Theme.P(10); } }

        void SetFromX(int x)
        {
            float t = (x - TrackL) / Math.Max(1f, TrackR - TrackL);
            int v = min + (int)Math.Round(Math.Max(0, Math.Min(1, t)) * (max - min));
            Commit(v);
        }

        void Commit(int v)
        {
            v = Math.Max(min, Math.Min(max, v));
            if (v == val) return;
            val = v;
            Invalidate();
            var h = ValueChanged;
            if (h != null) h(v);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            Focus();
            drag = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (drag) SetFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            int step = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Commit(val - step);
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Commit(val + step);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            float cy = Height / 2f, th = Theme.P(6);
            float t = (val - min) / (float)Math.Max(1, max - min);
            float x = TrackL + t * (TrackR - TrackL);
            Theme.Fill(g, new RectangleF(TrackL, cy - th / 2, TrackR - TrackL, th), th / 2, Theme.Field);
            Theme.Fill(g, new RectangleF(TrackL, cy - th / 2, Math.Max(th, x - TrackL), th), th / 2, C(Theme.Accent));
            float k = Theme.P(focusedOrHover ? 20 : 18);
            if (Focused && Enabled)
                using (var b = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
                    g.FillEllipse(b, x - k / 2 - Theme.P(5), cy - k / 2 - Theme.P(5), k + Theme.P(10), k + Theme.P(10));
            using (var b = new SolidBrush(C(Color.White))) g.FillEllipse(b, x - k / 2, cy - k / 2, k, k);
            using (var p = new Pen(C(Theme.Accent), Theme.P(3))) g.DrawEllipse(p, x - k / 2, cy - k / 2, k, k);
            if (Fogged) Theme.Fog(g, new RectangleF(1, cy - Theme.P(13), Width - 2, Theme.P(26)), Theme.P(13));
        }

        bool focusedOrHover { get { return Enabled && (hover || drag); } }
    }

    // ------------------------------------------------------------------ Açma/kapama anahtarı
    class Toggle : Ctl
    {
        bool on;
        public event Action<bool> Changed;

        public Toggle(string text) { Text = text; }

        public bool Checked
        {
            get { return on; }
            set { on = value; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled) return;
            on = !on;
            Invalidate();
            var h = Changed;
            if (h != null) h(on);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            float w = Theme.P(38), h = Theme.P(22), y = (Height - h) / 2f;
            var tr = new RectangleF(0.5f, y, w, h);
            if (on) Theme.Fill(g, tr, h / 2, C(hover ? Theme.AccentHover : Theme.Accent));
            else
            {
                Theme.Fill(g, tr, h / 2, C(hover ? Theme.FieldHover : Theme.Field));
                Theme.Stroke(g, tr, h / 2, C(Theme.Border), 1);
            }
            float k = h - Theme.P(6);
            float kx = on ? tr.Right - k - Theme.P(3) : tr.X + Theme.P(3);
            using (var b = new SolidBrush(C(on ? Color.White : Theme.Sub))) g.FillEllipse(b, kx, y + Theme.P(3), k, k);
            Theme.Draw(g, Text, Font, new RectangleF(w + Theme.P(10), 0, Width - w - Theme.P(10), Height), C(Theme.Text),
                StringAlignment.Near, StringAlignment.Center, false);
            if (Fogged) Theme.Fog(g, tr, h / 2);
        }
    }

    // ------------------------------------------------------------------ Yöntem kartı
    class ModeCard : Ctl
    {
        public string Title = "", Desc = "", Badge = "";
        public Color BadgeColor = Theme.Accent;
        bool sel;
        public event Action Picked;

        public bool Selected { get { return sel; } set { sel = value; Invalidate(); } }

        float BadgeRow { get { return Badge != "" ? Theme.P(24) : 0; } }

        // Başlık dar kartta iki satıra inebilir
        float TitleH(Graphics g, int width)
        {
            float tx = Theme.P(14) + Theme.P(18) + Theme.P(10);
            using (var bold = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold))
                return Math.Max(Theme.P(18), g.MeasureString(Title, bold, (int)(width - tx - Theme.P(14))).Height);
        }

        public int Measure(int width)
        {
            using (var g = CreateGraphics())
            {
                float pad = Theme.P(14);
                var d = g.MeasureString(Desc, Font, (int)(width - pad * 2));
                return (int)Math.Ceiling(pad + TitleH(g, width) + BadgeRow + Theme.P(10) + d.Height + Theme.P(16));
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled || sel) return;
            var h = Picked;
            if (h != null) h();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
            Theme.Fill(g, r, Theme.P(12), sel ? Theme.Mix(Theme.Card, Theme.Accent, 0.08f) : hover && Enabled ? Theme.Field : Theme.Card);
            Theme.Stroke(g, r, Theme.P(12), C(sel ? Theme.Accent : Theme.Border), sel ? Theme.P(2) : 1);

            float pad = Theme.P(14), rd = Theme.P(18);
            float rx = pad, ry = pad;
            using (var p = new Pen(C(sel ? Theme.Accent : Theme.Sub), Theme.P(2))) g.DrawEllipse(p, rx, ry, rd, rd);
            if (sel) using (var b = new SolidBrush(C(Theme.Accent))) g.FillEllipse(b, rx + Theme.P(4.5f), ry + Theme.P(4.5f), rd - Theme.P(9), rd - Theme.P(9));

            using (var bold = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold))
            {
                float tx = rx + rd + Theme.P(10);
                float th = TitleH(g, Width);
                Theme.Draw(g, Title, bold, new RectangleF(tx, ry - Theme.P(1), Width - tx - pad, th + Theme.P(2)), C(Theme.Text),
                    StringAlignment.Near, StringAlignment.Near, true);
                float top = ry + Math.Max(rd, th);
                float dy = top + Theme.P(10);
                if (Badge != "")
                {
                    using (var small = new Font(Font.FontFamily, Font.Size - 1.5f, FontStyle.Bold))
                    {
                        var bs = g.MeasureString(Badge, small);
                        var br = new RectangleF(pad, top + Theme.P(7), Math.Min(bs.Width + Theme.P(10), Width - pad * 2), bs.Height + Theme.P(2));
                        Theme.Fill(g, br, br.Height / 2, C(Theme.Mix(BadgeColor, Theme.Card, 0.75f)));
                        Theme.Draw(g, Badge, small, br, C(BadgeColor), StringAlignment.Center, StringAlignment.Center, false);
                    }
                    dy += BadgeRow;
                }
                Theme.Draw(g, Desc, Font, new RectangleF(pad, dy, Width - pad * 2, Height - dy - Theme.P(8)),
                    C(Theme.Sub), StringAlignment.Near, StringAlignment.Near, true);
            }
            if (Fogged) Theme.Fog(g, r, Theme.P(12));
        }
    }

    // ------------------------------------------------------------------ Durum kutusu
    enum StatusKind { Idle, Active, Warn }

    class StatusBox : Ctl
    {
        public StatusKind Kind;

        public StatusBox() { Cursor = Cursors.Default; }

        public int Measure(int width)
        {
            using (var g = CreateGraphics())
            {
                var s = g.MeasureString(Text, Font, (int)(width - Theme.P(52)));
                return (int)Math.Ceiling(s.Height) + Theme.P(26);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            Color dot = Kind == StatusKind.Active ? Theme.Ok : Kind == StatusKind.Warn ? Theme.Warn : Theme.Sub;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.P(10), Theme.Mix(Theme.Card, dot, 0.06f));
            Theme.Stroke(g, r, Theme.P(10), Theme.Mix(Theme.Border, dot, 0.25f), 1);
            float d = Theme.P(10), cy = Theme.P(13) + Font.Height / 2f;
            using (var b = new SolidBrush(Color.FromArgb(60, dot))) g.FillEllipse(b, Theme.P(16) - d / 2 - Theme.P(4), cy - d / 2 - Theme.P(4), d + Theme.P(8), d + Theme.P(8));
            using (var b = new SolidBrush(dot)) g.FillEllipse(b, Theme.P(16) - d / 2, cy - d / 2, d, d);
            Theme.Draw(g, Text, Font, new RectangleF(Theme.P(34), Theme.P(12), Width - Theme.P(46), Height - Theme.P(20)), Theme.Text,
                StringAlignment.Near, StringAlignment.Near, true);
        }
    }

    // ------------------------------------------------------------------ Monitör önizlemesi (sürüklenebilir)
    // Sağlam alan bir dikdörtgendir; dört kenarı ayrı ayrı sürüklenir, köşeden tutunca iki kenar birlikte,
    // ortasından tutunca dikdörtgenin tamamı taşınır.
    class MonitorPreview : Ctl
    {
        public int SignalW = 1920, SignalH = 1080;
        Cuts cuts = new Cuts(480, 0, 0, 0);
        Edge focus = Edge.Left;
        public bool Applied;
        public string BrokenLabel = "", ResultLabel = "", AppliedLabel = "", LockedLabel = "";
        public event Action<Cuts> CutsChanged;
        public event Action<Edge> FocusChanged;

        // sürükleme durumu
        bool dragL, dragT, dragR, dragB, dragMove;
        Point dragStart;
        Cuts dragFrom;
        // farenin üstünde olduğu kenarlar
        bool hotL, hotT, hotR, hotB, hotMove;

        public MonitorPreview() { Cursor = Cursors.Default; }

        public Cuts Cuts { get { return cuts; } set { cuts = value; Invalidate(); } }
        public Edge FocusEdge { get { return focus; } set { focus = value; Invalidate(); } }

        RectangleF ScreenRect()
        {
            float standH = Theme.P(30);
            float availW = Width - Theme.P(8), availH = Height - standH - Theme.P(4);
            float bezel = Theme.P(10);
            float aspect = SignalW / (float)Math.Max(1, SignalH);
            float w = availW, h = w / aspect;
            if (h > availH - bezel * 2) { h = availH - bezel * 2; w = h * aspect; }
            return new RectangleF((Width - w) / 2f, bezel + Theme.P(2), w, h);
        }

        RectangleF GoodRect(RectangleF s)
        {
            float kx = s.Width / Math.Max(1, SignalW), ky = s.Height / Math.Max(1, SignalH);
            return new RectangleF(s.X + cuts.L * kx, s.Y + cuts.T * ky,
                                  Math.Max(1, s.Width - cuts.H * kx), Math.Max(1, s.Height - cuts.V * ky));
        }

        // Fare hangi kenar çizgilerine yakın?
        void HitTest(Point p, out bool l, out bool t, out bool r, out bool b, out bool inside)
        {
            var s = ScreenRect();
            var g = GoodRect(s);
            float tol = Theme.P(9);
            bool inY = p.Y >= g.Y - tol && p.Y <= g.Bottom + tol;
            bool inX = p.X >= g.X - tol && p.X <= g.Right + tol;
            l = inY && Math.Abs(p.X - g.X) < tol;
            r = inY && Math.Abs(p.X - g.Right) < tol;
            t = inX && Math.Abs(p.Y - g.Y) < tol;
            b = inX && Math.Abs(p.Y - g.Bottom) < tol;
            // çok dar alanda iki çizgi birden yakınsa en yakını seç
            if (l && r) { if (Math.Abs(p.X - g.X) <= Math.Abs(p.X - g.Right)) r = false; else l = false; }
            if (t && b) { if (Math.Abs(p.Y - g.Y) <= Math.Abs(p.Y - g.Bottom)) b = false; else t = false; }
            inside = !(l || t || r || b) && g.Contains(p);
        }

        int SigX(float x, RectangleF s) { return (int)Math.Round((x - s.X) / s.Width * SignalW); }
        int SigY(float y, RectangleF s) { return (int)Math.Round((y - s.Y) / s.Height * SignalH); }

        void Drag(Point p)
        {
            var s = ScreenRect();
            var c = dragFrom;
            int maxW = SignalW * 2 / 3, maxH = SignalH * 2 / 3;
            if (dragMove)
            {
                int dx = (int)Math.Round((p.X - dragStart.X) / s.Width * SignalW);
                int dy = (int)Math.Round((p.Y - dragStart.Y) / s.Height * SignalH);
                int nl = Math.Max(0, Math.Min(c.H, c.L + dx)), nt = Math.Max(0, Math.Min(c.V, c.T + dy));
                c = new Cuts(nl, nt, c.H - nl, c.V - nt);
            }
            else
            {
                if (dragL) c.L = Math.Max(0, Math.Min(maxW - c.R, SigX(p.X, s)));
                if (dragR) c.R = Math.Max(0, Math.Min(maxW - c.L, SignalW - SigX(p.X, s)));
                if (dragT) c.T = Math.Max(0, Math.Min(maxH - c.B, SigY(p.Y, s)));
                if (dragB) c.B = Math.Max(0, Math.Min(maxH - c.T, SignalH - SigY(p.Y, s)));
            }
            SetCuts(c);
        }

        void SetCuts(Cuts c)
        {
            if (c == cuts) return;
            cuts = c;
            Invalidate();
            var h = CutsChanged;
            if (h != null) h(c);
        }

        void SetFocus(Edge e)
        {
            if (e == focus) return;
            focus = e;
            Invalidate();
            var h = FocusChanged;
            if (h != null) h(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || Applied || e.Button != MouseButtons.Left) return;
            var s = ScreenRect();
            if (!RectangleF.Inflate(s, Theme.P(6), Theme.P(6)).Contains(e.Location)) return;
            bool l, t, r, b, inside;
            HitTest(e.Location, out l, out t, out r, out b, out inside);
            dragStart = e.Location;
            dragFrom = cuts;
            if (l || t || r || b)
            {
                dragL = l; dragT = t; dragR = r; dragB = b;
            }
            else if (inside && cuts.Any)
            {
                dragMove = true;
            }
            else
            {
                // bozuk şeride ya da boş alana tıklandı: en yakın kenar oraya gelir
                var g = GoodRect(s);
                float dl = Math.Abs(e.X - g.X), dr = Math.Abs(e.X - g.Right), dt = Math.Abs(e.Y - g.Y), db = Math.Abs(e.Y - g.Bottom);
                float m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
                dragL = m == dl; dragR = !dragL && m == dr; dragT = !dragL && !dragR && m == dt; dragB = !dragL && !dragR && !dragT;
                Drag(e.Location);
            }
            if (dragL) SetFocus(Edge.Left);
            else if (dragR) SetFocus(Edge.Right);
            else if (dragT) SetFocus(Edge.Top);
            else if (dragB) SetFocus(Edge.Bottom);
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragL || dragT || dragR || dragB || dragMove) { Drag(e.Location); return; }
            bool l = false, t = false, r = false, b = false, inside = false;
            bool active = Enabled && !Applied;
            if (active) HitTest(e.Location, out l, out t, out r, out b, out inside);
            inside = inside && cuts.Any;
            if (l != hotL || t != hotT || r != hotR || b != hotB || inside != hotMove)
            {
                hotL = l; hotT = t; hotR = r; hotB = b; hotMove = inside;
                Invalidate();
            }
            bool onScreen = ScreenRect().Contains(e.Location);
            Cursor = !active ? Cursors.Default
                   : (l && t) || (r && b) ? Cursors.SizeNWSE
                   : (r && t) || (l && b) ? Cursors.SizeNESW
                   : l || r ? Cursors.SizeWE
                   : t || b ? Cursors.SizeNS
                   : inside ? Cursors.SizeAll
                   : onScreen ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragL = dragT = dragR = dragB = dragMove = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hotL = hotT = hotR = hotB = hotMove = false;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && (dragL || dragT || dragR || dragB || dragMove))
            {
                dragL = dragT = dragR = dragB = dragMove = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var s = ScreenRect();
            float bezel = Theme.P(10);

            // ayak
            float cx = Width / 2f, top = s.Bottom + bezel;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[] {
                    new PointF(cx - Theme.P(26), top - 1), new PointF(cx + Theme.P(26), top - 1),
                    new PointF(cx + Theme.P(34), top + Theme.P(18)), new PointF(cx - Theme.P(34), top + Theme.P(18)) });
                using (var b = new SolidBrush(Theme.Field)) g.FillPath(b, path);
            }
            Theme.Fill(g, new RectangleF(cx - Theme.P(70), top + Theme.P(16), Theme.P(140), Theme.P(8)), Theme.P(4), Theme.Field);

            // çerçeve
            var frame = RectangleF.Inflate(s, bezel, bezel);
            Theme.Fill(g, frame, Theme.P(14), Color.FromArgb(12, 13, 16));
            Theme.Stroke(g, frame, Theme.P(14), Theme.Border, 1);

            var gr = GoodRect(s);
            var state = g.Save();
            using (var clip = Theme.Round(s, Theme.P(4))) g.SetClip(clip);

            // bozuk alan (sağlam dikdörtgenin dışında kalan her yer)
            if (cuts.Any)
            {
                var st2 = g.Save();
                g.SetClip(gr, CombineMode.Exclude);
                using (var b = new SolidBrush(Color.FromArgb(48, 16, 20))) g.FillRectangle(b, s);
                using (var pen = new Pen(Color.FromArgb(110, Theme.Danger), Theme.P(3)))
                    for (float d = -s.Height; d < s.Width + s.Height; d += Theme.P(12))
                        g.DrawLine(pen, s.X + d, s.Bottom, s.X + d + s.Height, s.Y);
                g.Restore(st2);
            }

            // sağlam alan
            using (var b = new LinearGradientBrush(s, Theme.Mix(Theme.Accent, Theme.Bg, 0.55f), Theme.Mix(Theme.Accent, Theme.Bg, 0.8f), 45f))
                g.FillRectangle(b, gr);
            using (var pen = new Pen(Color.FromArgb(18, Color.White), 1))
                for (float gx = gr.X + Theme.P(24); gx < gr.Right; gx += Theme.P(24)) g.DrawLine(pen, gx, gr.Y, gx, gr.Bottom);
            g.Restore(state);

            // kenar çizgileri ve tutamaklar
            bool active = Enabled && !Applied;
            foreach (var edge in Cuts.All)
            {
                bool hot = Hot(edge);
                Color col = !active ? Theme.Sub : Color.White;
                PointF a, z;
                switch (edge)
                {
                    case Edge.Left: a = new PointF(gr.X, gr.Y); z = new PointF(gr.X, gr.Bottom); break;
                    case Edge.Right: a = new PointF(gr.Right, gr.Y); z = new PointF(gr.Right, gr.Bottom); break;
                    case Edge.Top: a = new PointF(gr.X, gr.Y); z = new PointF(gr.Right, gr.Y); break;
                    default: a = new PointF(gr.X, gr.Bottom); z = new PointF(gr.Right, gr.Bottom); break;
                }
                // kesilmemiş kenarın çizgisi ekran kenarında silik durur
                int alpha = cuts[edge] > 0 || hot ? 255 : 90;
                using (var pen = new Pen(Color.FromArgb(alpha, col), Theme.P(hot ? 3 : 2))) g.DrawLine(pen, a, z);
            }
            if (active)
                foreach (var edge in Cuts.All) DrawHandle(g, gr, edge);
            if (Fogged) Theme.Fog(g, frame, Theme.P(14));

            // etiketler
            using (var big = new Font(Font.FontFamily, Font.Size + 7f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 0.5f))
            {
                int rw = SignalW - cuts.H, rh = SignalH - cuts.V;
                string res = rw + " × " + rh;
                var bigSize = g.MeasureString(res, big);
                var smallSize = g.MeasureString(Applied ? AppliedLabel : ResultLabel, small);
                float blockH = bigSize.Height + smallSize.Height;
                // büyük yazı yan tutamaçlara değmesin diye iki yanda boşluk bırakılır
                if (gr.Width > bigSize.Width + Theme.P(44) && gr.Height > blockH + Theme.P(20))
                {
                    float by = gr.Y + (gr.Height - blockH) / 2;
                    Theme.Draw(g, res, big, new RectangleF(gr.X, by, gr.Width, bigSize.Height), Color.White, StringAlignment.Center, StringAlignment.Center, false);
                    Theme.Draw(g, Applied ? AppliedLabel : ResultLabel, small, new RectangleF(gr.X, by + bigSize.Height, gr.Width, smallSize.Height),
                        Color.FromArgb(220, 230, 240), StringAlignment.Center, StringAlignment.Center, false);
                }
                else if (gr.Width > g.MeasureString(res, small).Width + Theme.P(8) && gr.Height > smallSize.Height + Theme.P(24))
                    Theme.Draw(g, res, small, gr, Color.White, StringAlignment.Center, StringAlignment.Center, false);

                // "Bozuk" etiketi en geniş bozuk şeride
                var ls = g.MeasureString(BrokenLabel, small);
                RectangleF best = RectangleF.Empty;
                foreach (var edge in Cuts.All)
                {
                    if (cuts[edge] <= 0) continue;
                    var st = Strip(s, gr, edge);
                    if (st.Width * st.Height > best.Width * best.Height) best = st;
                }
                if (best.Width > ls.Width + Theme.P(34) && best.Height > ls.Height + Theme.P(34))
                {
                    var lr = new RectangleF(best.X + (best.Width - ls.Width - Theme.P(12)) / 2, best.Y + (best.Height - ls.Height - Theme.P(4)) / 2,
                                            ls.Width + Theme.P(12), ls.Height + Theme.P(4));
                    Theme.Fill(g, lr, lr.Height / 2, Color.FromArgb(200, 20, 8, 10));
                    Theme.Draw(g, BrokenLabel, small, lr, Theme.Danger, StringAlignment.Center, StringAlignment.Center, false);
                }

                // kilitliyken üst ortada asma kilit etiketi
                if (Fogged && LockedLabel != "")
                {
                    var ks = g.MeasureString(LockedLabel, small);
                    float pw = Math.Min(s.Width - Theme.P(12), ks.Width + Theme.P(34)), ph = ks.Height + Theme.P(8);
                    var pr = new RectangleF(s.X + (s.Width - pw) / 2, s.Y + Theme.P(8), pw, ph);
                    Theme.Fill(g, pr, ph / 2, Color.FromArgb(225, 18, 20, 25));
                    Theme.Stroke(g, pr, ph / 2, Color.FromArgb(120, Theme.Sub), 1);
                    Theme.Padlock(g, pr.X + Theme.P(10), pr.Y + ph / 2, Theme.P(11), Theme.Text);
                    Theme.Draw(g, LockedLabel, small, new RectangleF(pr.X + Theme.P(25), pr.Y, pr.Width - Theme.P(29), ph), Theme.Text,
                        StringAlignment.Near, StringAlignment.Center, false);
                }
            }
        }

        bool Hot(Edge e)
        {
            switch (e)
            {
                case Edge.Left: return hotL || dragL;
                case Edge.Right: return hotR || dragR;
                case Edge.Top: return hotT || dragT;
                default: return hotB || dragB;
            }
        }

        static RectangleF Strip(RectangleF s, RectangleF g, Edge e)
        {
            switch (e)
            {
                case Edge.Left: return new RectangleF(s.X, s.Y, g.X - s.X, s.Height);
                case Edge.Right: return new RectangleF(g.Right, s.Y, s.Right - g.Right, s.Height);
                case Edge.Top: return new RectangleF(g.X, s.Y, g.Width, g.Y - s.Y);
                default: return new RectangleF(g.X, g.Bottom, g.Width, s.Bottom - g.Bottom);
            }
        }

        // Kenarın ortasındaki tutamak; seçili kenarınki vurgulu
        void DrawHandle(Graphics g, RectangleF gr, Edge e)
        {
            bool v = Cuts.IsHorizontal(e);
            float px = e == Edge.Left ? gr.X : e == Edge.Right ? gr.Right : gr.X + gr.Width / 2;
            float py = e == Edge.Top ? gr.Y : e == Edge.Bottom ? gr.Bottom : gr.Y + gr.Height / 2;
            float len = Math.Min(Theme.P(40), (v ? gr.Height : gr.Width) * 0.6f);
            var hr = v ? new RectangleF(px - Theme.P(7), py - len / 2, Theme.P(14), len)
                       : new RectangleF(px - len / 2, py - Theme.P(7), len, Theme.P(14));
            bool sel = e == focus;
            Theme.Fill(g, hr, Theme.P(7), sel ? Theme.Accent : Color.White);
            if (sel) Theme.Stroke(g, hr, Theme.P(7), Color.White, Theme.P(2));
            using (var b = new SolidBrush(sel ? Color.White : Theme.Accent))
                for (int i = -1; i <= 1; i++)
                {
                    if (len < Theme.P(26) && i != 0) continue;
                    float ox = v ? hr.X + hr.Width / 2 : hr.X + hr.Width / 2 + i * Theme.P(8);
                    float oy = v ? hr.Y + hr.Height / 2 + i * Theme.P(8) : hr.Y + hr.Height / 2;
                    g.FillEllipse(b, ox - Theme.P(2), oy - Theme.P(2), Theme.P(4), Theme.P(4));
                }
        }
    }
}
