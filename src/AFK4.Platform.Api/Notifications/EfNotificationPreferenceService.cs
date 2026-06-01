using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>EF Core <see cref="INotificationPreferenceService"/> over <see cref="PlatformDbContext"/>.</summary>
public sealed class EfNotificationPreferenceService(PlatformDbContext db, TimeProvider timeProvider) : INotificationPreferenceService
{
    public async Task<bool> IsSuppressedAsync(
        NotificationRecipient recipient,
        NotificationCategory category,
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);

        // Transactional always sends, regardless of any stored preference (D4).
        if (category == NotificationCategory.Transactional)
        {
            return false;
        }

        if (recipient.StaffUserId is null && recipient.PlayerAccountId is null)
        {
            return false;
        }

        var categoryName = category.ToString();
        var channelName = channel.ToString();

        var preference = await db.NotificationPreferences.FirstOrDefaultAsync(
            row => row.Category == categoryName
                && row.Channel == channelName
                && ((recipient.StaffUserId != null && row.StaffUserId == recipient.StaffUserId)
                    || (recipient.PlayerAccountId != null && row.PlayerAccountId == recipient.PlayerAccountId)),
            cancellationToken);

        return preference is { OptedOut: true };
    }

    public async Task SetPreferenceAsync(
        Guid? staffUserId,
        Guid? playerAccountId,
        NotificationCategory category,
        NotificationChannel channel,
        bool optedOut,
        CancellationToken cancellationToken)
    {
        if (category == NotificationCategory.Transactional)
        {
            throw new InvalidOperationException("Transactional notifications cannot be opted out of.");
        }

        if (staffUserId is null == (playerAccountId is null))
        {
            throw new ArgumentException("Exactly one of staffUserId or playerAccountId must be supplied.");
        }

        var categoryName = category.ToString();
        var channelName = channel.ToString();

        var existing = await db.NotificationPreferences.FirstOrDefaultAsync(
            row => row.Category == categoryName
                && row.Channel == channelName
                && row.StaffUserId == staffUserId
                && row.PlayerAccountId == playerAccountId,
            cancellationToken);

        if (existing is null)
        {
            db.NotificationPreferences.Add(new NotificationPreferenceEntity
            {
                NotificationPreferenceId = Guid.NewGuid(),
                StaffUserId = staffUserId,
                PlayerAccountId = playerAccountId,
                Category = categoryName,
                Channel = channelName,
                OptedOut = optedOut,
                UpdatedUtc = timeProvider.GetUtcNow(),
            });
        }
        else
        {
            existing.OptedOut = optedOut;
            existing.UpdatedUtc = timeProvider.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
