using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EkranKurtar
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
                using (var st = typeof(Theme).Assembly.GetManifestResourceStream("EkranKurtar.logo.png"))
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
                using (var s = typeof(Theme).Assembly.GetManifestResourceStream("EkranKurtar.app.ico"))
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
        }
    }

    // ------------------------------------------------------------------ Yöntem kartı
    class ModeCard : Ctl
    {
        public string Title = "", Desc = "", Badge = "";
        bool sel;
        public event Action Picked;

        public bool Selected { get { return sel; } set { sel = value; Invalidate(); } }

        public int Measure(int width)
        {
            using (var g = CreateGraphics())
            {
                float pad = Theme.P(14);
                var d = g.MeasureString(Desc, Font, (int)(width - pad * 2));
                return (int)Math.Ceiling(pad + Theme.P(18) + Theme.P(10) + d.Height + Theme.P(16));
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
                var titleSize = g.MeasureString(Title, bold);
                Theme.Draw(g, Title, bold, new RectangleF(tx, ry - Theme.P(2), Width - tx - pad, titleSize.Height + 2), C(Theme.Text),
                    StringAlignment.Near, StringAlignment.Near, false);
                if (Badge != "")
                {
                    using (var small = new Font(Font.FontFamily, Font.Size - 1.5f, FontStyle.Bold))
                    {
                        var bs = g.MeasureString(Badge, small);
                        float bx = Math.Min(tx + titleSize.Width + Theme.P(6), Width - pad - bs.Width - Theme.P(10));
                        var br = new RectangleF(bx, ry, bs.Width + Theme.P(10), bs.Height + Theme.P(2));
                        Theme.Fill(g, br, br.Height / 2, C(Theme.Mix(Theme.Accent, Theme.Card, 0.75f)));
                        Theme.Draw(g, Badge, small, br, C(Theme.Accent), StringAlignment.Center, StringAlignment.Center, false);
                    }
                }
                Theme.Draw(g, Desc, Font, new RectangleF(pad, ry + rd + Theme.P(10), Width - pad * 2, Height - ry - rd - Theme.P(14)),
                    C(Theme.Sub), StringAlignment.Near, StringAlignment.Near, true);
            }
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
    class MonitorPreview : Ctl
    {
        public int SignalW = 1920, SignalH = 1080;
        public Edge Edge = Edge.Left;
        int cut = 480;
        public bool Applied;
        public string BrokenLabel = "", ResultLabel = "", AppliedLabel = "";
        bool drag, nearLine;
        public event Action<int> CutChanged;

        public MonitorPreview() { Cursor = Cursors.Default; }

        public int Cut { get { return cut; } set { cut = value; Invalidate(); } }
        bool Horizontal { get { return Edge == Edge.Left || Edge == Edge.Right; } }

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

        float CutPx(RectangleF s)
        {
            return Horizontal ? cut * s.Width / SignalW : cut * s.Height / SignalH;
        }

        RectangleF CutRect(RectangleF s)
        {
            float c = CutPx(s);
            switch (Edge)
            {
                case Edge.Left: return new RectangleF(s.X, s.Y, c, s.Height);
                case Edge.Right: return new RectangleF(s.Right - c, s.Y, c, s.Height);
                case Edge.Top: return new RectangleF(s.X, s.Y, s.Width, c);
                default: return new RectangleF(s.X, s.Bottom - c, s.Width, c);
            }
        }

        RectangleF GoodRect(RectangleF s)
        {
            float c = CutPx(s);
            switch (Edge)
            {
                case Edge.Left: return new RectangleF(s.X + c, s.Y, s.Width - c, s.Height);
                case Edge.Right: return new RectangleF(s.X, s.Y, s.Width - c, s.Height);
                case Edge.Top: return new RectangleF(s.X, s.Y + c, s.Width, s.Height - c);
                default: return new RectangleF(s.X, s.Y, s.Width, s.Height - c);
            }
        }

        float LinePos(RectangleF s)
        {
            float c = CutPx(s);
            switch (Edge)
            {
                case Edge.Left: return s.X + c;
                case Edge.Right: return s.Right - c;
                case Edge.Top: return s.Y + c;
                default: return s.Bottom - c;
            }
        }

        void SetFromMouse(Point p)
        {
            var s = ScreenRect();
            float v;
            switch (Edge)
            {
                case Edge.Left: v = (p.X - s.X) / s.Width * SignalW; break;
                case Edge.Right: v = (s.Right - p.X) / s.Width * SignalW; break;
                case Edge.Top: v = (p.Y - s.Y) / s.Height * SignalH; break;
                default: v = (s.Bottom - p.Y) / s.Height * SignalH; break;
            }
            int max = (Horizontal ? SignalW : SignalH) * 2 / 3;
            int nv = Math.Max(0, Math.Min(max, (int)Math.Round(v)));
            if (nv == cut) return;
            cut = nv;
            Invalidate();
            var h = CutChanged;
            if (h != null) h(nv);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || Applied) return;
            if (ScreenRect().Contains(e.Location)) { drag = true; SetFromMouse(e.Location); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag) { SetFromMouse(e.Location); return; }
            var s = ScreenRect();
            float lp = LinePos(s);
            bool near = Enabled && !Applied && s.Contains(e.Location) &&
                Math.Abs((Horizontal ? e.X : e.Y) - lp) < Theme.P(10);
            if (near != nearLine) { nearLine = near; Invalidate(); }
            Cursor = !Enabled || Applied ? Cursors.Default
                   : near ? (Horizontal ? Cursors.SizeWE : Cursors.SizeNS)
                   : s.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; }

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

            var cr = CutRect(s);
            var gr = GoodRect(s);
            var state = g.Save();
            using (var clip = Theme.Round(s, Theme.P(4))) g.SetClip(clip);

            // sağlam alan
            using (var b = new LinearGradientBrush(s, Theme.Mix(Theme.Accent, Theme.Bg, 0.55f), Theme.Mix(Theme.Accent, Theme.Bg, 0.8f), 45f))
                g.FillRectangle(b, gr);
            using (var pen = new Pen(Color.FromArgb(18, Color.White), 1))
                for (float gx = gr.X + Theme.P(24); gx < gr.Right; gx += Theme.P(24)) g.DrawLine(pen, gx, gr.Y, gx, gr.Bottom);

            // bozuk alan
            using (var b = new SolidBrush(Color.FromArgb(48, 16, 20))) g.FillRectangle(b, cr);
            if (cr.Width > 0 && cr.Height > 0)
            {
                var st2 = g.Save();
                g.SetClip(cr, CombineMode.Intersect);
                using (var pen = new Pen(Color.FromArgb(110, Theme.Danger), Theme.P(3)))
                    for (float d = -s.Height; d < s.Width + s.Height; d += Theme.P(12))
                        g.DrawLine(pen, s.X + d, s.Bottom, s.X + d + s.Height, s.Y);
                g.Restore(st2);
            }
            g.Restore(state);

            // ayırıcı çizgi ve tutamak
            float lp = LinePos(s);
            bool active = Enabled && !Applied;
            using (var pen = new Pen(active ? Color.White : Theme.Sub, Theme.P(nearLine || drag ? 3 : 2)))
            {
                if (Horizontal) g.DrawLine(pen, lp, s.Y, lp, s.Bottom);
                else g.DrawLine(pen, s.X, lp, s.Right, lp);
            }
            if (active)
            {
                var hr = Horizontal
                    ? new RectangleF(lp - Theme.P(7), s.Y + s.Height / 2 - Theme.P(20), Theme.P(14), Theme.P(40))
                    : new RectangleF(s.X + s.Width / 2 - Theme.P(20), lp - Theme.P(7), Theme.P(40), Theme.P(14));
                Theme.Fill(g, hr, Theme.P(7), Color.White);
                using (var b = new SolidBrush(Theme.Accent))
                    for (int i = -1; i <= 1; i++)
                    {
                        float ox = Horizontal ? hr.X + hr.Width / 2 : hr.X + hr.Width / 2 + i * Theme.P(8);
                        float oy = Horizontal ? hr.Y + hr.Height / 2 + i * Theme.P(8) : hr.Y + hr.Height / 2;
                        g.FillEllipse(b, ox - Theme.P(2), oy - Theme.P(2), Theme.P(4), Theme.P(4));
                    }
            }

            // etiketler
            using (var big = new Font(Font.FontFamily, Font.Size + 7f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 0.5f))
            {
                int rw = Horizontal ? SignalW - cut : SignalW;
                int rh = Horizontal ? SignalH : SignalH - cut;
                string res = rw + " × " + rh;
                var bigSize = g.MeasureString(res, big);
                var smallSize = g.MeasureString(Applied ? AppliedLabel : ResultLabel, small);
                float blockH = bigSize.Height + smallSize.Height;
                if (gr.Width > bigSize.Width && gr.Height > blockH)
                {
                    float by = gr.Y + (gr.Height - blockH) / 2;
                    Theme.Draw(g, res, big, new RectangleF(gr.X, by, gr.Width, bigSize.Height), Color.White, StringAlignment.Center, StringAlignment.Center, false);
                    Theme.Draw(g, Applied ? AppliedLabel : ResultLabel, small, new RectangleF(gr.X, by + bigSize.Height, gr.Width, smallSize.Height),
                        Color.FromArgb(220, 230, 240), StringAlignment.Center, StringAlignment.Center, false);
                }
                var ls = g.MeasureString(BrokenLabel, small);
                if (cr.Width > ls.Width + Theme.P(34) && cr.Height > ls.Height + Theme.P(34))
                {
                    float shift = Edge == Edge.Left ? -Theme.P(8) : Edge == Edge.Right ? Theme.P(8) : 0;
                    var lr = new RectangleF(cr.X + shift + (cr.Width - ls.Width - Theme.P(12)) / 2, cr.Y + (Edge == Edge.Top ? -Theme.P(8) : Edge == Edge.Bottom ? Theme.P(8) : 0) + (cr.Height - ls.Height - Theme.P(4)) / 2,
                                            ls.Width + Theme.P(12), ls.Height + Theme.P(4));
                    Theme.Fill(g, lr, lr.Height / 2, Color.FromArgb(200, 20, 8, 10));
                    Theme.Draw(g, BrokenLabel, small, lr, Theme.Danger, StringAlignment.Center, StringAlignment.Center, false);
                }
            }
        }
    }
}
