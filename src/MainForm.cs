using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EkranKurtar
{
    class MainForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "EkranKurtar";
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
        FlatButton btnMinus, btnPlus, btnApply, btnRevert, btnReset;
        Label lblValue;
        Toggle tPreview, tLock, tFix, tStartup;
        ModeCard cardDriver, cardSoft, cardVirtual;
        StatusBox status;
        Explainer explainer;

        public MainForm(Settings cfg)
        {
            this.cfg = cfg;
            Text = "Ekran Kurtar";
            Icon = Theme.AppIcon(32);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            using (var g = CreateGraphics()) Theme.S = g.DpiX / 96f;
            BuildUi();
            RefreshMonitors();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitle(this);
        }

        static int P(float v) { return Theme.P(v); }

        // ------------------------------------------------------------------ Arayüz kurulumu
        Label Section(string text)
        {
            var l = new Label();
            l.Text = text.ToUpper(L.Lang == "tr" ? new System.Globalization.CultureInfo("tr-TR") : System.Globalization.CultureInfo.CurrentCulture);
            l.ForeColor = Theme.Sub;
            l.BackColor = Theme.Bg;
            l.Font = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            l.AutoSize = true;
            Controls.Add(l);
            return l;
        }

        T Add<T>(T c) where T : Control
        {
            c.Font = Font;
            Controls.Add(c);
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
            Controls.Add(logo);

            lblTitle = new Label();
            lblTitle.Text = "Ekran Kurtar";
            lblTitle.Font = new Font(Font.FontFamily, 15f, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.AutoSize = true;
            Controls.Add(lblTitle);

            lblSub = new Label();
            lblSub.Text = L.T("Monitörün bozuk kısmını Windows'tan gizle", "Hide the broken part of your monitor from Windows");
            lblSub.ForeColor = Theme.Sub;
            lblSub.AutoSize = true;
            lblSub.MaximumSize = new Size(P(296), 0);
            Controls.Add(lblSub);

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
            preview.CutChanged += delegate (int v) { SetCut(v, preview); };

            secEdge = Section(L.T("Bozuk kenar", "Broken edge"));
            segEdge = Add(new Seg());
            segEdge.Items = new[] { L.T("Sol", "Left"), L.T("Sağ", "Right"), L.T("Üst", "Top"), L.T("Alt", "Bottom") };
            segEdge.Selected = Math.Max(0, Array.IndexOf(EdgeOrder, cfg.Edge));
            segEdge.Changed += delegate (int i)
            {
                if (loading) return;
                cfg.Edge = EdgeOrder[i];
                cfg.Cut = 0;
                UpdateRange();
            };

            secArea = Section(L.T("Gizlenecek alan (px)", "Area to hide (px)"));
            btnMinus = Add(new FlatButton("−", BtnKind.Secondary));
            btnMinus.Click += delegate { SetCut(cfg.Cut - 1, null); };
            btnPlus = Add(new FlatButton("+", BtnKind.Secondary));
            btnPlus.Click += delegate { SetCut(cfg.Cut + 1, null); };
            lblValue = new Label();
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.ForeColor = Theme.Text;
            lblValue.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            Controls.Add(lblValue);

            slider = Add(new FlatSlider());
            slider.ValueChanged += delegate (int v) { SetCut(v, slider); };

            tPreview = Add(new Toggle(L.T("Gizlenecek alanı ekranda kırmızı göster", "Show the hidden area in red on screen")));
            tPreview.Checked = true;
            tPreview.Changed += delegate { UpdatePreview(); };

            secMethod = Section(L.T("Yöntem", "Method"));
            cardDriver = Add(new ModeCard());
            cardDriver.Title = L.T("Sürücü modu", "Driver mode");
            cardDriver.Badge = L.T("Deneysel", "Experimental");
            cardDriver.BadgeColor = Theme.Warn;
            cardDriver.Desc = L.T("Windows monitörü daha küçük sanar. Bazı ekran kartı sürücüleri (ör. NVIDIA) desteklemez.",
                                  "Windows sees a smaller monitor. Some GPU drivers (e.g. NVIDIA) don't support it.");
            cardDriver.Picked += delegate { cfg.Mode = Mode.Driver; cfg.Save(); UpdateState(); };
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

            explainer = Add(new Explainer());
            Explain(preview, 0); Explain(slider, 0); Explain(segEdge, 0); Explain(btnMinus, 0); Explain(btnPlus, 0);
            Explain(tPreview, 0); Explain(tLock, 0);
            Explain(cardDriver, 1);
            Explain(segMonitor, 2);
            Explain(cardSoft, 3); Explain(tFix, 3); Explain(cardVirtual, 1);
            Explain(btnApply, 4); Explain(btnRevert, 4); Explain(btnReset, 4);
            explainer.Running = Visible;

            ResumeLayout(false);
        }

        // Soldaki ayarın üstüne gelince sağdaki panel o ayarın ne işe yaradığını canlandırır
        void Explain(Control c, int scene)
        {
            c.MouseEnter += delegate { explainer.Pin(scene); };
            c.MouseLeave += delegate { explainer.Unpin(); };
        }

        void DoLayout()
        {
            int W = P(620), x0 = P(26), w = W - x0 * 2, y = P(22);

            logo.SetBounds(x0, y, P(44), P(44));
            lblTitle.Location = new Point(x0 + P(56), y - P(3));
            lblSub.Location = new Point(x0 + P(58), y + P(26));
            segLang.SetBounds(x0 + w - P(196), y + P(7), P(196), P(30));
            y = Math.Max(y + P(68), lblSub.Bottom + P(20));

            secMonitor.Location = new Point(x0, y); y += P(22);
            segMonitor.SetBounds(x0, y, w, P(38)); y += P(38) + P(16);

            preview.SetBounds(x0, y, w, P(250)); y += P(250) + P(16);

            int half = (w - P(16)) / 2;
            secEdge.Location = new Point(x0, y);
            secArea.Location = new Point(x0 + half + P(16), y);
            y += P(22);
            segEdge.SetBounds(x0, y, half, P(38));
            int vx = x0 + half + P(16);
            btnMinus.SetBounds(vx, y, P(38), P(38));
            btnPlus.SetBounds(vx + half - P(38), y, P(38), P(38));
            lblValue.SetBounds(vx + P(42), y, half - P(84), P(38));
            y += P(38) + P(12);

            slider.SetBounds(x0 - P(4), y, w + P(8), P(30)); y += P(30) + P(8);
            tPreview.SetBounds(x0, y, w, P(26)); y += P(26) + P(20);

            secMethod.Location = new Point(x0, y); y += P(22);
            int gap = P(10), cw = (w - gap * 2) / 3;
            int ch = Math.Max(P(118), Math.Max(cardVirtual.Measure(cw), Math.Max(cardDriver.Measure(cw), cardSoft.Measure(cw))));
            cardSoft.SetBounds(x0, y, cw, ch);
            cardDriver.SetBounds(x0 + cw + gap, y, cw, ch);
            cardVirtual.SetBounds(x0 + (cw + gap) * 2, y, w - (cw + gap) * 2, ch);
            y += ch + P(12);

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

            btnApply.SetBounds(x0 + w - P(150), y, P(150), P(44));
            btnRevert.SetBounds(x0 + w - P(150) - P(12) - P(120), y, P(120), P(44));
            tStartup.SetBounds(x0, y + P(9), w - P(300), P(26));
            y += P(44) + P(14);

            var rs = TextRenderer.MeasureText(btnReset.Text, btnReset.Font);
            btnReset.SetBounds(x0 - P(8), y, rs.Width + P(20), P(30));
            y += P(30) + P(16);

            int ex = W - P(6), ew = P(380);
            explainer.SetBounds(ex, P(22), ew, y - P(22) - P(22));
            ClientSize = new Size(ex + ew + P(24), y);
        }

        void RebuildUi()
        {
            SuspendLayout();
            var old = new Control[Controls.Count];
            Controls.CopyTo(old, 0);
            Controls.Clear();
            foreach (var c in old) c.Dispose();
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

        bool Horizontal { get { return cfg.Edge == Edge.Left || cfg.Edge == Edge.Right; } }

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

        int MaxCut()
        {
            var m = Selected;
            if (m == null) return 100;
            return Math.Max(8, (Horizontal ? FullW(m) : FullH(m)) * 2 / 3);
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
            preview.Edge = cfg.Edge;
            int max = MaxCut();
            slider.Maximum = max;
            int v = cfg.Cut > 0 ? Math.Min(max, cfg.Cut) : max * 3 / 8;
            loading = false;
            SetCut(v, null);
            UpdateState();
        }

        void SetCut(int v, object from)
        {
            if (loading) return;
            v = Math.Max(1, Math.Min(MaxCut(), v));
            cfg.Cut = v;
            if (from != slider) slider.Value = v;
            if (from != preview) preview.Cut = v;
            var m = Selected;
            int total = m == null ? 1 : Math.Max(1, Horizontal ? FullW(m) : FullH(m));
            lblValue.Text = v + " px   ·   %" + (v * 100 / total);
            UpdatePreview();
        }

        void UpdatePreview()
        {
            var m = Selected;
            if (!Visible || !tPreview.Checked || cfg.Active || busy || m == null || m.IsModified) { overlay.Hide(); return; }
            var s = Shield.FindScreen(m.GdiName);
            if (s == null) { overlay.Hide(); return; }
            int full = Math.Max(1, Horizontal ? FullW(m) : FullH(m));
            int desk = Horizontal ? s.Bounds.Width : s.Bounds.Height;
            overlay.ShowAt(Geo.Cut(s.Bounds, cfg.Edge, cfg.Cut * desk / full));
        }

        void UpdateState()
        {
            var m = Selected;
            bool editable = !cfg.Active && !busy && !(m != null && m.IsModified);
            segMonitor.Enabled = !cfg.Active && !busy;
            segEdge.Enabled = editable;
            slider.Enabled = editable;
            btnMinus.Enabled = editable;
            btnPlus.Enabled = editable;
            preview.Enabled = editable;
            preview.Applied = cfg.Active || (m != null && m.IsModified);
            cardDriver.Enabled = editable;
            cardSoft.Enabled = editable;
            cardVirtual.Enabled = editable;
            cardDriver.Selected = cfg.Mode == Mode.Driver;
            cardSoft.Selected = cfg.Mode == Mode.Software;
            cardVirtual.Selected = cfg.Mode == Mode.Virtual;
            tPreview.Enabled = editable;
            btnApply.Enabled = editable && m != null;
            btnRevert.Enabled = !busy && (cfg.Active || (m != null && m.IsModified));

            if (cfg.Active && cfg.ActiveMode == Mode.Driver)
            {
                status.Kind = StatusKind.Active;
                status.Text = L.T("Sürücü modu etkin. Windows bu monitörü küçük ekran olarak görüyor. Program kapansa da ayar kalır.",
                                  "Driver mode active. Windows sees this monitor as a smaller screen. Stays even if the app is closed.");
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
            if (m == null || cfg.Cut <= 0) return;
            cfg.MonitorPath = m.DevicePath;

            if (cfg.Mode == Mode.Software)
            {
                if (!StartShield(m.GdiName)) { MsgDialog.Show(this, L.T("Monitör bulunamadı.", "Monitor not found."), true); return; }
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
                if (cfg.Mode == Mode.Driver) ApplyDriver(m);
                else ApplyVirtual(m);
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

        // ---------------- 2) Sürücü modu: önce standart yöntem, olmazsa NVIDIA warp
        void ApplyDriver(MonitorInfo m)
        {
            var res = DisplayConfig.TryApply(m.DevicePath, cfg.Edge, cfg.Cut, false);
            string log = res.Log;
            bool warp = false;
            uint nid = 0;

            if (!res.Ok && NvApi.DisplayId(m.GdiName, out nid) == NvApi.OK)
            {
                log += "\nNVIDIA warp: ";
                int pr = NvApi.Probe(nid);
                log += NvApi.Err(pr) + "\n";
                if (pr == NvApi.OK)
                {
                    res = DisplayConfig.TryApply(m.DevicePath, cfg.Edge, cfg.Cut, true);
                    log += res.Log;
                    if (res.Ok)
                    {
                        warp = SetWarp(nid, ref log);
                        if (!warp) { DisplayConfig.Restore(res.Original); res.Ok = false; }
                    }
                }
            }

            if (!res.Ok)
            {
                OfferSoftware(L.T("Ekran kartı sürücün bu yöntemi desteklemiyor. Hiçbir şey değişmedi.",
                                  "Your GPU driver does not support this method. Nothing was changed."), log);
                return;
            }

            if (Confirm(m.GdiName))
            {
                int r = DisplayConfig.Keep(res.Applied);
                if (r == 0)
                {
                    cfg.Cut = res.ActualCut;
                    cfg.Active = true;
                    cfg.ActiveMode = Mode.Driver;
                    cfg.DriverWarp = warp;
                    cfg.Save();
                    return;
                }
                MsgDialog.Show(this, L.T("Kaydedilemedi: ", "Could not save: ") + DisplayConfig.Err(r), true);
            }
            if (warp) NvApi.ClearWarp(nid);
            DisplayConfig.Restore(res.Original);
        }

        bool SetWarp(uint nid, ref string log)
        {
            NvApi.ScanoutInfo si;
            int sr = NvApi.Scanout(nid, out si);
            if (sr != NvApi.OK) { log += "Scanout: " + NvApi.Err(sr) + "\n"; return false; }
            var src = si.sourceDesktopRect;
            var dst = NvApi.Target(cfg.Edge, src.W, src.H, (int)si.targetDisplayWidth, (int)si.targetDisplayHeight);
            log += "Warp: " + src.W + "x" + src.H + " -> " + dst.X + "," + dst.Y + " (" + si.targetDisplayWidth + "x" + si.targetDisplayHeight + ")\n";
            bool sticky;
            int wr = NvApi.SetWarp(nid, src, dst, out sticky);
            log += "SetWarp: " + NvApi.Err(wr) + "\n";
            return wr == NvApi.OK;
        }

        // ---------------- 3) Sanal monitör modu
        // Gömülü sanal ekran sürücüsünü kullanıcıya sorup kurar.
        // Dönüş: null = kuruldu ve hazır, "" = kullanıcı vazgeçti, diğer = hata mesajı
        string InstallDriver()
        {
            if (!DriverSetup.Embedded)
                return L.T("Bu Ekran Kurtar sürümünde sanal ekran sürücüsü yok. driver\\build_driver.bat ile derleyip build.bat'i tekrar çalıştır.",
                           "This Ekran Kurtar build has no virtual display driver. Build it with driver\\build_driver.bat and run build.bat again.");
            if (!MsgDialog.Ask(this, L.T(
                    "Sanal monitör yöntemi için Ekran Kurtar'ın sanal ekran sürücüsü gerekiyor. Şimdi kurulsun mu?\n\n" +
                    "Windows yönetici izni isteyecek. Kurulum birkaç saniye sürer, ekran bir an kararabilir. " +
                    "İstediğin zaman \"EkranKurtar.exe --uninstall-driver\" ile kaldırabilirsin.",
                    "The virtual monitor method needs Ekran Kurtar's virtual display driver. Install it now?\n\n" +
                    "Windows will ask for administrator permission. It takes a few seconds and the screen may flicker. " +
                    "You can remove it any time with \"EkranKurtar.exe --uninstall-driver\"."),
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

            bool h = Horizontal;
            int vw = h ? (FullW(m) - cfg.Cut) & ~1 : FullW(m);
            int vh = h ? FullH(m) : (FullH(m) - cfg.Cut) & ~1;
            int realCut = h ? FullW(m) - vw : FullH(m) - vh;

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
            var mirror = new VirtualMirror(m.GdiName, vg, cfg.Edge, realCut);
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
                cfg.Cut = realCut;
                cfg.Active = true;
                cfg.ActiveMode = Mode.Virtual;
                cfg.Save();
                return;
            }
            mirror.Stop();
            DisplayLayout.RestoreBackup();
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

        bool StartShield(string gdiName)
        {
            StopShield();
            shield = new Shield(gdiName, cfg.Edge, cfg.Cut, cfg.LockCursor, cfg.FixWindows);
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
                if (!StartShield(target.GdiName)) Raise(L.T("Yazılım modu başlatılamadı.", "Could not start software mode."));
                return;
            }

            if (cfg.ActiveMode == Mode.Virtual)
            {
                RestoreVirtual();
                return;
            }

            bool ok = DisplayConfig.IsApplied(cfg.MonitorPath, cfg.Edge, cfg.Cut, cfg.DriverWarp);
            if (!ok)
            {
                var res = DisplayConfig.TryApply(cfg.MonitorPath, cfg.Edge, cfg.Cut, cfg.DriverWarp);
                ok = res.Ok && DisplayConfig.Keep(res.Applied) == 0;
                if (!ok && res.Original != null && res.Applied != null) DisplayConfig.Restore(res.Original);
            }
            if (ok && cfg.DriverWarp)
            {
                uint nid; string log = "";
                ok = NvApi.DisplayId(target.GdiName, out nid) == NvApi.OK && SetWarp(nid, ref log);
            }
            if (!ok) Raise(L.T("Ekran ayarı yeniden uygulanamadı. Uygulamayı açıp tekrar dene.", "Could not re-apply. Open the app and try again."));
            RefreshMonitors();
        }

        void RestoreVirtual()
        {
            var phys = FindPhysGdi();
            if (phys == null) { Raise(L.T("Ayarlı monitör bağlı değil.", "The configured monitor is not connected.")); return; }
            bool h = Horizontal;
            MonitorInfo pm = null;
            foreach (var m in monitors) if (m.DevicePath == cfg.MonitorPath) pm = m;
            int fw = pm != null ? FullW(pm) : 1920, fh = pm != null ? FullH(pm) : 1080;
            int vw = h ? fw - cfg.Cut : fw, vh = h ? fh : fh - cfg.Cut;

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
            mirror = new VirtualMirror(phys, vg, cfg.Edge, cfg.Cut);
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
            if (explainer != null) explainer.Running = Visible;
            UpdatePreview();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ExitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                overlay.Hide();
                if (cfg.Active) Raise(L.T("Ekran Kurtar tepside çalışmaya devam ediyor.", "Ekran Kurtar keeps running in the tray."));
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
