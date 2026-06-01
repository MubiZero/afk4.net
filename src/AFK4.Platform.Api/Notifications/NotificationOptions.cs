namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Configuration for the notification backbone, bound from the <c>Notifications</c> section.
/// SMTP/channel fields are added by the email channel slice; this carries the cross-cutting
/// settings the service and dispatcher need.
/// </summary>
public sealed class NotificationOptions
{
    public const string ConfigurationSection = "Notifications";

    /// <summary>Locale used when a recipient has none / an unknown one (D12).</summary>
    public string DefaultLocale { get; set; } = "ru";
}
