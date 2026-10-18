using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace BrokenScreenResizer
{
    // NVIDIA NvAPI: eski sürücü modunun (v1.5 ve öncesi) açtığı warp'ı kapatmak için gereken kısım.
    // Fonksiyon kimlikleri NVIDIA'nın açık kaynak nvapi_interface.h dosyasından alınmıştır.
    static class NvApi
    {
        const uint ID_Initialize = 0x0150e828;
        const uint ID_DispIdByName = 0xae457190;
        const uint ID_GetWarpState = 0x6f5435af;
        const uint ID_SetWarp = 0xb34bab4f;

        public const int OK = 0, NOT_SUPPORTED = -104, LIBRARY_NOT_FOUND = -2;

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr Query64(uint id);
        [DllImport("nvapi.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr Query32(uint id);

        [StructLayout(LayoutKind.Sequential)]
        public struct NvSBox { public int X, Y, W, H; }

        [StructLayout(LayoutKind.Sequential)]
        struct WarpState { public uint version; public uint enabled; }

        [StructLayout(LayoutKind.Sequential)]
        struct WarpData
        {
            public uint version;
            public IntPtr vertices;
            public int vertexFormat;     // 0 = TRIANGLESTRIP_XYUVRQ
            public int numVertices;
            public IntPtr textureRect;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InitFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DispIdFn([MarshalAs(UnmanagedType.LPStr)] string name, out uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int WarpStateFn(uint id, ref WarpState s);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetWarpFn(uint id, ref WarpData d, out int maxVertices, out int sticky);

        static bool initDone;
        static int initResult = LIBRARY_NOT_FOUND;

        static uint Ver<T>() { return (uint)(Marshal.SizeOf(typeof(T)) | (1 << 16)); }

        static T Fn<T>(uint id) where T : class
        {
            IntPtr p;
            try { p = IntPtr.Size == 8 ? Query64(id) : Query32(id); }
            catch { return null; }
            if (p == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        public static string Err(int r)
        {
            switch (r)
            {
                case 0: return L.T("başarılı", "success");
                case -1: return L.T("genel NVIDIA hatası (-1)", "generic NVIDIA error (-1)");
                case -2: return L.T("NVIDIA sürücüsü bulunamadı (-2)", "NVIDIA driver not found (-2)");
                case -3: return L.T("fonksiyon yok (-3)", "function not available (-3)");
                case -5: return L.T("geçersiz parametre (-5)", "invalid argument (-5)");
                case -6: return L.T("NVIDIA ekran kartı bulunamadı (-6)", "no NVIDIA GPU found (-6)");
                case -9: return L.T("yapı sürümü uyumsuz (-9)", "incompatible struct version (-9)");
                case -104: return L.T("bu kartta/sürücüde desteklenmiyor (-104)", "not supported on this GPU/driver (-104)");
                default: return "NvAPI " + r;
            }
        }

        public static int Init()
        {
            if (initDone) return initResult;
            initDone = true;
            var f = Fn<InitFn>(ID_Initialize);
            initResult = f == null ? LIBRARY_NOT_FOUND : f();
            return initResult;
        }

        public static int DisplayId(string gdiName, out uint id)
        {
            id = 0;
            int r = Init();
            if (r != OK) return r;
            var f = Fn<DispIdFn>(ID_DispIdByName);
            return f == null ? -3 : f(gdiName, out id);
        }

        // Warp destekleniyor mu? (hiçbir şeyi değiştirmez)
        public static int Probe(uint id)
        {
            var f = Fn<WarpStateFn>(ID_GetWarpState);
            if (f == null) return -3;
            var s = new WarpState();
            s.version = Ver<WarpState>();
            return f(id, ref s);
        }

        // Warp'ı kapatır (normal görüntüye döner)
        public static int ClearWarp(uint id)
        {
            var f = Fn<SetWarpFn>(ID_SetWarp);
            if (f == null) return -3;
            var d = new WarpData();
            d.version = Ver<WarpData>();
            d.vertices = IntPtr.Zero;
            d.vertexFormat = 0;
            d.numVertices = 0;
            d.textureRect = IntPtr.Zero;
            int maxV, st;
            return f(id, ref d, out maxV, out st);
        }

    }
}
