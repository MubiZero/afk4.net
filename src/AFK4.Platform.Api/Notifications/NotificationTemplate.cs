namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// A localized message template resolved by <see cref="EmbeddedTemplateProvider"/> for a
/// <c>(templateKey, locale)</c> pair. Carries the un-substituted subject and bodies; token
/// substitution is performed separately by <see cref="NotificationRenderer"/>.
/// </summary>
public sealed record NotificationTemplate(string Subject, string BodyText, string BodyHtml);
