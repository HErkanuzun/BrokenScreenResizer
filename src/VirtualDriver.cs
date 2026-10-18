using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Broken Screen Resizer'ın kendi sanal ekran sürücüsü (driver\ klasörü) ile iletişim.
    //  - Sürücü exe'nin içine gömülüdür; "Sanal monitör" ilk kez uygulanırken kurulur (DriverSetup).
    //  - Uygulama sürücünün aygıt arayüzünü açıp IOCTL ile istediği boyutta sanal monitör ekler/kaldırır.
    // Değerler driver\Public.h ile aynı olmalı.
    // ===================================================================================
    static class Vdd
    {
        public const string HardwareId = @"Root\BrokenScreenResizerVDD";
        // v0.6 öncesi ("Ekran Kurtar") sürücünün kimliği: kurulumda kaldırılır
        public const string LegacyHardwareId = @"Root\EkranKurtarVDD";
        public const string LegacyCertSubject = "CN=Ekran Kurtar Driver Signing";
        public const string CertSubject = "CN=Broken Screen Resizer Driver Signing";
        public const string DeviceName = "Broken Screen Resizer Virtual Display";
        static Guid InterfaceGuid = new Guid("69a8bde3-4c78-4ba6-ad55-75a6b2760ca4");

        const uint IOCTL_INFO = (0x22u << 16) | (0x900u << 2);
        const uint IOCTL_ADD = (0x22u << 16) | (0x901u << 2);
        const uint IOCTL_REMOVE = (0x22u << 16) | (0x902u << 2);
        const uint IOCTL_LIST = (0x22u << 16) | (0x903u << 2);

        public const int StatePending = 0, StateActive = 1, StateFailed = 2, StateRemoving = 3;
        public const int MaxMonitors = 4;

        public class MonitorState
        {
            public int Id, Width, Height, Refresh, State, Status;
        }

        // ------------------------------------------------------------------ P/Invoke
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        static extern int CM_Get_Device_Interface_List_SizeW(out int len, ref Guid g, string deviceId, int flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        static extern int CM_Get_Device_Interface_ListW(ref Guid g, string deviceId, char[] buffer, int len, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, int inLen, byte[] outBuf, int outLen, out int returned, IntPtr overlapped);

        const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, OPEN_EXISTING = 3;

        // ------------------------------------------------------------------ Sürücüye erişim
        static string InterfacePath()
        {
            try
            {
                int len;
                var g = InterfaceGuid;
                if (CM_Get_Device_Interface_List_SizeW(out len, ref g, null, 0) != 0 || len <= 1) return null;
                var buf = new char[len];
                if (CM_Get_Device_Interface_ListW(ref g, null, buf, len, 0) != 0) return null;
                string s = new string(buf).Split('\0')[0];
                return s.Length > 0 ? s : null;
            }
            catch { return null; }
        }

        // Sürücü kurulu ve çalışıyor mu
        public static bool Installed { get { return InterfacePath() != null; } }

        static bool Call(uint code, byte[] input, byte[] output, out int returned)
        {
            returned = 0;
            string path = InterfacePath();
            if (path == null) return false;
            using (var h = CreateFileW(path, GENERIC_READ | GENERIC_WRITE, 3, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return false;
                return DeviceIoControl(h, code, input, input == null ? 0 : input.Length,
                                       output, output == null ? 0 : output.Length, out returned, IntPtr.Zero);
            }
        }

        static byte[] Ints(params int[] v)
        {
            var b = new byte[v.Length * 4];
            for (int i = 0; i < v.Length; i++) BitConverter.GetBytes(v[i]).CopyTo(b, i * 4);
            return b;
        }

        // Adaptör hazır mı (Windows sürücüyü başlattıktan sonra birkaç saniye sürebilir)
        public static bool AdapterReady
        {
            get
            {
                var o = new byte[16];
                int n;
                return Call(IOCTL_INFO, null, o, out n) && n >= 16 && BitConverter.ToInt32(o, 8) == 1;
            }
        }

        public static List<MonitorState> List()
        {
            var list = new List<MonitorState>();
            var o = new byte[24 * MaxMonitors];
            int n;
            if (!Call(IOCTL_LIST, null, o, out n)) return list;
            for (int i = 0; i + 24 <= n; i += 24)
                list.Add(new MonitorState
                {
                    Id = BitConverter.ToInt32(o, i), Width = BitConverter.ToInt32(o, i + 4), Height = BitConverter.ToInt32(o, i + 8),
                    Refresh = BitConverter.ToInt32(o, i + 12), State = BitConverter.ToInt32(o, i + 16), Status = BitConverter.ToInt32(o, i + 20)
                });
            return list;
        }

        // Dönüş: monitör kimliği (1..4) ya da 0
        public static int Add(int w, int h, int hz)
        {
            var o = new byte[4];
            int n;
            if (!Call(IOCTL_ADD, Ints(w, h, hz), o, out n) || n < 4) return 0;
            return BitConverter.ToInt32(o, 0);
        }

        public static void Remove(int id)
        {
            int n;
            Call(IOCTL_REMOVE, Ints(id), null, out n);
        }

        // Tüm sanal monitörleri kapatır (geri al / program kapanışı / acil durum)
        public static void RemoveAll()
        {
            try { if (Installed) Remove(0); } catch { }
        }

        // ------------------------------------------------------------------ Windows tarafı (GDI adları)
        static bool IsOurAdapter(VNative.DISPLAY_DEVICE dd)
        {
            string id = dd.DeviceID ?? "";
            return id.IndexOf("BrokenScreenResizerVDD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("EkranKurtarVDD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(dd.DeviceString, DeviceName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOurs(string gdi)
        {
            if (string.IsNullOrEmpty(gdi)) return false;
            var dd = new VNative.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            for (uint i = 0; VNative.EnumDisplayDevices(null, i, ref dd, 0); i++)
            {
                if (dd.DeviceName == gdi) return IsOurAdapter(dd);
                dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            }
            return false;
        }

        // Sanal monitörün GDI adı (\\.\DISPLAYn). w/h verilirse o çözünürlüğü sunan monitör aranır.
        public static string FindGdi(int w = 0, int h = 0)
        {
            var dd = new VNative.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            string any = null;
            for (uint i = 0; VNative.EnumDisplayDevices(null, i, ref dd, 0); i++)
            {
                if (IsOurAdapter(dd))
                {
                    string gdi = dd.DeviceName;
                    var mon = new VNative.DISPLAY_DEVICE();
                    mon.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
                    bool hasMonitor = VNative.EnumDisplayDevices(gdi, 0, ref mon, 0);
                    if (hasMonitor)
                    {
                        if (w <= 0 || HasMode(gdi, w, h)) return gdi;
                        if (any == null) any = gdi;
                    }
                }
                dd.cb = Marshal.SizeOf(typeof(VNative.DISPLAY_DEVICE));
            }
            return w <= 0 ? any : null;
        }

        public static bool HasMode(string gdi, int w, int h)
        {
            var dm = VNative.NewDevMode();
            for (int i = 0; VNative.EnumDisplaySettings(gdi, i, ref dm); i++)
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h) return true;
            return false;
        }

        static bool WaitUntil(Func<bool> cond, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (cond()) return true;
                Application.DoEvents();
                Thread.Sleep(200);
            }
            return cond();
        }

        public static bool WaitReady(int timeoutMs)
        {
            return WaitUntil(() => Installed && AdapterReady, timeoutMs);
        }

        // İstenen boyutta tek bir sanal monitör olmasını sağlar. Hata yoksa null döner.
        public static string Ensure(int w, int h, int hz, out string gdi)
        {
            gdi = null;
            if (!WaitReady(15000))
                return L.T("Sanal ekran sürücüsü hazır değil. Bilgisayarı yeniden başlatıp tekrar dene.",
                           "The virtual display driver is not ready. Restart the computer and try again.");

            // Farklı boyuttaki eski sanal monitörleri kapat
            foreach (var m in List())
                if (m.Width != w || m.Height != h || m.Refresh != hz) Remove(m.Id);

            int id = Add(w, h, hz);
            if (id == 0)
                return L.T("Sanal monitör eklenemedi.", "Could not add the virtual monitor.");

            MonitorState st = null;
            WaitUntil(() =>
            {
                st = List().Find(x => x.Id == id);
                return st == null || st.State != StatePending;
            }, 10000);
            if (st == null || st.State != StateActive)
                return L.T("Windows sanal monitörü kabul etmedi", "Windows did not accept the virtual monitor") +
                       (st != null ? " (0x" + st.Status.ToString("X8") + ")." : ".");

            string found = null;
            WaitUntil(() => (found = FindGdi(w, h)) != null, 10000);
            if (found == null)
                return L.T("Sanal monitör Windows'ta görünmedi. Bilgisayarı yeniden başlatıp tekrar dene.",
                           "The virtual monitor did not appear in Windows. Restart the computer and try again.");
            gdi = found;
            return null;
        }
    }

    // ===================================================================================
    // Sürücü kurulumu: exe'ye gömülü sürücü paketini çıkarır, imza sertifikasını güvenilenlere ekler,
    // sanal aygıtı oluşturur ve sürücüyü yükler. Yönetici olarak "--install-driver" ile çalışır.
    // ===================================================================================
    static class DriverSetup
    {
        public const int Ok = 0, Cancelled = 1223;
        const string ResPrefix = "BrokenScreenResizer.driver.";
        static readonly string[] Files = { "BrokenScreenResizerVDD.dll", "BrokenScreenResizerVDD.inf", "BrokenScreenResizerVDD.cat", "BrokenScreenResizerVDD.cer" };

        static string WorkDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"BrokenScreenResizer\driver"); }
        }
        static string LogPath { get { return Path.Combine(WorkDir, "install.log"); } }

        // ------------------------------------------------------------------ P/Invoke
        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public IntPtr Reserved;
        }

        static Guid DisplayClass = new Guid("4d36e968-e325-11ce-bfc1-08002be10318");
        const int DICD_GENERATE_ID = 1, DIF_REGISTERDEVICE = 0x19, DIF_REMOVE = 0x05, SPDRP_HARDWAREID = 1;
        const int DIGCF_PRESENT = 2, DIGCF_ALLCLASSES = 4, INSTALLFLAG_FORCE = 1;
        static readonly IntPtr INVALID = new IntPtr(-1);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid classGuid, IntPtr hwnd);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiCreateDeviceInfoW(IntPtr set, string name, ref Guid classGuid, string desc, IntPtr hwnd, int flags, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiSetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA data, int prop, byte[] buf, int size);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiCallClassInstaller(int fn, IntPtr set, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr hwnd, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr set, int index, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA data, int prop, out int regType, byte[] buf, int size, out int required);
        [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool UpdateDriverForPlugAndPlayDevicesW(IntPtr hwnd, string hardwareId, string infPath, int flags, out bool reboot);
        [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool DiUninstallDriverW(IntPtr hwnd, string infPath, int flags, out bool reboot);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupUninstallOEMInfW(string infFileName, int flags, IntPtr reserved);
        const int SUOI_FORCEDELETE = 1;

        // ------------------------------------------------------------------ Yardımcılar
        public static bool Embedded
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var f in Files) if (asm.GetManifestResourceInfo(ResPrefix + f) == null) return false;
                return true;
            }
        }

        static bool IsAdmin
        {
            get
            {
                try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
                catch { return false; }
            }
        }

        static void Log(string s)
        {
            try
            {
                Directory.CreateDirectory(WorkDir);
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + Environment.NewLine);
            }
            catch { }
        }

        static void Extract()
        {
            Directory.CreateDirectory(WorkDir);
            var asm = Assembly.GetExecutingAssembly();
            foreach (var f in Files)
                using (var src = asm.GetManifestResourceStream(ResPrefix + f))
                using (var dst = File.Create(Path.Combine(WorkDir, f)))
                    src.CopyTo(dst);
        }

        static X509Certificate2 Cert()
        {
            return new X509Certificate2(File.ReadAllBytes(Path.Combine(WorkDir, "BrokenScreenResizerVDD.cer")));
        }

        static void TrustCert(bool add)
        {
            var cert = Cert();
            foreach (var name in new[] { StoreName.Root, StoreName.TrustedPublisher })
            {
                var st = new X509Store(name, StoreLocation.LocalMachine);
                st.Open(OpenFlags.ReadWrite);
                try
                {
                    if (add) st.Add(cert);
                    else
                        foreach (var c in st.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false)) st.Remove(c);
                }
                finally { st.Close(); }
            }
        }

        static string[] HardwareIds(IntPtr set, ref SP_DEVINFO_DATA d)
        {
            var buf = new byte[2048];
            int type, req;
            if (!SetupDiGetDeviceRegistryPropertyW(set, ref d, SPDRP_HARDWAREID, out type, buf, buf.Length, out req)) return new string[0];
            return Encoding.Unicode.GetString(buf, 0, Math.Max(0, req)).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Bizim donanım kimliğimize sahip aygıtlar için işlem yapar; aygıt sayısını döndürür
        static int ForEachDevice(Action<IntPtr, SP_DEVINFO_DATA> action)
        {
            return ForEachDevice(Vdd.HardwareId, action);
        }

        static int ForEachDevice(string hardwareId, Action<IntPtr, SP_DEVINFO_DATA> action)
        {
            return ForEachDevice(id => string.Equals(id, hardwareId, StringComparison.OrdinalIgnoreCase), action);
        }

        // Donanım kimliklerinden biri koşulu sağlayan tüm aygıtlar (bağlı olmayan "hayalet" aygıtlar dahil)
        static int ForEachDevice(Func<string, bool> match, Action<IntPtr, SP_DEVINFO_DATA> action)
        {
            IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES);
            if (set == INVALID) return 0;
            int count = 0;
            try
            {
                var d = new SP_DEVINFO_DATA();
                d.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                for (int i = 0; SetupDiEnumDeviceInfo(set, i, ref d); i++)
                {
                    foreach (var id in HardwareIds(set, ref d))
                        if (match(id))
                        {
                            count++;
                            if (action != null) action(set, d);
                            break;
                        }
                    d.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return count;
        }

        static int CreateRootDevice()
        {
            var cls = DisplayClass;
            IntPtr set = SetupDiCreateDeviceInfoList(ref cls, IntPtr.Zero);
            if (set == INVALID) return Marshal.GetLastWin32Error();
            try
            {
                var d = new SP_DEVINFO_DATA();
                d.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                if (!SetupDiCreateDeviceInfoW(set, "Display", ref cls, Vdd.DeviceName, IntPtr.Zero, DICD_GENERATE_ID, ref d))
                    return Marshal.GetLastWin32Error();
                var ids = Encoding.Unicode.GetBytes(Vdd.HardwareId + "\0\0");
                if (!SetupDiSetDeviceRegistryPropertyW(set, ref d, SPDRP_HARDWAREID, ids, ids.Length))
                    return Marshal.GetLastWin32Error();
                if (!SetupDiCallClassInstaller(DIF_REGISTERDEVICE, set, ref d))
                    return Marshal.GetLastWin32Error();
                return 0;
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }

        // Eski adlı sürücüyü (Ekran Kurtar, v0.6 öncesi) kaldırır: aygıt, sürücü paketi, sertifika
        static void RemoveLegacy()
        {
            try
            {
                int n = ForEachDevice(Vdd.LegacyHardwareId, (set, d) => SetupDiCallClassInstaller(DIF_REMOVE, set, ref d));
                if (n > 0) Log("Eski sürücünün aygıtı kaldırıldı: " + n);

                string infDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
                foreach (var f in Directory.GetFiles(infDir, "oem*.inf"))
                {
                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    if (text.IndexOf("EkranKurtarVDD", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool ok = SetupUninstallOEMInfW(Path.GetFileName(f), SUOI_FORCEDELETE, IntPtr.Zero);
                    Log("Eski sürücü paketi " + Path.GetFileName(f) + ": " + (ok ? "kaldırıldı" : "kaldırılamadı (" + Marshal.GetLastWin32Error() + ")"));
                }

                foreach (var name in new[] { StoreName.Root, StoreName.TrustedPublisher })
                {
                    var st = new X509Store(name, StoreLocation.LocalMachine);
                    st.Open(OpenFlags.ReadWrite);
                    try
                    {
                        foreach (var c in st.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, Vdd.LegacyCertSubject, false))
                        {
                            st.Remove(c);
                            Log("Eski sertifika kaldırıldı (" + name + "): " + c.Thumbprint);
                        }
                    }
                    finally { st.Close(); }
                }

                string oldDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Settings.LegacyName);
                if (Directory.Exists(oldDir)) try { Directory.Delete(oldDir, true); } catch { }
            }
            catch (Exception ex) { Log("Eski sürücü temizliği: " + ex.Message); }
        }

        // ------------------------------------------------------------------ Tam kaldırma (yönetici)
        // Programın (yeni ve eski adıyla) sistemde bıraktığı her şey:
        //  - sanal ekran kartı aygıtı ve sanal monitörlerin "hayalet" aygıtları
        //  - Windows sürücü deposundaki sürücü paketleri
        //  - güvenilen sertifikalar (Kök ve Güvenilen Yayımcılar)
        //  - Windows'un sanal monitörler için tuttuğu ekran düzeni/ölçek kayıtları
        //  - ProgramData klasörleri (çıkarılan sürücü dosyaları, kurulum günlüğü)
        static readonly string[] DriverNames = { "BrokenScreenResizerVDD", "EkranKurtarVDD" };
        static readonly string[] CertSubjects = { Vdd.CertSubject, Vdd.LegacyCertSubject };
        const string GraphicsKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        static readonly string[] GraphicsSubKeys = { "Configuration", "Connectivity", "ScaleFactors", "MonitorDataStore" };
        // Sanal monitör kimlikleri: EDID üretici kodu BSR (yeni) / EKR (eski) + 4 haneli ürün kodu
        static readonly Regex MonitorIdRx = new Regex(@"(^|[^A-Z])(BSR|EKR)[0-9A-F]{4}");

        static bool IsOurHardwareId(string id)
        {
            return string.Equals(id, Vdd.HardwareId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, Vdd.LegacyHardwareId, StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith(@"MONITOR\BSR", StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith(@"MONITOR\EKR", StringComparison.OrdinalIgnoreCase);
        }

        static List<string> OemInfs()
        {
            var list = new List<string>();
            try
            {
                string infDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
                foreach (var f in Directory.GetFiles(infDir, "oem*.inf"))
                {
                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    foreach (var n in DriverNames)
                        if (text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) { list.Add(Path.GetFileName(f)); break; }
                }
            }
            catch { }
            return list;
        }

        static List<X509Certificate2> MachineCerts(StoreName name, bool write, out X509Store store)
        {
            store = new X509Store(name, StoreLocation.LocalMachine);
            store.Open(write ? OpenFlags.ReadWrite : OpenFlags.ReadOnly);
            var list = new List<X509Certificate2>();
            foreach (var s in CertSubjects)
                foreach (X509Certificate2 c in store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, s, false)) list.Add(c);
            return list;
        }

        static List<string> GraphicsKeys()
        {
            var list = new List<string>();
            foreach (var sub in GraphicsSubKeys)
                try
                {
                    using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(GraphicsKey + "\\" + sub))
                    {
                        if (k == null) continue;
                        foreach (var n in k.GetSubKeyNames())
                            if (MonitorIdRx.IsMatch(n)) list.Add(sub + "\\" + n);
                    }
                }
                catch { }
            return list;
        }

        static string[] DataDirs
        {
            get
            {
                string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                return new[] { Path.Combine(pd, Settings.AppName), Path.Combine(pd, Settings.LegacyName) };
            }
        }

        // Yönetici izni gerektiren bir kalıntı var mı? (sadece okur, yönetici gerekmez)
        public static bool NeedsPurge()
        {
            try
            {
                if (ForEachDevice(IsOurHardwareId, null) > 0) return true;
                if (OemInfs().Count > 0) return true;
                foreach (var name in new[] { StoreName.Root, StoreName.TrustedPublisher })
                {
                    X509Store st;
                    var found = MachineCerts(name, false, out st);
                    st.Close();
                    if (found.Count > 0) return true;
                }
                if (GraphicsKeys().Count > 0) return true;
                foreach (var d in DataDirs) if (Directory.Exists(d)) return true;
            }
            catch { return true; }
            return false;
        }

        // "--uninstall-system": yönetici olarak çalışır. Dönüş: 0 = her şey silindi, 1 = bazı öğeler silinemedi
        public static int Purge()
        {
            if (!IsAdmin) return 5;
            int failed = 0;
            try { Vdd.RemoveAll(); } catch { }

            // aygıtlar (ekran kartı + hayalet monitörler)
            ForEachDevice(IsOurHardwareId, (set, d) => { if (!SetupDiCallClassInstaller(DIF_REMOVE, set, ref d)) failed++; });

            // sürücü paketleri
            foreach (var inf in OemInfs())
                if (!SetupUninstallOEMInfW(inf, SUOI_FORCEDELETE, IntPtr.Zero)) failed++;

            // sertifikalar
            foreach (var name in new[] { StoreName.Root, StoreName.TrustedPublisher })
            {
                X509Store st = null;
                try
                {
                    foreach (var c in MachineCerts(name, true, out st)) st.Remove(c);
                }
                catch { failed++; }
                finally { if (st != null) st.Close(); }
            }

            // Windows'un sanal monitörler için sakladığı ekran kayıtları
            foreach (var key in GraphicsKeys())
                try { Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(GraphicsKey + "\\" + key, false); }
                catch { failed++; }

            // çıkarılan sürücü dosyaları ve günlük
            foreach (var d in DataDirs)
                if (Directory.Exists(d))
                    try { Directory.Delete(d, true); } catch { failed++; }

            return failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ Kurulum (yönetici)
        public static int Install()
        {
            try
            {
                if (!IsAdmin) return 5; // ERROR_ACCESS_DENIED
                if (!Embedded) { Log("Sürücü paketi exe'ye gömülü değil."); return 2; }
                Log("Kurulum başladı.");
                RemoveLegacy();
                Extract();
                TrustCert(true);
                Log("Sertifika eklendi: " + Cert().Thumbprint);

                if (ForEachDevice(null) == 0)
                {
                    int cr = CreateRootDevice();
                    Log("Sanal aygıt oluşturma: " + cr);
                    if (cr != 0) return cr;
                }

                bool reboot;
                string inf = Path.Combine(WorkDir, "BrokenScreenResizerVDD.inf");
                if (!UpdateDriverForPlugAndPlayDevicesW(IntPtr.Zero, Vdd.HardwareId, inf, INSTALLFLAG_FORCE, out reboot))
                {
                    int e = Marshal.GetLastWin32Error();
                    Log("Sürücü yükleme hatası: " + e + " (0x" + e.ToString("X8") + ")");
                    return e != 0 ? e : 1;
                }
                Log("Sürücü yüklendi." + (reboot ? " Yeniden başlatma gerekli." : ""));
                return reboot ? 3010 : 0; // ERROR_SUCCESS_REBOOT_REQUIRED
            }
            catch (Exception ex)
            {
                Log("Kurulum istisnası: " + ex);
                return 1;
            }
        }

        public static int Uninstall()
        {
            try
            {
                if (!IsAdmin) return 5;
                Log("Kaldırma başladı.");
                Vdd.RemoveAll();
                RemoveLegacy();
                int n = ForEachDevice((set, d) => SetupDiCallClassInstaller(DIF_REMOVE, set, ref d));
                Log("Kaldırılan aygıt: " + n);
                if (Embedded)
                {
                    Extract();
                    bool reboot;
                    if (!DiUninstallDriverW(IntPtr.Zero, Path.Combine(WorkDir, "BrokenScreenResizerVDD.inf"), 0, out reboot))
                        Log("Sürücü paketi kaldırılamadı: " + Marshal.GetLastWin32Error());
                    TrustCert(false);
                    Log("Sertifika kaldırıldı.");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log("Kaldırma istisnası: " + ex);
                return 1;
            }
        }

        // ------------------------------------------------------------------ Uygulamadan çağrı
        // Kendini yönetici olarak "--install-driver" ile çalıştırır ve bitmesini bekler (arayüz donmaz).
        public static int RunElevated(string arg)
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, arg);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                using (var p = Process.Start(psi))
                {
                    var sw = Stopwatch.StartNew();
                    while (!p.WaitForExit(100))
                    {
                        Application.DoEvents();
                        if (sw.ElapsedMilliseconds > 180000) return 1460; // ERROR_TIMEOUT
                    }
                    return p.ExitCode;
                }
            }
            catch (Win32Exception ex) { return ex.NativeErrorCode == Cancelled ? Cancelled : ex.NativeErrorCode; }
            catch { return 1; }
        }

        public static string Err(int code)
        {
            switch (code)
            {
                case 0: return L.T("başarılı", "success");
                case 2: return L.T("sürücü paketi bu exe'de yok", "the driver package is not in this exe");
                case 5: return L.T("yönetici izni gerekli", "administrator permission required");
                case 1223: return L.T("iptal edildi", "cancelled");
                case 1460: return L.T("zaman aşımı", "timed out");
                case unchecked((int)0xE0000247): // ERROR_DRIVER_STORE_ADD_FAILED
                case unchecked((int)0x800B0109): // CERT_E_UNTRUSTEDROOT
                case unchecked((int)0xE0000242): // ERROR_AUTHENTICODE_PUBLISHER_NOT_TRUSTED
                    return L.T("Windows sürücü imzasını kabul etmedi (0x", "Windows rejected the driver signature (0x") + code.ToString("X8") + ")";
                default:
                    return L.T("hata kodu ", "error code ") + code + " (0x" + code.ToString("X8") + ")";
            }
        }

        public static string LogFile { get { return LogPath; } }
    }
}
