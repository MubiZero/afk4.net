using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
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
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
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

internal static class WalletEndpoints
{
    public static void MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("wallet/top-up-intents/{intentId:guid}/fulfil", async (
            Guid intentId,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var staffContext = staffContextAccessor.Current;

            var intent = await dbContext.PaymentIntents
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == staffContext.OrganizationId &&
                        candidate.PaymentIntentId == intentId,
                    cancellationToken);

            if (intent is null)
            {
                return Results.NotFound(new { Error = "Payment intent was not found." });
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                intent.BranchId,
                OrganizationPermissionNames.TopUpWallet,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    staffContext.OrganizationId,
                    intent.BranchId,
                    staffContext.StaffUserId,
                    AuditActionNames.FulfilPaymentIntent,
                    "PaymentIntent",
                    intentId.ToString("D"),
                    AuditOutcome.Denied,
                    new { intent.AmountMinorUnits, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Idempotency guard: already fulfilled → return current state, no second credit.
            if (intent.State == "fulfilled")
            {
                return Results.Ok(new PlayerTopUpIntentDto(
                    intent.PaymentIntentId,
                    intent.AmountMinorUnits,
                    intent.CurrencyCode,
                    intent.State,
                    intent.Purpose,
                    intent.Method,
                    intent.CreatedAtUtc,
                    intent.FulfilledAtUtc,
                    IsExpired: false));
            }

            // Expiry guard: pending but >24h old → 409 Conflict.
            if (intent.State == "pending" && intent.CreatedAtUtc < timeProvider.GetUtcNow().AddHours(-24))
            {
                return Results.Conflict(new { Error = "Payment intent has expired." });
            }

            // Only a still-pending intent may be credited. Any other state (e.g. "cancelled",
            // set by DcTopUpEndpoints' cancel action) must be rejected here, not silently credited.
            if (intent.State != "pending")
            {
                return Results.Conflict(new { Error = "Payment intent is not pending." });
            }

            var player = await LoadPlayerForStaffAsync(
                dbContext,
                intent.PlayerAccountId,
                staffContext.OrganizationId,
                cancellationToken);

            var inactiveGuard = RejectInactivePlayerMoneyAction(player);
            if (inactiveGuard is not null)
            {
                return inactiveGuard;
            }

            // The intent id is the idempotency key: it is the authoritative guard against a
            // double wallet credit. If two operators fulfil the same intent concurrently, both
            // pass the in-memory State == "pending" fast-path above, but TopUpWalletAsync
            // deduplicates on this key and writes exactly one ledger entry. The State flip below
            // is then idempotent (same values written twice is harmless).
            var topUpRequest = new TopUpWalletRequest(
                intent.OrganizationId,
                new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                TopUpIntentCreditReason,
                intent.PaymentIntentId.ToString("N"));

            var billingResult = await billingCommandService.TopUpWalletAsync(
                intent.PlayerAccountId,
                intent.BranchId,
                staffContext.StaffUserId,
                topUpRequest,
                cancellationToken);

            if (!billingResult.Succeeded)
            {
                return ToHttpResult(billingResult);
            }

            intent.State = "fulfilled";
            intent.FulfilledAtUtc = timeProvider.GetUtcNow();
            // FulfilledByLedgerEntryId left null (v1): TopUpWalletAsync returns WalletSummaryDto,
            // not the created ledger entry id.
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                intent.BranchId,
                staffContext.StaffUserId,
                AuditActionNames.FulfilPaymentIntent,
                "PaymentIntent",
                intentId.ToString("D"),
                AuditOutcome.Succeeded,
                new { intent.AmountMinorUnits, intent.CurrencyCode },
                cancellationToken);

            return Results.Ok(new PlayerTopUpIntentDto(
                intent.PaymentIntentId,
                intent.AmountMinorUnits,
                intent.CurrencyCode,
                intent.State,
                intent.Purpose,
                intent.Method,
                intent.CreatedAtUtc,
                intent.FulfilledAtUtc,
                IsExpired: false));
        });

        app.MapGet("branches/{branchId:guid}/wallet/top-up-intents", async (
            Guid branchId,
            string? status,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.TopUpWallet,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var stateFilter = string.IsNullOrWhiteSpace(status) ? "pending" : status;

            var intents = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(intent =>
                    intent.OrganizationId == organizationId &&
                    intent.BranchId == branchId &&
                    intent.State == stateFilter)
                .OrderByDescending(intent => intent.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var playerIds = intents.Select(i => i.PlayerAccountId).Distinct().ToList();

            var players = await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(p => playerIds.Contains(p.PlayerAccountId))
                .ToDictionaryAsync(p => p.PlayerAccountId, p => p.DisplayName, cancellationToken);

            var activeSessions = await dbContext.Sessions
                .AsNoTracking()
                .Where(s =>
                    s.BranchId == branchId &&
                    s.State == SessionStateNames.Active &&
                    s.PlayerAccountId != null &&
                    playerIds.Contains(s.PlayerAccountId!.Value))
                .ToListAsync(cancellationToken);

            var seatIds = activeSessions.Select(s => s.SeatId).Distinct().ToList();

            var seats = await dbContext.Seats
                .AsNoTracking()
                .Where(s => seatIds.Contains(s.SeatId))
                .ToDictionaryAsync(s => s.SeatId, s => s.Name, cancellationToken);

            var sessionBySeatLookup = activeSessions
                .GroupBy(s => s.PlayerAccountId!.Value)
                .ToDictionary(g => g.Key, g => g.First().SeatId);

            var items = intents.Select(intent =>
            {
                var displayName = players.TryGetValue(intent.PlayerAccountId, out var name) ? name : string.Empty;
                string? seatName = null;
                if (sessionBySeatLookup.TryGetValue(intent.PlayerAccountId, out var seatId) &&
                    seats.TryGetValue(seatId, out var sn))
                {
                    seatName = sn;
                }

                return new OperatorTopUpIntentDto(
                    intent.PaymentIntentId,
                    intent.PlayerAccountId,
                    displayName,
                    intent.AmountMinorUnits,
                    intent.CurrencyCode,
                    intent.State,
                    intent.Method,
                    intent.CreatedAtUtc,
                    seatName);
            }).ToList();

            return Results.Ok(items);
        });

    }
}
