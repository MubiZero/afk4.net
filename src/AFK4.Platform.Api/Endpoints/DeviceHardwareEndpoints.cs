using System.Text.Json;
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
/// Опись железа ПК (P9): агент присылает снимок своим ключом, Панель показывает, чем он отличается
/// от принятого, и принимает новое как норму.
/// </summary>
internal static class DeviceHardwareEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapDeviceHardwareEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        app.MapPost("/api/devices/{deviceId:guid}/hardware/report", async (
            Guid deviceId,
            DeviceHardwareReportRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspended = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspended is not null)
            {
                return suspended;
            }

            var now = timeProvider.GetUtcNow();
            var json = JsonSerializer.Serialize(request.Snapshot, Json);
            var fingerprint = DeviceHardware.Fingerprint(request.Snapshot);
            var entity = await dbContext.DeviceHardware.SingleOrDefaultAsync(row => row.DeviceId == deviceId, cancellationToken);
            if (entity is null)
            {
                // Первый снимок — сравнивать не с чем: он и есть норма, пока клуб не решит иначе.
                dbContext.DeviceHardware.Add(new DeviceHardwareEntity
                {
                    DeviceId = deviceId,
                    OrganizationId = request.OrganizationId,
                    BranchId = request.BranchId,
                    CurrentJson = json,
                    CurrentFingerprint = fingerprint,
                    ReportedAtUtc = now,
                    AcceptedJson = json,
                    AcceptedFingerprint = fingerprint,
                    AcceptedAtUtc = now
                });
            }
            else
            {
                entity.CurrentJson = json;
                entity.CurrentFingerprint = fingerprint;
                entity.ReportedAtUtc = now;
                entity.BranchId = request.BranchId;
                if (Read(entity.AcceptedJson) is { } accepted)
                {
                    // Накопители и мониторы, которых принятый снимок не знал (агент старше), — норма с первой
                    // описи. А не прочитавшиеся сейчас — прежние: отметка в списке не зажигается без изменения.
                    var norm = DeviceHardware.WithKnownParts(accepted, request.Snapshot);
                    if (norm.PhysicalDisks != accepted.PhysicalDisks || norm.Monitors != accepted.Monitors)
                    {
                        entity.AcceptedJson = JsonSerializer.Serialize(norm, Json);
                    }

                    entity.AcceptedFingerprint = DeviceHardware.Fingerprint(norm);
                    entity.CurrentFingerprint = DeviceHardware.Fingerprint(DeviceHardware.WithKnownParts(request.Snapshot, norm));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        organizations.MapGet("devices/{deviceId:guid}/hardware", async (
            Guid deviceId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId, OrganizationPermissionNames.ViewDeviceDetail, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || device.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var entity = await dbContext.DeviceHardware.AsNoTracking().SingleOrDefaultAsync(row => row.DeviceId == deviceId, cancellationToken);
            return Results.Ok(ToDto(entity));
        }).AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceDetail);

        organizations.MapPost("devices/{deviceId:guid}/hardware/accept", async (
            Guid deviceId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId, OrganizationPermissionNames.AcceptDeviceHardware, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            var staff = authorization.StaffContext!;
            if (!authorization.IsAllowed || device.OrganizationId != staff.OrganizationId)
            {
                await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, device.BranchId, staff.StaffUserId,
                    AuditActionNames.AcceptDeviceHardware, "Device", deviceId.ToString("D"), AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var entity = await dbContext.DeviceHardware.SingleOrDefaultAsync(row => row.DeviceId == deviceId, cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            var previous = Read(entity.AcceptedJson);
            var current = Read(entity.CurrentJson);
            var changes = DeviceHardware.Diff(previous, current);
            if (current is not null)
            {
                // Принимают то, что видно; не прочитавшиеся в последней описи мониторы и накопители
                // остаются прежними, а не стираются из нормы.
                var norm = DeviceHardware.WithKnownParts(current, previous);
                entity.AcceptedJson = JsonSerializer.Serialize(norm, Json);
                entity.AcceptedFingerprint = entity.CurrentFingerprint = DeviceHardware.Fingerprint(norm);
            }
            else
            {
                entity.AcceptedJson = entity.CurrentJson;
                entity.AcceptedFingerprint = entity.CurrentFingerprint;
            }

            entity.AcceptedAtUtc = timeProvider.GetUtcNow();
            entity.AcceptedByStaffUserId = staff.StaffUserId;
            entity.AcceptedByName = staff.DisplayName;
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, device.BranchId, staff.StaffUserId,
                AuditActionNames.AcceptDeviceHardware, "Device", deviceId.ToString("D"), AuditOutcome.Succeeded,
                new { Changes = changes }, cancellationToken);
            return Results.Ok(ToDto(entity));
        });
    }

    private static DeviceHardwareDto ToDto(DeviceHardwareEntity? entity)
    {
        if (entity is null)
        {
            return new DeviceHardwareDto(null, null, null, null, null, []);
        }

        var current = Read(entity.CurrentJson);
        var accepted = Read(entity.AcceptedJson);
        return new DeviceHardwareDto(
            current,
            entity.ReportedAtUtc,
            accepted,
            entity.AcceptedAtUtc,
            entity.AcceptedByName,
            DeviceHardware.Diff(accepted, current));
    }

    private static HardwareSnapshotDto? Read(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HardwareSnapshotDto>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
