using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace EkranKurtar
{
    class TrayApp : ApplicationContext
    {
        readonly MainForm form;
        readonly NotifyIcon tray;

        public TrayApp(bool startHidden)
        {
            var cfg = Settings.Load();
            form = new MainForm(cfg);

            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("Aç", "Open"), null, delegate { ShowForm(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Çıkış", "Exit"), null, delegate { Quit(); });

            tray = new NotifyIcon();
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { tray.Icon = SystemIcons.Shield; }
            tray.Text = "Ekran Kurtar";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowForm(); };

            form.Notify += delegate (string msg) { tray.ShowBalloonTip(4000, "Ekran Kurtar", msg, ToolTipIcon.Info); };
            form.RestoreActive();

            if (!startHidden) ShowForm();
        }

        void ShowForm()
        {
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        void Quit()
        {
            form.ExitRequested = true;
            form.Close();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool tray = false, reset = false;
            foreach (var a in args)
            {
                if (a == "--tray") tray = true;
                if (a == "--reset") reset = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Acil durum: EkranKurtar.exe --reset
            if (reset)
            {
                string log = DisplayConfig.ResetAll();
                var s = Settings.Load();
                s.Active = false;
                s.Save();
                MessageBox.Show(log, "Ekran Kurtar");
                return;
            }

            bool created;
            using (var mtx = new Mutex(true, "EkranKurtar_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show(L.T("Ekran Kurtar zaten çalışıyor (sağ alttaki simge).", "Ekran Kurtar is already running (tray icon)."), "Ekran Kurtar");
                    return;
                }
                Application.Run(new TrayApp(tray));
            }
        }
    }
}
