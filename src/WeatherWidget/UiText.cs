using System.Globalization;

namespace WeatherWidget;

internal static class UiText
{
    public static string Resolve(string? setting) => setting is "hr" or "en" ? setting :
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "hr" ? "hr" : "en";
    public static string T(string language, string hr, string en) => Resolve(language) == "hr" ? hr : en;
    public static CultureInfo Culture(string language) => CultureInfo.GetCultureInfo(Resolve(language) == "hr" ? "hr-HR" : "en-GB");
}
