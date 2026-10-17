using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EkranKurtar
{
    // Windows Display Configuration (CCD) API
    static class Ccd
    {
        [StructLayout(LayoutKind.Sequential)] public struct LUID { public uint Low; public int High; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH_SOURCE { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH_TARGET
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology;
            public uint rotation; public uint scaling; public uint refreshNum; public uint refreshDen;
            public uint scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH { public PATH_SOURCE src; public PATH_TARGET tgt; public uint flags; }

        // DISPLAYCONFIG_MODE_INFO (union açılmış hali)
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct MODE
        {
            [FieldOffset(0)] public uint infoType;   // 1 kaynak, 2 hedef, 3 masaüstü görüntüsü
            [FieldOffset(4)] public uint id;
            [FieldOffset(8)] public LUID adapterId;
            // kaynak modu
            [FieldOffset(16)] public uint srcWidth;
            [FieldOffset(20)] public uint srcHeight;
            [FieldOffset(24)] public uint pixelFormat;
            [FieldOffset(28)] public int posX;
            [FieldOffset(32)] public int posY;
            // hedef modu
            [FieldOffset(40)] public uint activeCx;
            [FieldOffset(44)] public uint activeCy;
            // masaüstü görüntü bilgisi
            [FieldOffset(16)] public int dSrcX;
            [FieldOffset(20)] public int dSrcY;
            [FieldOffset(24)] public int regL;
            [FieldOffset(28)] public int regT;
            [FieldOffset(32)] public int regR;
            [FieldOffset(36)] public int regB;
            [FieldOffset(40)] public int clipL;
            [FieldOffset(44)] public int clipT;
            [FieldOffset(48)] public int clipR;
            [FieldOffset(52)] public int clipB;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TARGET_NAME
        {
            public HEADER header; public uint flags; public uint outputTechnology;
            public ushort edidManufactureId; public ushort edidProductCodeId; public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string friendlyName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string devicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SOURCE_NAME
        {
            public HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string gdiName;
        }

        public const uint QDC_ONLY_ACTIVE_PATHS = 0x2, QDC_VIRTUAL_MODE_AWARE = 0x10;
        public const uint SDC_USE_DATABASE_CURRENT = 0xF, SDC_USE_SUPPLIED = 0x20, SDC_VALIDATE = 0x40, SDC_APPLY = 0x80,
                          SDC_SAVE = 0x200, SDC_ALLOW_CHANGES = 0x400, SDC_VIRTUAL_MODE_AWARE = 0x8000;

        [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] PATH[] paths, ref uint numModes, [Out] MODE[] modes, IntPtr topology);
        [DllImport("user32.dll")] public static extern int SetDisplayConfig(uint numPaths, [In] PATH[] paths, uint numModes, [In] MODE[] modes, uint flags);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref TARGET_NAME info);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref SOURCE_NAME info);
    }

    static class Win
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam;
        }

        public const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
        public const int ABN_POSCHANGED = 1;
        public const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

        [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    }
}
