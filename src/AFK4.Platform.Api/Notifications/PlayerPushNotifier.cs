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
    /// <summary>
    /// Сколько ответа клуба уходит в пуш. Целиком его читают в приложении, а длинное шторка
    /// всё равно обрежет — только посреди слова.
    /// </summary>
    internal const int ReplyExcerptLength = 120;

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

    /// <summary>
    /// Клуб ответил на заявку. Ожидание ответа — самое тревожное место продукта: деньги
    /// заморожены с момента отправки, а узнать решение раньше можно было только открыв
    /// приложение и потянув список вниз.
    /// </summary>
    public async Task ReservationAnsweredAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        DateTimeOffset startsAtUtc,
        bool confirmed,
        string? rejectReasonNote,
        CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .AsNoTracking()
            .Where(candidate => candidate.BranchId == branchId)
            .Select(candidate => new { candidate.Name, candidate.PreferredTimeZone })
            .FirstOrDefaultAsync(cancellationToken);

        var tokens = new Dictionary<string, string>
        {
            ["club"] = branch?.Name ?? string.Empty,
            ["time"] = ClubLocalTime.At(startsAtUtc, branch?.PreferredTimeZone),
        };

        if (!confirmed)
        {
            // Причина — словами администратора, если он их написал. Кода отказа здесь нет
            // намеренно: он служебный, и переводить его в текст пришлось бы вторым словарём
            // рядом с тем, который уже живёт в приложении.
            tokens["reason"] = (rejectReasonNote ?? string.Empty).Trim();
        }

        await SendAsync(
            confirmed
                ? NotificationTemplateKeys.PlayerReservationConfirmed
                : NotificationTemplateKeys.PlayerReservationRejected,
            playerAccountId,
            organizationId,
            branchId,
            tokens,
            $"player.reservation_answered:{playerAccountId}:{startsAtUtc:O}:{confirmed}",
            cancellationToken);
    }

    /// <summary>
    /// Клуб ответил на отзыв. Без пуша ответ читали бы только случайно: игрок пишет отзыв один
    /// раз и в список отзывов клуба сам не возвращается. Ключ — по отзыву, а не по тексту: пуш
    /// один на отзыв, и двойное нажатие «Ответить» не присылает его дважды.
    /// </summary>
    public async Task ReviewRepliedAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        Guid reviewId,
        string reply,
        CancellationToken cancellationToken)
    {
        var club = await dbContext.Branches
            .AsNoTracking()
            .Where(candidate => candidate.BranchId == branchId)
            .Select(candidate => candidate.Name)
            .FirstOrDefaultAsync(cancellationToken);

        await SendAsync(
            NotificationTemplateKeys.PlayerReviewReplied,
            playerAccountId,
            organizationId,
            branchId,
            new Dictionary<string, string>
            {
                ["club"] = club ?? string.Empty,
                ["reply"] = Excerpt(reply, ReplyExcerptLength),
            },
            $"player.review_replied:{reviewId:N}",
            cancellationToken);
    }

    /// <summary>
    /// Начало текста по границе слова, с «…», если пришлось резать. Переводы строк схлопываются:
    /// в шторке уведомления это одна-две строки, и абзацы в ней выглядят обрывами.
    /// </summary>
    internal static string Excerpt(string text, int limit)
    {
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= limit)
        {
            return flat;
        }

        var cut = flat.LastIndexOf(' ', limit);
        if (cut <= 0)
        {
            // Одно длинное слово без пробелов — режем по месту, но не посреди суррогатной пары:
            // половина эмодзи в пуше рисуется квадратиком.
            cut = char.IsHighSurrogate(flat[limit - 1]) ? limit - 1 : limit;
        }

        return flat[..cut].TrimEnd(' ', ',', '.', ';', ':', '—', '-') + "…";
    }

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
