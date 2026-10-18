using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EkranKurtar
{
    class MainForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "EkranKurtar";

        readonly Settings cfg;
        List<MonitorInfo> monitors = new List<MonitorInfo>();
        Shield shield;
        readonly PreviewOverlay overlay = new PreviewOverlay();
        bool busy, loading;
        public bool ExitRequested;
        public event Action<string> Notify;

        ComboBox cbMonitor, cbEdge;
        TrackBar track;
        NumericUpDown num;
        Label lblPercent, lblStatus;
        CheckBox chkPreview, chkLockCursor, chkFixWindows, chkStartup;
        RadioButton rbDriver, rbSoftware;
        Button btnApply, btnRevert;

        public MainForm(Settings cfg)
        {
            this.cfg = cfg;
            Text = "Ekran Kurtar";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);
            BuildUi();
            RefreshMonitors();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        // ------------------------------------------------------------------ Arayüz
        void BuildUi()
        {
            var t = new TableLayoutPanel();
            t.ColumnCount = 2;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var title = new Label();
            title.Text = L.T("Monitörün bozuk kısmını Windows'tan gizle", "Hide the broken part of your monitor from Windows");
            title.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, 10);
            t.Controls.Add(title, 0, 0);
            t.SetColumnSpan(title, 2);

            cbMonitor = Combo(360);
            cbMonitor.SelectedIndexChanged += delegate { if (!loading) OnMonitorChanged(); };
            Row(t, L.T("Monitör:", "Monitor:"), cbMonitor);

            cbEdge = Combo(160);
            cbEdge.Items.AddRange(new object[] { L.T("Sol", "Left"), L.T("Üst", "Top"), L.T("Sağ", "Right"), L.T("Alt", "Bottom") });
            cbEdge.SelectedIndexChanged += delegate { if (!loading) { cfg.Edge = (Edge)cbEdge.SelectedIndex; UpdateRange(); UpdatePreview(); } };
            Row(t, L.T("Bozuk kenar:", "Broken edge:"), cbEdge);

            var cutRow = new FlowLayoutPanel();
            cutRow.AutoSize = true;
            cutRow.WrapContents = false;
            cutRow.Margin = new Padding(0);
            track = new TrackBar();
            track.Width = 220;
            track.TickStyle = TickStyle.None;
            track.ValueChanged += delegate { if (!loading && num.Value != track.Value) num.Value = track.Value; };
            num = new NumericUpDown();
            num.Width = 70;
            num.ValueChanged += delegate
            {
                if (loading) return;
                if (track.Value != (int)num.Value) track.Value = (int)num.Value;
                cfg.Cut = (int)num.Value;
                UpdatePercent();
                UpdatePreview();
            };
            lblPercent = new Label();
            lblPercent.AutoSize = true;
            lblPercent.Margin = new Padding(6, 6, 0, 0);
            cutRow.Controls.Add(track);
            cutRow.Controls.Add(num);
            cutRow.Controls.Add(lblPercent);
            Row(t, L.T("Gizlenecek alan (px):", "Area to hide (px):"), cutRow);

            chkPreview = new CheckBox();
            chkPreview.Text = L.T("Gizlenecek alanı kırmızı göster (ayar yaparken)", "Show the hidden area in red (while adjusting)");
            chkPreview.AutoSize = true;
            chkPreview.Checked = true;
            chkPreview.CheckedChanged += delegate { UpdatePreview(); };
            Row(t, "", chkPreview);

            var modeBox = new FlowLayoutPanel();
            modeBox.FlowDirection = FlowDirection.TopDown;
            modeBox.AutoSize = true;
            modeBox.Margin = new Padding(0, 8, 0, 0);
            rbDriver = new RadioButton();
            rbDriver.Text = L.T("Sürücü modu (önerilen): Windows monitörü daha küçük sanar, oyunlar dahil her şey çalışır",
                                "Driver mode (recommended): Windows sees a smaller monitor, everything incl. games works");
            rbDriver.AutoSize = true;
            rbSoftware = new RadioButton();
            rbSoftware.Text = L.T("Yazılım modu: bozuk alanı kapatır, pencereleri ve fareyi dışarıda tutar",
                                  "Software mode: covers the broken area, keeps windows and mouse out");
            rbSoftware.AutoSize = true;
            rbDriver.CheckedChanged += delegate { if (!loading) { cfg.Mode = rbDriver.Checked ? Mode.Driver : Mode.Software; UpdateState(); } };
            chkLockCursor = new CheckBox();
            chkLockCursor.Text = L.T("Fareyi bozuk alana sokma", "Keep the mouse out of the broken area");
            chkLockCursor.AutoSize = true;
            chkLockCursor.Margin = new Padding(20, 0, 0, 0);
            chkLockCursor.CheckedChanged += delegate { cfg.LockCursor = chkLockCursor.Checked; if (shield != null) shield.LockCursor = cfg.LockCursor; cfg.Save(); };
            chkFixWindows = new CheckBox();
            chkFixWindows.Text = L.T("Taşan pencereleri sağlam alana it", "Push overflowing windows into the working area");
            chkFixWindows.AutoSize = true;
            chkFixWindows.Margin = new Padding(20, 0, 0, 0);
            chkFixWindows.CheckedChanged += delegate { cfg.FixWindows = chkFixWindows.Checked; if (shield != null) shield.FixWindows = cfg.FixWindows; cfg.Save(); };
            modeBox.Controls.Add(rbDriver);
            modeBox.Controls.Add(rbSoftware);
            modeBox.Controls.Add(chkLockCursor);
            modeBox.Controls.Add(chkFixWindows);
            Row(t, L.T("Yöntem:", "Method:"), modeBox);

            var buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Margin = new Padding(0, 12, 0, 0);
            btnApply = new Button();
            btnApply.Text = L.T("Uygula", "Apply");
            btnApply.AutoSize = true;
            btnApply.Padding = new Padding(10, 2, 10, 2);
            btnApply.Click += delegate { ApplyClicked(); };
            btnRevert = new Button();
            btnRevert.Text = L.T("Geri al", "Revert");
            btnRevert.AutoSize = true;
            btnRevert.Padding = new Padding(10, 2, 10, 2);
            btnRevert.Click += delegate { RevertClicked(); };
            buttons.Controls.Add(btnApply);
            buttons.Controls.Add(btnRevert);
            Row(t, "", buttons);

            lblStatus = new Label();
            lblStatus.AutoSize = true;
            lblStatus.MaximumSize = new Size(520, 0);
            lblStatus.Margin = new Padding(3, 8, 3, 3);
            Row(t, L.T("Durum:", "Status:"), lblStatus);

            var bottom = new FlowLayoutPanel();
            bottom.AutoSize = true;
            bottom.Margin = new Padding(0, 10, 0, 0);
            chkStartup = new CheckBox();
            chkStartup.Text = L.T("Windows ile başlat", "Start with Windows");
            chkStartup.AutoSize = true;
            chkStartup.Checked = IsStartup();
            chkStartup.CheckedChanged += delegate { SetStartup(chkStartup.Checked); };
            var linkReset = new LinkLabel();
            linkReset.Text = L.T("Acil durum: tüm monitörleri sıfırla", "Emergency: reset all monitors");
            linkReset.AutoSize = true;
            linkReset.Margin = new Padding(20, 4, 0, 0);
            linkReset.LinkClicked += delegate { EmergencyReset(); };
            bottom.Controls.Add(chkStartup);
            bottom.Controls.Add(linkReset);
            Row(t, "", bottom);

            Controls.Add(t);

            loading = true;
            cbEdge.SelectedIndex = (int)cfg.Edge;
            rbDriver.Checked = cfg.Mode == Mode.Driver;
            rbSoftware.Checked = cfg.Mode == Mode.Software;
            chkLockCursor.Checked = cfg.LockCursor;
            chkFixWindows.Checked = cfg.FixWindows;
            loading = false;
        }

        static ComboBox Combo(int width)
        {
            var c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Width = width;
            return c;
        }

        static void Row(TableLayoutPanel t, string label, Control c)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            var l = new Label();
            l.Text = label;
            l.AutoSize = true;
            l.Margin = new Padding(3, 7, 10, 3);
            t.Controls.Add(l, 0, r);
            t.Controls.Add(c, 1, r);
        }

        // ------------------------------------------------------------------ Monitörler
        MonitorInfo Selected
        {
            get { int i = cbMonitor.SelectedIndex; return i >= 0 && i < monitors.Count ? monitors[i] : null; }
        }

        void RefreshMonitors()
        {
            loading = true;
            monitors = DisplayConfig.GetMonitors();
            cbMonitor.Items.Clear();
            int sel = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                cbMonitor.Items.Add(monitors[i].ToString());
                if (monitors[i].DevicePath == cfg.MonitorPath) sel = i;
            }
            if (sel < 0 && monitors.Count > 0) sel = 0;
            cbMonitor.SelectedIndex = sel;
            loading = false;
            OnMonitorChanged();
        }

        void OnMonitorChanged()
        {
            var m = Selected;
            if (m != null && !cfg.Active) cfg.MonitorPath = m.DevicePath;
            UpdateRange();
            UpdateState();
        }

        int MaxCut()
        {
            var m = Selected;
            if (m == null) return 100;
            bool horiz = cfg.Edge == Edge.Left || cfg.Edge == Edge.Right;
            return Math.Max(8, (horiz ? m.SignalW : m.SignalH) * 2 / 3);
        }

        void UpdateRange()
        {
            loading = true;
            int max = MaxCut();
            track.Maximum = max;
            track.SmallChange = 1;
            track.LargeChange = 16;
            num.Maximum = max;
            int v = Math.Min(max, cfg.Cut > 0 ? cfg.Cut : max * 3 / 8);
            track.Value = v;
            num.Value = v;
            cfg.Cut = v;
            loading = false;
            UpdatePercent();
            UpdatePreview();
        }

        void UpdatePercent()
        {
            var m = Selected;
            if (m == null) { lblPercent.Text = ""; return; }
            bool horiz = cfg.Edge == Edge.Left || cfg.Edge == Edge.Right;
            int total = Math.Max(1, horiz ? m.SignalW : m.SignalH);
            lblPercent.Text = "%" + (cfg.Cut * 100 / total);
        }

        void UpdatePreview()
        {
            var m = Selected;
            if (!Visible || !chkPreview.Checked || cfg.Active || busy || m == null || m.IsModified) { overlay.Hide(); return; }
            var s = Shield.FindScreen(m.GdiName);
            if (s == null) { overlay.Hide(); return; }
            bool horiz = cfg.Edge == Edge.Left || cfg.Edge == Edge.Right;
            int signal = Math.Max(1, horiz ? m.SignalW : m.SignalH);
            int desk = horiz ? s.Bounds.Width : s.Bounds.Height;
            int c = cfg.Cut * desk / signal;
            overlay.ShowAt(Geo.Cut(s.Bounds, cfg.Edge, c));
        }

        void UpdateState()
        {
            var m = Selected;
            bool editable = !cfg.Active && !busy;
            cbMonitor.Enabled = editable;
            cbEdge.Enabled = editable;
            track.Enabled = editable;
            num.Enabled = editable;
            rbDriver.Enabled = editable;
            rbSoftware.Enabled = editable;
            chkLockCursor.Enabled = cfg.Mode == Mode.Software;
            chkFixWindows.Enabled = cfg.Mode == Mode.Software;
            btnApply.Enabled = editable && m != null && !(m.IsModified);
            btnRevert.Enabled = !busy && (cfg.Active || (m != null && m.IsModified));

            string s;
            if (cfg.Active && cfg.ActiveMode == Mode.Driver)
                s = L.T("Sürücü modu etkin. Windows bu monitörü küçük ekran olarak görüyor. Program kapansa da ayar kalır.",
                        "Driver mode active. Windows sees this monitor as a smaller screen. Stays even if the app is closed.");
            else if (cfg.Active)
                s = L.T("Yazılım modu etkin. Program tepside çalışmalı.", "Software mode active. The app must keep running in the tray.");
            else if (m != null && m.IsModified)
                s = L.T("Bu monitör daha önce kırpılmış. Tam ekrana dönmek için Geri al'a bas.",
                        "This monitor was cropped earlier. Press Revert to restore full screen.");
            else
                s = L.T("Etkin değil. Kırmızı alanı bozuk kısmı kaplayana kadar ayarla, sonra Uygula'ya bas.",
                        "Not active. Adjust until the red area covers the broken part, then press Apply.");
            lblStatus.Text = s;
            UpdatePreview();
        }

        // ------------------------------------------------------------------ Uygula / geri al
        void ApplyClicked()
        {
            var m = Selected;
            if (m == null) return;
            cfg.MonitorPath = m.DevicePath;
            cfg.Edge = (Edge)cbEdge.SelectedIndex;
            cfg.Cut = (int)num.Value;
            if (cfg.Cut <= 0) return;

            if (cfg.Mode == Mode.Software)
            {
                if (!StartShield(m.GdiName))
                {
                    MessageBox.Show(this, L.T("Monitör bulunamadı.", "Monitor not found."), Text);
                    return;
                }
                cfg.Active = true;
                cfg.ActiveMode = Mode.Software;
                cfg.Save();
                UpdateState();
                return;
            }

            busy = true;
            UpdateState();
            Cursor = Cursors.WaitCursor;
            ApplyResult res;
            try { res = DisplayConfig.TryApply(m.DevicePath, cfg.Edge, cfg.Cut); }
            finally { Cursor = Cursors.Default; }

            if (!res.Ok)
            {
                busy = false;
                RefreshMonitors();
                MessageBox.Show(this, res.Log + "\n" + L.T("Yazılım modunu deneyebilirsin.", "You can try software mode instead."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool keep;
            using (var dlg = new ConfirmDialog(SafeScreenRect(m.GdiName)))
                keep = dlg.ShowDialog() == DialogResult.OK;

            if (keep)
            {
                int r = DisplayConfig.Keep(res.Applied);
                if (r == 0)
                {
                    cfg.Cut = res.ActualCut;
                    cfg.Active = true;
                    cfg.ActiveMode = Mode.Driver;
                    cfg.Save();
                }
                else
                {
                    DisplayConfig.Restore(res.Original);
                    MessageBox.Show(this, L.T("Kaydedilemedi: ", "Could not save: ") + DisplayConfig.Err(r), Text);
                }
            }
            else DisplayConfig.Restore(res.Original);

            busy = false;
            RefreshMonitors();
        }

        void RevertClicked()
        {
            var m = Selected;
            if (cfg.Active && cfg.ActiveMode == Mode.Software) StopShield();

            string path = cfg.Active ? cfg.MonitorPath : (m != null ? m.DevicePath : "");
            bool needDriverReset = (cfg.Active && cfg.ActiveMode == Mode.Driver) || (m != null && m.IsModified);
            if (needDriverReset && path != "")
            {
                busy = true;
                int r = DisplayConfig.Reset(path);
                busy = false;
                if (r != 0)
                    MessageBox.Show(this, L.T("Geri alınamadı: ", "Could not revert: ") + DisplayConfig.Err(r) + "\n" +
                        L.T("Windows Ekran Ayarları'ndan çözünürlüğü önerilen değere getirebilirsin.",
                            "You can set the recommended resolution in Windows Display Settings."), Text);
            }
            cfg.Active = false;
            cfg.Save();
            RefreshMonitors();
        }

        void EmergencyReset()
        {
            StopShield();
            string log = DisplayConfig.ResetAll();
            cfg.Active = false;
            cfg.Save();
            RefreshMonitors();
            MessageBox.Show(this, log, Text);
        }

        // Onay penceresini mümkünse başka (sağlam) bir monitöre koy
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
            if (target == null) { Raise(L.T("Ayarlı monitör bağlı değil.", "The configured monitor is not connected.")); return; }

            if (cfg.ActiveMode == Mode.Software)
            {
                if (!StartShield(target.GdiName)) Raise(L.T("Yazılım modu başlatılamadı.", "Could not start software mode."));
                return;
            }
            if (DisplayConfig.IsApplied(cfg.MonitorPath, cfg.Edge, cfg.Cut)) return;

            // Windows ayarı sıfırlamış (ör. sürücü güncellemesi): yeniden uygula
            var res = DisplayConfig.TryApply(cfg.MonitorPath, cfg.Edge, cfg.Cut);
            if (res.Ok && DisplayConfig.Keep(res.Applied) == 0) Raise(L.T("Ekran ayarı yeniden uygulandı.", "Display setting re-applied."));
            else
            {
                if (res.Original != null && res.Applied != null) DisplayConfig.Restore(res.Original);
                Raise(L.T("Ekran ayarı yeniden uygulanamadı. Uygulamayı açıp tekrar dene.", "Could not re-apply. Open the app and try again."));
            }
            RefreshMonitors();
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
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        static void SetStartup(bool on)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                else k.DeleteValue(RunName, false);
            }
        }

        // ------------------------------------------------------------------ Pencere
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) RefreshMonitors();
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
            overlay.Close();
            base.OnFormClosing(e);
        }
    }
}
