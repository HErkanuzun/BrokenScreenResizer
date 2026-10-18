using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
    //   5) programın klasörünü gösterir (program kendini silmez; klasörü kullanıcı siler)
    // ===================================================================================
    static class Uninstaller
    {
        public const int Ok = 0, Cancelled = 1223, Partial = 1;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Uygulamanın içinden: ayrı bir işlem olarak başlatır (o işlem bu kopyayı da düzgünce kapatır).
        // Program kurulum paketiyle kurulduysa Windows'un kaldırıcısı açılır; o da aşağıdaki temizliği çağırır.
        public static void Launch()
        {
            try
            {
                string unins = Instances.SetupUninstaller;
                if (unins != null) Process.Start(unins);
                else Process.Start(Application.ExecutablePath, "--uninstall");
            }
            catch { }
        }

        // silent: kurulum paketinin kaldırıcısından çağrılır (onay ve kapanış soruları o pencerede sorulur);
        // sadece silinemeyen bir şey kalırsa bildirir
        public static int Run(bool silent)
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.S = g.DpiX / 96f; } catch { }

            if (!silent && !MsgDialog.Ask(null, L.T(
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
            Instances.StopOthers();
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

            // Not: geliştiricinin sürücü imzalama sertifikasına (CurrentUser\My, driver\sign_driver.ps1) dokunulmaz;
            // o programın bıraktığı bir kalıntı değil, sürücüyü yeniden imzalamak için gereken anahtardır.

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
            if (silent)
            {
                if (left.Count > 0)
                    MsgDialog.Show(null, L.T("Kaldırma tamamlandı, ama bazı şeyler silinemedi:", "Uninstall finished, but some items could not be removed:") +
                        "\n\n• " + string.Join("\n• ", left.ToArray()), true);
                return left.Count == 0 ? Ok : Partial;
            }
            var sb = new StringBuilder();
            sb.AppendLine(left.Count == 0 ? L.T("Broken Screen Resizer kaldırıldı.", "Broken Screen Resizer was removed.")
                                          : L.T("Kaldırma tamamlandı, ama bazı şeyler silinemedi:", "Uninstall finished, but some items could not be removed:"));
            sb.AppendLine();
            foreach (var s in left) sb.AppendLine("• " + s);
            if (left.Count > 0) sb.AppendLine();
            foreach (var s in report) sb.AppendLine("✓ " + s);
            sb.AppendLine();
            sb.Append(L.T("Program kurulum gerektirmediği için geriye sadece indirdiğin klasör kaldı. Klasörü açıp silebilirsin.",
                          "The app needs no installation, so only the folder you downloaded is left. You can open it and delete it."));

            // Program kendini silmez (gizli komutla kendini silen programlar virüs gibi algılanır); klasörü gösterir
            if (MsgDialog.Ask(null, sb.ToString(), L.T("Klasörü aç", "Open folder"), L.T("Kapat", "Close")))
                ShowFolder();
            return left.Count == 0 ? Ok : Partial;
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

        // Programın klasörünü Dosya Gezgini'nde, exe seçili olarak açar
        static void ShowFolder()
        {
            try { Process.Start("explorer.exe", "/select,\"" + Application.ExecutablePath + "\""); }
            catch { }
        }
    }
}
