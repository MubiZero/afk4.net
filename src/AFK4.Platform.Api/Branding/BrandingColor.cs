using System.Text.RegularExpressions;

namespace AFK4.Platform.Api.Branding;

/// <summary>
/// Проверки полей оформления. Значения едут на экран игрока и в оболочку игрового ПК, поэтому
/// принимаются только те, которые там безопасно подставить: цвет — шестнадцатеричный, логотип —
/// абсолютная http(s)-ссылка (никаких <c>javascript:</c> и <c>data:</c>).
/// </summary>
public static partial class BrandingColor
{
    public static bool IsHex(string value) => HexPattern().IsMatch(value);

    public static bool IsSafeImageUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();
}
