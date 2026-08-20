using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Разовое объявление о смене правил PIN — пушем, а не SMS: SMS платные, а сказать нужно один раз
/// и многим. Уходит вместе с концом перехода: постоянного механизма «объявление всем игрокам» в
/// проекте нет и заводить его ради одной новости не нужно.
///
/// Адресаты — те, у кого есть зарегистрированное устройство и ещё нет сетевого PIN. Беспокоить
/// того, кто PIN уже задал, значит учить его тому, что он сделал.
/// </summary>
public sealed class PinMigrationAnnouncer(
    PlatformDbContext dbContext,
    INotificationService notifications,
    ILogger<PinMigrationAnnouncer> logger)
{
    public async Task<int> AnnounceAsync(CancellationToken cancellationToken)
    {
        var accountIdsWithDevices = await dbContext.PlayerDevices
            .AsNoTracking()
            .Select(device => device.PlayerAccountId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var personIdsWithPin = await dbContext.PlatformPersons
            .AsNoTracking()
            .Where(person => person.PinHash != null)
            .Select(person => person.PlatformPersonId)
            .ToListAsync(cancellationToken);

        var targets = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.IsActive
                && accountIdsWithDevices.Contains(account.PlayerAccountId)
                && (account.PlatformPersonId == null
                    || !personIdsWithPin.Contains(account.PlatformPersonId.Value)))
            .Select(account => new
            {
                account.PlayerAccountId,
                account.OrganizationId,
                account.HomeBranchId,
                account.PreferredLocale,
            })
            .ToListAsync(cancellationToken);

        var queued = 0;
        foreach (var target in targets)
        {
            try
            {
                // Ключ идемпотентности на человека, а не на нажатие: вторая попытка разослать
                // объявление ничего не удваивает, и это важнее удобства — пуш будят телефон.
                await notifications.SendAsync(
                    new NotificationRequest(
                        TemplateKey: NotificationTemplateKeys.PlayerPinMigration,
                        Category: NotificationCategory.Operational,
                        Recipient: new NotificationRecipient(
                            Locale: target.PreferredLocale ?? string.Empty,
                            PlayerAccountId: target.PlayerAccountId),
                        Tokens: new Dictionary<string, string>(),
                        IdempotencyKey: $"player.pin_migration:{target.PlayerAccountId:N}",
                        PreferredChannels: [NotificationChannel.Push],
                        OrganizationId: target.OrganizationId,
                        BranchId: target.HomeBranchId),
                    cancellationToken);
                queued++;
            }
            catch (Exception exception)
            {
                // Один непринятый адресат не отменяет рассылку остальным.
                logger.LogWarning(
                    exception,
                    "Failed to queue the PIN migration notice for player {PlayerAccountId}.",
                    target.PlayerAccountId);
            }
        }

        logger.LogInformation("Queued the PIN migration notice for {Queued} of {Targets} players.", queued, targets.Count);
        return queued;
    }
}
