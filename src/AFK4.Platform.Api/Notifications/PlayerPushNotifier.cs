using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Поводы написать игроку, у которых есть событие: деньги зачислили, заказ собрали. В отличие от
/// напоминаний по времени искать их не нужно — достаточно позвать отсюда там, где это случилось.
///
/// Ошибка доставки здесь никогда не роняет операцию: пополнение состоялось, даже если пуш не ушёл.
/// </summary>
public sealed class PlayerPushNotifier(
    PlatformDbContext dbContext,
    INotificationService notifications,
    ILogger<PlayerPushNotifier> logger)
{
    public async Task BalanceToppedUpAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        long amountMinorUnits,
        long balanceMinorUnits,
        string currencyCode,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var locale = await LocaleAsync(playerAccountId, cancellationToken);
        await SendAsync(
            NotificationTemplateKeys.PlayerBalanceToppedUp,
            playerAccountId,
            organizationId,
            branchId,
            new Dictionary<string, string>
            {
                ["amount"] = MoneyFormatting.ToDisplayString(amountMinorUnits, currencyCode, locale),
                ["balance"] = MoneyFormatting.ToDisplayString(balanceMinorUnits, currencyCode, locale),
            },
            $"player.balance_topped_up:{idempotencyKey}",
            cancellationToken);
    }

    public async Task OrderReadyAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        string items,
        string seatName,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await SendAsync(
            NotificationTemplateKeys.PlayerOrderReady,
            playerAccountId,
            organizationId,
            branchId,
            new Dictionary<string, string>
            {
                ["items"] = items,
                ["seat"] = seatName,
            },
            $"player.order_ready:{idempotencyKey}",
            cancellationToken);

    private async Task<string> LocaleAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.PlayerAccountId == playerAccountId)
            .Select(account => account.PreferredLocale)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    private async Task SendAsync(
        string templateKey,
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        IReadOnlyDictionary<string, string> tokens,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var locale = await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(account => account.PlayerAccountId == playerAccountId)
                .Select(account => account.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken);

            await notifications.SendAsync(
                new NotificationRequest(
                    templateKey,
                    NotificationCategory.Operational,
                    new NotificationRecipient(locale ?? string.Empty, PlayerAccountId: playerAccountId),
                    tokens,
                    idempotencyKey,
                    PreferredChannels: [NotificationChannel.Push],
                    OrganizationId: organizationId,
                    BranchId: branchId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Деньги уже зачислены, заказ уже собран. Уронить операцию из-за неотправленного
            // уведомления — куда хуже, чем не отправить уведомление.
            logger.LogWarning(exception, "Failed to queue player push '{TemplateKey}'.", templateKey);
        }
    }
}
