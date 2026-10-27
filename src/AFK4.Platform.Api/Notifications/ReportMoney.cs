using System.Globalization;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Сумма в отчёте так, как её пишут в Панели: «1 200 с.», «54,5 с.». По-таджикски числа пишутся как
/// по-русски — то же правило, что <c>numberLocale</c> в @afk4/formatting.
/// </summary>
public static class ReportMoney
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TJS"] = "с.", ["USD"] = "$", ["EUR"] = "€", ["RUB"] = "₽"
    };

    public static CultureInfo Culture(string locale) =>
        CultureInfo.GetCultureInfo(locale.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "ru-RU");

    public static string Format(long minorUnits, string currencyCode, string locale, bool signed = false)
    {
        var major = minorUnits / 100m;
        var number = Math.Abs(major).ToString(major % 1 == 0 ? "#,0" : "#,0.##", Culture(locale));
        var sign = major < 0 ? "−" : signed && major > 0 ? "+" : string.Empty;
        var symbol = Symbols.TryGetValue(currencyCode, out var known) ? known : currencyCode;
        return $"{sign}{number} {symbol}".TrimEnd();
    }
}
