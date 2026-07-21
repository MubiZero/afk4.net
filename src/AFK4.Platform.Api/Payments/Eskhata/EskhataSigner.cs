using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Payments.Eskhata;

// Подпись Merchant API: SHA-256 (НЕ HMAC, вопреки формулировке доки) от конкатенации
// значений скалярных параметров в порядке спецификации + "." + Hash-Key. Значения массивов
// (items) и сам hash в конкатенацию не входят.
public static class EskhataSigner
{
    public static string BuildHash(IReadOnlyList<string> orderedValues, string hashKey)
    {
        var payload = string.Concat(orderedValues) + "." + hashKey;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string CompanyIdHeader(string companyId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(companyId));

    // Minor units → major-string с ровно двумя знаками (и для тела, и для строки хеша).
    public static string FormatAmount(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
