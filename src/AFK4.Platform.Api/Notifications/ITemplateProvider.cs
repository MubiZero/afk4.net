namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Resolves a <see cref="NotificationTemplate"/> for a <c>(templateKey, locale)</c> pair, falling
/// back to the configured default locale when the requested locale has no template. The current
/// implementation loads embedded resource files; a future DB-backed provider can implement the
/// same interface for owner-editable copy (D9).
/// </summary>
public interface ITemplateProvider
{
    NotificationTemplate Get(string templateKey, string locale);

    /// <summary>
    /// Throws if any of the supplied keys has no template in the default locale. Used at startup
    /// to fail fast on a missing template rather than dropping a send at runtime.
    /// </summary>
    void EnsureKeysPresent(IEnumerable<string> templateKeys);
}
