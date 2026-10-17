using System;
using System.IO;

namespace EkranKurtar
{
    enum Edge { Left = 0, Top = 1, Right = 2, Bottom = 3 }   // değerler AppBar kenar kodlarıyla aynı
    enum Mode { Driver = 0, Software = 1 }

    class Settings
    {
        public string MonitorPath = "";
        public Edge Edge = Edge.Left;
        public int Cut = 0;
        public Mode Mode = Mode.Driver;
        public bool Active = false;
        public Mode ActiveMode = Mode.Driver;
        public bool LockCursor = true;
        public bool FixWindows = true;
        public string Lang = "";

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EkranKurtar"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "monitor": s.MonitorPath = v; break;
                        case "edge": if (int.TryParse(v, out n) && n >= 0 && n <= 3) s.Edge = (Edge)n; break;
                        case "cut": if (int.TryParse(v, out n) && n >= 0) s.Cut = n; break;
                        case "mode": s.Mode = v == "1" ? Mode.Software : Mode.Driver; break;
                        case "active": s.Active = v == "1"; break;
                        case "activeMode": s.ActiveMode = v == "1" ? Mode.Software : Mode.Driver; break;
                        case "lockCursor": s.LockCursor = v == "1"; break;
                        case "fixWindows": s.FixWindows = v == "1"; break;
                        case "lang": s.Lang = v; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[] {
                    "monitor=" + MonitorPath,
                    "edge=" + (int)Edge,
                    "cut=" + Cut,
                    "mode=" + (int)Mode,
                    "active=" + (Active ? "1" : "0"),
                    "activeMode=" + (int)ActiveMode,
                    "lockCursor=" + (LockCursor ? "1" : "0"),
                    "fixWindows=" + (FixWindows ? "1" : "0"),
                    "lang=" + Lang
                });
            }
            catch { }
        }
    }
}
