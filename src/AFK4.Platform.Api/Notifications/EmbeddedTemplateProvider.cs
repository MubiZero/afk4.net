using System.Reflection;
using System.Text.Json;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// <see cref="ITemplateProvider"/> backed by embedded resource files under
/// <c>Notifications/Templates/{locale}/{key}.json</c>. Each file holds <c>subject</c>,
/// <c>bodyText</c> and <c>bodyHtml</c>. A requested locale with no template falls back to the
/// configured default locale (D12); a key missing even in the default locale is a hard error.
/// </summary>
public sealed class EmbeddedTemplateProvider : ITemplateProvider
{
    private const string ResourceRoot = "AFK4.Platform.Api.Notifications.Templates";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Assembly assembly = typeof(EmbeddedTemplateProvider).Assembly;
    private readonly string defaultLocale;

    public EmbeddedTemplateProvider(string defaultLocale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        this.defaultLocale = defaultLocale;
    }

    public NotificationTemplate Get(string templateKey, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        return TryLoad(templateKey, locale)
            ?? TryLoad(templateKey, defaultLocale)
            ?? throw new InvalidOperationException(
                $"No notification template found for key '{templateKey}' in locale '{locale}' or default locale '{defaultLocale}'.");
    }

    public void EnsureKeysPresent(IEnumerable<string> templateKeys)
    {
        ArgumentNullException.ThrowIfNull(templateKeys);

        var missing = templateKeys
            .Where(key => TryLoad(key, defaultLocale) is null)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing notification templates in default locale '{defaultLocale}': {string.Join(", ", missing)}.");
        }
    }

    private NotificationTemplate? TryLoad(string templateKey, string locale)
    {
        var resourceName = $"{ResourceRoot}.{locale}.{templateKey}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<TemplateDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Notification template '{resourceName}' is empty or invalid.");

        return new NotificationTemplate(
            Subject: document.Subject ?? string.Empty,
            BodyText: document.BodyText ?? string.Empty,
            BodyHtml: document.BodyHtml ?? string.Empty);
    }

    private sealed record TemplateDocument(string? Subject, string? BodyText, string? BodyHtml);
}
