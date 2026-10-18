using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BrokenScreenResizer
{
    class MonitorInfo
    {
        public string Name = "";
        public string DevicePath = "";
        public string GdiName = "";
        public int SignalW, SignalH;          // monitöre giden sinyalin boyutu
        public int DesktopW, DesktopH;        // Windows'un gördüğü masaüstü boyutu
        public bool HasRegion;                // sürücü "görüntü yeri" bilgisini veriyor mu
        public int RegL, RegT, RegR, RegB;    // masaüstü görüntüsünün sinyal içindeki yeri
        public int PathW, PathH;              // GPU'nun ürettiği tam görüntü boyutu

        public bool IsModified
        {
            get { return HasRegion && (RegL != 0 || RegT != 0 || RegR != PathW || RegB != PathH); }
        }

        public override string ToString()
        {
            string num = GdiName.Replace(@"\\.\DISPLAY", "");
            return num + ": " + Name + "  (" + SignalW + "x" + SignalH + ")";
        }
    }

    class Snapshot
    {
        public Ccd.PATH[] Paths;
        public Ccd.MODE[] Modes;
    }

    // Windows ekran yapılandırması: monitör listesi ve eski "sürücü modu" (v0.6 ve öncesi) kırpmasını geri alma.
    // Sürücü modu v0.7'de kaldırıldı; burada sadece monitörü tam ekrana döndüren kod kalır.
    static class DisplayConfig
    {
        const uint NONE = 0xFFFF;
        static Ccd.PATH[] paths = new Ccd.PATH[0];
        static Ccd.MODE[] modes = new Ccd.MODE[0];

        static uint SrcIdx(Ccd.PATH p) { return p.src.modeInfoIdx >> 16; }
        static uint TgtIdx(Ccd.PATH p) { return p.tgt.modeInfoIdx >> 16; }
        static uint DeskIdx(Ccd.PATH p) { return p.tgt.modeInfoIdx & 0xFFFF; }

        public static string Err(int r)
        {
            switch (r)
            {
                case 0: return L.T("başarılı", "success");
                case 5: return L.T("erişim reddedildi (5)", "access denied (5)");
                case 31: return L.T("sürücü hatası (31)", "driver error (31)");
                case 50: return L.T("desteklenmiyor (50)", "not supported (50)");
                case 87: return L.T("geçersiz parametre (87)", "invalid parameter (87)");
                case 1610: return L.T("geçersiz yapılandırma (1610)", "bad configuration (1610)");
                default: return L.T("hata kodu ", "error code ") + r;
            }
        }

        public static int Load()
        {
            uint f = Ccd.QDC_ONLY_ACTIVE_PATHS | Ccd.QDC_VIRTUAL_MODE_AWARE;
            uint np, nm;
            int r = Ccd.GetDisplayConfigBufferSizes(f, out np, out nm);
            if (r != 0) return r;
            var p = new Ccd.PATH[np];
            var m = new Ccd.MODE[nm];
            r = Ccd.QueryDisplayConfig(f, ref np, p, ref nm, m, IntPtr.Zero);
            if (r != 0) return r;
            Array.Resize(ref p, (int)np);
            Array.Resize(ref m, (int)nm);
            paths = p;
            modes = m;
            return 0;
        }

        static string TargetPath(Ccd.PATH p, out string friendly)
        {
            var t = new Ccd.TARGET_NAME();
            t.header.type = 2;
            t.header.size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Ccd.TARGET_NAME));
            t.header.adapterId = p.tgt.adapterId;
            t.header.id = p.tgt.id;
            friendly = L.T("(isimsiz monitör)", "(unnamed monitor)");
            if (Ccd.DisplayConfigGetDeviceInfo(ref t) != 0) return "";
            if (!string.IsNullOrEmpty(t.friendlyName)) friendly = t.friendlyName;
            return t.devicePath ?? "";
        }

        static string GdiName(Ccd.PATH p)
        {
            var s = new Ccd.SOURCE_NAME();
            s.header.type = 1;
            s.header.size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Ccd.SOURCE_NAME));
            s.header.adapterId = p.src.adapterId;
            s.header.id = p.src.id;
            return Ccd.DisplayConfigGetDeviceInfo(ref s) == 0 ? (s.gdiName ?? "") : "";
        }

        public static List<MonitorInfo> GetMonitors()
        {
            var list = new List<MonitorInfo>();
            if (Load() != 0) return list;
            foreach (var p in paths)
            {
                var mi = new MonitorInfo();
                mi.DevicePath = TargetPath(p, out mi.Name);
                mi.GdiName = GdiName(p);
                uint si = SrcIdx(p), ti = TgtIdx(p), di = DeskIdx(p);
                if (ti != NONE && ti < modes.Length) { mi.SignalW = (int)modes[ti].activeCx; mi.SignalH = (int)modes[ti].activeCy; }
                if (si != NONE && si < modes.Length) { mi.DesktopW = (int)modes[si].srcWidth; mi.DesktopH = (int)modes[si].srcHeight; }
                if (mi.SignalW == 0) { mi.SignalW = mi.DesktopW; mi.SignalH = mi.DesktopH; }
                if (di != NONE && di < modes.Length)
                {
                    mi.HasRegion = true;
                    mi.RegL = modes[di].regL; mi.RegT = modes[di].regT; mi.RegR = modes[di].regR; mi.RegB = modes[di].regB;
                    mi.PathW = modes[di].dSrcX > 0 ? modes[di].dSrcX : mi.SignalW;
                    mi.PathH = modes[di].dSrcY > 0 ? modes[di].dSrcY : mi.SignalH;
                }
                list.Add(mi);
            }
            return list;
        }

        static int FindPath(string devicePath)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string f;
                if (TargetPath(paths[i], out f) == devicePath) return i;
            }
            return -1;
        }

        static Snapshot Current()
        {
            var s = new Snapshot();
            s.Paths = (Ccd.PATH[])paths.Clone();
            s.Modes = (Ccd.MODE[])modes.Clone();
            return s;
        }

        // Monitörü tam ekrana döndüren yapılandırma (masaüstü = tüm sinyal)
        static Snapshot BuildReset(int pi, out string err)
        {
            err = null;
            var s = Current();
            var p = s.Paths[pi];
            uint si = SrcIdx(p), ti = TgtIdx(p), di = DeskIdx(p);
            if (si == NONE || ti == NONE) { err = L.T("Monitörün mod bilgisi okunamadı.", "Could not read monitor modes."); return null; }
            if (di == NONE) { err = L.T("Ekran sürücün 'görüntü yeri' ayarını sunmuyor.", "Your display driver does not expose the desktop image region."); return null; }

            var m = s.Modes;
            int W = m[di].dSrcX > 0 ? m[di].dSrcX : (int)m[ti].activeCx;
            int H = m[di].dSrcY > 0 ? m[di].dSrcY : (int)m[ti].activeCy;

            int x0 = m[si].posX, y0 = m[si].posY;
            int oldW = (int)m[si].srcWidth, oldH = (int)m[si].srcHeight;
            int dW = oldW - W, dH = oldH - H;
            m[si].srcWidth = (uint)W;
            m[si].srcHeight = (uint)H;

            // Komşu monitörlerle arada boşluk ya da çakışma kalmasın
            if (dW != 0)
            {
                if (x0 < 0) m[si].posX = x0 + dW;
                else for (int k = 0; k < m.Length; k++)
                        if (k != si && m[k].infoType == 1 && m[k].posX >= x0 + oldW) m[k].posX -= dW;
            }
            if (dH != 0)
            {
                if (y0 < 0) m[si].posY = y0 + dH;
                else for (int k = 0; k < m.Length; k++)
                        if (k != si && m[k].infoType == 1 && m[k].posY >= y0 + oldH) m[k].posY -= dH;
            }

            m[di].dSrcX = W; m[di].dSrcY = H;
            m[di].regL = 0; m[di].regT = 0; m[di].regR = W; m[di].regB = H;
            m[di].clipL = 0; m[di].clipT = 0; m[di].clipR = W; m[di].clipB = H;
            return s;
        }

        static int Set(Snapshot s, uint flags)
        {
            return Ccd.SetDisplayConfig((uint)s.Paths.Length, s.Paths, (uint)s.Modes.Length, s.Modes,
                Ccd.SDC_USE_SUPPLIED | Ccd.SDC_VIRTUAL_MODE_AWARE | flags);
        }

        // Monitörü tam ekrana döndürür ve kalıcı kaydeder.
        public static int Reset(string devicePath)
        {
            int r = Load();
            if (r != 0) return r;
            int pi = FindPath(devicePath);
            if (pi < 0) return 87;
            string err;
            var s = BuildReset(pi, out err);
            if (s == null) return 50;
            return Set(s, Ccd.SDC_APPLY | Ccd.SDC_SAVE | Ccd.SDC_ALLOW_CHANGES);
        }

        // Acil durum: kırpılmış tüm monitörleri tam ekrana döndürür.
        public static string ResetAll()
        {
            var sb = new StringBuilder();
            // sanal monitör düzeni ve NVIDIA warp da sıfırlanır
            if (DisplayLayout.HasBackup) sb.AppendLine(L.T("Ekran düzeni: ", "Display layout: ") + DisplayLayout.Err(DisplayLayout.RestoreBackup()));
            if (NvApi.Init() == NvApi.OK)
                foreach (var m in GetMonitors())
                {
                    uint id;
                    if (NvApi.DisplayId(m.GdiName, out id) == NvApi.OK && NvApi.Probe(id) == NvApi.OK) NvApi.ClearWarp(id);
                }
            foreach (var m in GetMonitors())
                if (m.IsModified)
                    sb.AppendLine(m.Name + ": " + Err(Reset(m.DevicePath)));
            return sb.Length == 0 ? L.T("Kırpılmış monitör yok.", "No cropped monitors.") : sb.ToString();
        }
    }
}
