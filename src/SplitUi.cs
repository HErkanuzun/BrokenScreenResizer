using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Ekran Çoğaltıcı arayüzü (v1.5)
    // ===================================================================================

    // ------------------------------------------------------------------ Büyük monitör çizimi: sanal ekranları yerleştir
    // Her ekran dört kenarından ve köşelerinden boyutlandırılır, ortasından tutunca taşınır.
    // Bir ekran komşusuna değince durur ve titrer; aynı yöne bir daha sürüklenirse komşusunu küçültür.
    class SplitCanvas : Ctl
    {
        static readonly Color[] Palette =
        {
            Color.FromArgb(46, 196, 176), Color.FromArgb(88, 146, 255), Color.FromArgb(168, 122, 255), Color.FromArgb(245, 165, 36),
            Color.FromArgb(240, 98, 146), Color.FromArgb(110, 200, 92), Color.FromArgb(56, 189, 248), Color.FromArgb(255, 128, 80)
        };
        public static Color ColorOf(int i) { return Palette[((i % Palette.Length) + Palette.Length) % Palette.Length]; }

        public int SignalW = 3840, SignalH = 2160;
        public double Ppi;
        public string LockedLabel = "", EmptyLabel = "", PushHint = "", MinHint = "";
        public event Action RegionsChanged, SelectionChanged, DragFinished;

        List<Rectangle> regions = new List<Rectangle>();
        int selected = -1, hotIdx = -1, hotPart;

        const int PL = 1, PT = 2, PR = 4, PB = 8, PMove = 16;

        // sürükleme
        int dragIdx = -1, dragPart;
        Point dragStart;
        Rectangle dragFrom;
        List<Rectangle> baseRegions;
        bool blockedThisDrag;

        // komşuya dayanınca: aynı ekran aynı yöne tekrar sürüklenirse komşuyu iter
        int armIdx = -1, armSides;
        int pushSides;   // bu sürüklemede itmeye izinli kenarlar (önceki sürüklemede dayanılanlar)
        DateTime armTime;

        // titreme ve uyarı etiketi
        readonly Timer anim = new Timer();
        DateTime shakeStart = DateTime.MinValue, bannerUntil = DateTime.MinValue;
        int shakeIdx = -1, shakeOther = -1;
        bool shakeX;
        string banner = "";
        const double ShakeMs = 420;

        public SplitCanvas()
        {
            Cursor = Cursors.Default;
            anim.Interval = 15;
            anim.Tick += delegate
            {
                var now = DateTime.Now;
                if ((now - shakeStart).TotalMilliseconds > ShakeMs) { shakeIdx = -1; shakeOther = -1; }
                if (shakeIdx < 0 && now > bannerUntil) anim.Stop();
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }

        public List<Rectangle> Regions
        {
            get { return new List<Rectangle>(regions); }
            set
            {
                regions = new List<Rectangle>(value ?? new List<Rectangle>());
                if (selected >= regions.Count) selected = regions.Count - 1;
                armIdx = -1;
                Invalidate();
            }
        }

        public int Selected
        {
            get { return selected; }
            set
            {
                int v = value < 0 || value >= regions.Count ? -1 : value;
                if (v == selected) return;
                selected = v;
                Invalidate();
                Fire(SelectionChanged);
            }
        }

        public bool Dragging { get { return dragIdx >= 0; } }

        Size Mon { get { return new Size(SignalW, SignalH); } }

        static void Fire(Action a) { if (a != null) a(); }

        // ---------------------------------------------------------------- geometri
        float Bezel { get { return Theme.P(12); } }

        RectangleF ScreenRect()
        {
            float standH = Theme.P(30), bezel = Bezel;
            float availW = Width - Theme.P(8) - bezel * 2, availH = Height - standH - bezel * 2 - Theme.P(4);
            float aspect = SignalW / (float)Math.Max(1, SignalH);
            float w = Math.Max(10, availW), h = w / aspect;
            if (h > availH) { h = Math.Max(10, availH); w = h * aspect; }
            return new RectangleF((Width - w) / 2f, bezel + Theme.P(2), w, h);
        }

        RectangleF View(Rectangle r, RectangleF s)
        {
            float kx = s.Width / Math.Max(1, SignalW), ky = s.Height / Math.Max(1, SignalH);
            return new RectangleF(s.X + r.X * kx, s.Y + r.Y * ky, r.Width * kx, r.Height * ky);
        }

        int HitTest(Point p, out int part)
        {
            part = 0;
            var s = ScreenRect();
            float tol = Theme.P(8);
            var order = new List<int>();
            if (selected >= 0) order.Add(selected);
            for (int i = regions.Count - 1; i >= 0; i--) if (i != selected) order.Add(i);
            foreach (int i in order)
            {
                var v = View(regions[i], s);
                if (p.X < v.Left - tol || p.X > v.Right + tol || p.Y < v.Top - tol || p.Y > v.Bottom + tol) continue;
                bool inY = p.Y >= v.Top - tol && p.Y <= v.Bottom + tol, inX = p.X >= v.Left - tol && p.X <= v.Right + tol;
                int m = 0;
                if (inY && Math.Abs(p.X - v.Left) < tol) m |= PL;
                if (inY && Math.Abs(p.X - v.Right) < tol) m |= PR;
                if (inX && Math.Abs(p.Y - v.Top) < tol) m |= PT;
                if (inX && Math.Abs(p.Y - v.Bottom) < tol) m |= PB;
                if ((m & PL) != 0 && (m & PR) != 0) m &= Math.Abs(p.X - v.Left) <= Math.Abs(p.X - v.Right) ? ~PR : ~PL;
                if ((m & PT) != 0 && (m & PB) != 0) m &= Math.Abs(p.Y - v.Top) <= Math.Abs(p.Y - v.Bottom) ? ~PB : ~PT;
                if (m == 0)
                {
                    if (!v.Contains(p)) continue;
                    m = PMove;
                }
                part = m;
                return i;
            }
            return -1;
        }

        static Cursor CursorFor(int part)
        {
            if (part == PMove) return Cursors.SizeAll;
            if (part == (PL | PT) || part == (PR | PB)) return Cursors.SizeNWSE;
            if (part == (PR | PT) || part == (PL | PB)) return Cursors.SizeNESW;
            if ((part & (PL | PR)) != 0) return Cursors.SizeWE;
            if ((part & (PT | PB)) != 0) return Cursors.SizeNS;
            return Cursors.Default;
        }

        // ---------------------------------------------------------------- fare
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || Fogged || e.Button != MouseButtons.Left) return;
            int part;
            int i = HitTest(e.Location, out part);
            if (i < 0) { Selected = -1; return; }
            Selected = i;
            dragIdx = i;
            dragPart = part;
            dragStart = e.Location;
            dragFrom = regions[i];
            baseRegions = new List<Rectangle>(regions);
            blockedThisDrag = false;
            if (armIdx != i || (DateTime.Now - armTime).TotalSeconds > 10) { armIdx = -1; armSides = 0; }
            pushSides = armIdx == i ? armSides : 0;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragIdx >= 0) { Drag(e.Location); return; }
            int part = 0;
            int i = Enabled && !Fogged ? HitTest(e.Location, out part) : -1;
            if (i != hotIdx || part != hotPart) { hotIdx = i; hotPart = part; Invalidate(); }
            Cursor = i < 0 ? Cursors.Default : CursorFor(part);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndDrag();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) EndDrag();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hotIdx >= 0) { hotIdx = -1; Invalidate(); }
        }

        void EndDrag()
        {
            if (dragIdx < 0) return;
            dragIdx = -1;
            Capture = false;
            Invalidate();
            Fire(DragFinished);
        }

        // İtme, ancak dayanmadan sonraki sürüklemede başlar; aynı sürüklemede devam etmek komşuyu küçültmez
        bool Armed(int side) { return (pushSides & side) != 0; }

        void Drag(Point p)
        {
            var s = ScreenRect();
            double kx = SignalW / (double)Math.Max(1, s.Width), ky = SignalH / (double)Math.Max(1, s.Height);
            int dx = (int)Math.Round((p.X - dragStart.X) * kx), dy = (int)Math.Round((p.Y - dragStart.Y) * ky);
            int tolX = (int)Math.Ceiling(Theme.P(8) * kx), tolY = (int)Math.Ceiling(Theme.P(8) * ky);
            var work = new List<Rectangle>(baseRegions);
            var f = dragFrom;
            int L, T, R, B;
            int hit = -1, hitSide = 0, h;
            bool hitPush = false;

            if (dragPart == PMove)
            {
                int w = f.Width, ht = f.Height;
                int nl = SnapMove(Clamp(f.Left + dx, 0, SignalW - w), w, true, tolX);
                if (nl > f.Left)
                {
                    int nr = ClampHi(dragIdx, true, f.Right, nl + w, f.Top, f.Bottom, work, Armed(PR), out h);
                    if (h >= 0) { hit = h; hitSide = PR; hitPush = Armed(PR); }
                    nl = nr - w;
                }
                else if (nl < f.Left)
                {
                    nl = ClampLo(dragIdx, true, f.Left, nl, f.Top, f.Bottom, work, Armed(PL), out h);
                    if (h >= 0) { hit = h; hitSide = PL; hitPush = Armed(PL); }
                }
                int nt = SnapMove(Clamp(f.Top + dy, 0, SignalH - ht), ht, false, tolY);
                if (nt > f.Top)
                {
                    int nb = ClampHi(dragIdx, false, f.Bottom, nt + ht, nl, nl + w, work, Armed(PB), out h);
                    if (h >= 0 && hit < 0) { hit = h; hitSide = PB; hitPush = Armed(PB); }
                    nt = nb - ht;
                }
                else if (nt < f.Top)
                {
                    nt = ClampLo(dragIdx, false, f.Top, nt, nl, nl + w, work, Armed(PT), out h);
                    if (h >= 0 && hit < 0) { hit = h; hitSide = PT; hitPush = Armed(PT); }
                }
                L = nl; T = nt; R = nl + w; B = nt + ht;
            }
            else
            {
                int minW = SplitLayout.MinW, minH = SplitLayout.MinH;
                int nL = f.Left, nR = f.Right, nT = f.Top, nB = f.Bottom;
                if ((dragPart & PL) != 0) nL = Clamp(SnapEdge(f.Left + dx, true, tolX), 0, f.Right - minW);
                if ((dragPart & PR) != 0) nR = Clamp(SnapEdge(f.Right + dx, true, tolX), f.Left + minW, SignalW);
                if ((dragPart & PT) != 0) nT = Clamp(SnapEdge(f.Top + dy, false, tolY), 0, f.Bottom - minH);
                if ((dragPart & PB) != 0) nB = Clamp(SnapEdge(f.Bottom + dy, false, tolY), f.Top + minH, SignalH);

                int pTop = Math.Min(f.Top, nT), pBot = Math.Max(f.Bottom, nB);
                if (nR > f.Right)
                {
                    nR = ClampHi(dragIdx, true, f.Right, nR, pTop, pBot, work, Armed(PR), out h);
                    if (h >= 0) { hit = h; hitSide = PR; hitPush = Armed(PR); }
                }
                if (nL < f.Left)
                {
                    nL = ClampLo(dragIdx, true, f.Left, nL, pTop, pBot, work, Armed(PL), out h);
                    if (h >= 0) { hit = h; hitSide = PL; hitPush = Armed(PL); }
                }
                int pLeft = Math.Min(f.Left, nL), pRight = Math.Max(f.Right, nR);
                if (nB > f.Bottom)
                {
                    nB = ClampHi(dragIdx, false, f.Bottom, nB, pLeft, pRight, work, Armed(PB), out h);
                    if (h >= 0 && hit < 0) { hit = h; hitSide = PB; hitPush = Armed(PB); }
                }
                if (nT < f.Top)
                {
                    nT = ClampLo(dragIdx, false, f.Top, nT, pLeft, pRight, work, Armed(PT), out h);
                    if (h >= 0 && hit < 0) { hit = h; hitSide = PT; hitPush = Armed(PT); }
                }
                L = nL; T = nT; R = nR; B = nB;
            }

            var nrect = Rectangle.FromLTRB(L, T, R, B);
            for (int j = 0; j < work.Count; j++)
                if (j != dragIdx && work[j].IntersectsWith(nrect)) return;   // köşeden çapraz çakışma: son geçerli konumda kal
            work[dragIdx] = nrect;
            bool changed = !SameList(work, regions);
            regions = work;
            if (hit >= 0) Block(hit, hitSide, hitPush);
            Invalidate();
            if (changed) Fire(RegionsChanged);
        }

        static bool SameList(List<Rectangle> a, List<Rectangle> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // Komşuya dayandı: dur ve titre. İlk seferde bir sonraki sürüklemede itmeye hazırlan.
        void Block(int other, int side, bool pushing)
        {
            if (blockedThisDrag) return;
            blockedThisDrag = true;
            if (!pushing)
            {
                armIdx = dragIdx;
                armSides |= side;
                armTime = DateTime.Now;
                banner = PushHint;
            }
            else banner = MinHint;
            shakeIdx = dragIdx;
            shakeOther = other;
            shakeX = side == PL || side == PR;
            shakeStart = DateTime.Now;
            bannerUntil = DateTime.Now.AddSeconds(3.5);
            anim.Start();
        }

        static int Clamp(int v, int lo, int hi) { return hi < lo ? lo : Math.Max(lo, Math.Min(hi, v)); }

        static int Lo(Rectangle r, bool x) { return x ? r.Left : r.Top; }
        static int Hi(Rectangle r, bool x) { return x ? r.Right : r.Bottom; }
        static int PLo(Rectangle r, bool x) { return x ? r.Top : r.Left; }
        static int PHi(Rectangle r, bool x) { return x ? r.Bottom : r.Right; }
        static Rectangle With(Rectangle r, bool x, int lo, int hi)
        {
            return x ? Rectangle.FromLTRB(lo, r.Top, hi, r.Bottom) : Rectangle.FromLTRB(r.Left, lo, r.Right, hi);
        }

        // "hi" kenarını (sağ/alt) ileri taşırken önündeki komşulara göre sınırlar.
        // push: komşular en küçük boyutlarına kadar küçültülür. hit: durduran komşu (yoksa -1).
        static int ClampHi(int idx, bool x, int fromHi, int newHi, int pLo, int pHi, List<Rectangle> work, bool push, out int hit)
        {
            hit = -1;
            if (newHi <= fromHi) return newHi;
            int min = x ? SplitLayout.MinW : SplitLayout.MinH;
            int limit = newHi;
            for (int j = 0; j < work.Count; j++)
            {
                if (j == idx) continue;
                var b = work[j];
                if (PLo(b, x) >= pHi || PHi(b, x) <= pLo || Lo(b, x) < fromHi) continue;
                int stop = push ? Math.Max(Lo(b, x), Hi(b, x) - min) : Lo(b, x);
                if (stop < limit) { limit = stop; hit = j; }
            }
            if (push)
                for (int j = 0; j < work.Count; j++)
                {
                    if (j == idx) continue;
                    var b = work[j];
                    if (PLo(b, x) >= pHi || PHi(b, x) <= pLo || Lo(b, x) < fromHi) continue;
                    if (Lo(b, x) < limit) work[j] = With(b, x, limit, Hi(b, x));
                }
            return limit;
        }

        // "lo" kenarını (sol/üst) geri taşırken arkasındaki komşulara göre sınırlar
        static int ClampLo(int idx, bool x, int fromLo, int newLo, int pLo, int pHi, List<Rectangle> work, bool push, out int hit)
        {
            hit = -1;
            if (newLo >= fromLo) return newLo;
            int min = x ? SplitLayout.MinW : SplitLayout.MinH;
            int limit = newLo;
            for (int j = 0; j < work.Count; j++)
            {
                if (j == idx) continue;
                var b = work[j];
                if (PLo(b, x) >= pHi || PHi(b, x) <= pLo || Hi(b, x) > fromLo) continue;
                int stop = push ? Math.Min(Hi(b, x), Lo(b, x) + min) : Hi(b, x);
                if (stop > limit) { limit = stop; hit = j; }
            }
            if (push)
                for (int j = 0; j < work.Count; j++)
                {
                    if (j == idx) continue;
                    var b = work[j];
                    if (PLo(b, x) >= pHi || PHi(b, x) <= pLo || Hi(b, x) > fromLo) continue;
                    if (Hi(b, x) > limit) work[j] = With(b, x, Lo(b, x), limit);
                }
            return limit;
        }

        // Kenarı yakındaki bir kenara (monitör ya da diğer ekranlar) yapıştırır; yoksa 8 piksellik ızgaraya oturtur
        int SnapEdge(int v, bool x, int tol)
        {
            int max = x ? SignalW : SignalH;
            int best = v, bestD = int.MaxValue;
            foreach (int c in Candidates(x))
            {
                int d = Math.Abs(c - v);
                if (d < bestD) { bestD = d; best = c; }
            }
            return bestD <= tol ? best : SplitLayout.SnapX(v, max);
        }

        // Taşırken iki kenardan hangisi bir kenara yakınsa ona yapıştırır
        int SnapMove(int lo, int size, bool x, int tol)
        {
            int max = x ? SignalW : SignalH;
            int best = lo, bestD = int.MaxValue;
            foreach (int c in Candidates(x))
            {
                int d1 = Math.Abs(c - lo), d2 = Math.Abs(c - (lo + size));
                if (d1 < bestD) { bestD = d1; best = c; }
                if (d2 < bestD) { bestD = d2; best = c - size; }
            }
            int v = bestD <= tol ? best : (int)Math.Round(lo / (double)SplitLayout.Grid) * SplitLayout.Grid;
            return Clamp(v, 0, max - size);
        }

        IEnumerable<int> Candidates(bool x)
        {
            yield return 0;
            yield return x ? SignalW : SignalH;
            for (int j = 0; j < baseRegions.Count; j++)
            {
                if (j == dragIdx) continue;
                yield return Lo(baseRegions[j], x);
                yield return Hi(baseRegions[j], x);
            }
        }

        // ---------------------------------------------------------------- ekle / kaldır
        // Boş alan varsa en büyük boş dikdörtgene, yoksa en büyük ekranı ikiye bölerek yeni ekran ekler
        public bool AddRegion()
        {
            if (regions.Count >= SplitLayout.MaxScreens) return false;
            var free = SplitLayout.LargestFree(regions, Mon);
            Rectangle nr;
            if (!free.IsEmpty) nr = free;
            else
            {
                int bi = -1;
                long ba = 0;
                for (int i = 0; i < regions.Count; i++)
                {
                    long a = (long)regions[i].Width * regions[i].Height;
                    if (a > ba) { ba = a; bi = i; }
                }
                if (bi < 0) return false;
                var b = regions[bi];
                bool canV = b.Width >= SplitLayout.MinW * 2, canH = b.Height >= SplitLayout.MinH * 2;
                bool vertical = canV && (!canH || b.Width * SignalH >= b.Height * SignalW);
                if (vertical)
                {
                    int mid = SplitLayout.SnapX(b.Left + b.Width / 2, SignalW);
                    regions[bi] = Rectangle.FromLTRB(b.Left, b.Top, mid, b.Bottom);
                    nr = Rectangle.FromLTRB(mid, b.Top, b.Right, b.Bottom);
                }
                else if (canH)
                {
                    int mid = SplitLayout.SnapX(b.Top + b.Height / 2, SignalH);
                    regions[bi] = Rectangle.FromLTRB(b.Left, b.Top, b.Right, mid);
                    nr = Rectangle.FromLTRB(b.Left, mid, b.Right, b.Bottom);
                }
                else return false;
            }
            regions.Add(nr);
            armIdx = -1;
            selected = regions.Count - 1;
            Invalidate();
            Fire(RegionsChanged);
            Fire(SelectionChanged);
            return true;
        }

        public bool CanAdd
        {
            get
            {
                if (regions.Count >= SplitLayout.MaxScreens) return false;
                if (!SplitLayout.LargestFree(regions, Mon).IsEmpty) return true;
                foreach (var r in regions) if (r.Width >= SplitLayout.MinW * 2 || r.Height >= SplitLayout.MinH * 2) return true;
                return false;
            }
        }

        public void RemoveSelected()
        {
            int i = selected >= 0 ? selected : regions.Count - 1;
            if (i < 0) return;
            regions.RemoveAt(i);
            armIdx = -1;
            selected = regions.Count == 0 ? -1 : Math.Min(i, regions.Count - 1);
            Invalidate();
            Fire(RegionsChanged);
            Fire(SelectionChanged);
        }

        // ---------------------------------------------------------------- çizim
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var s = ScreenRect();
            float bezel = Bezel;

            // ayak
            float cx = Width / 2f, top = s.Bottom + bezel;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[] {
                    new PointF(cx - Theme.P(30), top - 1), new PointF(cx + Theme.P(30), top - 1),
                    new PointF(cx + Theme.P(40), top + Theme.P(20)), new PointF(cx - Theme.P(40), top + Theme.P(20)) });
                using (var b = new SolidBrush(Theme.Field)) g.FillPath(b, path);
            }
            Theme.Fill(g, new RectangleF(cx - Theme.P(90), top + Theme.P(18), Theme.P(180), Theme.P(8)), Theme.P(4), Theme.Field);

            var frame = RectangleF.Inflate(s, bezel, bezel);
            Theme.Fill(g, frame, Theme.P(16), Color.FromArgb(12, 13, 16));
            Theme.Stroke(g, frame, Theme.P(16), Theme.Border, 1);

            // boş ekran: siyah, noktalı
            using (var b = new SolidBrush(Color.FromArgb(7, 8, 10))) g.FillRectangle(b, s);
            using (var b = new SolidBrush(Color.FromArgb(30, 33, 40)))
                for (float y = s.Y + Theme.P(10); y < s.Bottom; y += Theme.P(18))
                    for (float x = s.X + Theme.P(10); x < s.Right; x += Theme.P(18))
                        g.FillRectangle(b, x, y, Theme.P(1.5f), Theme.P(1.5f));

            using (var big = new Font(Font.FontFamily, Font.Size + 2.5f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 1f))
            using (var num = new Font(Font.FontFamily, Font.Size - 0.5f, FontStyle.Bold))
            {
                if (regions.Count == 0 && EmptyLabel != "")
                    Theme.Draw(g, EmptyLabel, Font, RectangleF.Inflate(s, -Theme.P(24), -Theme.P(24)), Theme.Sub,
                        StringAlignment.Center, StringAlignment.Center, true);

                var st = g.Save();
                g.SetClip(s);
                for (int i = 0; i < regions.Count; i++) if (i != selected) DrawRegion(g, s, i, big, small, num);
                if (selected >= 0 && selected < regions.Count) DrawRegion(g, s, selected, big, small, num);
                g.Restore(st);

                // uyarı etiketi
                if (DateTime.Now < bannerUntil && banner != "" && !Fogged)
                {
                    var bs = g.MeasureString(banner, small);
                    float pw = Math.Min(s.Width - Theme.P(16), bs.Width + Theme.P(24)), ph = bs.Height + Theme.P(10);
                    var pr = new RectangleF(s.X + (s.Width - pw) / 2, s.Bottom - ph - Theme.P(10), pw, ph);
                    Theme.Fill(g, pr, ph / 2, Color.FromArgb(235, 26, 22, 14));
                    Theme.Stroke(g, pr, ph / 2, Theme.Warn, 1);
                    Theme.Draw(g, banner, small, pr, Theme.Warn, StringAlignment.Center, StringAlignment.Center, false);
                }

                if (Fogged)
                {
                    Theme.Fog(g, frame, Theme.P(16));
                    if (LockedLabel != "")
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
        }

        void DrawRegion(Graphics g, RectangleF s, int i, Font big, Font small, Font num)
        {
            var reg = regions[i];
            var r = RectangleF.Inflate(View(reg, s), -Theme.P(1.5f), -Theme.P(1.5f));
            if (i == shakeIdx)
            {
                double t = Math.Min(1, (DateTime.Now - shakeStart).TotalMilliseconds / ShakeMs);
                float off = (float)(Math.Sin(t * Math.PI * 6) * Theme.P(5) * (1 - t));
                r.Offset(shakeX ? off : 0, shakeX ? 0 : off);
            }
            if (r.Width < 2 || r.Height < 2) return;
            Color c = ColorOf(i);
            bool sel = i == selected, hot = i == hotIdx && dragIdx < 0;
            float rad = Theme.P(6);

            using (var b = new LinearGradientBrush(r, Theme.Mix(c, Theme.Bg, sel ? 0.42f : hot ? 0.5f : 0.6f),
                                                      Theme.Mix(c, Theme.Bg, sel ? 0.66f : 0.78f), 45f))
            using (var path = Theme.Round(r, rad))
                g.FillPath(b, path);
            bool warn = i == shakeOther && shakeIdx >= 0;
            Theme.Stroke(g, r, rad, warn ? Theme.Warn : sel ? Color.White : Theme.Mix(c, Theme.Bg, 0.15f), warn || sel ? Theme.P(2) : 1);

            // sıra numarası
            float d = Theme.P(20);
            if (r.Width > d + Theme.P(12) && r.Height > d + Theme.P(12))
            {
                var nr = new RectangleF(r.X + Theme.P(7), r.Y + Theme.P(7), d, d);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, nr);
                Theme.Draw(g, (i + 1).ToString(), num, nr, Theme.OnAccent, StringAlignment.Center, StringAlignment.Center, false);
            }

            // ortada: çözünürlük, kalite, ekranın yüzdesi ve inç
            string res = reg.Width + " × " + reg.Height;
            int q = SplitAdvisor.Quality(reg.Size, Ppi);
            string qn = SplitAdvisor.QualityName(q);
            double pct = 100.0 * reg.Width * reg.Height / Math.Max(1.0, (double)SignalW * SignalH);
            string info = "%" + Math.Round(pct) + (Ppi > 0 ? "  ·  " + MonitorSize.Inch(SplitAdvisor.Inches(reg.Size, Ppi)) : "");
            var rs = g.MeasureString(res, big);
            var qs = g.MeasureString(qn, small);
            var ins = g.MeasureString(info, small);
            float lineGap = Theme.P(4);
            float blockH = rs.Height + qs.Height + Theme.P(4) + lineGap + ins.Height;
            if (r.Width > rs.Width + Theme.P(16) && r.Height > blockH + Theme.P(16))
            {
                float y = r.Y + (r.Height - blockH) / 2;
                Theme.Draw(g, res, big, new RectangleF(r.X, y, r.Width, rs.Height), Color.White, StringAlignment.Center, StringAlignment.Center, false);
                y += rs.Height;
                var qc = SplitAdvisor.QualityColor(q);
                var pill = new RectangleF(r.X + (r.Width - qs.Width - Theme.P(14)) / 2, y, qs.Width + Theme.P(14), qs.Height + Theme.P(4));
                Theme.Fill(g, pill, pill.Height / 2, Color.FromArgb(215, 12, 14, 18));
                Theme.Draw(g, qn, small, pill, qc, StringAlignment.Center, StringAlignment.Center, false);
                y += pill.Height + lineGap;
                Theme.Draw(g, info, small, new RectangleF(r.X, y, r.Width, ins.Height), Color.FromArgb(215, 225, 235),
                    StringAlignment.Center, StringAlignment.Center, false);
            }
            else
            {
                var ss = g.MeasureString(res, small);
                if (r.Width > ss.Width + Theme.P(8) && r.Height > ss.Height + Theme.P(8))
                    Theme.Draw(g, res, small, r, Color.White, StringAlignment.Center, StringAlignment.Center, false);
            }

            // seçili ekranın tutamakları
            if (sel && !Fogged && Enabled)
            {
                float hl = Math.Min(Theme.P(34), Math.Min(r.Width, r.Height) * 0.4f), hw = Theme.P(10);
                DrawGrip(g, new RectangleF(r.X - hw / 2, r.Y + r.Height / 2 - hl / 2, hw, hl));
                DrawGrip(g, new RectangleF(r.Right - hw / 2, r.Y + r.Height / 2 - hl / 2, hw, hl));
                DrawGrip(g, new RectangleF(r.X + r.Width / 2 - hl / 2, r.Y - hw / 2, hl, hw));
                DrawGrip(g, new RectangleF(r.X + r.Width / 2 - hl / 2, r.Bottom - hw / 2, hl, hw));
                float k = Theme.P(10);
                foreach (var pt in new[] { new PointF(r.X, r.Y), new PointF(r.Right, r.Y), new PointF(r.X, r.Bottom), new PointF(r.Right, r.Bottom) })
                    DrawGrip(g, new RectangleF(pt.X - k / 2, pt.Y - k / 2, k, k));
            }
        }

        static void DrawGrip(Graphics g, RectangleF r)
        {
            Theme.Fill(g, r, Math.Min(r.Width, r.Height) / 2, Color.White);
            Theme.Stroke(g, r, Math.Min(r.Width, r.Height) / 2, Color.FromArgb(120, 0, 0, 0), 1);
        }
    }

    // ------------------------------------------------------------------ Dağılım: her ekranın monitördeki payı
    class SplitStats : Ctl
    {
        public List<Rectangle> Regions = new List<Rectangle>();
        public Size Monitor = new Size(1920, 1080);
        public double Ppi;
        public int Selected = -1;
        public string FreeLabel = "", ScreenWord = "";
        public event Action<int> Picked;

        int BarH { get { return Theme.P(14); } }
        int RowH { get { return Theme.P(26); } }

        public SplitStats() { Cursor = Cursors.Default; }

        long FreeArea
        {
            get
            {
                long used = 0;
                foreach (var r in Regions) used += (long)r.Width * r.Height;
                return Math.Max(0, (long)Monitor.Width * Monitor.Height - used);
            }
        }

        int Rows { get { return Regions.Count + (FreeArea > 0 ? 1 : 0); } }

        public int Measure(int width) { return BarH + Theme.P(10) + Math.Max(1, Rows) * RowH; }

        int RowAt(Point p)
        {
            int y0 = BarH + Theme.P(10);
            if (p.Y < y0) return -1;
            int i = (p.Y - y0) / RowH;
            return i < Regions.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = RowAt(e.Location) >= 0 && Enabled ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = RowAt(e.Location);
            if (i >= 0 && Enabled && Picked != null) Picked(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            double total = Math.Max(1.0, (double)Monitor.Width * Monitor.Height);

            // çubuk
            var bar = new RectangleF(0.5f, 0.5f, Width - 1.5f, BarH);
            Theme.Fill(g, bar, BarH / 2f, Theme.Field);
            var st = g.Save();
            using (var clip = Theme.Round(bar, BarH / 2f)) g.SetClip(clip);
            float x = bar.X;
            for (int i = 0; i < Regions.Count; i++)
            {
                float w = (float)(bar.Width * Regions[i].Width * (double)Regions[i].Height / total);
                using (var b = new SolidBrush(i == Selected ? SplitCanvas.ColorOf(i) : Theme.Mix(SplitCanvas.ColorOf(i), Theme.Bg, 0.2f)))
                    g.FillRectangle(b, x, bar.Y, w, bar.Height);
                using (var p = new Pen(Theme.Bg, Theme.P(1.5f))) g.DrawLine(p, x + w, bar.Y, x + w, bar.Bottom);
                x += w;
            }
            g.Restore(st);

            // satırlar
            using (var small = new Font(Font.FontFamily, Font.Size - 1f))
            using (var bold = new Font(Font, FontStyle.Bold))
            {
                bool wide = Width >= Theme.P(380);
                float y = BarH + Theme.P(10);
                for (int i = 0; i < Rows; i++, y += RowH)
                {
                    bool free = i >= Regions.Count;
                    var row = new RectangleF(0, y, Width, RowH);
                    if (!free && i == Selected) Theme.Fill(g, RectangleF.Inflate(row, 0, -Theme.P(1)), Theme.P(6), Theme.Card);
                    Color c = free ? Theme.Field : SplitCanvas.ColorOf(i);
                    float d = Theme.P(10);
                    using (var b = new SolidBrush(C(c))) g.FillEllipse(b, Theme.P(8), y + (RowH - d) / 2, d, d);

                    double pct = free ? 100.0 * FreeArea / total : 100.0 * Regions[i].Width * (double)Regions[i].Height / total;
                    string name = free ? FreeLabel : ScreenWord + " " + (i + 1) + "   " + Regions[i].Width + " × " + Regions[i].Height;
                    float right = Width - Theme.P(6);
                    if (!free)
                    {
                        int q = SplitAdvisor.Quality(Regions[i].Size, Ppi);
                        string qn = SplitAdvisor.QualityName(q);
                        var qs = g.MeasureString(qn, small);
                        var pill = new RectangleF(right - qs.Width - Theme.P(14), y + (RowH - qs.Height - Theme.P(4)) / 2, qs.Width + Theme.P(14), qs.Height + Theme.P(4));
                        Theme.Fill(g, pill, pill.Height / 2, C(Theme.Mix(SplitAdvisor.QualityColor(q), Theme.Card, 0.8f)));
                        Theme.Draw(g, qn, small, pill, C(SplitAdvisor.QualityColor(q)), StringAlignment.Center, StringAlignment.Center, false);
                        right = pill.X - Theme.P(10);
                        if (wide && Ppi > 0)
                        {
                            string inch = MonitorSize.Inch(SplitAdvisor.Inches(Regions[i].Size, Ppi));
                            Theme.Draw(g, inch, small, new RectangleF(right - Theme.P(52), y, Theme.P(52), RowH), C(Theme.Sub),
                                StringAlignment.Far, StringAlignment.Center, false);
                            right -= Theme.P(60);
                        }
                    }
                    Theme.Draw(g, "%" + Math.Round(pct), small, new RectangleF(right - Theme.P(44), y, Theme.P(44), RowH), C(Theme.Sub),
                        StringAlignment.Far, StringAlignment.Center, false);
                    right -= Theme.P(50);
                    Theme.Draw(g, name, free ? small : Font, new RectangleF(Theme.P(26), y, Math.Max(10, right - Theme.P(26)), RowH),
                        C(free ? Theme.Sub : Theme.Text), StringAlignment.Near, StringAlignment.Center, false);
                }
            }
            FogOver(g, Theme.P(6));
        }
    }

    // ------------------------------------------------------------------ Şablonlar
    class TemplatePicker : Ctl
    {
        public Size Monitor = new Size(1920, 1080);
        public double Ppi;
        public SplitAdvisor.Pick[] Picks;          // Kalite kaybı olmadan / Önerilen / En fazla
        public string[] TierNames = { "", "", "" };
        public string CurrentId = "";
        public string NoFitLabel = "";
        public event Action<SplitTemplate> Picked;
        int hot = -1;

        public TemplatePicker() { Cursor = Cursors.Hand; }

        int Gap { get { return Theme.P(10); } }
        int CardH { get { return Theme.P(118); } }

        int Cols(int w)
        {
            return Math.Max(2, Math.Min(5, (w + Gap) / (Theme.P(150) + Gap)));
        }

        public int Measure(int width)
        {
            int cols = Cols(width), rows = (SplitTemplate.All.Length + cols - 1) / cols;
            return rows * CardH + (rows - 1) * Gap + 2;
        }

        Rectangle Card(int i)
        {
            int cols = Cols(Width);
            int cw = (Width - (cols - 1) * Gap) / cols;
            return new Rectangle((i % cols) * (cw + Gap), (i / cols) * (CardH + Gap), cw, CardH);
        }

        int At(Point p)
        {
            for (int i = 0; i < SplitTemplate.All.Length; i++)
                if (Card(i).Contains(p) && SplitTemplate.All[i].Fits(Monitor)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Enabled && !Fogged ? At(e.Location) : -1;
            if (h != hot) { hot = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled || Fogged) return;
            int i = At(e.Location);
            if (i >= 0 && Picked != null) Picked(SplitTemplate.All[i]);
        }

        // Bu şablon önerilerden birinin en iyi şablonu mu? (en önemli öneri öne çıkar: önerilen > kayıpsız > en fazla)
        int Tier(SplitTemplate t)
        {
            if (Picks == null) return -1;
            foreach (int k in new[] { 1, 0, 2 })
                if (Picks[k] != null && Picks[k].Template != null && Picks[k].Template.Id == t.Id) return k;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            using (var bold = new Font(Font.FontFamily, Font.Size, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
            using (var tiny = new Font(Font.FontFamily, Font.Size - 2f, FontStyle.Bold))
            {
                for (int i = 0; i < SplitTemplate.All.Length; i++)
                {
                    var t = SplitTemplate.All[i];
                    var rc = Card(i);
                    var r = new RectangleF(rc.X + 0.5f, rc.Y + 0.5f, rc.Width - 1.5f, rc.Height - 1.5f);
                    int tier = Tier(t);
                    bool cur = t.Id == CurrentId;
                    Theme.Fill(g, r, Theme.P(10), i == hot ? Theme.Field : cur ? Theme.Mix(Theme.Card, Theme.Accent, 0.08f) : Theme.Card);
                    Theme.Stroke(g, r, Theme.P(10), C(cur ? Theme.Accent : tier == 1 ? Theme.Mix(Theme.Accent, Theme.Border, 0.4f) : Theme.Border), cur ? Theme.P(2) : 1);

                    // küçük çizim
                    float pad = Theme.P(10);
                    float th = Theme.P(46), tw = th * Monitor.Width / Math.Max(1f, Monitor.Height);
                    if (tw > r.Width - pad * 2) { tw = r.Width - pad * 2; th = tw * Monitor.Height / Math.Max(1f, Monitor.Width); }
                    var thumb = new RectangleF(r.X + pad, r.Y + pad, tw, th);
                    using (var b = new SolidBrush(Color.FromArgb(7, 8, 10))) g.FillRectangle(b, thumb);
                    for (int k = 0; k < t.Cells.Length; k++)
                    {
                        var c = t.Cells[k];
                        var cr = new RectangleF(thumb.X + c.X * thumb.Width + 1, thumb.Y + c.Y * thumb.Height + 1, c.Width * thumb.Width - 2, c.Height * thumb.Height - 2);
                        using (var b = new SolidBrush(C(Theme.Mix(SplitCanvas.ColorOf(k), Theme.Bg, 0.35f)))) g.FillRectangle(b, cr);
                    }

                    // öneri etiketi
                    if (tier >= 0)
                    {
                        string tn = TierNames[tier];
                        var ts = g.MeasureString(tn, tiny);
                        float tx = thumb.Right + Theme.P(6);
                        var tr = new RectangleF(Math.Max(tx, r.Right - pad - ts.Width - Theme.P(10)), r.Y + pad, ts.Width + Theme.P(10), ts.Height + Theme.P(2));
                        if (tr.X >= tx)
                        {
                            Color tc = tier == 1 ? Theme.Accent : tier == 0 ? Theme.Ok : Theme.Warn;
                            Theme.Fill(g, tr, tr.Height / 2, C(Theme.Mix(tc, Theme.Card, 0.78f)));
                            Theme.Draw(g, tn, tiny, tr, C(tc), StringAlignment.Center, StringAlignment.Center, false);
                        }
                    }

                    // ad, en küçük bölge ve kalite
                    var cells = t.Apply(Monitor);
                    Size smallest = cells[0].Size;
                    foreach (var c in cells) if ((long)c.Width * c.Height < (long)smallest.Width * smallest.Height) smallest = c.Size;
                    bool fits = t.Fits(Monitor);
                    int q = SplitAdvisor.TemplateQuality(t, Monitor, Ppi);
                    float y = thumb.Bottom + Theme.P(8);
                    Theme.Draw(g, t.Name, bold, new RectangleF(r.X + pad, y, r.Width - pad * 2, bold.Height + 2), C(Theme.Text),
                        StringAlignment.Near, StringAlignment.Near, false);
                    y += bold.Height + Theme.P(3);
                    string qn = fits ? SplitAdvisor.QualityName(q) : NoFitLabel;
                    var qs = g.MeasureString(qn, small);
                    var pill = new RectangleF(r.Right - pad - qs.Width - Theme.P(10), y, qs.Width + Theme.P(10), qs.Height + Theme.P(2));
                    Theme.Fill(g, pill, pill.Height / 2, C(Theme.Mix(SplitAdvisor.QualityColor(q), Theme.Card, 0.8f)));
                    Theme.Draw(g, qn, small, pill, C(SplitAdvisor.QualityColor(q)), StringAlignment.Center, StringAlignment.Center, false);
                    string px = (AllSame(cells) ? "" : "≥ ") + smallest.Width + "×" + smallest.Height;
                    Theme.Draw(g, px, small, new RectangleF(r.X + pad, y, pill.X - r.X - pad - Theme.P(4), pill.Height), C(Theme.Sub),
                        StringAlignment.Near, StringAlignment.Center, false);
                    if (!fits)   // bu monitöre sığmayan şablon soluk ve tıklanamaz
                        using (var b = new SolidBrush(Color.FromArgb(150, BackColor))) g.FillRectangle(b, r);
                }
            }
            FogOver(g, Theme.P(10));
        }

        static bool AllSame(List<Rectangle> cells)
        {
            foreach (var c in cells) if (c.Size != cells[0].Size) return false;
            return true;
        }
    }

    // ------------------------------------------------------------------ Öneriler: monitör bilgisi ve piksel yoğunluğuna göre üç öneri
    class SplitAdvice : Ctl
    {
        public string Title = "", MonitorName = "", SourceText = "", SizeWord = "", AutoWord = "", Footer = "", ScreensWord = "";
        public string[] TierNames = { "", "", "" };
        public string[] TierDescs = { "", "", "" };
        public Size Monitor = new Size(1920, 1080);
        public double Diag, Ppi;
        public bool Manual;
        public SplitAdvisor.Pick[] Picks;
        public int Current = -1;   // şu an seçili ekran sayısı (eşleşen öneri vurgulanır)
        public event Action<int> PickClicked;
        public event Action<double> DiagChanged;   // 0 = otomatik

        RectangleF rMinus, rPlus, rAuto;
        readonly RectangleF[] rCards = new RectangleF[3];
        int hot = -1;   // 0..2 kart, 10 eksi, 11 artı, 12 otomatik

        public SplitAdvice() { Cursor = Cursors.Default; }

        int Pad { get { return Theme.P(16); } }
        int CardH { get { return Theme.P(84); } }

        float FooterH(Graphics g, int width)
        {
            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
                return g.MeasureString(Footer, small, Math.Max(10, width - Pad * 2)).Height;
        }

        public int Measure(int width)
        {
            using (var g = CreateGraphics())
                return (int)Math.Ceiling(Pad + Theme.P(22) + Theme.P(112) + Theme.P(14) + 3 * (CardH + Theme.P(10)) + Theme.P(4) + FooterH(g, width) + Pad);
        }

        int At(Point p)
        {
            if (rMinus.Contains(p)) return 10;
            if (rPlus.Contains(p)) return 11;
            if (Manual && rAuto.Contains(p)) return 12;
            for (int i = 0; i < 3; i++) if (rCards[i].Contains(p) && Picks != null && Picks[i] != null && Picks[i].Count > 1) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Enabled && !Fogged ? At(e.Location) : -1;
            if (h != hot) { hot = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled || Fogged) return;
            int h = At(e.Location);
            if (h >= 0 && h <= 2 && PickClicked != null) PickClicked(h);
            double step = (ModifierKeys & Keys.Shift) != 0 ? 0.1 : 1;
            double baseDiag = Diag > 0 ? Diag : 27;
            if (h == 10 && DiagChanged != null) DiagChanged(Math.Max(10, Math.Round((baseDiag - step) * 10) / 10));
            if (h == 11 && DiagChanged != null) DiagChanged(Math.Min(150, Math.Round((baseDiag + step) * 10) / 10));
            if (h == 12 && DiagChanged != null) DiagChanged(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var panel = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, panel, Theme.P(14), Theme.Card);
            Theme.Stroke(g, panel, Theme.P(14), Theme.Border, 1);
            float pad = Pad, w = Width - pad * 2;

            using (var head = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var bold = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold))
            using (var big = new Font(Font.FontFamily, Font.Size + 4f, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
            {
                float y = pad;
                Theme.Draw(g, Title, head, new RectangleF(pad, y, w, Theme.P(16)), Theme.Sub, StringAlignment.Near, StringAlignment.Near, false);
                y += Theme.P(22);

                // monitör kutusu
                var box = new RectangleF(pad, y, w, Theme.P(112));
                Theme.Fill(g, box, Theme.P(10), Theme.Bg);
                float bx = box.X + Theme.P(12), bw = box.Width - Theme.P(24), by = box.Y + Theme.P(10);
                Theme.Draw(g, MonitorName, bold, new RectangleF(bx, by, bw, bold.Height + 2), Theme.Text, StringAlignment.Near, StringAlignment.Near, false);
                by += bold.Height + Theme.P(2);
                string spec = Monitor.Width + " × " + Monitor.Height + (Ppi > 0 ? "  ·  " + Math.Round(Ppi) + " PPI" : "");
                Theme.Draw(g, spec, Font, new RectangleF(bx, by, bw, Font.Height + 2), Theme.Sub, StringAlignment.Near, StringAlignment.Near, false);
                by += Font.Height + Theme.P(8);

                // boyut: [−] 54.6″ [+]  otomatik
                float bh = Theme.P(28);
                var sw = g.MeasureString(SizeWord, Font);
                Theme.Draw(g, SizeWord, Font, new RectangleF(bx, by, sw.Width + 2, bh), Theme.Sub, StringAlignment.Near, StringAlignment.Center, false);
                float sx = bx + sw.Width + Theme.P(6);
                rMinus = new RectangleF(sx, by, bh, bh);
                string dv = Diag > 0 ? MonitorSize.Inch(Diag) : "?";
                var dvs = g.MeasureString(dv, bold);
                var rVal = new RectangleF(rMinus.Right, by, Math.Max(Theme.P(56), dvs.Width + Theme.P(8)), bh);
                rPlus = new RectangleF(rVal.Right, by, bh, bh);
                Button(g, rMinus, "−", hot == 10);
                Theme.Draw(g, dv, bold, rVal, Theme.Text, StringAlignment.Center, StringAlignment.Center, false);
                Button(g, rPlus, "+", hot == 11);
                if (Manual)
                {
                    var aw = g.MeasureString(AutoWord, small);
                    rAuto = new RectangleF(rPlus.Right + Theme.P(8), by, aw.Width + Theme.P(12), bh);
                    if (rAuto.Right <= box.Right - Theme.P(6))
                        Theme.Draw(g, AutoWord, small, rAuto, hot == 12 ? Theme.AccentHover : Theme.Accent, StringAlignment.Center, StringAlignment.Center, false);
                }
                else rAuto = RectangleF.Empty;
                by += bh + Theme.P(4);
                Theme.Draw(g, SourceText, small, new RectangleF(bx, by, bw, box.Bottom - by), Theme.Sub, StringAlignment.Near, StringAlignment.Near, false);
                y = box.Bottom + Theme.P(14);

                // üç öneri
                for (int k = 0; k < 3; k++)
                {
                    var pk = Picks != null ? Picks[k] : null;
                    var r = new RectangleF(pad, y, w, CardH);
                    rCards[k] = r;
                    Color tc = k == 1 ? Theme.Accent : k == 0 ? Theme.Ok : Theme.Warn;
                    bool cur = pk != null && pk.Count > 1 && pk.Count == Current;
                    Theme.Fill(g, r, Theme.P(10), hot == k ? Theme.Field : cur ? Theme.Mix(Theme.Bg, tc, 0.07f) : Theme.Bg);
                    Theme.Stroke(g, r, Theme.P(10), cur ? tc : k == 1 ? Theme.Mix(tc, Theme.Border, 0.5f) : Theme.Border, cur ? Theme.P(2) : 1);

                    float cx = r.X + Theme.P(12), cy = r.Y + Theme.P(9);
                    var ts = g.MeasureString(TierNames[k], small);
                    var tr = new RectangleF(cx, cy, ts.Width + Theme.P(12), ts.Height + Theme.P(2));
                    Theme.Fill(g, tr, tr.Height / 2, Theme.Mix(tc, Theme.Bg, 0.78f));
                    Theme.Draw(g, TierNames[k], small, tr, tc, StringAlignment.Center, StringAlignment.Center, false);

                    // sağda şablonun küçük çizimi
                    float thH = r.Height - Theme.P(22), thW = thH * Monitor.Width / Math.Max(1f, Monitor.Height);
                    if (thW > w * 0.34f) { thW = w * 0.34f; thH = thW * Monitor.Height / Math.Max(1f, Monitor.Width); }
                    var thumb = new RectangleF(r.Right - Theme.P(12) - thW, r.Y + (r.Height - thH) / 2, thW, thH);
                    using (var b = new SolidBrush(Color.FromArgb(7, 8, 10))) g.FillRectangle(b, thumb);
                    if (pk != null)
                    {
                        if (pk.Template == null)
                            using (var b = new SolidBrush(Theme.Mix(SplitCanvas.ColorOf(0), Theme.Bg, 0.35f))) g.FillRectangle(b, RectangleF.Inflate(thumb, -1, -1));
                        else
                            for (int i = 0; i < pk.Template.Cells.Length; i++)
                            {
                                var c = pk.Template.Cells[i];
                                using (var b = new SolidBrush(Theme.Mix(SplitCanvas.ColorOf(i), Theme.Bg, 0.35f)))
                                    g.FillRectangle(b, thumb.X + c.X * thumb.Width + 1, thumb.Y + c.Y * thumb.Height + 1, c.Width * thumb.Width - 2, c.Height * thumb.Height - 2);
                            }
                    }

                    float textW = thumb.X - cx - Theme.P(8);
                    float ly = tr.Bottom + Theme.P(4);
                    if (pk == null || pk.Count <= 1)
                    {
                        Theme.Draw(g, TierDescs[k], small, new RectangleF(cx, ly, textW, r.Bottom - ly - Theme.P(4)), Theme.Sub,
                            StringAlignment.Near, StringAlignment.Near, true);
                    }
                    else
                    {
                        string cnt = pk.Count + " " + ScreensWord;
                        Theme.Draw(g, cnt, big, new RectangleF(cx, ly, textW, big.Height + 2), Theme.Text, StringAlignment.Near, StringAlignment.Near, false);
                        ly += big.Height;
                        string det = pk.Smallest.Width + "×" + pk.Smallest.Height + " · " + SplitAdvisor.QualityName(pk.Quality);
                        Theme.Draw(g, det, small, new RectangleF(cx, ly, textW, small.Height + 2), SplitAdvisor.QualityColor(pk.Quality),
                            StringAlignment.Near, StringAlignment.Near, false);
                    }
                    y += CardH + Theme.P(10);
                }

                y += Theme.P(4);
                Theme.Draw(g, Footer, small, new RectangleF(pad, y, w, Height - y - Theme.P(8)), Theme.Sub, StringAlignment.Near, StringAlignment.Near, true);
            }
            FogOver(g, Theme.P(14));
        }

        void Button(Graphics g, RectangleF r, string text, bool hover)
        {
            Theme.Fill(g, r, Theme.P(7), hover ? Theme.FieldHover : Theme.Field);
            Theme.Stroke(g, r, Theme.P(7), Theme.Border, 1);
            Theme.Draw(g, text, Font, r, Theme.Text, StringAlignment.Center, StringAlignment.Center, false);
        }
    }
}
