using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Monitörlerin masaüstündeki yerleşimi (Windows Ekran ayarlarındaki gibi) ve her birinin görev çubuğu.
    // Bir monitörün kenarına tıklayınca çubuk o kenara geçer; ortasına tıklayınca monitör seçilir.
    // ===================================================================================
    class TaskbarCanvas : Ctl
    {
        public List<TbMonitor> Monitors = new List<TbMonitor>();
        public Dictionary<string, int> Sides = new Dictionary<string, int>();
        public TaskbarPlan Plan;
        public string PrimaryWord = "", NoneWord = "", WindowsLegend = "", AppLegend = "", ClickHint = "";
        public event Action<int> SelectionChanged;
        public event Action<int, int> SideClicked;   // monitör sırası, kenar

        int selected;
        int hotMon = -1, hotSide = -1;

        public int Selected
        {
            get { return selected; }
            set { if (value == selected) return; selected = value; Invalidate(); }
        }

        int LegendH { get { return Theme.P(30); } }

        // Masaüstü koordinatlarından çizim alanına
        RectangleF[] Places()
        {
            var res = new RectangleF[Monitors.Count];
            if (Monitors.Count == 0) return res;
            Rectangle all = Monitors[0].Bounds;
            foreach (var m in Monitors) all = Rectangle.Union(all, m.Bounds);
            float pad = Theme.P(18);
            float aw = Width - pad * 2, ah = Height - LegendH - pad * 2;
            float k = Math.Min(aw / Math.Max(1, all.Width), ah / Math.Max(1, all.Height));
            float ox = (Width - all.Width * k) / 2f, oy = pad + (ah - all.Height * k) / 2f;
            float gap = Theme.P(3);
            for (int i = 0; i < Monitors.Count; i++)
            {
                var b = Monitors[i].Bounds;
                res[i] = new RectangleF(ox + (b.X - all.X) * k + gap, oy + (b.Y - all.Y) * k + gap, b.Width * k - gap * 2, b.Height * k - gap * 2);
            }
            return res;
        }

        int SideFor(TbMonitor m)
        {
            return TaskbarLayout.SideOf(Sides, m, TaskbarLayout.PrimarySide(Monitors, Sides));
        }

        // Fare hangi monitörün hangi kenar bölgesinde? (kenar bölgesi: kısa kenarın ~%28'i)
        int Hit(Point p, out int side)
        {
            side = -1;
            var rs = Places();
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (!r.Contains(p)) continue;
                float z = Math.Max(Theme.P(12), Math.Min(r.Width, r.Height) * 0.28f);
                float dl = p.X - r.Left, dr = r.Right - p.X, dt = p.Y - r.Top, db = r.Bottom - p.Y;
                float min = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
                if (min <= z)
                {
                    if (min == db) side = TbSide.Bottom;
                    else if (min == dt) side = TbSide.Top;
                    else if (min == dl) side = TbSide.Left;
                    else side = TbSide.Right;
                }
                return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int s = -1;
            int i = Enabled ? Hit(e.Location, out s) : -1;
            if (i < 0) s = -1;
            if (i != hotMon || s != hotSide) { hotMon = i; hotSide = s; Invalidate(); }
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotMon = -1; hotSide = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            int s = -1;
            int i = Hit(e.Location, out s);
            if (i < 0) return;
            if (i != selected)
            {
                selected = i;
                if (SelectionChanged != null) SelectionChanged(i);
            }
            if (s >= 0 && SideClicked != null) SideClicked(i, s);
            Invalidate();
        }

        static readonly Color AppBar = Color.FromArgb(96, 150, 255);

        // Monitör içinde kenar şeridi
        static RectangleF StripOf(RectangleF r, int side, float t)
        {
            switch (side)
            {
                case TbSide.Left: return new RectangleF(r.X, r.Y, t, r.Height);
                case TbSide.Right: return new RectangleF(r.Right - t, r.Y, t, r.Height);
                case TbSide.Top: return new RectangleF(r.X, r.Y, r.Width, t);
                default: return new RectangleF(r.X, r.Bottom - t, r.Width, t);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var all = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, all, Theme.P(12), Theme.Card);
            Theme.Stroke(g, all, Theme.P(12), Theme.Border, 1);

            var rs = Places();
            using (var big = new Font(Font.FontFamily, Font.Size * 1.9f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
            using (var tiny = new Font(Font.FontFamily, Font.Size - 2f, FontStyle.Bold))
            {
                for (int i = 0; i < rs.Length; i++)
                {
                    var m = Monitors[i];
                    var r = rs[i];
                    bool sel = i == selected;
                    Theme.Fill(g, r, Theme.P(6), C(sel ? Theme.Mix(Theme.Field, Theme.Accent, 0.10f) : Theme.Field));

                    // görev çubuğu
                    int side = SideFor(m);
                    float t = Math.Max(Theme.P(7), Math.Min(r.Width, r.Height) * 0.085f);
                    bool program = Plan != null && !Plan.Native && !m.Primary;
                    if (side != TbSide.None)
                    {
                        var sr = StripOf(r, side, t);
                        Color bc = program ? AppBar : Theme.Accent;
                        using (var path = Theme.Round(r, Theme.P(6)))
                        {
                            var st = g.Save();
                            g.SetClip(path, CombineMode.Intersect);
                            using (var b = new SolidBrush(C(Theme.Mix(bc, Theme.Bg, 0.25f)))) g.FillRectangle(b, sr);
                            // küçük simgeler: Başlat + üç uygulama
                            float s = t * 0.5f;
                            bool vert = TbSide.Vertical(side);
                            for (int k = 0; k < 4; k++)
                            {
                                float along = (vert ? sr.Y : sr.X) + t * 0.6f + k * s * 1.7f;
                                if (along + s > (vert ? sr.Bottom : sr.Right) - t * 0.4f) break;
                                var ir = vert ? new RectangleF(sr.X + (t - s) / 2, along, s, s) : new RectangleF(along, sr.Y + (t - s) / 2, s, s);
                                using (var b = new SolidBrush(C(k == 0 ? Color.FromArgb(230, 240, 255) : Theme.Mix(bc, Color.White, 0.55f)))) g.FillRectangle(b, ir);
                            }
                            g.Restore(st);
                        }
                    }

                    // fare bir kenarın üstündeyse o kenarın önizlemesi
                    if (i == hotMon && hotSide >= 0 && hotSide != side && Enabled)
                    {
                        var hr = StripOf(r, hotSide, t);
                        using (var pen = new Pen(Theme.Mix(Theme.Accent, Color.White, 0.3f), Theme.P(1.5f)) { DashStyle = DashStyle.Dash })
                            g.DrawRectangle(pen, hr.X + 1, hr.Y + 1, hr.Width - 2, hr.Height - 2);
                    }

                    Theme.Stroke(g, r, Theme.P(6), C(sel ? Theme.Accent : i == hotMon ? Theme.Sub : Theme.Border), sel ? Theme.P(2) : 1);

                    // numara, ad, çözünürlük
                    var inner = RectangleF.Inflate(r, -t - Theme.P(4), -t - Theme.P(4));
                    float lineH = small.Height;
                    bool roomy = inner.Height >= big.Height + lineH * 2 + Theme.P(4) && inner.Width >= Theme.P(70);
                    if (roomy)
                    {
                        float cy = inner.Y + (inner.Height - (big.Height + lineH * 2)) / 2f;
                        Theme.Draw(g, m.Number.ToString(), big, new RectangleF(inner.X, cy, inner.Width, big.Height), C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);
                        Theme.Draw(g, m.Name, small, new RectangleF(inner.X, cy + big.Height, inner.Width, lineH), C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);
                        string info = m.Bounds.Width + " × " + m.Bounds.Height + "  ·  " + (side == TbSide.None ? NoneWord : TbSide.Name(side));
                        Theme.Draw(g, info, small, new RectangleF(inner.X, cy + big.Height + lineH, inner.Width, lineH), C(Theme.Sub), StringAlignment.Center, StringAlignment.Center, false);
                    }
                    else
                        Theme.Draw(g, m.Number.ToString(), big, inner, C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);

                    if (m.Primary)
                    {
                        var ps = g.MeasureString(PrimaryWord, tiny);
                        float px = r.X + Theme.P(6) + (side == TbSide.Left ? t : 0), py = r.Y + Theme.P(6) + (side == TbSide.Top ? t : 0);
                        var pr = new RectangleF(px, py, ps.Width + Theme.P(10), ps.Height + Theme.P(2));
                        Theme.Fill(g, pr, pr.Height / 2, C(Theme.Mix(Theme.Accent, Theme.Card, 0.75f)));
                        Theme.Draw(g, PrimaryWord, tiny, pr, C(Theme.Accent), StringAlignment.Center, StringAlignment.Center, false);
                    }
                }

                // açıklama: renkler ve ipucu
                float ly = Height - LegendH + Theme.P(4), lx = Theme.P(16), sq = Theme.P(10);
                foreach (var it in new[] { new KeyValuePair<Color, string>(Theme.Accent, WindowsLegend), new KeyValuePair<Color, string>(AppBar, AppLegend) })
                {
                    using (var b = new SolidBrush(C(Theme.Mix(it.Key, Theme.Bg, 0.25f)))) g.FillRectangle(b, lx, ly + (Theme.P(18) - sq) / 2, sq, sq);
                    var ts = g.MeasureString(it.Value, small);
                    Theme.Draw(g, it.Value, small, new RectangleF(lx + sq + Theme.P(6), ly, ts.Width + 2, Theme.P(18)), C(Theme.Sub), StringAlignment.Near, StringAlignment.Center, false);
                    lx += sq + Theme.P(6) + ts.Width + Theme.P(16);
                }
                float hx = lx + Theme.P(8);
                if (Width - hx - Theme.P(16) > Theme.P(120))
                    Theme.Draw(g, ClickHint, small, new RectangleF(hx, ly, Width - hx - Theme.P(16), Theme.P(18)), C(Theme.Dim(Theme.Text)), StringAlignment.Far, StringAlignment.Center, false);
            }
            FogOver(g, Theme.P(12));
        }
    }

    // Başlık ve madde işaretli açıklamalar içeren kart
    class InfoCard : Ctl
    {
        public string Title = "";
        public string[] Lines = new string[0];

        public InfoCard() { Cursor = Cursors.Default; }

        float Pad { get { return Theme.P(16); } }

        public int Measure(int width)
        {
            using (var g = CreateGraphics())
            using (var bold = new Font(Font, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 0.5f))
            {
                float y = Pad + bold.Height + Theme.P(8);
                float tw = width - Pad * 2 - Theme.P(14);
                foreach (var l in Lines) y += g.MeasureString(l, small, (int)tw).Height + Theme.P(8);
                return (int)Math.Ceiling(y + Pad - Theme.P(4));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.P(12), Theme.Card);
            Theme.Stroke(g, r, Theme.P(12), Theme.Border, 1);
            using (var bold = new Font(Font, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 0.5f))
            {
                Theme.Draw(g, Title, bold, new RectangleF(Pad, Pad, Width - Pad * 2, bold.Height + 2), Theme.Text, StringAlignment.Near, StringAlignment.Near, false);
                float y = Pad + bold.Height + Theme.P(8);
                float tw = Width - Pad * 2 - Theme.P(14);
                foreach (var l in Lines)
                {
                    float h = g.MeasureString(l, small, (int)tw).Height;
                    using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, Pad, y + small.Height / 2f - Theme.P(3), Theme.P(6), Theme.P(6));
                    Theme.Draw(g, l, small, new RectangleF(Pad + Theme.P(14), y, tw, h + 2), Theme.Sub, StringAlignment.Near, StringAlignment.Near, true);
                    y += h + Theme.P(8);
                }
            }
        }
    }
}
