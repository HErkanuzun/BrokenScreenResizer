using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace BrokenScreenResizer
{
    // Çalışan Broken Screen Resizer / Ekran Kurtar kopyaları ve sürümleri.
    // Yeni sürüm açılınca eski sürüm çalışıyorsa eskisi düzgünce kapatılır, yerini yeni sürüm alır.
    static class Instances
    {
        public const string LegacyMutex = "EkranKurtar_SingleInstance", LegacyExit = "EkranKurtar_Exit";

        public static Version Mine
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        // Bir exe dosyasının sürümü; sürüm bilgisi olmayan eski exe'ler (v0.7 ve öncesi) 0.0 sayılır
        public static Version FileVersionOf(string path)
        {
            try
            {
                var fv = FileVersionInfo.GetVersionInfo(path);
                return new Version(fv.FileMajorPart, fv.FileMinorPart, fv.FileBuildPart, fv.FilePrivatePart);
            }
            catch { return new Version(0, 0); }
        }

        // Bu işlem programın kendisi mi (testler ve yardımcı araçlar değil)
        public static bool IAmTheApp
        {
            get { return OurName(Assembly.GetExecutingAssembly().GetName().Name); }
        }

        static bool OurName(string n)
        {
            return n.StartsWith(Settings.AppName, StringComparison.OrdinalIgnoreCase) ||
                   n.StartsWith(Settings.LegacyName, StringComparison.OrdinalIgnoreCase);
        }

        // Programın bir kopyası mı? exe yeniden adlandırılmış olabilir (ör. "BrokenScreenResizer-v1.6.exe").
        // Program bir .NET derlemesidir; kurulum programı (…-Setup.exe) .NET olmadığı için hiçbir zaman eşleşmez.
        // Dönüş: exe yolu; bizim değilse ya da erişilemiyorsa (ör. yönetici olarak çalışan yardımcı) null
        static string OurPath(Process p)
        {
            if (!OurName(p.ProcessName)) return null;
            try
            {
                string path = p.MainModule.FileName;
                return OurName(AssemblyName.GetAssemblyName(path).Name) ? path : null;
            }
            catch { return null; }
        }

        // Çalışan diğer kopyaların en yeni sürümü; hiç yoksa null
        public static Version RunningVersion()
        {
            Version best = null;
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
                try
                {
                    if (p.Id == me) continue;
                    string path = OurPath(p);
                    if (path == null) continue;
                    var v = FileVersionOf(path);
                    if (best == null || v > best) best = v;
                }
                catch { }
                finally { p.Dispose(); }
            return best;
        }

        public static bool LegacyRunning
        {
            get
            {
                try { using (Mutex.OpenExisting(LegacyMutex)) return true; }
                catch { return false; }
            }
        }

        // Diğer kopyaları düzgünce kapatır (ayna, fare kilidi ve ekran düzeni bırakılır); kapanmayanları sonlandırır
        public static void StopOthers()
        {
            TrayApp.Signal(TrayApp.ExitEvent);
            TrayApp.Signal(LegacyExit);
            WaitMutex(TrayApp.MutexName, 10000);
            WaitMutex(LegacyMutex, 5000);

            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
                try
                {
                    if (p.Id == me || OurPath(p) == null) continue;
                    if (!p.WaitForExit(1500)) { p.Kill(); p.WaitForExit(3000); }
                }
                catch { }
                finally { p.Dispose(); }
        }

        static void WaitMutex(string name, int ms)
        {
            try
            {
                using (var m = Mutex.OpenExisting(name))
                {
                    try { if (m.WaitOne(ms)) m.ReleaseMutex(); }
                    catch (AbandonedMutexException) { try { m.ReleaseMutex(); } catch { } }
                }
            }
            catch { }   // mutex yok: çalışan kopya yok
        }

        // Program kurulum paketiyle kurulduysa (Inno Setup kaldırıcısı exe'nin yanında durur)
        public static string SetupUninstaller
        {
            get
            {
                try
                {
                    string p = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "unins000.exe");
                    return File.Exists(p) ? p : null;
                }
                catch { return null; }
            }
        }
    }
}
