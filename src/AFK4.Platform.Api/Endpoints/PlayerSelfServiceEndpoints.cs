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

internal static class PlayerSelfServiceEndpoints
{
    public static void MapPlayerSelfServiceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/profile", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                p => p.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new PlayerProfileDto(
                account.PlayerAccountId,
                account.DisplayName,
                account.PhoneNumber,
                player.PhoneVerified,
                account.PreferredLocale,
                account.MarketingOptIn));
        }).RequireRateLimiting("player-me");

        app.MapPatch("/api/me/profile", async (
            UpdatePlayerProfileRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            if (request.PreferredLocale is not null)
            {
                var locale = request.PreferredLocale.Trim();
                if (locale.Length is 0 or > 16)
                {
                    return Results.BadRequest(new { Error = "PreferredLocale must be 1-16 characters." });
                }

                account.PreferredLocale = locale;
            }

            if (request.MarketingOptIn is not null)
            {
                account.MarketingOptIn = request.MarketingOptIn.Value;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new PlayerProfileDto(
                account.PlayerAccountId,
                account.DisplayName,
                account.PhoneNumber,
                player.PhoneVerified,
                account.PreferredLocale,
                account.MarketingOptIn));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/dashboard", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var dashboard = await PlayerDashboardProjector.GetDashboardAsync(
                dbContext, player.PlayerAccountId, DateTimeOffset.UtcNow, cancellationToken);
            return Results.Ok(dashboard);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/visits", async (
            string? cursor,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var page = await PlayerHistoryProjector.GetVisitsAsync(
                dbContext, player.PlayerAccountId, cursor, cancellationToken);
            return Results.Ok(page);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/visits/{sessionId:guid}/receipt", async (
            Guid sessionId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var receipt = await PlayerHistoryProjector.GetVisitReceiptAsync(
                dbContext, player.PlayerAccountId, sessionId, cancellationToken);
            return receipt is null ? Results.NotFound() : Results.Ok(receipt);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/purchases", async (
            string? cursor,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var page = await PlayerHistoryProjector.GetPurchasesAsync(
                dbContext, player.PlayerAccountId, cursor, cancellationToken);
            return Results.Ok(page);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/wallet/top-up-intent", async (
            PlayerTopUpIntentRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IDcGateClientFactory dcGateClientFactory,
            IBranchPaymentGatewayResolver gatewayResolver,
            ISecretProtector secretProtector,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            if (request.AmountMinorUnits <= 0)
            {
                return Results.BadRequest(new { Error = "Amount must be greater than zero." });
            }

            var method = string.IsNullOrWhiteSpace(request.Method)
                ? "counter"
                : request.Method.Trim().ToLowerInvariant();
            if (method != "counter" && method != "dcgate")
            {
                return Results.BadRequest(new { Error = "Method must be 'counter' or 'dcgate'." });
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? "TJS"
                : request.CurrencyCode.Trim().ToUpperInvariant();

            var now = DateTimeOffset.UtcNow;
            var intent = new PaymentIntentEntity
            {
                PaymentIntentId = Guid.NewGuid(),
                PlayerAccountId = player.PlayerAccountId,
                OrganizationId = player.OrganizationId,
                BranchId = account.HomeBranchId,
                AmountMinorUnits = request.AmountMinorUnits,
                CurrencyCode = currencyCode,
                Purpose = "wallet_topup",
                State = "pending",
                Method = method,
                FulfilledByLedgerEntryId = null,
                CreatedAtUtc = now,
                FulfilledAtUtc = null
            };

            if (method == "dcgate")
            {
                var gateway = await gatewayResolver.ResolveForBranchAsync(
                    intent.OrganizationId, account.HomeBranchId, cancellationToken);
                if (gateway is null)
                {
                    return Results.Json(
                        new { Error = "online_payment_unavailable" },
                        statusCode: StatusCodes.Status409Conflict);
                }

                var apiKey = secretProtector.Unprotect(gateway.ApiKeyEncrypted);
                var dcGateClient = dcGateClientFactory.CreateForApiKey(apiKey);

                var payment = await dcGateClient.CreatePaymentAsync(
                    intent.AmountMinorUnits,
                    intent.CurrencyCode,
                    intent.PaymentIntentId.ToString("N"),
                    new { playerAccountId = intent.PlayerAccountId, branchId = intent.BranchId },
                    cancellationToken);
                intent.GatewayPaymentId = payment.PaymentId;
                intent.GatewayPayUrl = payment.PayUrl;
                intent.GatewayComment = payment.Comment;
                intent.GatewayExpiresAtUtc = payment.ExpiresAt;
            }

            dbContext.PaymentIntents.Add(intent);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new PlayerTopUpIntentDto(
                intent.PaymentIntentId,
                intent.AmountMinorUnits,
                intent.CurrencyCode,
                intent.State,
                intent.Purpose,
                intent.Method,
                intent.CreatedAtUtc,
                intent.FulfilledAtUtc,
                IsExpired: false,
                PayUrl: intent.GatewayPayUrl,
                Comment: intent.GatewayComment,
                GatewayExpiresAtUtc: intent.GatewayExpiresAtUtc));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/wallet/top-up-intents", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var expiryCutoff = now.AddHours(-24);

            var intents = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(intent => intent.PlayerAccountId == player.PlayerAccountId)
                .OrderByDescending(intent => intent.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var dtos = intents.Select(intent => new PlayerTopUpIntentDto(
                intent.PaymentIntentId,
                intent.AmountMinorUnits,
                intent.CurrencyCode,
                intent.State,
                intent.Purpose,
                intent.Method,
                intent.CreatedAtUtc,
                intent.FulfilledAtUtc,
                IsExpired: intent.State == "pending" && intent.CreatedAtUtc < expiryCutoff,
                PayUrl: intent.GatewayPayUrl,
                Comment: intent.GatewayComment,
                GatewayExpiresAtUtc: intent.GatewayExpiresAtUtc))
                .ToList();

            return Results.Ok(dtos);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/reservations", async (
            CreatePlayerReservationRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IReservationService reservationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            // D8 gate: verified phone required for booking actions.
            if (!player.PhoneVerified)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var now = DateTimeOffset.UtcNow;
            if (request.StartsAtUtc >= request.EndsAtUtc)
            {
                return Results.BadRequest(new { Error = "End time must be after start time." });
            }

            if (request.StartsAtUtc <= now)
            {
                return Results.BadRequest(new { Error = "Start time must be in the future." });
            }

            var account = await dbContext.PlayerAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (account is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CreateOnlineAsync(
                player.PlayerAccountId,
                player.OrganizationId,
                account.HomeBranchId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                if (result.Conflict)
                {
                    return Results.Conflict(new { Error = result.Error });
                }

                if (result.NotFound)
                {
                    return Results.NotFound(new { Error = result.Error });
                }

                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(ToPlayerReservationDto(result.Response!));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/reservations", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var reservations = await dbContext.Reservations
                .AsNoTracking()
                .Where(reservation => reservation.PlayerAccountId == player.PlayerAccountId)
                .OrderByDescending(reservation => reservation.StartsAtUtc)
                .ToListAsync(cancellationToken);

            var seatIds = reservations
                .Where(r => r.SeatId is not null)
                .Select(r => r.SeatId!.Value)
                .Distinct()
                .ToList();

            var seatNames = seatIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await dbContext.Seats
                    .AsNoTracking()
                    .Where(seat => seatIds.Contains(seat.SeatId))
                    .ToDictionaryAsync(seat => seat.SeatId, seat => seat.Name, cancellationToken);

            var dtos = reservations.Select(r => new PlayerReservationDto(
                r.ReservationId,
                r.SeatId,
                r.SeatId is not null ? seatNames.GetValueOrDefault(r.SeatId.Value) : null,
                r.StartsAtUtc,
                r.EndsAtUtc,
                r.State,
                string.IsNullOrEmpty(r.Note) ? null : r.Note))
                .ToList();

            return Results.Ok(dtos);
        }).RequireRateLimiting("player-me");

        app.MapDelete("/api/me/reservations/{reservationId:guid}", async (
            Guid reservationId,
            IPlayerContextAccessor playerContextAccessor,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var result = await reservationService.CancelOnlineAsync(
                reservationId,
                player.PlayerAccountId,
                cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound();
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(ToPlayerReservationDto(result.Response!));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/sessions/start", async (
            PlayerSelfStartRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var assignment = await (
                from a in dbContext.DeviceSeatAssignments.AsNoTracking()
                join d in dbContext.Devices.AsNoTracking() on a.DeviceId equals d.DeviceId
                where a.DeviceId == request.DeviceId &&
                      a.OrganizationId == player.OrganizationId &&
                      a.DetachedAtUtc == null &&
                      d.EnrollmentState == DeviceEnrollmentStateNames.Approved
                orderby a.AttachedAtUtc descending
                select a).FirstOrDefaultAsync(cancellationToken);
            if (assignment is null) return Results.NotFound(new { error = "device_not_assigned" });

            if (!Guid.TryParse(request.TariffRuleVersionId, out var tariffVersionId))
                return Results.BadRequest(new { error = "invalid_tariff" });

            var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
                v => v.OrganizationId == player.OrganizationId &&
                     v.BranchId == assignment.BranchId &&
                     v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is null) return Results.BadRequest(new { error = "invalid_tariff" });

            var pricing = new TariffPricing(
                version.PricePerMinuteMinorUnits, version.MinimumBillableMinutes,
                version.RoundingIncrementMinutes, version.CurrencyCode);
            var charge = TariffBilling.ComputeForMinutes(request.DurationMinutes, pricing);
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, player.PlayerAccountId, cancellationToken);
            var walletBalance = wallet?.WalletBalance.MinorUnits ?? 0;
            if (walletBalance < charge.AmountMinorUnits)
                return Results.Conflict(new { error = "insufficient_balance" });

            var startRequest = new StartGuestSessionRequest(
                OrganizationId: player.OrganizationId,
                SeatId: assignment.SeatId,
                TariffRuleVersionId: request.TariffRuleVersionId,
                IdempotencyKey: request.IdempotencyKey,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: request.DurationMinutes,
                PlayerAccountId: player.PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersionId);

            var result = await sessionCommandService.StartGuestSessionAsync(
                assignment.BranchId, Guid.Empty, startRequest, cancellationToken);

            if (result.Conflict) return Results.Conflict(new { error = result.Error });
            if (result.NotFound) return Results.NotFound(new { error = result.Error });
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Response);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/sessions/{sessionId:guid}/extend", async (
            Guid sessionId,
            PlayerSelfExtendRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var session = await dbContext.Sessions.AsNoTracking().SingleOrDefaultAsync(
                s => s.SessionId == sessionId, cancellationToken);

            // Ownership-scoped: a session the caller does not own is indistinguishable from a missing one.
            if (session is null ||
                session.PlayerAccountId != player.PlayerAccountId ||
                session.State != SessionStateNames.Active)
            {
                return Results.NotFound();
            }

            if (!Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
                return Results.BadRequest(new { error = "invalid_tariff" });

            var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
                v => v.OrganizationId == session.OrganizationId &&
                     v.BranchId == session.BranchId &&
                     v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is null) return Results.BadRequest(new { error = "invalid_tariff" });

            var pricing = new TariffPricing(
                version.PricePerMinuteMinorUnits, version.MinimumBillableMinutes,
                version.RoundingIncrementMinutes, version.CurrencyCode);
            var charge = TariffBilling.ComputeForMinutes(request.AdditionalMinutes, pricing);
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, player.PlayerAccountId, cancellationToken);
            if ((wallet?.WalletBalance.MinorUnits ?? 0) < charge.AmountMinorUnits)
                return Results.Conflict(new { error = "insufficient_balance" });

            var extendRequest = new ExtendSessionRequest(
                AdditionalMinutes: request.AdditionalMinutes,
                TariffRuleVersionId: session.TariffRuleVersionId,
                IdempotencyKey: request.IdempotencyKey,
                PlayerAccountId: player.PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersionId);

            var result = await sessionCommandService.ExtendSessionAsync(
                sessionId, Guid.Empty, extendRequest, cancellationToken);

            if (result.Conflict) return Results.Conflict(new { error = result.Error });
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });
            return Results.Ok(result.Response);
        }).RequireRateLimiting("player-me");

    }
}
