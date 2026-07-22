using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Payments.Dc;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// DC-пополнение в Кассе: кассир заводит намерение → ссылка/QR → игрок платит → кассир
// подтверждает существующим /api/wallet/top-up-intents/{id}/fulfil. Здесь только create + cancel.
internal static class DcTopUpEndpoints
{
    public static void MapDcTopUpEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/pos/dc-topups", async (
            Guid branchId,
            CreateDcTopUpRequest request,
            StaffAuthorizationService authorizationService,
            ISecretProtector secretProtector,
            PlatformDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.TopUpWallet, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.AmountMinorUnits <= 0)
                return Results.BadRequest(new { Error = "Amount must be greater than zero." });

            var orgId = authorization.StaffContext!.OrganizationId;
            var config = await db.DcPayLinkConfigs.AsNoTracking()
                .SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null && c.IsActive, ct);
            if (config is null || string.IsNullOrEmpty(config.ReceivingCardEncrypted))
                return Results.Json(new { Error = "dc_not_configured" }, statusCode: StatusCodes.Status409Conflict);

            var player = await db.PlayerAccounts.AsNoTracking()
                .SingleOrDefaultAsync(p => p.PlayerAccountId == request.PlayerAccountId && p.OrganizationId == orgId, ct);
            if (player is null) return Results.NotFound(new { Error = "Player was not found." });

            var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? "TJS" : request.CurrencyCode.Trim().ToUpperInvariant();

            var intentId = Guid.NewGuid();
            var reference = intentId.ToString("N")[..8];
            var comment = DcPayLink.BuildComment(config.CommentTemplate, reference);
            var card = secretProtector.Unprotect(config.ReceivingCardEncrypted);
            var payUrl = DcPayLink.BuildUrl(card, request.AmountMinorUnits, comment);

            var now = timeProvider.GetUtcNow();
            var intent = new PaymentIntentEntity
            {
                PaymentIntentId = intentId,
                PlayerAccountId = request.PlayerAccountId,
                OrganizationId = orgId,
                BranchId = branchId,
                AmountMinorUnits = request.AmountMinorUnits,
                CurrencyCode = currencyCode,
                Purpose = "wallet_topup",
                State = "pending",
                Method = "dc",
                GatewayPayUrl = payUrl,
                GatewayComment = comment,
                CreatedAtUtc = now
            };
            db.PaymentIntents.Add(intent);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new DcTopUpDto(
                intentId, payUrl, comment, request.AmountMinorUnits, currencyCode, config.CardLast4));
        });

        app.MapPost("/api/branches/{branchId:guid}/pos/dc-topups/{intentId:guid}/cancel", async (
            Guid branchId,
            Guid intentId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.TopUpWallet, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var intent = await db.PaymentIntents.SingleOrDefaultAsync(
                i => i.PaymentIntentId == intentId && i.OrganizationId == orgId && i.BranchId == branchId, ct);
            if (intent is null || intent.Method != "dc")
                return Results.NotFound(new { Error = "DC top-up was not found." });
            if (intent.State != "pending")
                return Results.Conflict(new { Error = "Only a pending DC top-up can be cancelled." });

            intent.State = "cancelled";
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
