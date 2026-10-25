using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Публичный webhook Eskhata Merchant. Тело БЕЗ подписи — защита только IP allowlist банка,
// поэтому перед зачислением статус ПЕРЕПРОВЕРЯЕТСЯ запросом /orders/status (там наша подпись).
// Идемпотентность — ключ биллинга (intentId); intent.State — лишь короткий путь для повторов.
internal static class EskhataPaymentEndpoints
{
    public static void MapEskhataPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/payments/eskhata/webhook", async (
            HttpRequest httpRequest,
            IEskhataMerchantClientFactory clientFactory,
            EfBillingCommandService billingCommandService,
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
            if (await EskhataSettlement.FetchStatusAsync(client, intent, ct) != "COMPLETED")
            {
                return Results.Ok(); // не подтверждено API → без кредита
            }

            // Отменённая игроком заявка тоже зачисляется: QR уже оплачен, деньги у клуба.
            // Не вышло зачислить — 503, чтобы банк повторил; не повторит — подберёт сверка.
            return await EskhataSettlement.SettleConfirmedAsync(billingCommandService, db, intent, timeProvider.GetUtcNow())
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }).RequireRateLimiting("payment-webhook");
    }
}
