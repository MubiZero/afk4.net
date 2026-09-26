using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Профиль защиты ПК филиала (спека оболочки, §6.3): Панель его правит, агент читает по ключу
/// устройства. Править может тот, кто правит настройки филиала — владелец и управляющий.
/// </summary>
internal static class ProtectionProfileEndpoints
{
    private const string TargetType = "BranchProtectionProfile";

    public static void MapProtectionProfileEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("branches/{branchId:guid}/settings/protection", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchSettings, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewBranchSettings, TargetType, branchId.ToString("D"), AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            if (!await BranchBelongsAsync(dbContext, organizationId, branchId, cancellationToken))
            {
                return Results.NotFound();
            }

            var entity = await dbContext.BranchProtectionProfiles.AsNoTracking()
                .SingleOrDefaultAsync(profile => profile.BranchId == branchId, cancellationToken);
            return Results.Ok(new BranchProtectionProfileDto(organizationId, branchId, ProtectionProfiles.For(entity), entity?.UpdatedAtUtc));
        });

        organizations.MapPut("branches/{branchId:guid}/settings/protection", async (
            Guid branchId,
            UpdateBranchProtectionProfileRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchSettings, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateProtectionProfile, TargetType, branchId.ToString("D"), AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            if (request.OrganizationId != organizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (ProtectionProfiles.Validate(request) is { } validationError)
            {
                return Results.BadRequest(new { Error = validationError });
            }

            if (!await BranchBelongsAsync(dbContext, organizationId, branchId, cancellationToken))
            {
                return Results.NotFound();
            }

            var saved = await ProtectionProfiles.SaveAsync(
                dbContext, organizationId, branchId, authorization.StaffContext.StaffUserId, request, timeProvider.GetUtcNow(), cancellationToken);
            if (saved is null)
            {
                return Results.Conflict(new
                {
                    Error = "The protection profile was saved by someone else; reload it.",
                    Code = ProtectionProfileErrorCodeNames.VersionConflict
                });
            }

            await WriteAuditAsync(
                auditRecordWriter, organizationId, branchId, authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateProtectionProfile, TargetType, branchId.ToString("D"), AuditOutcome.Succeeded,
                saved, cancellationToken);

            return Results.Ok(new BranchProtectionProfileDto(organizationId, branchId, saved, timeProvider.GetUtcNow()));
        });

        // Агент читает профиль ключом устройства. Организация и филиал — в строке запроса: ключ
        // проверяется вместе с ними, как в теле остальных запросов агента.
        app.MapGet("/api/devices/{deviceId:guid}/policy", async (
            Guid deviceId,
            Guid organizationId,
            Guid branchId,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(organizationId, branchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspended = await organizationStatusGuard.RequireActiveAsync(organizationId, cancellationToken);
            if (suspended is not null)
            {
                return suspended;
            }

            return Results.Ok(await ProtectionProfiles.ResolveAsync(dbContext, branchId, cancellationToken));
        });

        // Агент применил профиль (или снял его на обслуживание) и докладывает по пунктам.
        app.MapPost("/api/devices/{deviceId:guid}/policy/report", async (
            Guid deviceId,
            DeviceProtectionReportRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            if (request.Items.Count > ProtectionReports.MaxItems)
            {
                return Results.BadRequest(new { Error = $"A protection report holds at most {ProtectionReports.MaxItems} items." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices.SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            device.ProtectionReportJson = ProtectionReports.Write(
                new DeviceProtectionReportDto(request.Version, request.AppliedAtUtc, request.Items));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static Task<bool> BranchBelongsAsync(
        PlatformDbContext dbContext, Guid organizationId, Guid branchId, CancellationToken cancellationToken) =>
        dbContext.Branches.AsNoTracking().AnyAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
            cancellationToken);
}
