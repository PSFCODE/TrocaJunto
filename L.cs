using System.Globalization;

namespace TrocaJunto;

/// <summary>Textos em português (Windows em português) ou inglês (qualquer outro idioma).</summary>
public static class L
{
    public static readonly bool Pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
    public static string T(string pt, string en) => Pt ? pt : en;
}
