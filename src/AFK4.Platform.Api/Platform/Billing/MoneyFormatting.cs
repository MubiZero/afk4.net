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

    // TJS (somoni) is subdivided into 100 diram, like every currency this product bills in today.
    // The table exists so a zero-decimal currency does not silently render as "1500.00".
    private static int Exponent(string currencyCode) => currencyCode switch
    {
        "TJS" or "RUB" or "USD" or "EUR" => 2,
        _ => DefaultExponent
    };
}
