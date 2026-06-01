namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Performs safe <c>{{token}}</c> substitution against a <see cref="NotificationTemplate"/>.
/// Pure and presentation-only: values are formatted upstream by the caller; the renderer simply
/// substitutes, HTML-escaping interpolated values in the HTML body.
/// </summary>
public interface INotificationRenderer
{
    RenderedNotification Render(NotificationTemplate template, IReadOnlyDictionary<string, string> tokens);
}
