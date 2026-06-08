using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Payments.DcGate;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.OwnerCodes;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PaymentGatewayEndpoints
{
    public static void MapPaymentGatewayEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/payments/dcgate/webhook", async (
            HttpRequest httpRequest,
            IBranchPaymentGatewayResolver gatewayResolver,
            ISecretProtector secretProtector,
            IBillingCommandService billingCommandService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            httpRequest.EnableBuffering();
            string rawBody;
            using (var reader = new StreamReader(httpRequest.Body, Encoding.UTF8, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync(cancellationToken);
            }
            httpRequest.Body.Position = 0;

            if (!httpRequest.Headers.TryGetValue("x-dcgate-project-id", out var projectIdHeader)
                || string.IsNullOrWhiteSpace(projectIdHeader.ToString()))
            {
                return Results.Unauthorized();
            }

            var gateway = await gatewayResolver.ResolveByProjectIdAsync(
                projectIdHeader.ToString(), cancellationToken);
            if (gateway is null)
            {
                return Results.Unauthorized();
            }

            string webhookSecret;
            try
            {
                webhookSecret = secretProtector.Unprotect(gateway.WebhookSecretEncrypted);
            }
            catch (Exception)
            {
                return Results.Unauthorized();
            }

            if (!DcGateSignatureIsValid(httpRequest, rawBody, webhookSecret))
            {
                return Results.Unauthorized();
            }

            DcGateWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<DcGateWebhookPayload>(
                    rawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.EventId))
            {
                return Results.BadRequest();
            }

            if (await dbContext.DcGateWebhookEvents.AnyAsync(e => e.EventId == payload.EventId, cancellationToken))
            {
                return Results.Ok();
            }

            if (!Guid.TryParseExact(payload.Payment.ExternalOrderId, "N", out var intentId))
            {
                return Results.Ok();
            }

            var intent = await dbContext.PaymentIntents.SingleOrDefaultAsync(
                i => i.PaymentIntentId == intentId, cancellationToken);
            if (intent is null)
            {
                return Results.Ok();
            }

            switch (payload.EventType)
            {
                case "payment.paid":
                    // Credit unless already fulfilled — this intentionally includes an "expired"
                    // intent, because dcgate may confirm a payment after we locally expired it.
                    if (intent.State != "fulfilled")
                    {
                        var topUpRequest = new TopUpWalletRequest(
                            intent.OrganizationId,
                            new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                            TopUpIntentCreditReason,
                            intent.PaymentIntentId.ToString("N"));

                        var billingResult = await billingCommandService.CreditOnlineTopUpAsync(
                            intent.PlayerAccountId,
                            intent.BranchId,
                            topUpRequest,
                            cancellationToken);

                        if (!billingResult.Succeeded)
                        {
                            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                        }

                        intent.State = "fulfilled";
                        intent.FulfilledAtUtc = DateTimeOffset.UtcNow;
                    }
                    break;

                case "payment.expired":
                    if (intent.State == "pending")
                    {
                        intent.State = "expired";
                    }
                    break;

                case "payment.disputed":
                    intent.Disputed = true;
                    break;

                default:
                    return Results.Ok(); // unknown event type — ack so dcgate stops retrying
            }

            dbContext.DcGateWebhookEvents.Add(new DcGateWebhookEventEntity
            {
                DcGateWebhookEventId = Guid.NewGuid(),
                EventId = payload.EventId,
                EventType = payload.EventType,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Race: a concurrent delivery may have already recorded this event.
                // Re-query to confirm — if the unique index fired on a duplicate, treat as idempotent no-op.
                if (await dbContext.DcGateWebhookEvents.AnyAsync(e => e.EventId == payload.EventId, cancellationToken))
                {
                    return Results.Ok();
                }
                throw;
            }

            return Results.Ok();
        }).RequireRateLimiting("player-public");

        app.MapGet("/api/owner/payment-gateways", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }
            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var orgId = authorization.StaffContext!.OrganizationId;
            var rows = await dbContext.BranchPaymentGateways
                .AsNoTracking()
                .Where(g => g.OrganizationId == orgId)
                .OrderBy(g => g.CreatedAtUtc)
                .Select(g => new OwnerPaymentGatewayDto(
                    g.BranchPaymentGatewayId, g.BranchId, g.DcgateProjectId,
                    g.CardLast4, g.Status, g.CreatedAtUtc, g.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(new OwnerPaymentGatewayListResponse(rows));
        });

        app.MapGet("/api/owner/payment-gateways/telegram-credentials", async (
            string? phone,
            StaffAuthorizationService authorizationService,
            ISecretProtector secretProtector,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var normalized = (phone ?? string.Empty).Trim();
            var existing = await dbContext.OrganizationTelegramApiCredentials.AsNoTracking().SingleOrDefaultAsync(
                c => c.OrganizationId == orgId && c.PhoneNumber == normalized, cancellationToken);
            if (existing is null) return Results.Ok(new OwnerTelegramCredentialsResponse(false, null));
            long apiId;
            try
            {
                apiId = long.Parse(secretProtector.Unprotect(existing.ApiIdEncrypted), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                    title: "telegram_api_credentials_unreadable",
                    detail: "Saved Telegram credentials could not be read. Re-enter them.");
            }
            return Results.Ok(new OwnerTelegramCredentialsResponse(true, apiId));
        });

        app.MapPost("/api/owner/payment-gateways", async (
            ProvisionPaymentGatewayRequest request,
            StaffAuthorizationService authorizationService,
            IDcGateAdminClient adminClient,
            ISecretProtector secretProtector,
            IOptions<DcGateOptions> dcGateOptions,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var options = dcGateOptions.Value;

            if (string.IsNullOrWhiteSpace(options.AdminSecret) || string.IsNullOrWhiteSpace(options.WebhookUrl))
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "online_payment_unavailable",
                    detail: "Payment provisioning is not configured on this environment.");
            }

            var cardNumber = (request.CardNumber ?? string.Empty).Trim();
            if (cardNumber.Length < 12)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cardNumber"] = ["Enter a valid card number."]
                });
            }

            // If a branch scope is given, it must be assigned to the caller.
            if (request.BranchId is Guid branchScope && !authorization.StaffContext.BranchIds.Contains(branchScope))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Serialize the scope-check + insert so two concurrent provisions (e.g. a browser
            // double-submit) can't both pass the check and persist two non-disabled rows for the
            // same scope, which would break Subsystem A's SingleOrDefaultAsync resolver.
            // InMemory test provider doesn't support transactions, so gate on IsRelational().
            var useTransaction = dbContext.Database.IsRelational();
            var tx = useTransaction
                ? await dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;
            await using var _ = tx;

            // One active/pending gateway per scope (A deferred this invariant to B).
            var scopeTaken = await dbContext.BranchPaymentGateways.AnyAsync(g =>
                g.OrganizationId == orgId && g.BranchId == request.BranchId
                && g.Status != BranchPaymentGatewayStatus.Disabled,
                cancellationToken);
            if (scopeTaken)
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "gateway_scope_taken",
                    detail: "This scope already has a payment card. Disable it before adding another.");
            }

            var gatewayId = Guid.NewGuid();
            var name = $"AFK4 / {orgId} / {(request.BranchId?.ToString() ?? "org")}";

            DcGateAdminProjectResult created;
            try
            {
                created = await adminClient.CreateProjectAsync(
                    new DcGateCreateProjectRequest(name, cardNumber, options.WebhookUrl,
                        options.PaymentExpiresInMinutes, gatewayId.ToString()),
                    cancellationToken);
            }
            catch (DcGateAdminException ex)
            {
                // Transaction rolls back on dispose; nothing is persisted.
                return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
            }

            if (string.IsNullOrEmpty(created.ApiKey) || string.IsNullOrEmpty(created.WebhookSecret))
            {
                // Replay with no creds means we lost the first response — cannot persist usable creds.
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "provision_replay_without_secret",
                    detail: "dcgate replayed an existing project without returning credentials. Disable and retry.");
            }

            var now = DateTimeOffset.UtcNow;
            var row = new BranchPaymentGatewayEntity
            {
                BranchPaymentGatewayId = gatewayId,
                OrganizationId = orgId,
                BranchId = request.BranchId,
                DcgateProjectId = created.Id,
                ApiKeyEncrypted = secretProtector.Protect(created.ApiKey),
                WebhookSecretEncrypted = secretProtector.Protect(created.WebhookSecret),
                CardLast4 = string.IsNullOrEmpty(created.CardLast4)
                    ? cardNumber[^4..]
                    : created.CardLast4,
                Status = BranchPaymentGatewayStatus.PendingTelegram,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.BranchPaymentGateways.Add(row);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);

            return Results.Ok(new OwnerPaymentGatewayDto(
                row.BranchPaymentGatewayId, row.BranchId, row.DcgateProjectId,
                row.CardLast4, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc));
        });

        // Resolves a gateway by id scoped to the authenticated owner's org.
        // Returns (row, errorResult); exactly one is non-null.
        static async Task<(BranchPaymentGatewayEntity? Row, IResult? Error)> ResolveOwnerGatewayAsync(
            Guid gatewayId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken ct)
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return (null, Results.Unauthorized());
            if (!authorization.IsAllowed) return (null, Results.StatusCode(StatusCodes.Status403Forbidden));

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await dbContext.BranchPaymentGateways
                .FirstOrDefaultAsync(g => g.BranchPaymentGatewayId == gatewayId && g.OrganizationId == orgId, ct);
            return row is null ? (null, Results.NotFound()) : (row, null);
        }

        // Flip pending_telegram -> active once dcgate reports the session attached.
        static async Task<string> ApplyAttachResultAsync(
            BranchPaymentGatewayEntity row, string state, PlatformDbContext dbContext, CancellationToken ct)
        {
            if (state == DcGateTelegramState.Attached && row.Status == BranchPaymentGatewayStatus.PendingTelegram)
            {
                row.Status = BranchPaymentGatewayStatus.Active;
                row.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(ct);
            }
            return row.Status;
        }

        app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/start", async (
            Guid id, TelegramStartRequest request,
            StaffAuthorizationService authorizationService,
            IDcGateAdminClient adminClient,
            ISecretProtector secretProtector,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
            if (error is not null) return error;
            var orgId = row!.OrganizationId;
            var phone = (request.Phone ?? string.Empty).Trim();

            long apiId;
            string apiHash;
            var existing = await dbContext.OrganizationTelegramApiCredentials.SingleOrDefaultAsync(
                c => c.OrganizationId == orgId && c.PhoneNumber == phone, cancellationToken);

            if (request.ApiId is long suppliedId && !string.IsNullOrWhiteSpace(request.ApiHash))
            {
                if (suppliedId <= 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["apiId"] = ["api_id must be a positive integer."] });
                }
                apiId = suppliedId;
                apiHash = request.ApiHash.Trim();
                var now = DateTimeOffset.UtcNow;
                if (existing is null)
                {
                    dbContext.OrganizationTelegramApiCredentials.Add(new OrganizationTelegramApiCredentialEntity
                    {
                        OrganizationTelegramApiCredentialId = Guid.NewGuid(),
                        OrganizationId = orgId,
                        PhoneNumber = phone,
                        ApiIdEncrypted = secretProtector.Protect(apiId.ToString(CultureInfo.InvariantCulture)),
                        ApiHashEncrypted = secretProtector.Protect(apiHash),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                    try
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException)
                    {
                        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                            title: "telegram_api_credentials_conflict",
                            detail: "Credentials for this phone are being saved concurrently. Retry.");
                    }
                }
                else
                {
                    existing.ApiIdEncrypted = secretProtector.Protect(apiId.ToString(CultureInfo.InvariantCulture));
                    existing.ApiHashEncrypted = secretProtector.Protect(apiHash);
                    existing.UpdatedAtUtc = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            else if (existing is not null)
            {
                try
                {
                    apiId = long.Parse(secretProtector.Unprotect(existing.ApiIdEncrypted), CultureInfo.InvariantCulture);
                    apiHash = secretProtector.Unprotect(existing.ApiHashEncrypted);
                }
                catch (Exception)
                {
                    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                        title: "telegram_api_credentials_unreadable",
                        detail: "Saved Telegram credentials could not be read. Re-enter them.");
                }
            }
            else
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "telegram_api_credentials_required",
                    detail: "Enter api_id and api_hash for this Telegram account.");
            }

            try
            {
                var result = await adminClient.StartTelegramAsync(row.DcgateProjectId, phone, apiId, apiHash, cancellationToken);
                if (result.State == DcGateTelegramState.Attached)
                {
                    await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
                }
                return Results.Ok(new TelegramStartResponse(result.LoginAttemptId, result.State));
            }
            catch (DcGateAdminException ex)
            {
                return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
            }
        });

        app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/verify-code", async (
            Guid id, TelegramVerifyCodeRequest request,
            StaffAuthorizationService authorizationService,
            IDcGateAdminClient adminClient,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
            if (error is not null) return error;
            try
            {
                var result = await adminClient.VerifyTelegramCodeAsync(
                    row!.DcgateProjectId, request.LoginAttemptId, request.Code, cancellationToken);
                var status = await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
                return Results.Ok(new TelegramVerifyResponse(result.State, status));
            }
            catch (DcGateAdminException ex)
            {
                return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
            }
        });

        app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/verify-password", async (
            Guid id, TelegramVerifyPasswordRequest request,
            StaffAuthorizationService authorizationService,
            IDcGateAdminClient adminClient,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
            if (error is not null) return error;
            try
            {
                var result = await adminClient.VerifyTelegramPasswordAsync(
                    row!.DcgateProjectId, request.LoginAttemptId, request.Password, cancellationToken);
                var status = await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
                return Results.Ok(new TelegramVerifyResponse(result.State, status));
            }
            catch (DcGateAdminException ex)
            {
                return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
            }
        });

        app.MapGet("/api/owner/payment-gateways/{id:guid}/status", async (
            Guid id,
            StaffAuthorizationService authorizationService,
            IDcGateAdminClient adminClient,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
            if (error is not null) return error;
            try
            {
                var status = await adminClient.GetStatusAsync(row!.DcgateProjectId, cancellationToken);
                return Results.Ok(new OwnerGatewayStatusResponse(
                    row.Status, status.SessionHealth, status.LastConnectedAt,
                    status.LastMessageAt, status.TelegramMessagesCount));
            }
            catch (DcGateAdminException ex)
            {
                return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
            }
        });

        // Disable a gateway: marks it disabled (idempotent) so its scope frees up for a new card.
        // We keep the dcgate project intact — A's resolver still verifies late webhooks for a
        // disabled gateway, so in-flight payments are still credited.
        app.MapPost("/api/owner/payment-gateways/{id:guid}/disable", async (
            Guid id,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
            if (error is not null) return error;

            if (row!.Status != BranchPaymentGatewayStatus.Disabled)
            {
                row.Status = BranchPaymentGatewayStatus.Disabled;
                row.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new OwnerPaymentGatewayDto(
                row.BranchPaymentGatewayId, row.BranchId, row.DcgateProjectId,
                row.CardLast4, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc));
        });

    }
}
