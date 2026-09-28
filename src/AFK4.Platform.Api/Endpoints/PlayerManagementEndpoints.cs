using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerManagementEndpoints
{
    public static void MapPlayerManagementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/players", async (
            Guid branchId,
            CreatePlayerAccountRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.CreatePlayerAccount,
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

        app.MapPatch("branches/{branchId:guid}/players/{playerAccountId:guid}", async (
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
                OrganizationPermissionNames.CreatePlayerAccount,
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

        app.MapPost("branches/{branchId:guid}/players/{playerAccountId:guid}/active-state", async (
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
                OrganizationPermissionNames.CreatePlayerAccount,
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

        app.MapGet("branches/{branchId:guid}/players", async (
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
                OrganizationPermissionNames.ViewPlayers,
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

        // Репутация по сети. Два маршрута, и оба намеренно НЕ помечены
        // AllowPlatformSupportAccess: чужая клиентура — не предмет обращения в поддержку.
        //
        // Карточка заявки и карточка клиента спрашивают этот же маршрут вместо того, чтобы
        // возить агрегат внутри списков: репутация — единственное действие, которое пишется в
        // аудит на сам факт чтения, и в списке это означало бы запись про каждого, кого клуб
        // просто пролистал.
        app.MapGet("branches/{branchId:guid}/players/reputation/{platformPersonId:guid}", async (
            Guid branchId,
            Guid platformPersonId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPlayerReputationService reputationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewPlayers, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteReputationAuditAsync(
                    auditRecordWriter, authorization, branchId, platformPersonId.ToString("D"),
                    AuditOutcome.Denied, new { Lookup = "person_id", authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var reputation = await reputationService.GetForLinkedPersonAsync(
                authorization.StaffContext!.OrganizationId, platformPersonId, cancellationToken);

            await WriteReputationAuditAsync(
                auditRecordWriter, authorization, branchId, platformPersonId.ToString("D"),
                reputation is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
                new { Lookup = "person_id" },
                cancellationToken);

            // «Нет такой личности» и «личность не наша» отвечают одинаково — иначе перебор
            // идентификаторов сам по себе рассказывал бы, кто в сети есть.
            return reputation is null ? Results.NotFound() : Results.Ok(reputation);
        });

        app.MapPost("branches/{branchId:guid}/players/reputation/lookup", async (
            Guid branchId,
            PlayerReputationLookupRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPlayerReputationService reputationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewPlayers, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteReputationAuditAsync(
                    auditRecordWriter, authorization, branchId, null,
                    AuditOutcome.Denied,
                    new { Lookup = "exact_phone", authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var reputation = await reputationService.GetByExactPhoneAsync(
                request.PhoneNumber, cancellationToken);
            if (reputation is null)
            {
                // Номер, который не может принадлежать никому, ничего ни о ком не выдаёт —
                // поэтому отличаться этому ответу можно. Поиска по части номера через платформу
                // нет: по своим игрокам клуб ищет клубным поиском, чужих через платформу не ищет.
                return Results.BadRequest(new { Error = "invalid_phone" });
            }

            // Идентификатор личности в записи не хранится намеренно: свой аудит клуб читает сам,
            // и заполненный TargetId у знакомого сети номера отличал бы его от незнакомого —
            // ровно то, что скрыл ответ.
            await WriteReputationAuditAsync(
                auditRecordWriter, authorization, branchId, null,
                AuditOutcome.Succeeded,
                new
                {
                    Lookup = "exact_phone",
                    Phone = "+" + PhoneNumberNormalizer.Normalize(request.PhoneNumber)
                },
                cancellationToken);

            return Results.Ok(reputation);
        }).RequireRateLimiting("reputation-lookup");

        app.MapGet("players/{playerAccountId:guid}/wallet-summary", async (
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
                OrganizationPermissionNames.ViewBilling,
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

        app.MapGet("players/{playerAccountId:guid}/ledger", async (
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
                OrganizationPermissionNames.ViewBilling,
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

        app.MapPost("players/{playerAccountId:guid}/wallet/top-ups", async (
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
                OrganizationPermissionNames.TopUpWallet,
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

        app.MapPost("players/{playerAccountId:guid}/ledger/{ledgerEntryId:guid}/refunds", async (
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
                OrganizationPermissionNames.RefundLedgerEntry,
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

        app.MapPost("players/{playerAccountId:guid}/ledger/manual-corrections", async (
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
                OrganizationPermissionNames.ManualLedgerCorrection,
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

    /// <summary>Кто спросил, о ком и когда. Пишется и на успех, и на отказ: перебор виден по отказам.</summary>
    private static Task WriteReputationAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        StaffAuthorizationResult authorization,
        Guid branchId,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.ViewPlayerReputation,
            "platform_person",
            targetId,
            outcome,
            details,
            cancellationToken);
}
