using System;
using System.Drawing;
using System.Windows.Forms;

namespace EkranKurtar
{
    // Ayar yaparken kırık alanı kırmızı gösteren, tıklamaları geçiren yarı saydam katman
    class PreviewOverlay : Form
    {
        public PreviewOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Red;
            Opacity = 0.45;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x20 | 0x80000 | 0x08000000 | 0x8; // TOOLWINDOW | TRANSPARENT | LAYERED | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public void ShowAt(Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) { Hide(); return; }
            Bounds = r;
            if (!Visible) Show();
        }
    }

    // Koyu temalı küçük pencere tabanı
    class DarkDialog : Form
    {
        public DarkDialog()
        {
            Text = "Ekran Kurtar";
            Icon = Theme.AppIcon(32);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font(L.FontName, 9.5f);
            TopMost = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitle(this);
        }

        protected Label MakeLabel(string text, float size, bool bold, Color color, int width)
        {
            var l = new Label();
            l.Text = text;
            l.Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular);
            l.ForeColor = color;
            l.BackColor = Theme.Bg;
            l.AutoSize = true;
            l.MaximumSize = new Size(width, 0);
            Controls.Add(l);
            return l;
        }
    }

    // "Böyle kalsın mı?" penceresi; süre dolarsa geri alır
    class ConfirmDialog : DarkDialog
    {
        const int Seconds = 15;
        readonly DateTime end;
        readonly Label lblTitle, lblText;
        readonly Timer timer;
        readonly Panel bar;
        readonly int barW;

        public ConfirmDialog(Rectangle placeOn)
        {
            StartPosition = FormStartPosition.Manual;
            int W = Theme.P(460), x0 = Theme.P(24), w = W - x0 * 2;

            lblTitle = MakeLabel(L.T("Görüntü doğru mu?", "Does it look right?"), 13f, true, Theme.Text, w);
            lblTitle.Location = new Point(x0, Theme.P(20));
            lblText = MakeLabel("", 9.5f, false, Theme.Sub, w);
            lblText.Location = new Point(x0, Theme.P(56));

            var track = new Panel();
            track.BackColor = Theme.Field;
            track.SetBounds(x0, Theme.P(130), w, Theme.P(4));
            bar = new Panel();
            bar.BackColor = Theme.Accent;
            barW = w;
            bar.SetBounds(0, 0, w, Theme.P(4));
            track.Controls.Add(bar);
            Controls.Add(track);

            var revert = new FlatButton(L.T("Geri al", "Revert"), BtnKind.Secondary);
            revert.Font = Font;
            revert.SetBounds(W - x0 - Theme.P(150) - Theme.P(12) - Theme.P(110), Theme.P(152), Theme.P(110), Theme.P(40));
            revert.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            var keep = new FlatButton(L.T("Böyle kalsın", "Keep changes"), BtnKind.Primary);
            keep.Font = Font;
            keep.SetBounds(W - x0 - Theme.P(150), Theme.P(152), Theme.P(150), Theme.P(40));
            keep.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(revert);
            Controls.Add(keep);
            ClientSize = new Size(W, Theme.P(212));
            KeyPreview = true;
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) { DialogResult = DialogResult.Cancel; Close(); } };

            end = DateTime.Now.AddSeconds(Seconds);
            UpdateText();
            timer = new Timer();
            timer.Interval = 50;
            timer.Tick += delegate
            {
                if (DateTime.Now >= end) { timer.Stop(); DialogResult = DialogResult.Cancel; Close(); return; }
                UpdateText();
            };

            Load += delegate
            {
                Location = new Point(placeOn.Left + (placeOn.Width - Width) / 2, placeOn.Top + (placeOn.Height - Height) / 2);
                timer.Start();
            };
        }

        void UpdateText()
        {
            double left = Math.Max(0, (end - DateTime.Now).TotalSeconds);
            string t = string.Format(L.T(
                "Masaüstü artık sadece sağlam kısımda mı görünüyor?\n\nOnaylamazsan {0} saniye içinde eski haline dönecek.",
                "Is the desktop now shown only on the working part?\n\nReverting in {0} seconds unless you confirm."), (int)Math.Ceiling(left));
            if (lblText.Text != t) lblText.Text = t;
            bar.Width = (int)(barW * left / Seconds);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            base.OnFormClosed(e);
        }
    }

    // Koyu temalı mesaj kutusu
    class MsgDialog : DarkDialog
    {
        MsgDialog(string text, bool warning)
        {
            StartPosition = FormStartPosition.CenterParent;
            int W = Theme.P(480), x0 = Theme.P(24), w = W - x0 * 2;

            var dot = new Panel();
            dot.BackColor = warning ? Theme.Warn : Theme.Accent;
            dot.SetBounds(x0, Theme.P(26), Theme.P(4), Theme.P(22));
            Controls.Add(dot);

            var title = MakeLabel(warning ? L.T("Dikkat", "Attention") : "Ekran Kurtar", 12f, true, Theme.Text, w);
            title.Location = new Point(x0 + Theme.P(14), Theme.P(24));

            var body = MakeLabel(text.TrimEnd(), 9.5f, false, Theme.Sub, w);
            body.Location = new Point(x0, Theme.P(64));
            int bodyH = body.PreferredHeight;
            using (var g = CreateGraphics())
                bodyH = (int)Math.Ceiling(g.MeasureString(body.Text, body.Font, w).Height);

            int by = Theme.P(64) + bodyH + Theme.P(20);
            var ok = new FlatButton(L.T("Tamam", "OK"), BtnKind.Primary);
            ok.Font = Font;
            ok.SetBounds(W - x0 - Theme.P(110), by, Theme.P(110), Theme.P(40));
            ok.Click += delegate { Close(); };
            Controls.Add(ok);
            ClientSize = new Size(W, by + Theme.P(40) + Theme.P(20));
            KeyPreview = true;
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close(); };
        }

        public static void Show(IWin32Window owner, string text, bool warning)
        {
            using (var d = new MsgDialog(text, warning)) d.ShowDialog(owner);
        }
    }
}
