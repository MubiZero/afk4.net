using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Resolves and records recipient opt-outs (D4). Transactional notifications always send; only
/// Operational/Digest categories honour preferences, defaulting to on when no row exists.
/// </summary>
public interface INotificationPreferenceService
{
    Task<bool> IsSuppressedAsync(
        NotificationRecipient recipient,
        NotificationCategory category,
        NotificationChannel channel,
        CancellationToken cancellationToken);

    /// <summary>Upsert an opt-out. Throws if <paramref name="category"/> is Transactional (§8).</summary>
    Task SetPreferenceAsync(
        Guid? staffUserId,
        Guid? playerAccountId,
        NotificationCategory category,
        NotificationChannel channel,
        bool optedOut,
        CancellationToken cancellationToken);
}
