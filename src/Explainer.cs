using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrokenScreenResizer
{
    // "Nasıl çalışır?" paneli: her özelliği küçük animasyonlarla anlatır.
    class Explainer : Ctl
    {
        const float SceneLen = 5.2f;
        const int SceneCount = 5;

        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        int scene;
        double sceneStart;
        bool pinned;
        int hotRow = -1;

        public Explainer()
        {
            Cursor = Cursors.Default;
            timer.Interval = 16;
            timer.Tick += delegate { Tick(); };
        }

        public bool Running
        {
            set { if (value) timer.Start(); else timer.Stop(); }
        }

        double Now { get { return clock.Elapsed.TotalSeconds; } }

        // Soldaki bir ayarın üstüne gelince ilgili sahneyi göster
        public void Pin(int s)
        {
            if (s != scene) { scene = s; sceneStart = Now; }
            pinned = true;
            Invalidate();
        }

        public void Unpin() { pinned = false; }

        void Tick()
        {
            if (!pinned && Now - sceneStart >= SceneLen)
            {
                scene = (scene + 1) % SceneCount;
                sceneStart = Now;
            }
            Invalidate();
        }

        float T { get { return (float)((Now - sceneStart) % SceneLen); } }

        // ------------------------------------------------------------------ metinler
        static string Title(int i)
        {
            switch (i)
            {
                case 0: return L.T("Fare bozuk alana girmez", "The mouse skips the broken area");
                case 1: return L.T("Tam ekran = sağlam alan", "Fullscreen = working area");
                case 2: return L.T("Monitörler birleşir", "Monitors join seamlessly");
                case 3: return L.T("Pencereler dışarıda kalır", "Windows stay out");
                default: return L.T("Her şey geri alınabilir", "Everything is reversible");
            }
        }

        static string Desc(int i)
        {
            switch (i)
            {
                case 0: return L.T("İmleç bir monitörün bittiği yerden diğerinin sağlam kısmına doğrudan geçer. Arada ölü alan yoktur.",
                                   "The cursor goes straight from one monitor's edge to the working part of the next. No dead zone in between.");
                case 1: return L.T("Büyütülen pencereler, videolar ve kenarlıksız tam ekran oyunlar sadece sağlam kısmı kaplar.",
                                   "Maximized windows, videos and borderless fullscreen games only fill the working part.");
                case 2: return L.T("Windows bozuk kısmı yok sayar ve monitörleri yan yana yapıştırır.",
                                   "Windows ignores the broken part and places the monitors edge to edge.");
                case 3: return L.T("Yazılım modunda bozuk alana taşan pencereler otomatik olarak geri itilir.",
                                   "In software mode, windows that spill into the broken area are pushed back automatically.");
                default: return L.T("Her değişiklik onay ister. Onaylamazsan 15 saniye içinde eski haline döner.",
                                    "Every change needs confirmation. Without it, it reverts within 15 seconds.");
            }
        }

        // ------------------------------------------------------------------ yerleşim
        float Pad { get { return Theme.P(20); } }
        RectangleF Canvas { get { float w = Width - Pad * 2; return new RectangleF(Pad, Theme.P(52), w, w * 0.72f); } }
        float CaptionY { get { return Canvas.Bottom + Theme.P(16); } }
        float ListY { get { return CaptionY + Theme.P(104); } }
        float RowH { get { return Theme.P(46); } }

        RectangleF Row(int i) { return new RectangleF(Pad, ListY + i * (RowH + Theme.P(6)), Width - Pad * 2, RowH); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < SceneCount; i++) if (Row(i).Contains(e.Location)) h = i;
            if (h != hotRow) { hotRow = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hotRow = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < SceneCount; i++)
                if (Row(i).Contains(e.Location)) { scene = i; sceneStart = Now; Invalidate(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------ çizim
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Theme.Prep(e, BackColor);
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, card, Theme.P(16), Theme.Card);
            Theme.Stroke(g, card, Theme.P(16), Theme.Border, 1);

            using (var sec = new Font(Font.FontFamily, 8f, FontStyle.Bold))
                Theme.Draw(g, L.T("NASIL ÇALIŞIR?", "HOW IT WORKS"), sec, new RectangleF(Pad, Theme.P(20), Width - Pad * 2, Theme.P(20)),
                    Theme.Sub, StringAlignment.Near, StringAlignment.Center, false);

            // sahne
            var c = Canvas;
            Theme.Fill(g, c, Theme.P(12), Color.FromArgb(17, 19, 23));
            var st = g.Save();
            using (var clip = Theme.Round(c, Theme.P(12))) g.SetClip(clip);
            using (var pen = new Pen(Color.FromArgb(10, Color.White), 1))
            {
                for (float x = c.X + Theme.P(16); x < c.Right; x += Theme.P(16)) g.DrawLine(pen, x, c.Y, x, c.Bottom);
                for (float y = c.Y + Theme.P(16); y < c.Bottom; y += Theme.P(16)) g.DrawLine(pen, c.X, y, c.Right, y);
            }
            g.TranslateTransform(c.X, c.Y);
            float t = T;
            switch (scene)
            {
                case 0: SceneMouse(g, c.Width, c.Height, t); break;
                case 1: SceneFullscreen(g, c.Width, c.Height, t); break;
                case 2: SceneJoin(g, c.Width, c.Height, t); break;
                case 3: ScenePush(g, c.Width, c.Height, t); break;
                default: SceneRevert(g, c.Width, c.Height, t); break;
            }
            g.Restore(st);
            Theme.Stroke(g, c, Theme.P(12), Theme.Border, 1);

            // açıklama
            float cy = CaptionY;
            using (var bold = new Font(Font.FontFamily, Font.Size + 2f, FontStyle.Bold))
            {
                Theme.Draw(g, Title(scene), bold, new RectangleF(Pad, cy, Width - Pad * 2, Theme.P(26)), Theme.Text,
                    StringAlignment.Near, StringAlignment.Near, false);
                Theme.Draw(g, Desc(scene), Font, new RectangleF(Pad, cy + Theme.P(30), Width - Pad * 2, Theme.P(70)), Theme.Sub,
                    StringAlignment.Near, StringAlignment.Near, true);
            }

            // özellik listesi
            for (int i = 0; i < SceneCount; i++)
            {
                var r = Row(i);
                bool act = i == scene;
                if (act) Theme.Fill(g, r, Theme.P(10), Theme.Field);
                else if (i == hotRow) Theme.Fill(g, r, Theme.P(10), Theme.Mix(Theme.Card, Theme.Field, 0.5f));
                float d = Theme.P(26);
                var nr = new RectangleF(r.X + Theme.P(10), r.Y + (r.Height - d) / 2, d, d);
                using (var b = new SolidBrush(act ? Theme.Accent : Theme.Field)) g.FillEllipse(b, nr);
                using (var nf = new Font(Font.FontFamily, Font.Size - 1f, FontStyle.Bold))
                    Theme.Draw(g, (i + 1).ToString(), nf, nr, act ? Theme.OnAccent : Theme.Sub, StringAlignment.Center, StringAlignment.Center, false);
                Theme.Draw(g, Title(i), Font, new RectangleF(nr.Right + Theme.P(12), r.Y, r.Right - nr.Right - Theme.P(20), r.Height),
                    act ? Theme.Text : Theme.Sub, StringAlignment.Near, StringAlignment.Center, false);
                if (act)
                {
                    float p = pinned ? 1f : (float)Math.Min(1, (Now - sceneStart) / SceneLen);
                    var pr = new RectangleF(nr.Right + Theme.P(12), r.Bottom - Theme.P(7), r.Right - nr.Right - Theme.P(24), Theme.P(3));
                    Theme.Fill(g, pr, Theme.P(1.5f), Theme.Bg);
                    Theme.Fill(g, new RectangleF(pr.X, pr.Y, Math.Max(pr.Height, pr.Width * p), pr.Height), Theme.P(1.5f), Theme.Accent);
                }
            }

            using (var small = new Font(Font.FontFamily, Font.Size - 1.5f))
                Theme.Draw(g, L.T("Açık kaynak · MIT lisansı", "Open source · MIT license"), small,
                    new RectangleF(Pad, Height - Theme.P(36), Width - Pad * 2, Theme.P(20)), Theme.Dim(Theme.Sub),
                    StringAlignment.Center, StringAlignment.Center, false);
        }

        // ------------------------------------------------------------------ çizim yardımcıları
        static float Clamp(float x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
        static float Part(float t, float a, float b) { return Clamp((t - a) / (b - a)); }
        static float Ease(float x) { x = Clamp(x); return x < 0.5f ? 4 * x * x * x : 1 - (float)Math.Pow(-2 * x + 2, 3) / 2; }
        static float Back(float x)
        {
            x = Clamp(x);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * (float)Math.Pow(x - 1, 3) + c1 * (float)Math.Pow(x - 1, 2);
        }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static PointF Lerp(PointF a, PointF b, float t) { return new PointF(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t)); }
        static int A(float x) { return (int)(255 * Clamp(x)); }

        static void Monitor(Graphics g, RectangleF s, float cutFrac, float hatchAlpha, bool stand)
        {
            float bz = Theme.P(5);
            var frame = RectangleF.Inflate(s, bz, bz);
            if (stand)
            {
                float cx = s.X + s.Width / 2;
                using (var b = new SolidBrush(Theme.Field))
                {
                    g.FillRectangle(b, cx - Theme.P(6), frame.Bottom, Theme.P(12), Theme.P(10));
                    g.FillRectangle(b, cx - Theme.P(22), frame.Bottom + Theme.P(9), Theme.P(44), Theme.P(4));
                }
            }
            Theme.Fill(g, frame, Theme.P(7), Color.FromArgb(10, 11, 14));
            Theme.Stroke(g, frame, Theme.P(7), Theme.Border, 1);
            float cw = s.Width * cutFrac;
            var good = new RectangleF(s.X + cw, s.Y, s.Width - cw, s.Height);
            using (var b = new LinearGradientBrush(s, Theme.Mix(Theme.Accent, Theme.Bg, 0.55f), Theme.Mix(Theme.Accent, Theme.Bg, 0.8f), 45f))
                g.FillRectangle(b, good);
            if (cw > 0 && hatchAlpha > 0) Hatch(g, new RectangleF(s.X, s.Y, cw, s.Height), hatchAlpha);
        }

        static void Hatch(Graphics g, RectangleF r, float alpha)
        {
            using (var b = new SolidBrush(Color.FromArgb(A(alpha), 48, 16, 20))) g.FillRectangle(b, r);
            var st = g.Save();
            g.SetClip(r, CombineMode.Intersect);
            using (var p = new Pen(Color.FromArgb(A(alpha * 0.45f), Theme.Danger), Theme.P(2)))
                for (float d = -r.Height; d < r.Width + r.Height; d += Theme.P(8))
                    g.DrawLine(p, r.X + d, r.Bottom, r.X + d + r.Height, r.Y);
            g.Restore(st);
        }

        static void DrawCursor(Graphics g, PointF p, float scale)
        {
            float s = Theme.S * scale;
            PointF[] pts = {
                new PointF(0, 0), new PointF(0, 17), new PointF(4.5f, 13), new PointF(7.5f, 20),
                new PointF(10.5f, 18.8f), new PointF(7.5f, 12), new PointF(12.5f, 12) };
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(p.X + pts[i].X * s, p.Y + pts[i].Y * s);
            using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, pts);
            using (var pen = new Pen(Color.Black, 1.2f * Theme.S)) g.DrawPolygon(pen, pts);
        }

        static void Window(Graphics g, RectangleF r, float alpha, string label)
        {
            if (r.Width < 4 || r.Height < 4) return;
            float rad = Theme.P(4);
            Theme.Fill(g, r, rad, Color.FromArgb(A(alpha), 42, 47, 56));
            float tb = Math.Min(Theme.P(10), r.Height / 3);
            var bar = new RectangleF(r.X, r.Y, r.Width, tb);
            Theme.Fill(g, bar, rad, Color.FromArgb(A(alpha), 58, 64, 75));
            using (var b = new SolidBrush(Color.FromArgb(A(alpha), Theme.Sub)))
                for (int i = 0; i < 3; i++)
                    g.FillEllipse(b, r.Right - Theme.P(8) - i * Theme.P(7), r.Y + tb / 2 - Theme.P(2), Theme.P(4), Theme.P(4));
            using (var b = new SolidBrush(Color.FromArgb(A(alpha * 0.6f), Theme.Field)))
                for (int i = 0; i < 3; i++)
                {
                    float y = r.Y + tb + Theme.P(6) + i * Theme.P(7);
                    if (y + Theme.P(3) > r.Bottom - Theme.P(3)) break;
                    g.FillRectangle(b, r.X + Theme.P(6), y, (r.Width - Theme.P(12)) * (i == 2 ? 0.5f : 0.85f), Theme.P(3));
                }
            if (!string.IsNullOrEmpty(label))
                using (var f = new Font("Segoe UI", 8f * Math.Min(1.4f, Math.Max(0.8f, r.Height / Theme.P(60))), FontStyle.Bold))
                    Theme.Draw(g, label, f, new RectangleF(r.X, r.Y + tb, r.Width, r.Height - tb), Color.FromArgb(A(alpha), Color.White),
                        StringAlignment.Center, StringAlignment.Center, false);
        }

        static void NoEntry(Graphics g, PointF c, float alpha)
        {
            float r = Theme.P(9);
            using (var b = new SolidBrush(Color.FromArgb(A(alpha), Theme.Danger))) g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
            using (var p = new Pen(Color.FromArgb(A(alpha), Color.White), Theme.P(2.5f)))
                g.DrawLine(p, c.X - r * 0.5f, c.Y, c.X + r * 0.5f, c.Y);
        }

        static void Trail(Graphics g, PointF a, PointF b, float alpha)
        {
            if (alpha <= 0) return;
            float lift = Math.Abs(b.X - a.X) * 0.45f;
            using (var p = new Pen(Color.FromArgb(A(alpha), Theme.Accent), Theme.P(2)))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawBezier(p, a, new PointF(a.X, a.Y - lift), new PointF(b.X, b.Y - lift), b);
            }
        }

        static void Ripple(Graphics g, PointF c, float k)
        {
            if (k <= 0 || k >= 1) return;
            float r = Theme.P(4) + k * Theme.P(14);
            using (var p = new Pen(Color.FromArgb(A(1 - k), Theme.Accent), Theme.P(2))) g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
        }

        // ------------------------------------------------------------------ sahneler
        // 1) İmleç komşu monitörden kırık alanın üstünden atlayarak geçer
        void SceneMouse(Graphics g, float W, float H, float t)
        {
            float mw = W * 0.38f, mh = mw * 9 / 16, y = (H - mh) / 2 - Theme.P(6);
            var a = new RectangleF(W * 0.07f, y, mw, mh);
            var b = new RectangleF(W - W * 0.07f - mw, y, mw, mh);
            const float cut = 0.3f;
            Monitor(g, a, 0, 0, true);
            Monitor(g, b, cut, 1, true);

            float cy = y + mh * 0.55f;
            var p0 = new PointF(a.X + mw * 0.45f, cy);
            var p1 = new PointF(a.Right - Theme.P(3), cy);
            var p2 = new PointF(b.X + mw * cut + Theme.P(2), cy);
            var p3 = new PointF(b.X + mw * 0.7f, cy);

            PointF p;
            if (t < 0.4f) p = p0;
            else if (t < 1.6f) p = Lerp(p0, p1, Ease(Part(t, 0.4f, 1.6f)));
            else if (t < 2.7f) p = Lerp(p2, p3, Ease(Part(t, 1.7f, 2.7f)));
            else if (t < 3.1f) p = p3;
            else if (t < 4.0f) p = Lerp(p3, p2, Ease(Part(t, 3.1f, 4.0f)));
            else p = Lerp(p1, p0, Ease(Part(t, 4.1f, 4.9f)));

            float j1 = 1 - Part(t, 1.6f, 2.3f), j2 = 1 - Part(t, 4.0f, 4.7f);
            if (t >= 1.6f && t < 2.3f) Trail(g, p1, p2, j1);
            if (t >= 4.0f && t < 4.7f) Trail(g, p2, p1, j2);
            float flash = Math.Max(t >= 1.6f ? j1 : 0, t >= 4.0f ? j2 : 0);
            if (flash > 0)
            {
                var hr = new RectangleF(b.X, b.Y, mw * cut, mh);
                using (var pen = new Pen(Color.FromArgb(A(flash), Theme.Danger), Theme.P(2))) g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height);
                NoEntry(g, new PointF(hr.X + hr.Width / 2, hr.Y + hr.Height / 2), flash);
            }
            DrawCursor(g, p, 1f);
        }

        // 2) Büyütülen pencere sadece sağlam alanı kaplar
        void SceneFullscreen(Graphics g, float W, float H, float t)
        {
            float mw = W * 0.74f, mh = mw * 9 / 16;
            var s = new RectangleF((W - mw) / 2, (H - mh) / 2 - Theme.P(8), mw, mh);
            const float cut = 0.28f;
            Monitor(g, s, cut, 1, true);
            var good = new RectangleF(s.X + mw * cut, s.Y, mw * (1 - cut), mh);
            var small = new RectangleF(good.X + good.Width * 0.2f, good.Y + good.Height * 0.22f, good.Width * 0.55f, good.Height * 0.55f);

            float k = Ease(Part(t, 1.1f, 1.9f)) * (1 - Ease(Part(t, 4.0f, 4.8f)));
            var r = new RectangleF(Lerp(small.X, good.X, k), Lerp(small.Y, good.Y, k), Lerp(small.Width, good.Width, k), Lerp(small.Height, good.Height, k));
            Window(g, r, 1, k > 0.95f ? L.T("Tam ekran", "Fullscreen") : null);

            // imleç büyütme düğmesine gidip tıklar
            float tb = Math.Min(Theme.P(10), small.Height / 3);
            var btn = new PointF(small.Right - Theme.P(13), small.Y + tb / 2);
            var start = new PointF(good.X + good.Width * 0.5f, good.Bottom - Theme.P(10));
            PointF cp = t < 1.0f ? Lerp(start, btn, Ease(Part(t, 0.2f, 0.9f))) : Lerp(btn, new PointF(btn.X + Theme.P(10), btn.Y + Theme.P(26)), Ease(Part(t, 1.9f, 2.6f)));
            Ripple(g, btn, Part(t, 0.95f, 1.5f));
            if (t < 3.8f) DrawCursor(g, cp, 1f);

            if (k > 0.95f)
                using (var pen = new Pen(Color.FromArgb(180, Theme.Accent), Theme.P(2)))
                    g.DrawRectangle(pen, good.X + 1, good.Y + 1, good.Width - 2, good.Height - 2);
        }

        // 3) Bozuk kısım kaybolur, monitörler arasındaki boşluk kapanır
        void SceneJoin(Graphics g, float W, float H, float t)
        {
            float mw = W * 0.38f, mh = mw * 9 / 16, y = (H - mh) / 2 - Theme.P(6);
            var a = new RectangleF(W * 0.07f, y, mw, mh);
            const float cut = 0.3f;
            float fade = 1 - Ease(Part(t, 0.9f, 1.8f)) + Ease(Part(t, 4.5f, 5.1f));
            float slide = Ease(Part(t, 1.8f, 2.8f)) * (1 - Ease(Part(t, 4.5f, 5.1f)));

            var b = new RectangleF(W - W * 0.07f - mw, y, mw, mh);
            float goodX0 = b.X + mw * cut;
            float goodX1 = a.Right + Theme.P(10);   // iki çerçeve birbirine değer
            Monitor(g, a, 0, 0, true);

            if (fade > 0.02f)
            {
                var full = new RectangleF(b.X + (goodX1 - goodX0) * slide, b.Y, mw, mh);
                using (var tmp = new SolidBrush(Color.FromArgb(A(fade), 10, 11, 14)))
                    g.FillRectangle(tmp, RectangleF.Inflate(new RectangleF(full.X, full.Y, mw * cut, mh), Theme.P(5), Theme.P(5)));
                Hatch(g, new RectangleF(full.X, full.Y, mw * cut, mh), fade);
            }
            var gb = new RectangleF(Lerp(goodX0, goodX1, slide), b.Y, mw * (1 - cut), mh);
            Monitor(g, gb, 0, 0, true);

            float glow = Part(t, 2.8f, 3.2f) * (1 - Part(t, 4.3f, 4.6f));
            if (glow > 0)
            {
                float sx = (a.Right + gb.X) / 2;
                using (var pen = new Pen(Color.FromArgb(A(glow * (0.6f + 0.4f * (float)Math.Sin(t * 8))), Theme.Accent), Theme.P(3)))
                    g.DrawLine(pen, sx, y - Theme.P(4), sx, y + mh + Theme.P(4));
                var cp = Lerp(new PointF(a.X + mw * 0.5f, y + mh * 0.5f), new PointF(gb.X + gb.Width * 0.6f, y + mh * 0.5f), Ease(Part(t, 3.1f, 4.2f)));
                DrawCursor(g, cp, 1f);
            }
        }

        // 4) Kırık alana sürüklenen pencere geri itilir
        void ScenePush(Graphics g, float W, float H, float t)
        {
            float mw = W * 0.74f, mh = mw * 9 / 16;
            var s = new RectangleF((W - mw) / 2, (H - mh) / 2 - Theme.P(8), mw, mh);
            const float cut = 0.28f;
            Monitor(g, s, cut, 1, true);
            float gx = s.X + mw * cut;
            float ww = mw * 0.38f, wh = mh * 0.48f, wy = s.Y + mh * 0.25f;
            float x0 = gx + mw * 0.3f;
            float xIn = gx - ww * 0.45f;
            float xBack = gx + Theme.P(2);

            float x;
            if (t < 0.5f) x = x0;
            else if (t < 1.8f) x = Lerp(x0, xIn, Ease(Part(t, 0.5f, 1.8f)));
            else if (t < 2.6f) x = Lerp(xIn, xBack, Back(Part(t, 2.0f, 2.6f)));
            else x = Lerp(xBack, x0, Ease(Part(t, 4.2f, 5.0f)));
            if (t >= 1.8f && t < 2.0f) x = xIn;

            var r = new RectangleF(x, wy, ww, wh);
            Window(g, r, 1, null);

            float flash = Part(t, 1.8f, 1.9f) * (1 - Part(t, 2.0f, 2.6f));
            if (flash > 0)
            {
                var ov = RectangleF.Intersect(r, new RectangleF(s.X, s.Y, mw * cut, mh));
                if (ov.Width > 0)
                    using (var b = new SolidBrush(Color.FromArgb(A(flash * 0.5f), Theme.Danger))) g.FillRectangle(b, ov);
                NoEntry(g, new PointF(s.X + mw * cut / 2, wy - Theme.P(14)), flash);
            }
            if (t > 2.0f && t < 3.4f)
            {
                float k = Part(t, 2.0f, 3.4f);
                using (var pen = new Pen(Color.FromArgb(A(1 - k), Theme.Accent), Theme.P(2)))
                {
                    pen.EndCap = LineCap.ArrowAnchor;
                    g.DrawLine(pen, gx - Theme.P(4), wy + wh + Theme.P(12), gx + Theme.P(26), wy + wh + Theme.P(12));
                }
            }
            var grip = new PointF(r.X + ww * 0.5f, wy + Theme.P(4));
            if (t < 1.85f)
            {
                var from = new PointF(x0 + ww * 0.5f + Theme.P(20), wy + wh + Theme.P(20));
                DrawCursor(g, t < 0.5f ? Lerp(from, grip, Ease(Part(t, 0, 0.45f))) : grip, 1f);
            }
            else if (t < 3.6f) DrawCursor(g, new PointF(xIn + ww * 0.5f, wy + Theme.P(4)), 1f);
        }

        // 5) 15 saniyelik geri sayım ve otomatik geri alma
        void SceneRevert(Graphics g, float W, float H, float t)
        {
            float r = Math.Min(W, H) * 0.26f;
            var c = new PointF(W / 2, H / 2 - Theme.P(14));
            float p = 1 - Part(t, 0.3f, 3.9f);
            int secs = (int)Math.Ceiling(15 * p);
            using (var pen = new Pen(Theme.Field, Theme.P(8))) g.DrawEllipse(pen, c.X - r, c.Y - r, r * 2, r * 2);
            if (p > 0)
                using (var pen = new Pen(Theme.Accent, Theme.P(8)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, c.X - r, c.Y - r, r * 2, r * 2, -90, 360 * p);
                }

            bool done = t >= 3.9f;
            using (var big = new Font("Segoe UI", 20f, FontStyle.Bold))
            {
                if (!done)
                    Theme.Draw(g, secs.ToString(), big, new RectangleF(c.X - r, c.Y - r, r * 2, r * 2), Color.White,
                        StringAlignment.Center, StringAlignment.Center, false);
                else
                {
                    float k = Back(Part(t, 3.9f, 4.4f));
                    float ar = r * 0.45f * k;
                    using (var pen = new Pen(Theme.Accent, Theme.P(4)))
                    {
                        pen.EndCap = LineCap.ArrowAnchor;
                        if (ar > 1) g.DrawArc(pen, c.X - ar, c.Y - ar, ar * 2, ar * 2, 40, 280);
                    }
                }
            }
            using (var f = new Font(Font.FontFamily, Font.Size - 0.5f, FontStyle.Bold))
            {
                string lbl = done ? L.T("Geri alındı", "Reverted") : L.T("Geri al", "Revert");
                var br = new RectangleF(c.X - Theme.P(60), c.Y + r + Theme.P(18), Theme.P(120), Theme.P(26));
                Theme.Fill(g, br, Theme.P(6), done ? Theme.Mix(Theme.Accent, Theme.Bg, 0.7f) : Theme.Field);
                Theme.Draw(g, lbl, f, br, done ? Theme.Accent : Theme.Sub, StringAlignment.Center, StringAlignment.Center, false);
            }
        }
    }
}
