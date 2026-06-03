using System.Net;
using System.Text.RegularExpressions;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Default <see cref="INotificationRenderer"/>. Substitutes every <c>{{token}}</c> placeholder
/// from the supplied tokens; an unresolved placeholder is a hard error (never a half-rendered
/// message). Interpolated values are HTML-escaped in the HTML body only.
/// </summary>
public sealed partial class NotificationRenderer : INotificationRenderer
{
    public RenderedNotification Render(NotificationTemplate template, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(tokens);

        return new RenderedNotification(
            Subject: Substitute(template.Subject, tokens, htmlEscape: false),
            BodyText: Substitute(template.BodyText, tokens, htmlEscape: false),
            BodyHtml: Substitute(template.BodyHtml, tokens, htmlEscape: true));
    }

    private static string Substitute(string content, IReadOnlyDictionary<string, string> tokens, bool htmlEscape)
    {
        return PlaceholderPattern().Replace(content, match =>
        {
            var key = match.Groups["key"].Value;
            if (!tokens.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException(
                    $"Notification template references token '{key}' but no value was supplied.");
            }

            return htmlEscape ? WebUtility.HtmlEncode(value) : value;
        });
    }

    [GeneratedRegex(@"\{\{\s*(?<key>[\w.]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
