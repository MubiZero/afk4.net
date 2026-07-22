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

internal static class PlayerManagementEndpoints
{
    public static void MapPlayerManagementEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/players", async (
            Guid branchId,
            CreatePlayerAccountRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreatePlayerAccount,
                    "PlayerAccount",
                    null,
                    AuditOutcome.Denied,
                    new { request.DisplayName, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await billingCommandService.CreatePlayerAccountAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreatePlayerAccount,
                "PlayerAccount",
                result.Response!.PlayerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.DisplayName },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/branches/{branchId:guid}/players/{playerAccountId:guid}/pin", async (
            Guid branchId,
            Guid playerAccountId,
            SetPlayerPinRequest request,
            StaffAuthorizationService authorizationService,
            IPlayerCredentialService credentialService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length < 4)
            {
                return Results.BadRequest(new { error = "PIN must be at least 4 characters." });
            }

            var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
                p => p.PlayerAccountId == playerAccountId
                    && p.OrganizationId == authorization.StaffContext!.OrganizationId,
                cancellationToken);
            if (account is null)
            {
                return Results.NotFound();
            }

            await credentialService.SetPasswordAsync(playerAccountId, request.Pin, cancellationToken);
            return Results.NoContent();
        });

        app.MapPatch("/api/branches/{branchId:guid}/players/{playerAccountId:guid}", async (
            Guid branchId,
            Guid playerAccountId,
            UpdatePlayerAccountRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdatePlayerAccount,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.DisplayName, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await billingCommandService.UpdatePlayerAccountAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                playerAccountId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdatePlayerAccount,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.DisplayName },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/branches/{branchId:guid}/players/{playerAccountId:guid}/active-state", async (
            Guid branchId,
            Guid playerAccountId,
            SetPlayerActiveStateRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var auditAction = request.IsActive
                ? AuditActionNames.ActivatePlayerAccount
                : AuditActionNames.DeactivatePlayerAccount;

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    auditAction,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await billingCommandService.SetPlayerActiveStateAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                playerAccountId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.IsActive },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/branches/{branchId:guid}/players", async (
            Guid branchId,
            string? query,
            int? limit,
            bool? includeInactive,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IOperatorReferenceDataService referenceDataService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ViewPlayers,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewPlayers,
                    "PlayerAccount",
                    null,
                    AuditOutcome.Denied,
                    new { query, limit, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var players = await referenceDataService.SearchPlayersAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                limit ?? 20,
                includeInactive ?? false,
                cancellationToken);

            return Results.Ok(players);
        });

        app.MapGet("/api/players/{playerAccountId:guid}/wallet-summary", async (
            Guid playerAccountId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.ViewBilling,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            if (!player.Authorization!.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerAccountId, cancellationToken);

            return summary is null
                ? Results.NotFound()
                : Results.Ok(summary);
        });

        app.MapGet("/api/players/{playerAccountId:guid}/ledger", async (
            Guid playerAccountId,
            string? entryType,
            string? accountType,
            string? before,
            int? limit,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            if (!PlayerLedgerFilter.IsValidEntryType(entryType))
            {
                return Results.BadRequest(new { Error = $"Unknown entryType '{entryType}'." });
            }

            if (!PlayerLedgerFilter.IsValidAccountType(accountType))
            {
                return Results.BadRequest(new { Error = $"Unknown accountType '{accountType}'." });
            }

            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.ViewBilling,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            if (!player.Authorization!.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var page = await PlayerLedgerProjector.GetLedgerPageAsync(
                dbContext,
                playerAccountId,
                entryType,
                accountType,
                before,
                PlayerLedgerFilter.ClampLimit(limit),
                cancellationToken);

            return Results.Ok(page);
        });

        app.MapPost("/api/players/{playerAccountId:guid}/wallet/top-ups", async (
            Guid playerAccountId,
            TopUpWalletRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.TopUpWallet,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            var authorization = player.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    player.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.TopUpWallet,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Amount, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var inactiveGuard = RejectInactivePlayerMoneyAction(player.Player);
            if (inactiveGuard is not null)
            {
                return inactiveGuard;
            }

            var result = await billingCommandService.TopUpWalletAsync(
                playerAccountId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.TopUpWallet,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Amount },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/players/{playerAccountId:guid}/ledger/{ledgerEntryId:guid}/refunds", async (
            Guid playerAccountId,
            Guid ledgerEntryId,
            RefundLedgerEntryRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            IMoneyActionPolicyResolver moneyActionPolicyResolver,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.RefundLedgerEntry,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            var authorization = player.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    player.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.RefundLedgerEntry,
                    "LedgerEntry",
                    ledgerEntryId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Amount, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (request.LedgerEntryId != ledgerEntryId)
            {
                return Results.BadRequest(new { Error = "Route ledgerEntryId must match request LedgerEntryId." });
            }

            var inactiveGuard = RejectInactivePlayerMoneyAction(player.Player);
            if (inactiveGuard is not null)
            {
                return inactiveGuard;
            }

            var originalEntry = await dbContext.LedgerEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entry =>
                        entry.OrganizationId == authorization.StaffContext.OrganizationId &&
                        entry.BranchId == player.BranchId &&
                        entry.PlayerAccountId == playerAccountId &&
                        entry.LedgerEntryId == ledgerEntryId,
                    cancellationToken);
            if (originalEntry is null)
            {
                return Results.NotFound();
            }

            // §5.2: gate the direct refund through the same guard as /money-actions. Over-threshold/over-cap
            // refunds cannot be pushed straight to the ledger here — they must go through the approval front door.
            var refundGate = await GuardLegacyMoneyActionAsync(
                dbContext,
                moneyActionPolicyResolver,
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                MoneyActionType.Refund,
                originalEntry.AccountType,
                -Math.Abs(request.Amount.MinorUnits),
                cancellationToken);
            if (refundGate is not null)
            {
                return refundGate;
            }

            var result = await billingCommandService.RefundLedgerEntryAsync(
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.RefundLedgerEntry,
                "LedgerEntry",
                result.Response!.LedgerEntryId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.LedgerEntryId, request.Amount },
                cancellationToken,
                amountMinorUnits: Math.Abs(request.Amount.MinorUnits));

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/players/{playerAccountId:guid}/ledger/manual-corrections", async (
            Guid playerAccountId,
            ManualLedgerCorrectionRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            IMoneyActionPolicyResolver moneyActionPolicyResolver,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.ManualLedgerCorrection,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            var authorization = player.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    player.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ManualLedgerCorrection,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.AccountType, request.Amount, request.QuantitySeconds, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var inactiveGuard = RejectInactivePlayerMoneyAction(player.Player);
            if (inactiveGuard is not null)
            {
                return inactiveGuard;
            }

            // §5.2: gate the direct correction through the same guard as /money-actions. Over-threshold/over-cap
            // corrections (including debt write-offs) cannot bypass the approval front door here.
            var correctionGate = await GuardLegacyMoneyActionAsync(
                dbContext,
                moneyActionPolicyResolver,
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                MoneyActionType.ManualCorrection,
                request.AccountType,
                request.Amount.MinorUnits,
                cancellationToken);
            if (correctionGate is not null)
            {
                return correctionGate;
            }

            var result = await billingCommandService.ManualCorrectionAsync(
                playerAccountId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ManualLedgerCorrection,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.AccountType, request.Amount, request.QuantitySeconds },
                cancellationToken,
                amountMinorUnits: Math.Abs(request.Amount.MinorUnits));

            return Results.Ok(result.Response);
        });

        // Anti-fraud control layer (§5.2): the guarded front door for high-risk money actions. The guard
        // decides execute-now / hold-for-approval / refuse before any ledger write; approval replays the
        // action through the verified billing path with a second pair of eyes.
    }
}
