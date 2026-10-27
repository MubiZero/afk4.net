using System.Net;
using System.Text.RegularExpressions;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>
/// Письмо — в сообщение Telegram. Шаблоны у почты и Telegram общие (третий набор текстов на трёх
/// языках разошёлся бы с почтой), а разметку Telegram понимает урезанную: без абзацев и списков.
/// Значения подставлены в HTML уже экранированными (<see cref="NotificationRenderer"/>).
/// </summary>
public static partial class TelegramText
{
    public static string FromEmail(string subject, string bodyHtml)
    {
        var body = bodyHtml
            .Replace("<strong>", "<b>", StringComparison.OrdinalIgnoreCase)
            .Replace("</strong>", "</b>", StringComparison.OrdinalIgnoreCase)
            .Replace("<em>", "<i>", StringComparison.OrdinalIgnoreCase)
            .Replace("</em>", "</i>", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<li>", "• ", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);
        body = LineBreak().Replace(body, "\n");
        body = UnsupportedTag().Replace(body, string.Empty);
        body = ExtraBlankLines().Replace(body, "\n\n").Trim();

        return string.IsNullOrWhiteSpace(subject)
            ? body
            : $"<b>{WebUtility.HtmlEncode(subject)}</b>\n\n{body}";
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    // Telegram отклоняет сообщение целиком, встретив незнакомый тег, — оставляем только его набор.
    [GeneratedRegex(@"</?(?!(?:b|i|u|s|a|code|pre)\b)[a-z][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex UnsupportedTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}
