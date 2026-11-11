using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Tanı günlüğü (v1.9): %AppData%\BrokenScreenResizer\log.txt
    // Açılışta ayarın geri yüklenmesi ve aynanın durumu (yeniden kurulma, yavaş kareler) yazılır.
    // Sadece bu bilgisayarda kalır, hiçbir yere gönderilmez. 256 KB'ı geçince eski kısım log.old.txt olur.
    // ===================================================================================
    static class Log
    {
        static readonly object gate = new object();
        const long MaxSize = 256 * 1024;

        public static string FilePath { get { return Path.Combine(Settings.Dir, "log.txt"); } }

        public static void Write(string msg)
        {
            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(Settings.Dir);
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > MaxSize)
                    {
                        string old = Path.Combine(Settings.Dir, "log.old.txt");
                        try { File.Delete(old); } catch { }
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // ===================================================================================
    // Öncelik ve tam ekran algılama (v1.9)
    // ===================================================================================
    static class Perf
    {
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool SetPriorityClass(IntPtr process, uint cls);
        [DllImport("gdi32.dll")] static extern int D3DKMTSetProcessSchedulingPriorityClass(IntPtr process, int cls);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x8000;
        const int GPU_HIGH = 4;   // D3DKMT_SCHEDULINGPRIORITYCLASS_HIGH

        // Ayna çalışırken: ağır bir oyun ekran kartını (GPU) ya da işlemciyi tam kullansa bile
        // aynanın kareleri sırada beklemesin. Yönetici izni gerektirmez; olmazsa normal öncelikte kalır.
        public static void Boost()
        {
            IntPtr me = GetCurrentProcess();
            bool cpu = false;
            int gpu = -1;
            try { cpu = SetPriorityClass(me, ABOVE_NORMAL_PRIORITY_CLASS); } catch { }
            try { gpu = D3DKMTSetProcessSchedulingPriorityClass(me, GPU_HIGH); } catch { }
            Log.Write("priority: cpu above-normal=" + cpu + ", gpu high=" + (gpu == 0 ? "ok" : "0x" + gpu.ToString("X8")));
        }

        // Öndeki program tam ekran mı (oyun, video, sunum)?
        public static bool FullScreenApp
        {
            get
            {
                int s;
                try
                {
                    if (SHQueryUserNotificationState(out s) != 0) return false;
                }
                catch { return false; }
                // 2 = QUNS_BUSY (tam ekran uygulama), 3 = QUNS_RUNNING_D3D_FULL_SCREEN, 4 = QUNS_PRESENTATION_MODE
                return s == 2 || s == 3 || s == 4;
            }
        }
    }
}
