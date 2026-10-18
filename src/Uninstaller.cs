using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Tam kaldırma: "BrokenScreenResizer.exe --uninstall" (ya da Uninstall.bat, uygulamadaki "Programı kaldır").
    // Program kurulum gerektirmez; bu yüzden kaldırma, programın bilgisayarda bıraktığı her şeyi
    // (yeni ve eski "Ekran Kurtar" adıyla) temizler:
    //   1) çalışan kopyayı kapatır, ekran düzenini / kırpılmış monitörleri / NVIDIA warp'ı eski haline getirir
    //   2) "Windows ile başlat" kaydını siler
    //   3) yönetici izniyle: sanal ekran sürücüsü, hayalet monitörler, sürücü paketleri, sertifikalar,
    //      Windows'un sanal monitör kayıtları, ProgramData klasörleri (bkz. DriverSetup.Purge)
    //   4) ayar klasörlerini (%AppData%) siler
    //   5) istenirse programın kendisini ve Uninstall.bat'i siler
    // ===================================================================================
    static class Uninstaller
    {
        public const int Ok = 0, Cancelled = 1223, Partial = 1;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Uygulamanın içinden: ayrı bir işlem olarak başlatır (o işlem bu kopyayı da düzgünce kapatır)
        public static void Launch()
        {
            try { Process.Start(Application.ExecutablePath, "--uninstall"); }
            catch { }
        }

        public static int Run()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.S = g.DpiX / 96f; } catch { }

            if (!MsgDialog.Ask(null, L.T(
                    "Broken Screen Resizer bilgisayarından tamamen kaldırılsın mı?\n\n" +
                    "Silinecekler: ayarlar, \"Windows ile başlat\" kaydı, ekran değişiklikleri (ekranlar eski haline döner), " +
                    "sanal ekran sürücüsü ve sertifikası, Windows'un sanal monitör kayıtları. Eski \"Ekran Kurtar\" kalıntıları da temizlenir.\n\n" +
                    "Sürücü kuruluysa Windows yönetici izni isteyecek.",
                    "Remove Broken Screen Resizer completely from this computer?\n\n" +
                    "This removes: settings, the \"Start with Windows\" entry, display changes (screens go back to normal), " +
                    "the virtual display driver and its certificate, and Windows' records of the virtual monitors. Leftovers of the old \"Ekran Kurtar\" name are removed too.\n\n" +
                    "If the driver is installed, Windows will ask for administrator permission."),
                    L.T("Kaldır", "Uninstall"), L.T("Vazgeç", "Cancel")))
                return Cancelled;

            var report = new List<string>();
            var left = new List<string>();
            var wait = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            // 1) çalışan kopyaları kapat ve ekranı eski haline getir
            StopRunning();
            try
            {
                MoveLegacyBackup();
                if (DisplayLayout.HasBackup)
                {
                    int r = DisplayLayout.RestoreBackup();
                    if (r != 0) left.Add(L.T("Ekran düzeni geri yüklenemedi: ", "Could not restore the display layout: ") + DisplayLayout.Err(r));
                }
                Vdd.RemoveAll();
                DisplayConfig.ResetAll();
                report.Add(L.T("Ekranlar eski haline getirildi.", "Displays restored."));
            }
            catch (Exception ex) { left.Add(L.T("Ekran sıfırlama: ", "Display reset: ") + ex.Message); }

            // 2) başlangıç kaydı
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null)
                    {
                        k.DeleteValue(Settings.AppName, false);
                        k.DeleteValue(Settings.LegacyName, false);
                    }
                report.Add(L.T("\"Windows ile başlat\" kaydı silindi.", "\"Start with Windows\" entry removed."));
            }
            catch (Exception ex) { left.Add(L.T("Başlangıç kaydı: ", "Startup entry: ") + ex.Message); }

            // Geliştiricinin bu bilgisayarda oluşturduğu imzalama sertifikası (sadece driver\build_driver.bat çalıştırıldıysa vardır)
            try
            {
                var st = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                st.Open(OpenFlags.ReadWrite);
                try
                {
                    foreach (var s in new[] { Vdd.CertSubject, Vdd.LegacyCertSubject })
                        foreach (X509Certificate2 c in st.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, s, false)) st.Remove(c);
                }
                finally { st.Close(); }
            }
            catch { }

            // 3) yönetici gerektiren kalıntılar
            if (DriverSetup.NeedsPurge())
            {
                Cursor.Current = wait;
                int r = DriverSetup.RunElevated("--uninstall-system");
                Cursor.Current = Cursors.WaitCursor;
                if (r == DriverSetup.Cancelled)
                    left.Add(L.T("Yönetici izni verilmediği için sanal ekran sürücüsü ve sistem kayıtları silinmedi. Kaldırmayı tekrar çalıştırabilirsin.",
                                 "Administrator permission was not given, so the virtual display driver and system records were not removed. You can run the uninstaller again."));
                else if (r != 0 || DriverSetup.NeedsPurge())
                    left.Add(L.T("Bazı sistem kalıntıları silinemedi (", "Some system leftovers could not be removed (") + DriverSetup.Err(r) +
                             L.T("). Bilgisayarı yeniden başlatıp kaldırmayı tekrar çalıştır.", "). Restart the computer and run the uninstaller again."));
                else
                    report.Add(L.T("Sanal ekran sürücüsü, sertifikalar ve Windows kayıtları silindi.", "Virtual display driver, certificates and Windows records removed."));
            }

            // 4) ayar klasörleri
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var name in new[] { Settings.AppName, Settings.LegacyName })
            {
                string d = Path.Combine(appData, name);
                if (!Directory.Exists(d)) continue;
                try { Directory.Delete(d, true); }
                catch (Exception ex) { left.Add(d + ": " + ex.Message); }
            }
            report.Add(L.T("Ayarlar silindi.", "Settings removed."));
            Cursor.Current = wait;

            // 5) sonuç ve programın kendisi
            var sb = new StringBuilder();
            sb.AppendLine(left.Count == 0 ? L.T("Broken Screen Resizer kaldırıldı.", "Broken Screen Resizer was removed.")
                                          : L.T("Kaldırma tamamlandı, ama bazı şeyler silinemedi:", "Uninstall finished, but some items could not be removed:"));
            sb.AppendLine();
            foreach (var s in left) sb.AppendLine("• " + s);
            if (left.Count > 0) sb.AppendLine();
            foreach (var s in report) sb.AppendLine("✓ " + s);
            sb.AppendLine();
            sb.Append(L.T("Programın kendisi de (", "Delete the program file too (") + Path.GetFileName(Application.ExecutablePath) +
                      L.T(" ve Uninstall.bat) silinsin mi?", " and Uninstall.bat)?"));

            if (MsgDialog.Ask(null, sb.ToString(), L.T("Programı da sil", "Delete the program"), L.T("Kalsın", "Keep it")))
                DeleteSelf();
            return left.Count == 0 ? Ok : Partial;
        }

        // Çalışan Broken Screen Resizer / Ekran Kurtar kopyalarını düzgünce kapatır (ayna ve fare kilidi bırakılır)
        static void StopRunning()
        {
            TrayApp.Signal(TrayApp.ExitEvent);
            TrayApp.Signal("EkranKurtar_Exit");
            WaitMutex(TrayApp.MutexName, 10000);
            WaitMutex("EkranKurtar_SingleInstance", 5000);

            // eski sürümler sinyali tanımaz: zorla kapat (ekran düzeni sonra zaten geri yükleniyor)
            int me = Process.GetCurrentProcess().Id;
            foreach (var name in new[] { Settings.AppName, Settings.LegacyName })
                foreach (var p in Process.GetProcessesByName(name))
                    try
                    {
                        if (p.Id == me) continue;
                        if (!p.WaitForExit(1500)) { p.Kill(); p.WaitForExit(3000); }
                    }
                    catch { }
                    finally { p.Dispose(); }
        }

        static void WaitMutex(string name, int ms)
        {
            try
            {
                using (var m = new Mutex(false, name))
                {
                    try { if (m.WaitOne(ms)) m.ReleaseMutex(); }
                    catch (AbandonedMutexException) { try { m.ReleaseMutex(); } catch { } }
                }
            }
            catch { }
        }

        // Eski adla kaydedilmiş ekran düzeni yedeği varsa onu da geri yükleyebilmek için yeni klasöre alır
        static void MoveLegacyBackup()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string old = Path.Combine(appData, Settings.LegacyName, "layout_backup.txt");
                string now = Path.Combine(Settings.Dir, "layout_backup.txt");
                if (File.Exists(old) && !File.Exists(now))
                {
                    Directory.CreateDirectory(Settings.Dir);
                    File.Copy(old, now);
                }
            }
            catch { }
        }

        // Program kapandıktan birkaç saniye sonra exe'yi ve yanındaki Uninstall.bat'i siler
        static void DeleteSelf()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string bat = Path.Combine(Path.GetDirectoryName(exe), "Uninstall.bat");
                string cmd = "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + exe + "\"";
                if (File.Exists(bat)) cmd += " & del /f /q \"" + bat + "\"";
                var psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }
    }
}
