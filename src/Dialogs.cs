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

    // "Böyle kalsın mı?" penceresi; süre dolarsa geri alır
    class ConfirmDialog : Form
    {
        int left = 15;
        readonly Label label;
        readonly Timer timer;

        public ConfirmDialog(Rectangle placeOn)
        {
            Text = "Ekran Kurtar";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Font = new Font("Segoe UI", 10f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16);

            var panel = new FlowLayoutPanel();
            panel.FlowDirection = FlowDirection.TopDown;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            label = new Label();
            label.AutoSize = true;
            label.MaximumSize = new Size(420, 0);
            panel.Controls.Add(label);

            var buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Margin = new Padding(0, 12, 0, 0);
            var keep = new Button();
            keep.Text = L.T("Böyle kalsın", "Keep changes");
            keep.AutoSize = true;
            keep.DialogResult = DialogResult.OK;
            var revert = new Button();
            revert.Text = L.T("Geri al", "Revert");
            revert.AutoSize = true;
            revert.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(keep);
            buttons.Controls.Add(revert);
            panel.Controls.Add(buttons);
            Controls.Add(panel);

            AcceptButton = revert;   // Enter yanlışlıkla basılırsa güvenli taraf
            CancelButton = revert;

            UpdateText();
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += delegate
            {
                left--;
                if (left <= 0) { timer.Stop(); DialogResult = DialogResult.Cancel; Close(); return; }
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
            label.Text = L.T(
                "Masaüstü artık sadece sağlam kısımda mı görünüyor?\n\nOnaylamazsan " + left + " saniye içinde eski haline dönecek.",
                "Is the desktop now shown only on the working part?\n\nReverting in " + left + " seconds unless you confirm.");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            base.OnFormClosed(e);
        }
    }
}
