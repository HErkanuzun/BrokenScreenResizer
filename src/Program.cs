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
            if (cfg.Lang != "") L.Set(cfg.Lang);
            form = new MainForm(cfg);

            tray = new NotifyIcon();
            tray.Icon = Theme.AppIcon(16);
            tray.Text = "Ekran Kurtar";
            tray.ContextMenuStrip = BuildMenu();
            form.LanguageChanged += delegate { var old = tray.ContextMenuStrip; tray.ContextMenuStrip = BuildMenu(); old.Dispose(); };
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowForm(); };

            form.Notify += delegate (string msg) { tray.ShowBalloonTip(4000, "Ekran Kurtar", msg, ToolTipIcon.Info); };

            // İkinci kez açılınca pencereyi göster; "--reset" gelince düzgünce kapan
            invoker = new Control();
            invoker.CreateControl();
            var signalThread = new Thread(ListenSignals);
            signalThread.IsBackground = true;
            signalThread.Start();

            form.RestoreActive();

            if (!startHidden) ShowForm();
        }

        public const string MutexName = "EkranKurtar_SingleInstance";
        public const string ShowEvent = "EkranKurtar_Show", ExitEvent = "EkranKurtar_Exit";
        readonly Control invoker;
        readonly EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent);
        readonly EventWaitHandle exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEvent);

        // Başka bir Ekran Kurtar işleminden çalışan kopyaya sinyal gönderir
        public static bool Signal(string name)
        {
            try
            {
                using (var e = EventWaitHandle.OpenExisting(name)) return e.Set();
            }
            catch { return false; }
        }

        void ListenSignals()
        {
            var handles = new WaitHandle[] { showSignal, exitSignal };
            while (true)
            {
                int i = WaitHandle.WaitAny(handles);
                try
                {
                    if (i == 0) invoker.BeginInvoke((MethodInvoker)ShowForm);
                    else { invoker.BeginInvoke((MethodInvoker)Quit); return; }
                }
                catch { return; }
            }
        }

        ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("Aç", "Open"), null, delegate { ShowForm(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Çıkış", "Exit"), null, delegate { Quit(); });
            return menu;
        }

        void ShowForm()
        {
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.BringIntoView();
            form.Activate();
        }

        void Quit()
        {
            form.Shutdown();
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
            // Yönetici olarak açılan yardımcı çağrılar: gömülü sanal ekran sürücüsünü kur / kaldır
            if (args.Length == 1 && args[0] == "--install-driver")
            {
                Environment.Exit(DriverSetup.Install());
                return;
            }
            if (args.Length >= 1 && args[0] == "--uninstall-driver")
            {
                // Kullanıcı doğrudan çalıştırdıysa kendini yönetici olarak yeniden başlatır
                int ur = args.Length == 2 && args[1] == "--elevated"
                    ? DriverSetup.Uninstall()
                    : DriverSetup.RunElevated("--uninstall-driver --elevated");
                if (args.Length == 1)
                    MessageBox.Show(ur == 0 ? L.T("Sanal ekran sürücüsü kaldırıldı.", "The virtual display driver was removed.")
                                            : L.T("Sürücü kaldırılamadı: ", "Could not remove the driver: ") + DriverSetup.Err(ur), "Ekran Kurtar");
                Environment.Exit(ur);
                return;
            }

            bool tray = false, reset = false;
            foreach (var a in args)
            {
                if (a == "--tray") tray = true;
                if (a == "--reset") reset = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var saved = Settings.Load();
            if (saved.Lang != "") L.Set(saved.Lang);

            // Acil durum: EkranKurtar.exe --reset
            if (reset)
            {
                // Çalışan Ekran Kurtar varsa önce onu düzgünce kapat (aynayı, fare kilidini bırakır)
                TrayApp.Signal(TrayApp.ExitEvent);
                using (var m = new Mutex(false, TrayApp.MutexName))
                {
                    try { if (m.WaitOne(10000)) m.ReleaseMutex(); }
                    catch (AbandonedMutexException) { try { m.ReleaseMutex(); } catch { } }
                }
                DisplayLayout.RestoreBackup();
                Vdd.RemoveAll();
                string log = DisplayConfig.ResetAll();
                var s = Settings.Load();
                s.Active = false;
                s.Save();
                MessageBox.Show(log, "Ekran Kurtar");
                return;
            }

            bool created;
            using (var mtx = new Mutex(true, TrayApp.MutexName, out created))
            {
                if (!created)
                {
                    // Zaten çalışıyor: çalışan kopyanın penceresini görünür yere getir
                    if (!TrayApp.Signal(TrayApp.ShowEvent))
                        MessageBox.Show(L.T("Ekran Kurtar zaten çalışıyor (sağ alttaki simge).", "Ekran Kurtar is already running (tray icon)."), "Ekran Kurtar");
                    return;
                }
                Application.Run(new TrayApp(tray));
            }
        }
    }
}
