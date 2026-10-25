using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Payments.Eskhata;

/// <summary>
/// Зачисление оплаты, которую подтвердил банк. Одно на вебхук, опрос приложения и сверку: раньше
/// у каждого была своя копия, и в каждой отметка «завершено» ставилась до денег.
/// </summary>
internal static class EskhataSettlement
{
    public const string CreditReason = "eskhata_online_topup";

    /// <summary>Статус заказа подписанным запросом к банку — единственное, чему здесь верят.</summary>
    public static Task<string?> FetchStatusAsync(
        IEskhataMerchantClient client, PaymentIntentEntity intent, CancellationToken cancellationToken) =>
        client.GetOrderStatusAsync(
            intent.PaymentIntentId.ToString("N"),
            intent.GatewayPaymentId ?? "",
            intent.AmountMinorUnits,
            intent.CurrencyCode == "TJS" ? "972" : intent.CurrencyCode,
            intent.GatewayPosId ?? 0,
            cancellationToken);

    /// <summary>
    /// Сначала зачисление (идемпотентно по заявке), потом отметка. Процесс упал между ними — заявка
    /// осталась ожидающей при пополненном кошельке, и следующий вебхук, опрос или сверка допишут
    /// отметку без второго зачисления. Обратный порядок терял деньги: отметка есть, денег нет, а
    /// повтор вебхука отвечал «уже сделано».
    ///
    /// Без токена запроса: банк или игрок, оборвавший связь посередине, не должны разорвать пару.
    /// </summary>
    public static async Task<bool> SettleConfirmedAsync(
        EfBillingCommandService billingCommandService,
        PlatformDbContext dbContext,
        PaymentIntentEntity intent,
        DateTimeOffset now)
    {
        var request = new TopUpWalletRequest(
            intent.OrganizationId,
            new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
            CreditReason,
            intent.PaymentIntentId.ToString("N"));
        var credit = await billingCommandService.CreditOnlineTopUpAsync(
            intent.PlayerAccountId, intent.BranchId, request, CancellationToken.None);
        if (!credit.Succeeded)
        {
            return false;
        }

        await PaymentIntentClaim.MarkPaidByBankAsync(dbContext, intent, now, CancellationToken.None);
        return true;
    }
}

/// <summary>
/// Сверка с банком: оплата, о которой не узнали ни вебхук (не дошёл, сервер выкладывался), ни опрос
/// (игрок закрыл приложение). Без неё такие деньги лежали бы у клуба, а кошелёк был бы пуст, пока
/// игрок не придёт ругаться к стойке.
/// </summary>
public sealed class EskhataReconciliationHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<EskhataReconciliationHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    // Свежие заявки ведёт опрос приложения; старше суток заявка просрочена и для игрока.
    private static readonly TimeSpan MinAge = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    // ponytail: сотня свежайших за прогон; при тысячах открытых заявок в сутки старые не дождутся
    // очереди — тогда курсор по CreatedAtUtc.
    private const int BatchSize = 100;

    protected override string JobName => PlatformJobNames.EskhataReconciliation;

    protected override TimeSpan Interval => TickInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var dbContext = scopedServices.GetRequiredService<PlatformDbContext>();
        var clientFactory = scopedServices.GetRequiredService<IEskhataMerchantClientFactory>();
        var billingCommandService = scopedServices.GetRequiredService<EfBillingCommandService>();

        var now = GetUtcNow();
        var newest = now - MinAge;
        var oldest = now - MaxAge;
        // Отменённые тоже: игрок мог отменить заявку и всё равно оплатить уже показанный QR.
        var open = await dbContext.PaymentIntents
            .Where(intent => intent.Method == "eskhata" &&
                intent.GatewayPaymentId != null &&
                (intent.State == PaymentIntentClaim.Pending || intent.State == PaymentIntentClaim.Cancelled) &&
                intent.CreatedAtUtc >= oldest &&
                intent.CreatedAtUtc <= newest)
            .OrderByDescending(intent => intent.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var settled = 0;
        foreach (var organization in open.GroupBy(intent => intent.OrganizationId))
        {
            var client = await clientFactory.CreateForOrganizationAsync(organization.Key, cancellationToken);
            if (client is null)
            {
                continue;
            }

            foreach (var intent in organization)
            {
                if (await EskhataSettlement.FetchStatusAsync(client, intent, cancellationToken) != "COMPLETED")
                {
                    continue;
                }

                if (await EskhataSettlement.SettleConfirmedAsync(billingCommandService, dbContext, intent, GetUtcNow()))
                {
                    settled++;
                    Log.LogWarning(
                        "Eskhata payment {PaymentIntentId} credited by reconciliation: webhook and app poll missed it.",
                        intent.PaymentIntentId);
                }
            }
        }

        return settled;
    }
}
