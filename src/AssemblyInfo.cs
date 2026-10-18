using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// exe'nin "Özellikler > Ayrıntılar" bilgileri (Windows sürüm kaynağı bunlardan üretilir)
[assembly: AssemblyTitle("Broken Screen Resizer")]
[assembly: AssemblyDescription("Hides the broken part of a monitor from Windows")]
[assembly: AssemblyCompany("HErkanuzun")]
[assembly: AssemblyProduct("Broken Screen Resizer")]
[assembly: AssemblyCopyright("Copyright (c) 2026 HErkanuzun. MIT License.")]
[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]
[assembly: AssemblyInformationalVersion("1.6")]
[assembly: NeutralResourcesLanguage("en")]
[assembly: ComVisible(false)]

namespace BrokenScreenResizer
{
    static class AppInfo
    {
        public const string Name = "Broken Screen Resizer";
        public const string Version = "1.6";    // AssemblyInformationalVersion ile aynı; sürüm etiketi v1.6
        public const string Channel = "preview"; // "stable" = kararlı (main), "preview" = ön sürüm (develop)
        public const string Repo = "https://github.com/HErkanuzun/BrokenScreenResizer";
    }
}
