using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Ekran düzeni (v1.7): monitörlerin masaüstündeki yerini Windows Ekran ayarlarına gitmeden ayarlar.
    //  - Monitörler sürüklenir; bırakıldığı yerde en yakın monitörün kenarına yapışır, üst/alt/orta hizalanır.
    //  - Monitörler üst üste binmez ve her zaman en az bir kenarla birbirine değer (fare geçebilsin).
    //  - Sanal monitör / Ekran Çoğaltıcı açıkken aynanın çizildiği fiziksel monitör gösterilmez;
    //    uygulanınca o her zaman sanal ekranların altına, ulaşılamayan yere alınır.
    // ===================================================================================
    class ArrangeMon
    {
        public string Gdi = "", Name = "";
        public Rectangle Rect;      // masaüstü koordinatları (fiziksel piksel)
        public bool Primary, Virtual;
        public int Group;           // aynı gruptakiler birlikte taşınır (Ekran Çoğaltıcı'nın ekranları)
        public int Number;
    }

    static class DisplayArrange
    {
        // Şu anki düzen; aynanın gizlediği fiziksel monitör hariç
        public static List<ArrangeMon> Load(VirtualMirror mirror)
        {
            var list = new List<ArrangeMon>();
            var names = new Dictionary<string, string>();
            try { foreach (var mi in DisplayConfig.GetMonitors()) if (!string.IsNullOrEmpty(mi.GdiName)) names[mi.GdiName] = mi.Name; }
            catch { }
            string physName = null;
            if (mirror != null) names.TryGetValue(mirror.PhysGdi, out physName);
            string[] vg = mirror != null ? mirror.VirtGdis : new string[0];

            int group = 1;
            foreach (var e in DisplayLayout.Capture())
            {
                if (!e.Attached || e.W <= 0 || e.H <= 0) continue;
                if (mirror != null && e.Gdi == mirror.PhysGdi) continue;
                var m = new ArrangeMon { Gdi = e.Gdi, Rect = e.Rect, Primary = e.Primary };
                int vi = Array.IndexOf(vg, e.Gdi);
                if (vi >= 0)
                {
                    m.Virtual = true;
                    m.Group = 0;
                    string baseName = string.IsNullOrEmpty(physName) ? L.T("Bozuk monitör", "Broken monitor") : physName;
                    m.Name = mirror.IsSplit
                        ? baseName + " · " + L.T("Ekran", "Screen") + " " + (vi + 1)
                        : baseName + " · " + L.T("sağlam alan", "working area");
                }
                else
                {
                    m.Group = group++;
                    string n;
                    if (names.TryGetValue(e.Gdi, out n) && !string.IsNullOrEmpty(n)) m.Name = n;
                    else if (Vdd.IsOurs(e.Gdi)) m.Name = L.T("Sanal ekran", "Virtual screen");
                    else m.Name = e.Gdi.Replace(@"\\.\", "");
                }
                list.Add(m);
            }
            Number(list);
            return list;
        }

        // Windows'taki gibi soldan sağa, yukarıdan aşağıya numara
        public static void Number(List<ArrangeMon> list)
        {
            var order = new List<ArrangeMon>(list);
            order.Sort((a, b) => a.Rect.X != b.Rect.X ? a.Rect.X.CompareTo(b.Rect.X) : a.Rect.Y.CompareTo(b.Rect.Y));
            for (int i = 0; i < order.Count; i++) order[i].Number = i + 1;
        }

        public static Rectangle GroupRect(List<ArrangeMon> list, int group)
        {
            Rectangle r = Rectangle.Empty;
            foreach (var m in list)
                if (m.Group == group) r = r.IsEmpty ? m.Rect : Rectangle.Union(r, m.Rect);
            return r;
        }

        // ---------------------------------------------------------------- Yapışma
        // Taşınan grubun (b: istenen yer) en yakın geçerli konumu: başka bir monitörün kenarına değer,
        // hiçbir monitörle üst üste binmez; yakınsa üst/alt/orta hizasına oturur.
        public static Point Snap(Rectangle b, List<Rectangle> others, int threshold)
        {
            if (others.Count == 0) return b.Location;
            Point best = b.Location;
            long bestD = long.MaxValue;
            foreach (var o in others)
            {
                int minV = Math.Max(1, Math.Min(64, Math.Min(b.Height, o.Height) / 4));
                int minH = Math.Max(1, Math.Min(64, Math.Min(b.Width, o.Width) / 4));
                // solunda / sağında
                foreach (int x in new[] { o.Left - b.Width, o.Right })
                {
                    int y = Clamp(b.Y, o.Top - b.Height + minV, o.Bottom - minV);
                    y = Align(y, new[] { o.Top, o.Bottom - b.Height, o.Top + (o.Height - b.Height) / 2 }, threshold);
                    Try(new Rectangle(x, y, b.Width, b.Height), b, others, ref best, ref bestD);
                }
                // üstünde / altında
                foreach (int y in new[] { o.Top - b.Height, o.Bottom })
                {
                    int x = Clamp(b.X, o.Left - b.Width + minH, o.Right - minH);
                    x = Align(x, new[] { o.Left, o.Right - b.Width, o.Left + (o.Width - b.Width) / 2 }, threshold);
                    Try(new Rectangle(x, y, b.Width, b.Height), b, others, ref best, ref bestD);
                }
            }
            return best;
        }

        static int Clamp(int v, int lo, int hi) { return hi < lo ? lo : Math.Max(lo, Math.Min(hi, v)); }

        static int Align(int v, int[] marks, int threshold)
        {
            int best = v, bd = threshold + 1;
            foreach (int m in marks)
            {
                int d = Math.Abs(v - m);
                if (d < bd) { bd = d; best = m; }
            }
            return bd <= threshold ? best : v;
        }

        static void Try(Rectangle c, Rectangle want, List<Rectangle> others, ref Point best, ref long bestD)
        {
            foreach (var o in others)
            {
                var i = Rectangle.Intersect(c, o);
                if (i.Width > 0 && i.Height > 0) return;
            }
            long dx = c.X - want.X, dy = c.Y - want.Y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = c.Location; }
        }

        // ---------------------------------------------------------------- Uygulama
        // Fiziksel monitörü (sadece ayna) verilen alanın altına, ulaşılamayan yere koyar (VirtualMirror.Arrange gibi)
        public static void Park(LayoutEntry phys, Rectangle anchor, List<LayoutEntry> others)
        {
            int bottom = anchor.Bottom;
            foreach (var e in others)
                if (e.X < anchor.X + phys.W && e.X + e.W > anchor.X) bottom = Math.Max(bottom, e.Y + e.H);
            phys.X = anchor.X;
            phys.Y = bottom;
            phys.Primary = false;
        }

        static void Normalize(List<LayoutEntry> list)
        {
            LayoutEntry prim = null;
            foreach (var e in list) if (e.Attached && e.Primary) prim = e;
            if (prim == null || (prim.X == 0 && prim.Y == 0)) return;
            int ox = prim.X, oy = prim.Y;
            foreach (var e in list) { e.X -= ox; e.Y -= oy; }
        }

        // Yeni düzeni Windows'a uygular. Dönüş: ChangeDisplaySettingsEx kodu (0 = başarılı)
        public static int Apply(List<ArrangeMon> mons, VirtualMirror mirror)
        {
            var cap = DisplayLayout.Capture();
            var list = new List<LayoutEntry>();
            LayoutEntry phys = null;
            bool anyPrimary = false;
            foreach (var m in mons) if (m.Primary) anyPrimary = true;
            foreach (var e in cap)
            {
                if (!e.Attached) continue;
                if (mirror != null && e.Gdi == mirror.PhysGdi) { phys = e; continue; }
                foreach (var m in mons)
                    if (m.Gdi == e.Gdi)
                    {
                        e.X = m.Rect.X; e.Y = m.Rect.Y;
                        if (anyPrimary) e.Primary = m.Primary;
                    }
                list.Add(e);
            }
            if (phys != null)
            {
                var vg = mirror.VirtGdis;
                Rectangle anchor = Rectangle.Empty;
                foreach (var e in list)
                    if (Array.IndexOf(vg, e.Gdi) >= 0) anchor = anchor.IsEmpty ? e.Rect : Rectangle.Union(anchor, e.Rect);
                if (anchor.IsEmpty) anchor = new Rectangle(phys.X, phys.Y, 0, 0);
                Park(phys, anchor, list);
                list.Add(phys);
            }
            Normalize(list);
            return DisplayLayout.Apply(list);
        }

        // Önceki düzene döner (onaylanmadıysa)
        public static int Restore(List<LayoutEntry> before)
        {
            var list = new List<LayoutEntry>();
            foreach (var e in before) if (e.Attached) list.Add(e);
            return DisplayLayout.Apply(list);
        }

        // Sanal monitör / Ekran Çoğaltıcı açıkken yeni düzen, program kapanınca geri yüklenen yedeğe de işlenir.
        // Böylece program yeniden açıldığında (ve Geri al'dan sonra) monitörler kullanıcının seçtiği yerde olur.
        // Fiziksel monitör, sağlam alanı (ya da bölgeleri) sanal ekranların şimdiki yerine denk gelecek şekilde konur.
        public static void UpdateBackup(VirtualMirror mirror)
        {
            if (mirror == null || !DisplayLayout.HasBackup) return;
            var backup = DisplayLayout.LoadBackup();
            var now = DisplayLayout.Capture();
            var areas = mirror.PhysAreas;
            var vg = mirror.VirtGdis;
            var p = DisplayLayout.Find(backup, mirror.PhysGdi);
            var v0 = vg.Length > 0 ? DisplayLayout.Find(now, vg[0]) : null;
            if (p == null || !p.Attached || v0 == null || !v0.Attached || areas.Length == 0) return;

            bool groupPrimary = false;
            foreach (var g in vg) { var e = DisplayLayout.Find(now, g); if (e != null && e.Primary) groupPrimary = true; }

            // bozuk ekran modunda sanal monitör fiziksel monitörden küçüktür; komşular kesilen kadar kaydırılır
            var a = areas[0];
            var vr = v0.Rect;
            int cutL = 0, cutT = 0, cutR = 0, cutB = 0;
            if (!mirror.IsSplit)
            {
                cutL = a.X; cutT = a.Y;
                cutR = Math.Max(0, p.W - a.X - vr.Width); cutB = Math.Max(0, p.H - a.Y - vr.Height);
            }

            foreach (var b in backup)
            {
                if (!b.Attached || b.Gdi == p.Gdi) continue;
                var n = DisplayLayout.Find(now, b.Gdi);
                if (n == null || !n.Attached) continue;
                b.X = n.X; b.Y = n.Y; b.Primary = n.Primary && !groupPrimary;
                if (b.X >= vr.Right) b.X += cutR; else if (b.X + b.W <= vr.X) b.X -= cutL;
                if (b.Y >= vr.Bottom) b.Y += cutB; else if (b.Y + b.H <= vr.Y) b.Y -= cutT;
            }
            p.X = v0.X - a.X;
            p.Y = v0.Y - a.Y;
            p.Primary = groupPrimary;
            Normalize(backup);
            DisplayLayout.SaveBackup(backup);
        }
    }

    // ===================================================================================
    // Monitör çizimi: sürükle-bırak ile yerleştirme
    // ===================================================================================
    class ArrangeCanvas : Ctl
    {
        List<ArrangeMon> mons = new List<ArrangeMon>();
        public string PrimaryWord = "", Hint = "";
        public event Action Changed;
        public event Action SelectionChanged;

        int selected = -1;
        int hot = -1;
        bool dragging, moved;
        int dragGroup;
        Point dragStart;
        Rectangle groupStart;
        readonly Dictionary<ArrangeMon, Point> startPos = new Dictionary<ArrangeMon, Point>();
        List<Rectangle> ghosts = new List<Rectangle>();   // sürüklemeden önceki yerler (soluk)

        // çizim dönüşümü (sürükleme boyunca sabit kalır, çizim zıplamasın)
        float k = 1, ox, oy;
        Rectangle world;

        public bool Dragging { get { return dragging; } }

        public List<ArrangeMon> Monitors
        {
            get { return mons; }
            set
            {
                string sel = selected >= 0 && selected < mons.Count ? mons[selected].Gdi : null;
                mons = value ?? new List<ArrangeMon>();
                selected = -1;
                for (int i = 0; i < mons.Count; i++) if (mons[i].Gdi == sel) selected = i;
                if (selected < 0) for (int i = 0; i < mons.Count; i++) if (mons[i].Primary) selected = i;
                if (selected < 0 && mons.Count > 0) selected = 0;
                dragging = false;
                Fit();
                Invalidate();
            }
        }

        public ArrangeMon SelectedMonitor { get { return selected >= 0 && selected < mons.Count ? mons[selected] : null; } }

        int HintH { get { return Theme.P(28); } }

        // Tüm monitörleri, etrafında sürükleme payı bırakarak sığdırır
        void Fit()
        {
            if (mons.Count == 0) { world = new Rectangle(0, 0, 1920, 1080); }
            else
            {
                Rectangle all = mons[0].Rect;
                foreach (var m in mons) all = Rectangle.Union(all, m.Rect);
                int maxW = 0, maxH = 0;
                foreach (var m in mons) { maxW = Math.Max(maxW, m.Rect.Width); maxH = Math.Max(maxH, m.Rect.Height); }
                int padX = mons.Count > 1 ? maxW * 2 / 5 : maxW / 6, padY = mons.Count > 1 ? maxH * 2 / 5 : maxH / 6;
                world = Rectangle.Inflate(all, padX, padY);
            }
            float pad = Theme.P(14);
            float aw = Math.Max(10, Width - pad * 2), ah = Math.Max(10, Height - HintH - pad * 2);
            k = Math.Min(aw / Math.Max(1, world.Width), ah / Math.Max(1, world.Height));
            ox = (Width - world.Width * k) / 2f;
            oy = pad + (ah - world.Height * k) / 2f;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!dragging) Fit();
        }

        RectangleF ToView(Rectangle r)
        {
            float gap = Theme.P(2);
            return new RectangleF(ox + (r.X - world.X) * k + gap, oy + (r.Y - world.Y) * k + gap, r.Width * k - gap * 2, r.Height * k - gap * 2);
        }

        int Hit(Point p)
        {
            for (int i = mons.Count - 1; i >= 0; i--) if (ToView(mons[i].Rect).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            int i = Hit(e.Location);
            if (i < 0) return;
            if (i != selected)
            {
                selected = i;
                var h = SelectionChanged;
                if (h != null) h();
            }
            if (mons.Count > 1)
            {
                dragging = true;
                moved = false;
                dragGroup = mons[i].Group;
                dragStart = e.Location;
                groupStart = DisplayArrange.GroupRect(mons, dragGroup);
                startPos.Clear();
                ghosts = new List<Rectangle>();
                foreach (var m in mons) if (m.Group == dragGroup) { startPos[m] = m.Rect.Location; ghosts.Add(m.Rect); }
                Capture = true;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                int dx = (int)Math.Round((e.X - dragStart.X) / k), dy = (int)Math.Round((e.Y - dragStart.Y) / k);
                if (!moved && Math.Abs(e.X - dragStart.X) + Math.Abs(e.Y - dragStart.Y) < Theme.P(4)) return;
                moved = true;
                var want = new Rectangle(groupStart.X + dx, groupStart.Y + dy, groupStart.Width, groupStart.Height);
                var others = new List<Rectangle>();
                foreach (var m in mons) if (m.Group != dragGroup) others.Add(m.Rect);
                var at = DisplayArrange.Snap(want, others, (int)(Theme.P(12) / k));
                int ddx = at.X - groupStart.X, ddy = at.Y - groupStart.Y;
                foreach (var kv in startPos) kv.Key.Rect.Location = new Point(kv.Value.X + ddx, kv.Value.Y + ddy);
                Invalidate();
                return;
            }
            int i = Enabled ? Hit(e.Location) : -1;
            if (i != hot) { hot = i; Invalidate(); }
            Cursor = i >= 0 ? (mons.Count > 1 ? Cursors.SizeAll : Cursors.Hand) : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            ghosts = new List<Rectangle>();
            bool changed = false;
            foreach (var kv in startPos) if (kv.Key.Rect.Location != kv.Value) changed = true;
            startPos.Clear();
            if (changed)
            {
                DisplayArrange.Number(mons);
                var h = Changed;
                if (h != null) h();
            }
            Fit();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, this);
            var all = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Glass.Card(g, all, Theme.P(12), Color.Empty);

            using (var big = new Font(Font.FontFamily, Font.Size * 1.9f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
            using (var tiny = new Font(Font.FontFamily, Font.Size - 2f, FontStyle.Bold))
            {
                var clip = g.Save();
                g.SetClip(new RectangleF(Theme.P(4), Theme.P(4), Width - Theme.P(8), Height - HintH - Theme.P(4)));

                // sürüklenen grubun eski yeri
                foreach (var r in ghosts)
                {
                    var gr = ToView(r);
                    using (var pen = new Pen(Theme.Dim(Theme.Sub), 1) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, gr.X, gr.Y, gr.Width, gr.Height);
                }

                // Ekran Çoğaltıcı ekranları tek parça: çevrelerine kesik çizgi
                bool hasGroup = false;
                foreach (var m in mons) if (m.Group == 0) hasGroup = true;
                if (hasGroup)
                {
                    int count = 0;
                    foreach (var m in mons) if (m.Group == 0) count++;
                    if (count > 1)
                    {
                        var gr = ToView(DisplayArrange.GroupRect(mons, 0));
                        gr.Inflate(Theme.P(3), Theme.P(3));
                        using (var pen = new Pen(Theme.Mix(Theme.Accent, Theme.Card, 0.35f), Theme.P(1.5f)) { DashStyle = DashStyle.Dash })
                            g.DrawRectangle(pen, gr.X, gr.Y, gr.Width, gr.Height);
                    }
                }

                for (int i = 0; i < mons.Count; i++)
                {
                    var m = mons[i];
                    var r = ToView(m.Rect);
                    bool sel = i == selected;
                    bool isDrag = dragging && m.Group == dragGroup;
                    Color fill = sel ? Theme.Mix(Theme.Field, Theme.Accent, 0.12f) : Theme.Field;
                    if (m.Virtual) fill = Theme.Mix(fill, Color.FromArgb(96, 150, 255), 0.10f);
                    Theme.Fill(g, r, Theme.P(6), C(fill));
                    Theme.Stroke(g, r, Theme.P(6), C(sel || isDrag ? Theme.Accent : i == hot ? Theme.Sub : Theme.Border), sel ? Theme.P(2) : 1);

                    var inner = RectangleF.Inflate(r, -Theme.P(6), -Theme.P(6));
                    float lineH = small.Height;
                    bool roomy = inner.Height >= big.Height + lineH * 2 + Theme.P(2) && inner.Width >= Theme.P(80);
                    if (roomy)
                    {
                        float cy = inner.Y + (inner.Height - (big.Height + lineH * 2)) / 2f;
                        Theme.Draw(g, m.Number.ToString(), big, new RectangleF(inner.X, cy, inner.Width, big.Height), C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);
                        Theme.Draw(g, m.Name, small, new RectangleF(inner.X, cy + big.Height, inner.Width, lineH), C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);
                        Theme.Draw(g, m.Rect.Width + " × " + m.Rect.Height, small, new RectangleF(inner.X, cy + big.Height + lineH, inner.Width, lineH), C(Theme.Sub), StringAlignment.Center, StringAlignment.Center, false);
                    }
                    else
                        Theme.Draw(g, m.Number.ToString(), big, inner, C(Theme.Text), StringAlignment.Center, StringAlignment.Center, false);

                    if (m.Primary)
                    {
                        var ps = g.MeasureString(PrimaryWord, tiny);
                        var pr = new RectangleF(r.X + Theme.P(6), r.Y + Theme.P(6), ps.Width + Theme.P(10), ps.Height + Theme.P(2));
                        Theme.Fill(g, pr, pr.Height / 2, C(Theme.Mix(Theme.Accent, Theme.Card, 0.75f)));
                        Theme.Draw(g, PrimaryWord, tiny, pr, C(Theme.Accent), StringAlignment.Center, StringAlignment.Center, false);
                    }
                }
                g.Restore(clip);

                // alt satır: ipucu
                Theme.Draw(g, Hint, small, new RectangleF(Theme.P(16), Height - HintH, Width - Theme.P(32), HintH - Theme.P(6)),
                           C(Theme.Dim(Theme.Text)), StringAlignment.Near, StringAlignment.Center, false);
            }
            FogOver(g, Theme.P(12));
        }
    }
}
