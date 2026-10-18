using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrokenScreenResizer
{
    class MainForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BrokenScreenResizer";
        static readonly Edge[] EdgeOrder = { Edge.Left, Edge.Right, Edge.Top, Edge.Bottom };

        readonly Settings cfg;
        List<MonitorInfo> monitors = new List<MonitorInfo>();
        Shield shield;
        VirtualMirror mirror;
        readonly PreviewOverlay overlay = new PreviewOverlay();
        bool busy, loading;
        public bool ExitRequested;
        public event Action<string> Notify;
        public event Action LanguageChanged;

        // arayüz
        PictureBox logo;
        Label lblTitle, lblSub, secMonitor, secEdge, secArea, secMethod;
        Seg segLang, segMonitor, segEdge;
        MonitorPreview preview;
        FlatSlider slider;
        FlatButton btnMinus, btnPlus, btnApply, btnRevert, btnReset, btnUninstall;
        Label lblValue, lblHint;
        Toggle tPreview, tLock, tFix, tStartup;
        ModeCard cardSoft, cardVirtual;
        StatusBox status;
        Explainer explainer;

        // Duyarlı yerleşim: içerik kaydırılabilir bir sayfada durur; pencere küçülünce
        // "Nasıl çalışır" paneli gizlenir, öğeler alt alta dizilir, gerekirse kaydırma çubuğu çıkar.
        readonly BufferedPanel body = new BufferedPanel(), page = new BufferedPanel();
        bool inLayout, autoFit = true, twoColumns = true;

        // Ekrandaki kırmızı önizleme en fazla ~30 kez/sn güncellenir (sürükleme akıcı kalsın)
        readonly Timer overlayTimer = new Timer();
        bool overlayDirty;

        // Kırmızı önizleme sadece kullanıcı ayar yaparken görünür; son değişiklikten birkaç saniye sonra kaybolur.
        // Program açılınca ekran hiçbir zaman kendiliğinden kırmızı olmaz.
        readonly Timer overlayHold = new Timer();
        bool editing;

        public MainForm(Settings cfg)
        {
            this.cfg = cfg;
            Text = AppInfo.Name + " " + AppInfo.Version;
            Icon = Theme.AppIcon(32);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            using (var g = CreateGraphics()) Theme.S = g.DpiX / 96f;

            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.BackColor = Theme.Bg;
            page.BackColor = Theme.Bg;
            body.Controls.Add(page);
            Controls.Add(body);
            body.Resize += delegate { DoLayout(); };
            ResizeBegin += delegate { autoFit = false; };   // kullanıcı boyutlandırınca pencereye dokunma

            overlayTimer.Interval = 33;
            overlayTimer.Tick += delegate { if (overlayDirty) ApplyOverlay(); else overlayTimer.Stop(); };
            overlayHold.Interval = 3000;
            overlayHold.Tick += delegate
            {
                if (preview != null && preview.Capture) return;   // sürükleme sürüyor
                overlayHold.Stop();
                editing = false;
                UpdatePreview();
            };

            MigrateStartup();
            BuildUi();
            InitialSize();
            RefreshMonitors();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitle(this);
        }

        static int P(float v) { return Theme.P(v); }

        // Pencere ilk açılışta bulunduğu ekrana sığacak boyutta açılır
        void InitialSize()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            var frame = SizeFromClientSize(Size.Empty);
            MinimumSize = SizeFromClientSize(new Size(P(360), P(320)));
            int cw = Math.Min(P(1018), wa.Width - frame.Width);
            int ch = Math.Min(P(900), wa.Height - frame.Height);
            ClientSize = new Size(Math.Max(P(360), cw), Math.Max(P(320), ch));
        }

        static string Upper(string s)
        {
            return s.ToUpper(L.Lang == "tr" ? new System.Globalization.CultureInfo("tr-TR") : System.Globalization.CultureInfo.CurrentCulture);
        }

        static string EdgeName(Edge e)
        {
            switch (e)
            {
                case Edge.Left: return L.T("Sol", "Left");
                case Edge.Right: return L.T("Sağ", "Right");
                case Edge.Top: return L.T("Üst", "Top");
                default: return L.T("Alt", "Bottom");
            }
        }

        static string EditHint()
        {
            return L.T("Bozuk olan her kenarı sürükle (birden fazla kenar olabilir). Köşeden tutunca iki kenar birlikte, ortadan tutunca tüm alan taşınır.",
                       "Drag each broken edge (more than one is fine). Grab a corner to move two edges, or the middle to move the whole area.");
        }

        static string LockedHint()
        {
            return L.T("Ayar uygulandı, sisli kısımlar kilitli. Bozuk alanı ya da yöntemi değiştirmek için önce Geri al'a bas; ekran eski haline döner, sonra yeniden ayarlayıp Uygula'ya basabilirsin.",
                       "The setting is applied, so the faded parts are locked. To change the broken area or the method, press Revert first; the screen goes back to normal, then adjust and press Apply again.");
        }

        // ------------------------------------------------------------------ Arayüz kurulumu
        Label Section(string text)
        {
            var l = new Label();
            l.Text = Upper(text);
            l.ForeColor = Theme.Sub;
            l.BackColor = Theme.Bg;
            l.Font = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            l.AutoSize = true;
            page.Controls.Add(l);
            return l;
        }

        T Add<T>(T c) where T : Control
        {
            c.Font = Font;
            page.Controls.Add(c);
            return c;
        }

        void BuildUi()
        {
            SuspendLayout();
            Font = new Font(L.FontName, 9.5f);

            logo = new PictureBox();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Theme.Bg;
            logo.Image = Theme.Logo();
            page.Controls.Add(logo);

            lblTitle = new Label();
            lblTitle.Text = "Broken Screen Resizer";
            lblTitle.Font = new Font(Font.FontFamily, 15f, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.AutoSize = true;
            page.Controls.Add(lblTitle);

            lblSub = new Label();
            lblSub.Text = L.T("Monitörün bozuk kısmını Windows'tan gizle", "Hide the broken part of your monitor from Windows");
            lblSub.ForeColor = Theme.Sub;
            lblSub.AutoSize = true;
            page.Controls.Add(lblSub);

            segLang = Add(new Seg());
            segLang.Items = new[] { "TR", "EN", "中文", "RU" };
            segLang.Font = new Font("Segoe UI", 8.5f);
            segLang.Selected = Math.Max(0, Array.IndexOf(L.Codes, L.Lang));
            segLang.Changed += delegate (int i)
            {
                cfg.Lang = L.Codes[i];
                cfg.Save();
                L.Set(cfg.Lang);
                BeginInvoke((MethodInvoker)RebuildUi);
            };

            secMonitor = Section(L.T("Monitör", "Monitor"));
            segMonitor = Add(new Seg());
            segMonitor.Changed += delegate { if (!loading) OnMonitorChanged(); };

            preview = Add(new MonitorPreview());
            preview.BrokenLabel = L.T("Bozuk", "Broken");
            preview.ResultLabel = L.T("Windows'un göreceği ekran", "What Windows will see");
            preview.AppliedLabel = L.T("Etkin", "Active");
            preview.LockedLabel = L.T("Kilitli · değiştirmek için Geri al", "Locked · press Revert to change");
            preview.CutsChanged += delegate (Cuts c) { OnPreviewCuts(c); };
            preview.FocusChanged += delegate (Edge e) { SetFocus(e); };

            lblHint = new Label();
            lblHint.Text = EditHint();
            lblHint.ForeColor = Theme.Sub;
            lblHint.Font = new Font(Font.FontFamily, 8.5f);
            lblHint.AutoSize = true;
            page.Controls.Add(lblHint);

            // Dört kenarın değeri burada hep görünür; birine tıklayınca aşağıdaki ince ayar o kenarı düzenler
            secEdge = Section(L.T("Bozuk kenarlar", "Broken edges"));
            segEdge = Add(new Seg());
            UpdateEdgeItems();
            segEdge.Selected = Math.Max(0, Array.IndexOf(EdgeOrder, cfg.Focus));
            segEdge.Changed += delegate (int i)
            {
                if (loading) return;
                SetFocus(EdgeOrder[i]);
            };

            secArea = Section(L.T("Gizlenecek alan (px)", "Area to hide (px)"));
            btnMinus = Add(new FlatButton("−", BtnKind.Secondary));
            btnMinus.Click += delegate { SetCut(cfg.Cuts[cfg.Focus] - 1, null); };
            btnPlus = Add(new FlatButton("+", BtnKind.Secondary));
            btnPlus.Click += delegate { SetCut(cfg.Cuts[cfg.Focus] + 1, null); };
            lblValue = new Label();
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.ForeColor = Theme.Text;
            lblValue.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            page.Controls.Add(lblValue);

            slider = Add(new FlatSlider());
            slider.ValueChanged += delegate (int v) { SetCut(v, slider); };

            tPreview = Add(new Toggle(L.T("Ayarlarken ekranda kırmızıyla göster","Show the hidden area in red (while adjusting)")));
            tPreview.Checked = true;
            tPreview.Changed += delegate (bool v) { if (v) MarkEditing(); UpdatePreview(); };

            secMethod = Section(L.T("Yöntem", "Method"));
            cardSoft = Add(new ModeCard());
            cardSoft.Title = L.T("Yazılım modu", "Software mode");
            cardSoft.Badge = L.T("Önerilen", "Recommended");
            cardSoft.Desc = L.T("Bozuk alanı kapatır, pencereleri ve fareyi dışarıda tutar. Her sistemde çalışır.",
                                "Covers the broken area and keeps windows and the mouse out. Works everywhere.");
            cardSoft.Picked += delegate { cfg.Mode = Mode.Software; cfg.Save(); UpdateState(); };

            cardVirtual = Add(new ModeCard());
            cardVirtual.Title = L.T("Sanal monitör", "Virtual monitor");
            cardVirtual.Badge = L.T("Deneysel", "Experimental");
            cardVirtual.BadgeColor = Theme.Warn;
            cardVirtual.Desc = L.T("Sağlam alan boyutunda sanal bir monitör açıp görüntüsünü buraya kopyalar. Gerekli sürücü ilk kullanımda kurulur.",
                                   "Creates a virtual monitor the size of the working area and mirrors it here. The required driver is installed on first use.");
            cardVirtual.Picked += delegate { cfg.Mode = Mode.Virtual; cfg.Save(); UpdateState(); };

            tLock = Add(new Toggle(L.T("Fareyi bozuk alana sokma", "Keep the mouse out of the broken area")));
            tLock.Checked = cfg.LockCursor;
            tLock.Changed += delegate (bool v) { cfg.LockCursor = v; if (shield != null) shield.LockCursor = v; cfg.Save(); };
            tFix = Add(new Toggle(L.T("Taşan pencereleri sağlam alana it", "Push overflowing windows into the working area")));
            tFix.Checked = cfg.FixWindows;
            tFix.Changed += delegate (bool v) { cfg.FixWindows = v; if (shield != null) shield.FixWindows = v; cfg.Save(); };

            status = Add(new StatusBox());

            tStartup = Add(new Toggle(L.T("Windows ile başlat", "Start with Windows")));
            tStartup.Checked = IsStartup();
            tStartup.Changed += delegate (bool v) { SetStartup(v); };

            btnRevert = Add(new FlatButton(L.T("Geri al", "Revert"), BtnKind.Secondary));
            btnRevert.Click += delegate { RevertClicked(); };
            btnApply = Add(new FlatButton(L.T("Uygula", "Apply"), BtnKind.Primary));
            btnApply.Click += delegate { ApplyClicked(); };

            btnReset = Add(new FlatButton(L.T("Acil durum: tüm monitörleri sıfırla", "Emergency: reset all monitors"), BtnKind.DangerGhost));
            btnReset.Font = new Font(Font.FontFamily, 8.5f);
            btnReset.Click += delegate { EmergencyReset(); };

            btnUninstall = Add(new FlatButton(L.T("Programı kaldır…", "Uninstall…"), BtnKind.Ghost));
            btnUninstall.Font = new Font(Font.FontFamily, 8.5f);
            btnUninstall.Click += delegate { Uninstaller.Launch(); };

            explainer = Add(new Explainer());
            Explain(preview, 0); Explain(lblHint, 0); Explain(slider, 0); Explain(segEdge, 0); Explain(btnMinus, 0); Explain(btnPlus, 0);
            Explain(tPreview, 0); Explain(tLock, 0);
            Explain(segMonitor, 2);
            Explain(cardSoft, 3); Explain(tFix, 3); Explain(cardVirtual, 1);
            Explain(btnApply, 4); Explain(btnRevert, 4); Explain(btnReset, 4);
            explainer.Running = Visible && twoColumns;

            ResumeLayout(false);
        }

        // Soldaki ayarın üstüne gelince sağdaki panel o ayarın ne işe yaradığını canlandırır
        void Explain(Control c, int scene)
        {
            c.MouseEnter += delegate { explainer.Pin(scene); };
            c.MouseLeave += delegate { explainer.Unpin(); };
        }

        // Pencere boyutuna göre yerleşim. Kullanıcı boyutlandırmadıysa pencere içeriğe (ekrana sığdığı kadar) uyar.
        void DoLayout()
        {
            if (inLayout || slider == null) return;
            inLayout = true;
            try
            {
                Relayout();
                if (autoFit && WindowState == FormWindowState.Normal)
                {
                    var wa = Screen.FromRectangle(Bounds).WorkingArea;
                    int frameH = Height - ClientSize.Height;
                    int want = Math.Max(P(320), Math.Min(page.Height, wa.Height - frameH));
                    if (want != ClientSize.Height)
                    {
                        ClientSize = new Size(ClientSize.Width, want);
                        if (Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);
                        Relayout();
                    }
                }
            }
            finally { inLayout = false; }
        }

        // Kaydırma çubuğu çıkınca/kaybolunca genişlik değişir; yerleşim oturana kadar tekrarlanır
        void Relayout()
        {
            // Dikey kaydırma çubuğu gerekecekse genişlik baştan ona göre hesaplanır (gereksiz yatay çubuk çıkmasın)
            int full = body.Width, bar = SystemInformation.VerticalScrollBarWidth;
            int cw = Math.Max(P(340), full);
            int h = LayoutAt(cw);
            if (h > body.Height)
            {
                cw = Math.Max(P(340), full - bar);
                h = LayoutAt(cw);
            }
            page.Size = new Size(cw, h);
        }

        // Verilen genişliğe göre öğeleri yerleştirir; içerik yüksekliğini döndürür
        int LayoutAt(int CW)
        {
            int ew = P(380);
            twoColumns = CW >= P(980);
            int leftW = twoColumns ? Math.Min(CW - ew - P(18), P(760)) : CW;
            // geniş pencerede (tam ekran vb.) iki sütun birlikte ortalanır; sağda boşluk birikmez
            int ox = twoColumns ? Math.Max(0, (CW - leftW - ew - P(18)) / 2) : 0;
            int margin = leftW >= P(520) ? P(26) : P(14);
            int x0 = margin + ox;
            int w = leftW - margin * 2;
            int y = P(22);

            // başlık; dar pencerede dil seçici alt satıra iner
            bool langInline = w >= P(500);
            int textW = Math.Max(P(100), w - P(58) - (langInline ? P(206) : 0));
            logo.SetBounds(x0, y, P(44), P(44));
            lblTitle.MaximumSize = new Size(textW, 0);
            lblTitle.Location = new Point(x0 + P(56), y - P(3));
            int titleH = lblTitle.GetPreferredSize(new Size(textW, 0)).Height;
            lblSub.MaximumSize = new Size(textW, 0);
            lblSub.Location = new Point(x0 + P(58), y - P(3) + titleH);
            int subH = lblSub.GetPreferredSize(new Size(textW, 0)).Height;
            y = Math.Max(y + P(68), lblSub.Top + subH + P(20));
            if (langInline) segLang.SetBounds(x0 + w - P(196), P(29), P(196), P(30));
            else { segLang.SetBounds(x0, y - P(6), Math.Min(w, P(240)), P(30)); y += P(30) + P(12); }

            secMonitor.Location = new Point(x0, y); y += P(22);
            segMonitor.SetBounds(x0, y, w, P(38)); y += P(38) + P(16);

            // önizleme genişliğe göre; alçak pencerede daha kısa
            int ph = Math.Max(P(150), Math.Min(P(250), w * 44 / 100));
            if (body.ClientSize.Height < P(620)) ph = Math.Min(ph, P(180));
            preview.SetBounds(x0, y, w, ph); y += ph + P(4);
            lblHint.MaximumSize = new Size(w, 0);
            lblHint.Location = new Point(x0, y);
            y += lblHint.GetPreferredSize(new Size(w, 0)).Height + P(14);

            secEdge.Location = new Point(x0, y); y += P(22);
            segEdge.SetBounds(x0, y, w, P(38)); y += P(38) + P(14);

            // seçili kenarın ince ayarı: − değer + ve kaydırıcı (genişse aynı satırda)
            secArea.Location = new Point(x0, y); y += P(22);
            int vw = P(170);
            btnMinus.SetBounds(x0, y, P(38), P(38));
            lblValue.SetBounds(x0 + P(42), y, vw, P(38));
            btnPlus.SetBounds(x0 + P(46) + vw, y, P(38), P(38));
            if (w >= P(460))
            {
                int sx = x0 + P(96) + vw;
                slider.SetBounds(sx, y + P(4), x0 + w - sx + P(4), P(30));
                y += P(38) + P(10);
            }
            else
            {
                y += P(38) + P(8);
                slider.SetBounds(x0 - P(4), y, w + P(8), P(30)); y += P(30) + P(8);
            }
            tPreview.SetBounds(x0, y, w, P(26)); y += P(26) + P(20);

            // yöntem kartları: yan yana ya da alt alta
            secMethod.Location = new Point(x0, y); y += P(22);
            int gap = P(10);
            if (w >= P(420))
            {
                int cw = (w - gap) / 2;
                int ch = Math.Max(P(110), Math.Max(cardSoft.Measure(cw), cardVirtual.Measure(w - cw - gap)));
                cardSoft.SetBounds(x0, y, cw, ch);
                cardVirtual.SetBounds(x0 + cw + gap, y, w - cw - gap, ch);
                y += ch + P(12);
            }
            else
            {
                int h1 = Math.Max(P(96), cardSoft.Measure(w)), h2 = Math.Max(P(96), cardVirtual.Measure(w));
                cardSoft.SetBounds(x0, y, w, h1); y += h1 + gap;
                cardVirtual.SetBounds(x0, y, w, h2); y += h2 + P(12);
            }

            bool soft = cfg.Mode == Mode.Software;
            tLock.Visible = soft;
            tFix.Visible = soft;
            if (soft)
            {
                tLock.SetBounds(x0 + P(4), y, w, P(26)); y += P(30);
                tFix.SetBounds(x0 + P(4), y, w, P(26)); y += P(30) + P(6);
            }

            y += P(4);
            int sh = status.Measure(w);
            status.SetBounds(x0, y, w, sh); y += sh + P(18);

            if (w >= P(470))
            {
                btnApply.SetBounds(x0 + w - P(150), y, P(150), P(44));
                btnRevert.SetBounds(x0 + w - P(150) - P(12) - P(120), y, P(120), P(44));
                tStartup.SetBounds(x0, y + P(9), w - P(300), P(26));
                y += P(44) + P(14);
            }
            else
            {
                tStartup.SetBounds(x0, y, w, P(26)); y += P(26) + P(12);
                int bw = (w - P(12)) / 2;
                btnRevert.SetBounds(x0, y, bw, P(44));
                btnApply.SetBounds(x0 + bw + P(12), y, w - bw - P(12), P(44));
                y += P(44) + P(14);
            }

            // acil durum (sol) ve kaldırma (sağ); sığmazsa alt alta
            var rs = TextRenderer.MeasureText(btnReset.Text, btnReset.Font);
            var us = TextRenderer.MeasureText(btnUninstall.Text, btnUninstall.Font);
            int rw = Math.Min(w + P(16), rs.Width + P(20)), uw = Math.Min(w + P(16), us.Width + P(20));
            btnReset.SetBounds(x0 - P(8), y, rw, P(30));
            if (rw + uw + P(12) <= w + P(16)) btnUninstall.SetBounds(x0 + w + P(8) - uw, y, uw, P(30));
            else { y += P(30) + P(2); btnUninstall.SetBounds(x0 - P(8), y, uw, P(30)); }
            y += P(30) + P(16);

            explainer.Visible = twoColumns;
            explainer.Running = Visible && twoColumns;
            if (twoColumns) explainer.SetBounds(ox + leftW - P(6), P(22), ew, y - P(44));
            return y;
        }

        void RebuildUi()
        {
            SuspendLayout();
            var old = new Control[page.Controls.Count];
            page.Controls.CopyTo(old, 0);
            page.Controls.Clear();
            foreach (var c in old) c.Dispose();
            slider = null;   // yeni arayüz kurulana kadar yerleşim yapılmasın
            BuildUi();
            ResumeLayout(false);
            RefreshMonitors();
            var h = LanguageChanged;
            if (h != null) h();
        }

        // ------------------------------------------------------------------ Monitörler ve değerler
        MonitorInfo Selected
        {
            get { int i = segMonitor.Selected; return i >= 0 && i < monitors.Count ? monitors[i] : null; }
        }

        bool Horizontal { get { return Cuts.IsHorizontal(cfg.Focus); } }

        static Edge Opposite(Edge e)
        {
            return e == Edge.Left ? Edge.Right : e == Edge.Right ? Edge.Left : e == Edge.Top ? Edge.Bottom : Edge.Top;
        }

        static int FullW(MonitorInfo m) { return m.PathW > 0 ? m.PathW : m.SignalW; }
        static int FullH(MonitorInfo m) { return m.PathH > 0 ? m.PathH : m.SignalH; }

        void RefreshMonitors()
        {
            loading = true;
            monitors = DisplayConfig.GetMonitors();
            try { monitors.RemoveAll(x => Vdd.IsOurs(x.GdiName)); } catch { }
            var names = new string[monitors.Count];
            int sel = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                names[i] = (i + 1) + "  ·  " + monitors[i].Name;
                if (monitors[i].DevicePath == cfg.MonitorPath) sel = i;
            }
            if (sel < 0 && monitors.Count > 0) sel = 0;
            segMonitor.Items = names;
            segMonitor.Selected = sel;
            loading = false;
            OnMonitorChanged();
        }

        void OnMonitorChanged()
        {
            var m = Selected;
            if (m != null && !cfg.Active) cfg.MonitorPath = m.DevicePath;
            UpdateRange();
        }

        // Bir yöndeki toplam kesim en fazla üçte iki olabilir (sağlam alan en az üçte bir kalır)
        int Limit(bool horizontal)
        {
            var m = Selected;
            if (m == null) return 100;
            return Math.Max(8, (horizontal ? FullW(m) : FullH(m)) * 2 / 3);
        }

        // Düzenlenen kenar için en büyük değer (karşı kenarın kesimi düşülür)
        int MaxCut()
        {
            return Math.Max(0, Limit(Horizontal) - cfg.Cuts[Opposite(cfg.Focus)]);
        }

        void UpdateRange()
        {
            var m = Selected;
            loading = true;
            if (m != null)
            {
                preview.SignalW = FullW(m);
                preview.SignalH = FullH(m);
            }
            // kesimleri bu monitörün boyutuna sığdır
            var c = cfg.Cuts;
            int lw = Limit(true), lh = Limit(false);
            c.L = Math.Min(c.L, lw); c.R = Math.Min(c.R, lw - c.L);
            c.T = Math.Min(c.T, lh); c.B = Math.Min(c.B, lh - c.T);
            cfg.Cuts = c;   // ilk açılışta hiçbir kenar kesilmez; kullanıcı kendisi ayarlar
            preview.Cuts = cfg.Cuts;
            preview.FocusEdge = cfg.Focus;
            loading = false;
            SyncControls(null);
            UpdateState();
        }

        void MarkEditing()
        {
            editing = true;
            overlayHold.Stop();
            overlayHold.Start();
        }

        void SetFocus(Edge e)
        {
            MarkEditing();
            cfg.Focus = e;
            loading = true;
            segEdge.Selected = Array.IndexOf(EdgeOrder, e);
            preview.FocusEdge = e;
            loading = false;
            SyncControls(null);
        }

        void SetCut(int v, object from)
        {
            if (loading) return;
            v = Math.Max(0, Math.Min(MaxCut(), v));
            MarkEditing();
            cfg.Cuts[cfg.Focus] = v;
            SyncControls(from);
        }

        void OnPreviewCuts(Cuts c)
        {
            if (loading) return;
            MarkEditing();
            cfg.Cuts = c;
            SyncControls(preview);
        }

        // Kaydırıcı, değer etiketi, kenar seçici ve önizlemeyi ayardaki kesimlere eşitler.
        // Önizlemede sürüklerken de her harekette çağrılır; değerler anında çizilir.
        void SyncControls(object from)
        {
            int v = cfg.Cuts[cfg.Focus];
            slider.Maximum = Math.Max(1, MaxCut());
            if (from != slider) slider.Value = v;
            if (from != preview) preview.Cuts = cfg.Cuts;
            var m = Selected;
            int total = m == null ? 1 : Math.Max(1, Horizontal ? FullW(m) : FullH(m));
            lblValue.Text = v + " px  ·  %" + (v * 100 / total);
            secArea.Text = Upper(L.T("Gizlenecek alan", "Area to hide") + " · " + EdgeName(cfg.Focus) + " (px)");
            UpdateEdgeItems();
            if (from == preview)
            {
                // sürükleme mesajları arasında yeniden çizim beklemesin
                lblValue.Update(); slider.Update(); segEdge.Update(); secArea.Update();
            }
            if (cfg.Cuts.Any != shownAny) UpdateState();   // Uygula düğmesi ve durum metni
            UpdatePreview();
        }

        bool shownAny;

        // Kenar seçicide her kenarın güncel değeri yazar (ör. "Sol 409"); kesim olmayan kenar sadece adıyla
        void UpdateEdgeItems()
        {
            var names = new string[EdgeOrder.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int v = cfg.Cuts[EdgeOrder[i]];
                names[i] = EdgeName(EdgeOrder[i]) + (v > 0 ? " " + v : "");
            }
            var old = segEdge.Items;
            if (old.Length == names.Length && string.Join("|", old) == string.Join("|", names)) return;
            segEdge.Items = names;
        }

        bool OverlayWanted(MonitorInfo m)
        {
            return Visible && tPreview.Checked && (editing || preview.Capture) && cfg.Cuts.Any &&
                   !cfg.Active && !busy && m != null && !m.IsModified;
        }

        void UpdatePreview()
        {
            if (!OverlayWanted(Selected)) { overlayDirty = false; overlay.Hide(); return; }
            overlayDirty = true;
            if (overlayTimer.Enabled) return;   // sonraki tikte uygulanır
            ApplyOverlay();
            overlayTimer.Start();
        }

        void ApplyOverlay()
        {
            overlayDirty = false;
            var m = Selected;
            if (!OverlayWanted(m)) { overlay.Hide(); return; }
            var s = Shield.FindScreen(m.GdiName);
            if (s == null) { overlay.Hide(); return; }
            var c = cfg.Cuts.Scale(FullW(m), FullH(m), s.Bounds.Width, s.Bounds.Height);
            overlay.ShowFrame(s.Bounds, c.Good(s.Bounds));
        }

        void UpdateState()
        {
            var m = Selected;
            bool locked = cfg.Active || (m != null && m.IsModified);   // uygulanmış ayar var: Geri al'a kadar değişmez
            bool editable = !locked && !busy;
            segMonitor.Enabled = !cfg.Active && !busy;
            segEdge.Enabled = editable;
            slider.Enabled = editable;
            btnMinus.Enabled = editable;
            btnPlus.Enabled = editable;
            preview.Enabled = editable;
            preview.Applied = cfg.Active || (m != null && m.IsModified);
            cardSoft.Enabled = editable;
            cardVirtual.Enabled = editable;
            cardSoft.Selected = cfg.Mode == Mode.Software;
            cardVirtual.Selected = cfg.Mode == Mode.Virtual;
            tPreview.Enabled = editable;
            shownAny = cfg.Cuts.Any;
            btnApply.Enabled = editable && m != null && shownAny;
            btnRevert.Enabled = !busy && locked;

            // kilitli kısımlar sisli görünür; yapılacak tek şey Geri al olduğu için o öne çıkar
            foreach (var c in new Ctl[] { segEdge, slider, btnMinus, btnPlus, preview, cardSoft, cardVirtual, tPreview }) c.Fogged = locked;
            segMonitor.Fogged = cfg.Active;
            foreach (var l in new[] { secEdge, secArea, secMethod }) l.ForeColor = locked ? Theme.Dim(Theme.Sub) : Theme.Sub;
            lblValue.ForeColor = locked ? Theme.Dim(Theme.Text) : Theme.Text;
            lblHint.ForeColor = locked ? Theme.Warn : Theme.Sub;
            lblHint.Text = locked ? LockedHint() : EditHint();
            btnRevert.Kind = locked ? BtnKind.Primary : BtnKind.Secondary;
            btnApply.Kind = locked ? BtnKind.Secondary : BtnKind.Primary;
            btnRevert.Invalidate(); btnApply.Invalidate();

            if (cfg.Active && cfg.ActiveMode == Mode.Driver)
            {
                // v0.6 ve öncesinden kalan sürücü modu ayarı
                status.Kind = StatusKind.Warn;
                status.Text = L.T("Eski sürücü modu ayarı hâlâ etkin. Bu sürümde sürücü modu yok; kaldırmak için Geri al'a bas.",
                                  "An old driver-mode setting is still active. This version has no driver mode; press Revert to remove it.");
            }
            else if (cfg.Active && cfg.ActiveMode == Mode.Virtual)
            {
                status.Kind = StatusKind.Active;
                status.Text = L.T("Sanal monitör modu etkin. Program tepside çalışmalı; kapatılırsa ekran düzeni eski haline döner.",
                                  "Virtual monitor mode active. The app must keep running; closing it restores the display layout.");
            }
            else if (cfg.Active)
            {
                status.Kind = StatusKind.Active;
                status.Text = L.T("Yazılım modu etkin. Program tepside çalışmalı.", "Software mode active. The app must keep running in the tray.");
            }
            else if (m != null && m.IsModified)
            {
                status.Kind = StatusKind.Warn;
                status.Text = L.T("Bu monitör daha önce kırpılmış. Tam ekrana dönmek için Geri al'a bas.",
                                  "This monitor was cropped earlier. Press Revert to restore full screen.");
            }
            else if (!cfg.Cuts.Any)
            {
                status.Kind = StatusKind.Idle;
                status.Text = L.T("Etkin değil. Önce bozuk alanı seç: monitör çiziminde bozuk kenarı içeri sürükle ya da bir kenar seçip değer gir.",
                                  "Not active. First mark the broken area: drag the broken edge inward on the monitor drawing, or pick an edge and enter a value.");
            }
            else
            {
                status.Kind = StatusKind.Idle;
                status.Text = L.T("Etkin değil. Kırmızı alanı bozuk kısmı kaplayana kadar ayarla, sonra Uygula'ya bas.",
                                  "Not active. Adjust until the red area covers the broken part, then press Apply.");
            }
            status.Invalidate();
            preview.Invalidate();
            DoLayout();
            UpdatePreview();
        }

        // ------------------------------------------------------------------ Uygula / geri al
        void ApplyClicked()
        {
            var m = Selected;
            if (m == null || !cfg.Cuts.Any) return;
            cfg.MonitorPath = m.DevicePath;

            if (cfg.Mode == Mode.Software)
            {
                if (!StartShield(m)) { MsgDialog.Show(this, L.T("Monitör bulunamadı.", "Monitor not found."), true); return; }
                cfg.Active = true;
                cfg.ActiveMode = Mode.Software;
                cfg.Save();
                UpdateState();
                return;
            }

            busy = true;
            UpdateState();
            Cursor = Cursors.WaitCursor;
            try
            {
                ApplyVirtual(m);
            }
            finally
            {
                Cursor = Cursors.Default;
                busy = false;
                RefreshMonitors();
            }
        }

        // Yöntem başarısız olunca yazılım modunu öner
        void OfferSoftware(string why, string log)
        {
            busy = false;
            bool sw = MsgDialog.Ask(this, why + "\n\n" + L.T("Yazılım moduyla uygulansın mı?", "Apply with software mode instead?") +
                (string.IsNullOrEmpty(log) ? "" : "\n\n" + log.TrimEnd()),
                L.T("Yazılım moduyla uygula", "Apply with software mode"), L.T("Kapat", "Close"));
            if (!sw) return;
            cfg.Mode = Mode.Software;
            cfg.Save();
            BeginInvoke((MethodInvoker)ApplyClicked);
        }

        bool Confirm(string gdi)
        {
            using (var dlg = new ConfirmDialog(SafeScreenRect(gdi)))
                return dlg.ShowDialog() == DialogResult.OK;
        }

        // ---------------- Sanal monitör modu
        // Gömülü sanal ekran sürücüsünü kullanıcıya sorup kurar.
        // Dönüş: null = kuruldu ve hazır, "" = kullanıcı vazgeçti, diğer = hata mesajı
        string InstallDriver()
        {
            if (!DriverSetup.Embedded)
                return L.T("Bu Broken Screen Resizer sürümünde sanal ekran sürücüsü yok. driver\\build_driver.bat ile derleyip build.bat'i tekrar çalıştır.",
                           "This Broken Screen Resizer build has no virtual display driver. Build it with driver\\build_driver.bat and run build.bat again.");
            if (!MsgDialog.Ask(this, L.T(
                    "Sanal monitör yöntemi için Broken Screen Resizer'ın sanal ekran sürücüsü gerekiyor. Şimdi kurulsun mu?\n\n" +
                    "Windows yönetici izni isteyecek. Kurulum birkaç saniye sürer, ekran bir an kararabilir. " +
                    "İstediğin zaman \"BrokenScreenResizer.exe --uninstall-driver\" ile kaldırabilirsin.",
                    "The virtual monitor method needs Broken Screen Resizer's virtual display driver. Install it now?\n\n" +
                    "Windows will ask for administrator permission. It takes a few seconds and the screen may flicker. " +
                    "You can remove it any time with \"BrokenScreenResizer.exe --uninstall-driver\"."),
                    L.T("Kur ve devam et", "Install and continue"), L.T("Vazgeç", "Cancel")))
                return "";

            Cursor = Cursors.WaitCursor;
            int r = DriverSetup.RunElevated("--install-driver");
            if (r == DriverSetup.Cancelled) return "";
            if (r == 3010)
                return L.T("Sürücü kuruldu; Windows'un yeniden başlatılması gerekiyor. Yeniden başlattıktan sonra Uygula'ya tekrar bas.",
                           "The driver was installed; Windows needs a restart. Press Apply again after restarting.");
            if (r != 0)
                return L.T("Sanal ekran sürücüsü kurulamadı: ", "Could not install the virtual display driver: ") + DriverSetup.Err(r) +
                       "\n" + L.T("Ayrıntılar: ", "Details: ") + DriverSetup.LogFile;
            if (!Vdd.WaitReady(20000))
                return L.T("Sürücü kuruldu ama henüz başlamadı. Birkaç saniye sonra tekrar dene; olmazsa bilgisayarı yeniden başlat.",
                           "The driver was installed but has not started yet. Try again in a few seconds, or restart the computer.");
            return null;
        }

        // Fiziksel monitörün yenileme hızı (ayna akıcı olsun diye sanal monitör de aynı hızda açılır)
        static int PhysRefresh(string gdi)
        {
            var e = DisplayLayout.Find(DisplayLayout.Capture(), gdi);
            int hz = e != null ? e.Freq : 60;
            return hz >= 24 && hz <= 240 ? hz : 60;
        }

        void ApplyVirtual(MonitorInfo m)
        {
            // 0) sürücü yoksa sor, kur ve kaldığı yerden devam et
            if (!Vdd.Installed)
            {
                busy = false;
                string ie = InstallDriver();
                busy = true;
                if (ie != null) { if (ie.Length > 0) MsgDialog.Show(this, ie, true); return; }
            }

            int vw, vh;
            var realCuts = EvenSize(cfg.Cuts, FullW(m), FullH(m), out vw, out vh);

            // 1) sağlam alan boyutunda sanal monitörü aç
            string vg;
            string ve = Vdd.Ensure(vw, vh, PhysRefresh(m.GdiName), out vg);
            if (ve != null)
            {
                OfferSoftware(ve, null);
                return;
            }

            // 2) ekran düzenini yedekle ve sanal monitörü fiziksel monitörün yerine koy
            if (!DisplayLayout.HasBackup) BackupWithoutVirtual(vg);
            int lr = VirtualMirror.Arrange(m.GdiName, vg, vw, vh);
            if (lr != 0)
            {
                DisplayLayout.RestoreBackup();
                OfferSoftware(L.T("Ekran düzeni değiştirilemedi: ", "Could not change the display layout: ") + DisplayLayout.Err(lr), null);
                return;
            }
            Pause(1200);

            // 3) aynayı başlat
            var mirror = new VirtualMirror(m.GdiName, vg, realCuts);
            string err = mirror.Start();
            if (err != null)
            {
                DisplayLayout.RestoreBackup();
                OfferSoftware(err, null);
                return;
            }

            if (Confirm(m.GdiName))
            {
                StopMirror();
                this.mirror = mirror;
                cfg.Cuts = realCuts;
                cfg.Active = true;
                cfg.ActiveMode = Mode.Virtual;
                cfg.Save();
                return;
            }
            mirror.Stop();
            DisplayLayout.RestoreBackup();
        }

        // Sanal monitörün boyutu çift sayı olmalı; artan piksel kesilen kenara eklenir
        static Cuts EvenSize(Cuts c, int fw, int fh, out int vw, out int vh)
        {
            vw = (fw - c.H) & ~1;
            vh = (fh - c.V) & ~1;
            if (fw - c.H - vw > 0) { if (c.R > 0 || c.L == 0) c.R++; else c.L++; }
            if (fh - c.V - vh > 0) { if (c.B > 0 || c.T == 0) c.B++; else c.T++; }
            return c;
        }

        // Geri alınca sanal monitör de kapansın (fazladan "hayalet" monitör kalmasın)
        static void BackupWithoutVirtual(string vg)
        {
            var cap = DisplayLayout.Capture();
            var ve = DisplayLayout.Find(cap, vg);
            if (ve != null) ve.Attached = false;
            DisplayLayout.SaveBackup(cap);
        }

        static void Pause(int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
        }

        void StopMirror()
        {
            if (mirror != null) { mirror.Stop(); mirror = null; }
        }

        // Pencere ulaşılamayan bir yerde kaldıysa (aynanın arkası ya da hiçbir ekran) görünür yere getirir
        public void BringIntoView()
        {
            var center = new Point(Left + Width / 2, Top + Height / 2);
            Rectangle target = Rectangle.Empty;
            if (mirror != null && mirror.PhysRect.Contains(center)) target = mirror.VirtRect;
            else
            {
                bool onScreen = false;
                foreach (var s in Screen.AllScreens) if (s.WorkingArea.Contains(center)) onScreen = true;
                if (!onScreen) target = Screen.PrimaryScreen.WorkingArea;
            }
            if (target.IsEmpty) return;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            Location = new Point(target.X + Math.Max(0, (target.Width - Width) / 2), target.Y + Math.Max(0, (target.Height - Height) / 2));
        }

        // Program kapanırken ekranı güvenli hale getirir (pencere hiç açılmamış olsa bile)
        public void Shutdown()
        {
            StopShield();
            if (mirror != null) { StopMirror(); DisplayLayout.RestoreBackup(); Vdd.RemoveAll(); }
        }

        void RevertClicked()
        {
            var m = Selected;
            if (cfg.Active && cfg.ActiveMode == Mode.Software) StopShield();

            if (cfg.Active && cfg.ActiveMode == Mode.Virtual)
            {
                StopMirror();
                int vr = DisplayLayout.RestoreBackup();
                if (vr != 0) MsgDialog.Show(this, L.T("Ekran düzeni geri yüklenemedi: ", "Could not restore the display layout: ") + DisplayLayout.Err(vr), true);
                Vdd.RemoveAll();
            }

            string path = cfg.Active ? cfg.MonitorPath : (m != null ? m.DevicePath : "");
            bool needDriverReset = (cfg.Active && cfg.ActiveMode == Mode.Driver) || (m != null && m.IsModified);
            if (needDriverReset && path != "")
            {
                busy = true;
                if (cfg.DriverWarp && m != null)
                {
                    uint nid;
                    if (NvApi.DisplayId(m.GdiName, out nid) == NvApi.OK) NvApi.ClearWarp(nid);
                }
                int r = DisplayConfig.Reset(path);
                busy = false;
                if (r != 0)
                    MsgDialog.Show(this, L.T("Geri alınamadı: ", "Could not revert: ") + DisplayConfig.Err(r) + "\n" +
                        L.T("Windows Ekran Ayarları'ndan çözünürlüğü önerilen değere getirebilirsin.",
                            "You can set the recommended resolution in Windows Display Settings."), true);
            }
            cfg.Active = false;
            cfg.DriverWarp = false;
            cfg.Save();
            RefreshMonitors();
        }

        void EmergencyReset()
        {
            StopShield();
            StopMirror();
            DisplayLayout.RestoreBackup();
            Vdd.RemoveAll();
            string log = DisplayConfig.ResetAll();
            cfg.Active = false;
            cfg.DriverWarp = false;
            cfg.Save();
            RefreshMonitors();
            MsgDialog.Show(this, log, false);
        }

        static Rectangle SafeScreenRect(string gdiName)
        {
            foreach (var s in Screen.AllScreens)
                if (s.DeviceName != gdiName) return s.WorkingArea;
            var t = Shield.FindScreen(gdiName);
            return t != null ? t.WorkingArea : Screen.PrimaryScreen.WorkingArea;
        }

        bool StartShield(MonitorInfo m)
        {
            StopShield();
            shield = new Shield(m.GdiName, cfg.Cuts, FullW(m), FullH(m), cfg.LockCursor, cfg.FixWindows);
            if (shield.Start()) return true;
            shield = null;
            return false;
        }

        public void StopShield()
        {
            if (shield != null) { shield.Stop(); shield = null; }
        }

        // Program açılışında önceki etkin ayarı geri getirir
        public void RestoreActive()
        {
            if (!cfg.Active) return;
            MonitorInfo target = null;
            foreach (var m in monitors) if (m.DevicePath == cfg.MonitorPath) target = m;
            if (target == null && !(cfg.ActiveMode == Mode.Virtual && DisplayLayout.HasBackup))
            {
                Raise(L.T("Ayarlı monitör bağlı değil.", "The configured monitor is not connected."));
                return;
            }

            if (cfg.ActiveMode == Mode.Software)
            {
                if (!StartShield(target)) Raise(L.T("Yazılım modu başlatılamadı.", "Could not start software mode."));
                return;
            }

            if (cfg.ActiveMode == Mode.Virtual)
            {
                RestoreVirtual();
                return;
            }

            // Eski sürücü modu (v0.6 ve öncesi): ayar Windows'ta kalıcıdır, bu sürüm yeniden uygulamaz.
            // Etkin bilgisi korunur ki "Geri al" monitörü tam ekrana döndürebilsin.
            RefreshMonitors();
        }

        void RestoreVirtual()
        {
            var phys = FindPhysGdi();
            if (phys == null) { Raise(L.T("Ayarlı monitör bağlı değil.", "The configured monitor is not connected.")); return; }
            MonitorInfo pm = null;
            foreach (var m in monitors) if (m.DevicePath == cfg.MonitorPath) pm = m;
            int fw = pm != null ? FullW(pm) : 1920, fh = pm != null ? FullH(pm) : 1080;
            int vw = fw - cfg.Cuts.H, vh = fh - cfg.Cuts.V;

            // Sürücü yoksa (ör. eski adlı sürücü kaldırıldı) ekranı güvenli hale getir; Uygula'ya basınca kurulur
            if (!Vdd.Installed)
            {
                DisplayLayout.RestoreBackup();
                cfg.Active = false;
                cfg.Save();
                RefreshMonitors();
                Raise(L.T("Sanal monitör sürücüsü güncellenmeli. Uygula'ya basınca kurulacak.",
                          "The virtual monitor driver needs an update. It will be installed when you press Apply."));
                return;
            }

            // sanal monitörü (program kapanınca kapatılmıştı) yeniden aç
            string vg;
            string ve = Vdd.Ensure(vw, vh, PhysRefresh(phys), out vg);
            if (ve != null) { Raise(ve); return; }

            var now = DisplayLayout.Capture();
            var v = DisplayLayout.Find(now, vg);
            if (v == null || !v.Attached || v.W != vw || v.H != vh)
            {
                if (!DisplayLayout.HasBackup) BackupWithoutVirtual(vg);
                int lr = VirtualMirror.Arrange(phys, vg, vw, vh);
                if (lr != 0) { DisplayLayout.RestoreBackup(); Raise(L.T("Sanal monitör düzeni kurulamadı.", "Could not set up the virtual monitor layout.")); return; }
                Pause(1200);
            }
            StopMirror();
            mirror = new VirtualMirror(phys, vg, cfg.Cuts);
            string err = mirror.Start();
            if (err != null) { mirror = null; DisplayLayout.RestoreBackup(); Raise(err); }
        }

        string FindPhysGdi()
        {
            foreach (var m in monitors) if (m.DevicePath == cfg.MonitorPath) return m.GdiName;
            return null;
        }

        void Raise(string msg)
        {
            var h = Notify;
            if (h != null) h(msg);
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            if (busy || !IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate { if (!busy) RefreshMonitors(); });
        }

        // ------------------------------------------------------------------ Başlangıç
        // Başlangıç kaydını bu sürüme bağlar: eski adla ("Ekran Kurtar") eklenmiş kaydı taşır; kayıt silinmiş ya da
        // daha eski bir sürümün exe'sini gösteriyorsa (ör. güncelleme sonrası) bu exe'yi yazar
        static void MigrateStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    string me = "\"" + Application.ExecutablePath + "\" --tray";
                    if (k.GetValue(Settings.LegacyName) != null)
                    {
                        k.DeleteValue(Settings.LegacyName, false);
                        if (k.GetValue(RunName) == null) k.SetValue(RunName, me);
                    }
                    var cur = k.GetValue(RunName) as string;
                    if (cur == null || cur == me || !Instances.IAmTheApp) return;
                    string path = cur.StartsWith("\"") ? cur.Substring(1, Math.Max(0, cur.IndexOf('"', 1) - 1)) : cur.Split(' ')[0];
                    if (!System.IO.File.Exists(path) || Instances.FileVersionOf(path) < Instances.Mine) k.SetValue(RunName, me);
                }
            }
            catch { }
        }

        static bool IsStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            catch { return false; }
        }

        static void SetStartup(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------ Pencere
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) RefreshMonitors();
            if (explainer != null) explainer.Running = Visible && twoColumns;
            UpdatePreview();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ExitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                overlay.Hide();
                if (cfg.Active) Raise(L.T("Broken Screen Resizer tepside çalışmaya devam ediyor.", "Broken Screen Resizer keeps running in the tray."));
                return;
            }
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            StopShield();
            // Sanal monitör modu programsız çalışamaz: kapanırken ekran düzenini güvenli hale döndür.
            // (Etkin bilgisi korunur; program tekrar açılınca kendiliğinden yeniden kurulur.)
            if (mirror != null) { StopMirror(); DisplayLayout.RestoreBackup(); Vdd.RemoveAll(); }
            overlay.Close();
            base.OnFormClosing(e);
        }
    }
}
