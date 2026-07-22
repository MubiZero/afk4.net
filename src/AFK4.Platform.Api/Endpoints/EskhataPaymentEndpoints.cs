using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Публичный webhook Eskhata Merchant. Тело БЕЗ подписи — защита только IP allowlist банка,
// поэтому перед зачислением статус ПЕРЕПРОВЕРЯЕТСЯ запросом /orders/status (там наша подпись).
// Идемпотентность — по intent.State + ключ идемпотентности биллинга (intentId).
internal static class EskhataPaymentEndpoints
{
    private const string CreditReason = "eskhata_online_topup";

    public static void MapEskhataPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/payments/eskhata/webhook", async (
            HttpRequest httpRequest,
            IEskhataMerchantClientFactory clientFactory,
            IBillingCommandService billingCommandService,
            PlatformDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            string raw;
            using (var reader = new StreamReader(httpRequest.Body))
            {
                raw = await reader.ReadToEndAsync(ct);
            }

            JsonElement data;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    return Results.Ok();
                }
                data = dataElement.Clone();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            var orderStatus = data.TryGetProperty("orderStatus", out var s) ? s.GetString() : null;
            var invoiceId = data.TryGetProperty("invoiceId", out var i) ? i.GetString() : null;
            var orderId = data.TryGetProperty("orderId", out var o) ? o.GetString() : null;
            if (orderStatus != "COMPLETED" || string.IsNullOrEmpty(invoiceId) || string.IsNullOrEmpty(orderId))
            {
                return Results.Ok(); // ack, не наш случай
            }

            if (!Guid.TryParseExact(invoiceId, "N", out var intentId))
            {
                return Results.Ok();
            }

            var intent = await db.PaymentIntents.SingleOrDefaultAsync(x => x.PaymentIntentId == intentId, ct);
            if (intent is null || intent.Method != "eskhata")
            {
                return Results.Ok();
            }
            if (intent.GatewayPaymentId != orderId)
            {
                return Results.Ok(); // не совпал заказ → игнор
            }
            if (intent.State == "fulfilled")
            {
                return Results.Ok(); // идемпотентность
            }

            // Перепроверка статуса через API банка (подписанный запрос) перед кредитом.
            var client = await clientFactory.CreateForOrganizationAsync(intent.OrganizationId, ct);
            if (client is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            var verified = await client.GetOrderStatusAsync(
                invoiceId, orderId, intent.AmountMinorUnits,
                intent.CurrencyCode == "TJS" ? "972" : intent.CurrencyCode,
                intent.GatewayPosId ?? 0, ct);
            if (verified != "COMPLETED")
            {
                return Results.Ok(); // не подтверждено API → без кредита
            }

            var topUpRequest = new TopUpWalletRequest(
                intent.OrganizationId,
                new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                CreditReason,
                intent.PaymentIntentId.ToString("N"));

            var billingResult = await billingCommandService.CreditOnlineTopUpAsync(
                intent.PlayerAccountId, intent.BranchId, topUpRequest, ct);
            if (!billingResult.Succeeded)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            intent.State = "fulfilled";
            intent.FulfilledAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);

            return Results.Ok();
        }).RequireRateLimiting("player-public");
    }
}
