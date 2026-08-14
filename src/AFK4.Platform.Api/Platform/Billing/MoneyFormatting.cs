using System.Globalization;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// Formats minor-unit amounts for notification bodies. The frontend uses @afk4/money; the backend
/// renders email templates and has no access to that package, so the exponent table lives here.
/// </summary>
public static class MoneyFormatting
{
    private const int DefaultExponent = 2;

    public static string ToMajorString(long minorUnits, string currencyCode)
    {
        var exponent = Exponent(currencyCode);
        var scale = (decimal)Math.Pow(10, exponent);
        return (minorUnits / scale).ToString("F" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Сумма так, как её показывает приложение: разделитель по языку, знак валюты после числа.
    /// «120,00 с.» читается, «120.00 TJS» — жаргон, а уведомление человек видит чаще экрана.
    /// Незнакомая валюта показывается своим кодом — выдуманный знак хуже честного кода.
    /// </summary>
    public static string ToDisplayString(long minorUnits, string currencyCode, string locale)
    {
        var exponent = Exponent(currencyCode);
        var scale = (decimal)Math.Pow(10, exponent);
        var culture = CultureFor(locale);
        var amount = (minorUnits / scale).ToString(
            "N" + exponent.ToString(CultureInfo.InvariantCulture), culture);

        return $"{amount} {Symbol(currencyCode)}";
    }

    private static string Symbol(string currencyCode) => currencyCode.ToUpperInvariant() switch
    {
        "TJS" => "с.",
        "USD" => "$",
        "EUR" => "€",
        "RUB" => "₽",
        _ => currencyCode,
    };

    /// <summary>Язык влияет только на разделители; неизвестный откатывается к русскому.</summary>
    private static CultureInfo CultureFor(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(locale) ? "ru" : locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("ru");
        }
    }

    // TJS (somoni) is subdivided into 100 diram, like every currency this product bills in today.
    // The table exists so a zero-decimal currency does not silently render as "1500.00".
    private static int Exponent(string currencyCode) => currencyCode switch
    {
        "TJS" or "RUB" or "USD" or "EUR" => 2,
        _ => DefaultExponent
    };
}
