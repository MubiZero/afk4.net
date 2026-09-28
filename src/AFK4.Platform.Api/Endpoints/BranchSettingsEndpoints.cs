using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Localization;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class BranchSettingsEndpoints
{
    public static void MapBranchSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/settings", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchSettings,
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
                    AuditActionNames.ViewBranchSettings,
                    "BranchSettings",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var branch = await dbContext.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                    cancellationToken);

            if (branch is null)
            {
                return Results.NotFound();
            }

            var response = new BranchSettingsDto(
                branch.OrganizationId,
                branch.BranchId,
                branch.RequireManualDeviceApproval,
                branch.PreferredLocale,
                MoneyControlPolicy.ResolveDiscrepancyTolerance(
                    branch.ShiftDiscrepancyToleranceMinorUnits,
                    MoneyControlPolicy.DefaultDiscrepancyToleranceMinorUnits));

            return Results.Ok(response);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchSettings);

        app.MapPut("branches/{branchId:guid}/settings", async (
            Guid branchId,
            UpdateBranchSettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchSettings,
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
                    AuditActionNames.UpdateBranchSettings,
                    "BranchSettings",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.RequireManualDeviceApproval, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (!SupportedLocales.IsSupported(request.PreferredLocale))
            {
                return Results.BadRequest(new
                {
                    Error = $"PreferredLocale must be one of: {string.Join(", ", SupportedLocales.All)}."
                });
            }

            var branch = await dbContext.Branches
                .SingleOrDefaultAsync(
                    candidate => candidate.OrganizationId == request.OrganizationId && candidate.BranchId == branchId,
                    cancellationToken);

            if (branch is null)
            {
                return Results.NotFound();
            }

            var changed = branch.RequireManualDeviceApproval != request.RequireManualDeviceApproval
                || branch.PreferredLocale != request.PreferredLocale;
            if (changed)
            {
                branch.RequireManualDeviceApproval = request.RequireManualDeviceApproval;
                branch.PreferredLocale = request.PreferredLocale;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var response = new BranchSettingsDto(
                branch.OrganizationId,
                branch.BranchId,
                branch.RequireManualDeviceApproval,
                branch.PreferredLocale,
                MoneyControlPolicy.ResolveDiscrepancyTolerance(
                    branch.ShiftDiscrepancyToleranceMinorUnits,
                    MoneyControlPolicy.DefaultDiscrepancyToleranceMinorUnits));

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateBranchSettings,
                "BranchSettings",
                branchId.ToString("D"),
                AuditOutcome.Succeeded,
                new { branch.RequireManualDeviceApproval, branch.PreferredLocale, Changed = changed },
                cancellationToken);

            return Results.Ok(response);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchSettings);

        MapBranchBookingSettingsEndpoints(app);
    }

    /// <summary>
    /// Как филиал принимает гостей с платформы. Режим поддержки сюда намеренно не пускают ни
    /// на чтение, ни на запись: принимать ли брони и на каких условиях — решение клуба, и
    /// сотруднику платформы нечего в нём делать даже с добрыми намерениями.
    /// </summary>
    private static void MapBranchBookingSettingsEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/booking-settings", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBranchBookingSettingsService bookingSettingsService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchSettings,
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
                    AuditActionNames.ViewBranchSettings,
                    "BranchBookingSettings",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            if (!await dbContext.Branches.AsNoTracking().AnyAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken))
            {
                return Results.NotFound();
            }

            return Results.Ok(await bookingSettingsService.GetAsync(organizationId, branchId, cancellationToken));
        });

        app.MapPut("branches/{branchId:guid}/booking-settings", async (
            Guid branchId,
            UpdateBranchBookingSettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBranchBookingSettingsService bookingSettingsService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchSettings,
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
                    AuditActionNames.UpdateBookingSettings,
                    "BranchBookingSettings",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.AcceptanceMode, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            if (request.OrganizationId != organizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (BranchBookingSettingsDefaults.Validate(request) is { } validationError)
            {
                return Results.BadRequest(new { Error = validationError });
            }

            if (!await dbContext.Branches.AsNoTracking().AnyAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken))
            {
                return Results.NotFound();
            }

            var updated = await bookingSettingsService.UpdateAsync(
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateBookingSettings,
                "BranchBookingSettings",
                branchId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    updated.AcceptanceMode,
                    updated.RespondWithinMinutes,
                    updated.RequirePrepaymentFromNewGuests,
                    updated.MaxActiveReservationsForNewGuests,
                    updated.RegularAfterVisits,
                    updated.HoldSeatAfterStartMinutes,
                    updated.KeepPrepaymentOnNoShow
                },
                cancellationToken);

            return Results.Ok(updated);
        });
    }
}
