using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EkranKurtar
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

    class ApplyResult
    {
        public bool Ok;
        public int ActualCut;
        public string Log = "";
        public Snapshot Original;
        public Snapshot Applied;
    }

    // "Sürücü modu": Windows'un ekran yapılandırmasını değiştirerek masaüstünü sinyalin sağlam kısmına yerleştirir.
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

        // cut = 0 => tam ekran (sıfırlama)
        static Snapshot Build(int pi, Edge edge, int cut, out int actualCut, out string err)
        {
            actualCut = 0;
            err = null;
            var s = Current();
            var p = s.Paths[pi];
            uint si = SrcIdx(p), ti = TgtIdx(p), di = DeskIdx(p);
            if (si == NONE || ti == NONE) { err = L.T("Monitörün mod bilgisi okunamadı.", "Could not read monitor modes."); return null; }
            if (di == NONE) { err = L.T("Ekran sürücün 'görüntü yeri' ayarını sunmuyor.", "Your display driver does not expose the desktop image region."); return null; }

            var m = s.Modes;
            // PathSourceSize: GPU'nun ürettiği tam görüntü (değişmez).
            // DesktopImageRegion: masaüstünün bu görüntü içindeki yeri (bunun içinde kalmalı).
            // DesktopImageClip ve kaynak modu: Windows'un gördüğü masaüstü boyutu.
            int W = m[di].dSrcX > 0 ? m[di].dSrcX : (int)m[ti].activeCx;
            int H = m[di].dSrcY > 0 ? m[di].dSrcY : (int)m[ti].activeCy;
            int nw = W, nh = H;
            int L1 = 0, T1 = 0, R1 = W, B1 = H;

            if (cut > 0)
            {
                switch (edge)
                {
                    case Edge.Left: nw = (W - cut) & ~7; L1 = W - nw; break;
                    case Edge.Right: nw = (W - cut) & ~7; R1 = nw; break;
                    case Edge.Top: nh = (H - cut) & ~1; T1 = H - nh; break;
                    case Edge.Bottom: nh = (H - cut) & ~1; B1 = nh; break;
                }
            }
            actualCut = (edge == Edge.Left || edge == Edge.Right) ? W - nw : H - nh;

            int x0 = m[si].posX, y0 = m[si].posY;
            int oldW = (int)m[si].srcWidth, oldH = (int)m[si].srcHeight;
            int dW = oldW - nw, dH = oldH - nh;

            m[si].srcWidth = (uint)nw;
            m[si].srcHeight = (uint)nh;

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
            m[di].regL = L1; m[di].regT = T1; m[di].regR = R1; m[di].regB = B1;
            m[di].clipL = 0; m[di].clipT = 0; m[di].clipR = nw; m[di].clipB = nh;
            return s;
        }

        static int Set(Snapshot s, uint flags)
        {
            return Ccd.SetDisplayConfig((uint)s.Paths.Length, s.Paths, (uint)s.Modes.Length, s.Modes,
                Ccd.SDC_USE_SUPPLIED | Ccd.SDC_VIRTUAL_MODE_AWARE | flags);
        }

        // Ayarı uygular (kaydetmeden). Sonra Keep() ya da Restore() çağrılmalı.
        public static ApplyResult TryApply(string devicePath, Edge edge, int cut)
        {
            var res = new ApplyResult();
            var log = new StringBuilder();
            int r = Load();
            if (r != 0) { res.Log = L.T("Ekran yapılandırması okunamadı: ", "Could not read display config: ") + Err(r); return res; }
            int pi = FindPath(devicePath);
            if (pi < 0) { res.Log = L.T("Monitör bulunamadı.", "Monitor not found."); return res; }

            res.Original = Current();
            string err;
            var s = Build(pi, edge, cut, out res.ActualCut, out err);
            if (s == null) { res.Log = err; return res; }
            {
                var dm = s.Modes[DeskIdx(s.Paths[pi])];
                var sm = s.Modes[SrcIdx(s.Paths[pi])];
                log.AppendLine(L.T("Denenen: masaüstü ", "Trying: desktop ") + sm.srcWidth + "x" + sm.srcHeight +
                    ", " + L.T("görüntü ", "image ") + dm.dSrcX + "x" + dm.dSrcY +
                    ", " + L.T("yer ", "region ") + dm.regL + "," + dm.regT + "-" + dm.regR + "," + dm.regB);
            }

            // Farklı ölçekleme modlarını sırayla dene: mevcut, özel, birebir, ortalanmış
            uint[] scalings = { s.Paths[pi].tgt.scaling, 5, 1, 2 };
            bool applied = false;
            foreach (uint sc in scalings)
            {
                s.Paths[pi].tgt.scaling = sc;
                int v = Set(s, Ccd.SDC_VALIDATE);
                log.AppendLine(L.T("Ölçekleme ", "Scaling ") + sc + L.T(" kontrolü: ", " validate: ") + Err(v));
                if (v != 0) continue;
                int a = Set(s, Ccd.SDC_APPLY);
                log.AppendLine(L.T("Ölçekleme ", "Scaling ") + sc + L.T(" uygulama: ", " apply: ") + Err(a));
                if (a == 0) { applied = true; break; }
            }
            if (!applied)
            {
                log.AppendLine(L.T("Sürücü bu ayarı kabul etmedi. Hiçbir şey değişmedi.", "The driver rejected this setting. Nothing was changed."));
                res.Log = log.ToString();
                return res;
            }
            res.Applied = s;

            // Sürücü gerçekten ne uyguladı?
            Thread.Sleep(1200);
            Load();
            int pj = FindPath(devicePath);
            if (pj >= 0)
            {
                uint di = DeskIdx(paths[pj]);
                uint wantDi = DeskIdx(s.Paths[pi]);
                if (di != NONE && di < modes.Length)
                {
                    var got = modes[di];
                    var want = s.Modes[wantDi];
                    log.AppendLine(L.T("İstenen yer: ", "Requested: ") + want.regL + "," + want.regT + " - " + want.regR + "," + want.regB);
                    log.AppendLine(L.T("Uygulanan yer: ", "Applied: ") + got.regL + "," + got.regT + " - " + got.regR + "," + got.regB);
                    if (Math.Abs(got.regL - want.regL) > 8 || Math.Abs(got.regT - want.regT) > 8 ||
                        Math.Abs(got.regR - want.regR) > 8 || Math.Abs(got.regB - want.regB) > 8)
                    {
                        log.AppendLine(L.T("Sürücü konumu kendi bildiği gibi değiştirdi; bu sistemde sürücü modu çalışmıyor.",
                                           "The driver overrode the position; driver mode does not work on this system."));
                        Restore(res.Original);
                        res.Log = log.ToString();
                        return res;
                    }
                }
            }
            res.Ok = true;
            res.Log = log.ToString();
            return res;
        }

        public static int Keep(Snapshot applied)
        {
            return Set(applied, Ccd.SDC_APPLY | Ccd.SDC_SAVE);
        }

        public static int Restore(Snapshot original)
        {
            int r = Set(original, Ccd.SDC_APPLY | Ccd.SDC_ALLOW_CHANGES);
            if (r != 0) r = Ccd.SetDisplayConfig(0, null, 0, null, Ccd.SDC_APPLY | Ccd.SDC_USE_DATABASE_CURRENT);
            return r;
        }

        // Monitörü tam ekrana döndürür ve kalıcı kaydeder.
        public static int Reset(string devicePath)
        {
            int r = Load();
            if (r != 0) return r;
            int pi = FindPath(devicePath);
            if (pi < 0) return 87;
            int c; string err;
            var s = Build(pi, Edge.Left, 0, out c, out err);
            if (s == null) return 50;
            return Set(s, Ccd.SDC_APPLY | Ccd.SDC_SAVE | Ccd.SDC_ALLOW_CHANGES);
        }

        // Acil durum: kırpılmış tüm monitörleri tam ekrana döndürür.
        public static string ResetAll()
        {
            var sb = new StringBuilder();
            foreach (var m in GetMonitors())
                if (m.IsModified)
                    sb.AppendLine(m.Name + ": " + Err(Reset(m.DevicePath)));
            return sb.Length == 0 ? L.T("Kırpılmış monitör yok.", "No cropped monitors.") : sb.ToString();
        }

        public static bool IsApplied(string devicePath, Edge edge, int cut)
        {
            foreach (var m in GetMonitors())
            {
                if (m.DevicePath != devicePath) continue;
                if (!m.HasRegion) return false;
                switch (edge)
                {
                    case Edge.Left: return Math.Abs(m.RegL - cut) <= 8;
                    case Edge.Right: return Math.Abs(m.PathW - m.RegR - cut) <= 8;
                    case Edge.Top: return Math.Abs(m.RegT - cut) <= 2;
                    case Edge.Bottom: return Math.Abs(m.PathH - m.RegB - cut) <= 2;
                }
            }
            return false;
        }
    }
}
