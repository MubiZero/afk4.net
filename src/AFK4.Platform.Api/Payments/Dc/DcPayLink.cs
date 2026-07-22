using System.Globalization;

namespace AFK4.Platform.Api.Payments.Dc;

// Сборщик платёжной ссылки DushanbeCity. Ссылка — «тупая»: банк не подтверждает оплату,
// подтверждает кассир вручную. Формат фиксирован: pay.dc.tj/?A=карта&s=сумма&c=коммент&f1=133.
public static class DcPayLink
{
    private const string BaseUrl = "http://pay.dc.tj/";
    private const string ConstParams = "f1=133";

    // Minor units → мажорные сомони, ровно 2 знака.
    public static string FormatAmount(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    // Подставляет {ref} в шаблон комментария (напр. "AFK4-{ref}").
    public static string BuildComment(string template, string reference) =>
        template.Replace("{ref}", reference, StringComparison.Ordinal);

    public static string BuildUrl(string cardNumber, long amountMinor, string comment)
    {
        var s = FormatAmount(amountMinor);
        var c = Uri.EscapeDataString(comment);
        return $"{BaseUrl}?A={cardNumber}&s={s}&c={c}&{ConstParams}";
    }
}
