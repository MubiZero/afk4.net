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

internal static class PackageEndpoints
{
    public static void MapPackageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/packages", async (
            Guid branchId,
            CreatePackageDefinitionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPackageService packageService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManagePackages,
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
                    AuditActionNames.CreatePackageDefinition,
                    "PackageDefinition",
                    null,
                    AuditOutcome.Denied,
                    new { request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await packageService.CreatePackageDefinitionAsync(
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
                AuditActionNames.CreatePackageDefinition,
                "PackageDefinition",
                result.Response!.PackageDefinitionId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Name, request.Price },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPatch("/api/branches/{branchId:guid}/packages/{packageDefinitionId:guid}", async (
            Guid branchId,
            Guid packageDefinitionId,
            UpdatePackageDefinitionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPackageService packageService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManagePackages,
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
                    AuditActionNames.UpdatePackageDefinition,
                    "PackageDefinition",
                    packageDefinitionId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Name, request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await packageService.UpdatePackageDefinitionAsync(
                branchId,
                packageDefinitionId,
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
                AuditActionNames.UpdatePackageDefinition,
                "PackageDefinition",
                result.Response!.PackageDefinitionId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Name, request.Price, request.IsActive },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/branches/{branchId:guid}/packages/options", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IOperatorReferenceDataService referenceDataService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ViewPackages,
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
                    AuditActionNames.ViewPackages,
                    "PackageDefinition",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var options = await referenceDataService.GetPackageOptionsAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return Results.Ok(options);
        });

        app.MapPost("/api/players/{playerAccountId:guid}/packages/purchases", async (
            Guid playerAccountId,
            PurchasePackageRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPackageService packageService,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.PurchasePackage,
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
                    AuditActionNames.PurchasePackage,
                    "PackageDefinition",
                    request.PackageDefinitionId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
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

            var result = await packageService.PurchasePackageAsync(
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
                AuditActionNames.PurchasePackage,
                "PlayerPackage",
                result.Response!.PlayerPackageId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.PackageDefinitionId },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/players/{playerAccountId:guid}/packages", async (
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

            var authorization = player.Authorization!;
            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var packages = await dbContext.PlayerPackages
                .AsNoTracking()
                .Where(package =>
                    package.PlayerAccountId == playerAccountId &&
                    package.OrganizationId == authorization.StaffContext!.OrganizationId &&
                    package.BranchId == player.BranchId)
                .OrderByDescending(package => package.PurchasedAtUtc)
                .ToListAsync(cancellationToken);

            var response = new List<PlayerPackageDto>();
            foreach (var package in packages)
            {
                var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
                    dbContext,
                    package.PlayerPackageId,
                    cancellationToken);
                response.Add(new PlayerPackageDto(
                    package.PlayerPackageId,
                    package.PackageDefinitionId,
                    package.PlayerAccountId,
                    package.Name,
                    new MoneyDto(package.CurrencyCode, package.PurchasedPriceMinorUnits),
                    package.IncludedSeconds,
                    package.BonusSeconds,
                    remaining.IncludedSeconds,
                    remaining.BonusSeconds,
                    package.PurchasedAtUtc,
                    package.ExpiresAtUtc));
            }

            return Results.Ok(response);
        });

    }
}
