using System.Globalization;

namespace EkranKurtar
{
    // Basit iki dilli metin: Windows dili Türkçe ise Türkçe, değilse İngilizce.
    static class L
    {
        public static readonly bool Tr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        public static string T(string tr, string en) { return Tr ? tr : en; }
    }
}
