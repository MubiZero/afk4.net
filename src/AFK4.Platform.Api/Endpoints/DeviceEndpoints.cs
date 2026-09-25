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

internal static class DeviceEndpoints
{
    /// <summary>Общее правило «на связи»: сердцебиение не старше допустимого.</summary>
    private static DeviceOnlineWindow OnlineWindow(
        TimeProvider timeProvider,
        IOptions<BranchDiagnosticsOptions> diagnosticsOptions)
    {
        return new DeviceOnlineWindow(timeProvider.GetUtcNow(), diagnosticsOptions.Value.StaleHeartbeatSeconds);
    }

    public static void MapDeviceEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/device-enrollment-codes", async (
            Guid branchId,
            CreateDeviceEnrollmentCodeRequest request,
            IDeviceEnrollmentService enrollmentService,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.CreateDeviceEnrollmentCode,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: branchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.CreateDeviceEnrollmentCode,
                    TargetType: "DeviceEnrollmentCode",
                    TargetId: null,
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        request.ExpiresInSeconds,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "OrganizationId is required." });
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (request.ExpiresInSeconds <= 0)
            {
                return Results.BadRequest(new { Error = "Enrollment code lifetime must be positive." });
            }

            var code = await enrollmentService.CreateEnrollmentCodeAsync(branchId, request, cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext.OrganizationId,
                BranchId: branchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.CreateDeviceEnrollmentCode,
                TargetType: "DeviceEnrollmentCode",
                TargetId: code.Code,
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    request.ExpiresInSeconds,
                    code.ExpiresAtUtc
                })),
                cancellationToken);

            return Results.Ok(code);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.CreateDeviceEnrollmentCode);

        organizations.MapPost("install/auth/discover", async (
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.InstallDevice);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var staff = authorization.StaffContext!;
            var result = await installService.DiscoverForStaffAsync(
                staff.OrganizationId, staff.BranchIds, staff.DisplayName, staff.StaffUserId, cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                staff.OrganizationId,
                Guid.Empty,
                staff.StaffUserId,
                AuditActionNames.InstallDiscoverInvoked,
                "Install",
                null,
                AuditOutcome.Succeeded,
                new { BranchCount = staff.BranchIds.Count },
                cancellationToken);

            return ToInstallHttpResult(result);
        })
            // Мастер установки — не панель управляющего: проверку версии Organization Admin
            // к нему не применяем, доменная защита группы остаётся.
            .AllowNonOrganizationAdminClients();

        organizations.MapPost("install/auth/seats", async (
            AuthenticatedInstallCreateSeatRequest request,
            HttpContext httpContext,
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                request.BranchId, OrganizationPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }
            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var sourceIp = GetSourceIp(httpContext);
            var result = await installService.CreateSeatForStaffAsync(
                staff.OrganizationId, staff.StaffUserId, request, cancellationToken);
            if (result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    result.OrganizationId!.Value,
                    result.BranchId!.Value,
                    staff.StaffUserId,
                    AuditActionNames.CreateSeat,
                    "Seat",
                    result.Value!.SeatId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { request.ZoneId, request.Name, SourceIp = sourceIp, Via = "phone_auth_install" },
                    cancellationToken);
            }

            return ToInstallHttpResult(result);
        })
            // Мастер установки — не панель управляющего: проверку версии Organization Admin
            // к нему не применяем, доменная защита группы остаётся.
            .AllowNonOrganizationAdminClients();

        organizations.MapPost("install/auth/enroll", async (
            AuthenticatedInstallEnrollRequest request,
            HttpContext httpContext,
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                request.BranchId, OrganizationPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var sourceIp = GetSourceIp(httpContext);
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    request.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.InstallEnrollRejected,
                    "Device",
                    null,
                    AuditOutcome.Denied,
                    new { request.Role, authorization.DenialReason, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var result = await installService.EnrollForStaffAsync(staff.OrganizationId, request, cancellationToken);
            if (result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    result.OrganizationId!.Value,
                    result.BranchId!.Value,
                    staff.StaffUserId,
                    AuditActionNames.InstallEnrollSucceeded,
                    "Device",
                    result.Value!.DeviceId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { request.SeatId, request.Role, request.DisplayName, result.Value.EnrollmentState, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
            }
            else
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    staff.OrganizationId,
                    request.BranchId,
                    staff.StaffUserId,
                    AuditActionNames.InstallEnrollRejected,
                    "Device",
                    null,
                    AuditOutcome.Denied,
                    new { request.BranchId, request.SeatId, request.Role, result.Error, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
            }

            return ToInstallHttpResult(result);
        })
            // Мастер установки — не панель управляющего: проверку версии Organization Admin
            // к нему не применяем, доменная защита группы остаётся.
            .AllowNonOrganizationAdminClients();

        app.MapPost("/api/devices/enroll", async (
            DeviceEnrollmentRequest request,
            IDeviceEnrollmentService enrollmentService,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            var suspendedCheck = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var result = await enrollmentService.EnrollAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { result.Error });
            }

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/devices/{deviceId:guid}/heartbeat", async (
            Guid deviceId,
            DeviceHeartbeatRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IDeviceHeartbeatService heartbeatService,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.Validate(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }
            var allowOperationalCommands = credentialValidator.ValidateApproved(
                request.OrganizationId,
                request.BranchId,
                deviceId,
                credentialSecret);

            var suspendedCheck = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var response = await heartbeatService.RecordHeartbeatAsync(
                deviceId,
                request,
                allowOperationalCommands,
                cancellationToken);

            return Results.Ok(response);
        });

        // Агент меняет свой ключ сам, предъявив действующий. Право клуба здесь ни при чём: это
        // не человек за стойкой, а машина, которая уже доказала, что она эта машина.
        app.MapPost("/api/devices/{deviceId:guid}/credentials/self-rotate", async (
            Guid deviceId,
            SelfRotateDeviceCredentialRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IDeviceCredentialLifecycleService credentialLifecycleService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(
                request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var rotated = await credentialLifecycleService.RotateForAgentAsync(deviceId, cancellationToken);
            if (rotated is null)
            {
                return Results.NotFound();
            }

            // Смена ключа — событие безопасности, и клуб должен видеть его в своём журнале, даже
            // когда её сделала машина без человека.
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: rotated.OrganizationId,
                BranchId: rotated.BranchId,
                ActorStaffUserId: null,
                Action: AuditActionNames.RotateDeviceCredential,
                TargetType: "DeviceCredential",
                TargetId: rotated.CredentialId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "Agent",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    DeviceId = deviceId,
                    Rotation = "self"
                })),
                cancellationToken);

            return Results.Ok(rotated);
        });

        app.MapPost("/api/devices/{deviceId:guid}/commands/{commandId:guid}/result", async (
            Guid deviceId,
            Guid commandId,
            DeviceCommandResultDto result,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IDeviceCommandStore commandStore,
            ISessionCommandResultProcessor sessionCommandResultProcessor,
            IHubContext<DeviceHub> hubContext,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != result.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match result DeviceId." });
            }

            if (commandId != result.CommandId)
            {
                return Results.BadRequest(new { Error = "Route commandId must match result CommandId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(result.OrganizationId, result.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspendedCheck = await organizationStatusGuard.RequireActiveAsync(result.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            await commandStore.ApplyResultAsync(result, cancellationToken);
            await sessionCommandResultProcessor.ProcessAsync(result, cancellationToken);
            await hubContext.Clients
                .Group(DeviceHubGroups.Branch(result.BranchId))
                .SendAsync(DeviceRealtimeEvents.DeviceCommandResult, result, cancellationToken);

            return Results.Ok();
        });

        app.MapPost("/api/devices/{deviceId:guid}/session-reconciliation", async (
            Guid deviceId,
            DeviceSessionSnapshotRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IDeviceCommandDispatchService commandDispatchService,
            IOrganizationStatusGuard organizationStatusGuard,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            if (request.ObservedAtUtc == default)
            {
                return Results.BadRequest(new { Error = "ObservedAtUtc is required." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspendedCheck = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var now = timeProvider.GetUtcNow();
            var cloudSession = await dbContext.Sessions
                .Where(session =>
                    session.OrganizationId == request.OrganizationId &&
                    session.BranchId == request.BranchId &&
                    session.DeviceId == deviceId &&
                    (session.State == SessionStateNames.Active ||
                        session.State == SessionStateNames.Paused ||
                        session.State == SessionStateNames.Ending))
                .OrderByDescending(session => session.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (cloudSession is not null)
            {
                if (cloudSession.State == SessionStateNames.Ending)
                {
                    return await CompleteReconciliationAsync(
                        dbContext,
                        commandDispatchService,
                        request,
                        action: "lock",
                        reason: "cloud-session-ending",
                        cloudSession,
                        lease: null,
                        dispatchCommand: true,
                        recordedAtUtc: now,
                        cancellationToken);
                }

                var currentLease = await LoadCurrentLeaseAsync(dbContext, cloudSession, cancellationToken);
                if (currentLease is not null && LocalLeaseMatches(request, cloudSession, currentLease, now))
                {
                    return await CompleteReconciliationAsync(
                        dbContext,
                        commandDispatchService,
                        request,
                        action: "continue",
                        reason: "local-lease-current",
                        cloudSession,
                        lease: null,
                        dispatchCommand: false,
                        recordedAtUtc: now,
                        cancellationToken);
                }

                if (currentLease is null)
                {
                    return Results.Conflict(new { Error = "Active session has no current lease." });
                }

                return await CompleteReconciliationAsync(
                    dbContext,
                    commandDispatchService,
                    request,
                    action: "unlock",
                    reason: "cloud-session-active",
                    cloudSession,
                    lease: currentLease,
                    dispatchCommand: true,
                    recordedAtUtc: now,
                    cancellationToken);
            }

            var localSessionId = request.ActiveSessionId ?? request.ActiveLease?.SessionId;
            if (localSessionId is not null)
            {
                var localSession = await dbContext.Sessions
                    .SingleOrDefaultAsync(session => session.SessionId == localSessionId, cancellationToken);

                return await CompleteReconciliationAsync(
                    dbContext,
                    commandDispatchService,
                    request,
                    action: "lock",
                    reason: localSession is null ? "unknown-local-session" : "cloud-session-not-active",
                    localSession,
                    lease: null,
                    dispatchCommand: true,
                    recordedAtUtc: now,
                    cancellationToken);
            }

            return Results.Ok(new SessionReconciliationResponse(
                Action: "continue",
                Reason: "no-active-session",
                SessionId: null,
                Lease: null));
        });

        app.MapPost("/api/devices/{deviceId:guid}/installed-apps/report", async (
            Guid deviceId,
            InstalledAppReportRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            if (request.OrganizationId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "OrganizationId is required." });
            }

            if (request.BranchId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "BranchId is required." });
            }

            if (request.ReportedAtUtc == default)
            {
                return Results.BadRequest(new { Error = "ReportedAtUtc is required." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspendedCheck = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var existingApps = await dbContext.DeviceInstalledApps
                .Where(app => app.DeviceId == deviceId)
                .ToListAsync(cancellationToken);
            dbContext.DeviceInstalledApps.RemoveRange(existingApps);

            foreach (var app in request.Apps.Where(app => !string.IsNullOrWhiteSpace(app.DisplayName)))
            {
                dbContext.DeviceInstalledApps.Add(new DeviceInstalledAppEntity
                {
                    DeviceInstalledAppId = Guid.NewGuid(),
                    OrganizationId = request.OrganizationId,
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    DisplayName = app.DisplayName.Trim(),
                    Version = string.IsNullOrWhiteSpace(app.Version) ? null : app.Version.Trim(),
                    Publisher = string.IsNullOrWhiteSpace(app.Publisher) ? null : app.Publisher.Trim(),
                    InstallLocation = string.IsNullOrWhiteSpace(app.InstallLocation) ? null : app.InstallLocation.Trim(),
                    InstalledAtUtc = app.InstalledAtUtc,
                    ReportedAtUtc = request.ReportedAtUtc
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        // Игрок нажал «позвать оператора». Зовёт агент своим ключом устройства: на запертом
        // экране сессии нет, и назвать себя игроку нечем — а машина известна всегда.
        //
        // Повторное нажатие не сдвигает время: стойка должна видеть, сколько человек уже ждёт,
        // а не «позвали только что» после десятого тычка в кнопку.
        app.MapPost("/api/devices/{deviceId:guid}/assistance-request", async (
            Guid deviceId,
            DeviceAssistanceRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "OrganizationId and BranchId are required." });
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

            var device = await dbContext.Devices.SingleOrDefaultAsync(
                candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var observedAtUtc = timeProvider.GetUtcNow();
            if (device.AssistanceRequestedAtUtc is null)
            {
                device.AssistanceRequestedAtUtc = request.RequestedAtUtc == default
                    ? observedAtUtc
                    : request.RequestedAtUtc;
                await dbContext.SaveChangesAsync(cancellationToken);
                await NotifyDeviceChangesAsync(hubContext, dbContext, [deviceId], observedAtUtc, cancellationToken);
            }

            return Results.Ok(new DeviceAssistanceStateDto(deviceId, device.AssistanceRequestedAtUtc));
        });

        // «Вернуть в зал» с самого ПК (спека оболочки, §6.5). Кнопку на полосе может нажать любой,
        // кто стоит у ПК, и это безопасно: возврат в зал только закрывает машину обратно. Поэтому
        // хватает ключа устройства — сотрудника здесь не спрашивают.
        app.MapPost("/api/devices/{deviceId:guid}/maintenance/return", async (
            Guid deviceId,
            DeviceMaintenanceReturnRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
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

            var device = await dbContext.Devices.SingleOrDefaultAsync(
                candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            // Уже в зале — нечего возвращать; кнопку жмут и дважды.
            if (device.MaintenanceSinceUtc is null)
            {
                return Results.NoContent();
            }

            var details = JsonSerializer.Serialize(new
            {
                device.DeviceId,
                device.MaintenanceSinceUtc,
                device.MaintenanceByStaffUserId,
                device.MaintenanceByName
            });
            DeviceMaintenance.Clear(device);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: device.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: null,
                Action: AuditActionNames.ReturnDeviceFromMaintenance,
                TargetType: "Device",
                TargetId: deviceId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "Agent",
                DetailsJson: details),
                cancellationToken);
            await NotifyDeviceChangesAsync(hubContext, dbContext, [deviceId], timeProvider.GetUtcNow(), cancellationToken);
            return Results.NoContent();
        });

        // Игрок входит на самом ПК — номером и ПИН-кодом, через агента с ключом устройства
        // (спека оболочки, §5.2). Публичный вход здесь не годится: он не знает машины, не может
        // привязать к ней токены и считает попытки на адрес всего клуба за одним роутером.
        app.MapPost("/api/devices/{deviceId:guid}/player-sign-in", async (
            Guid deviceId,
            DevicePlayerSignInRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            IDevicePlayerSignInService signInService,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "OrganizationId and BranchId are required." });
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

            var device = await dbContext.Devices.SingleOrDefaultAsync(
                candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var result = await signInService.SignInAsync(device, request.PhoneNumber, request.Pin, cancellationToken);
            return result.Error switch
            {
                null => Results.Ok(result.Session),
                DevicePlayerSignInErrorCodeNames.TooManyAttempts => Results.Json(
                    new DevicePlayerSignInErrorDto(result.Error, result.RetryAfterUtc),
                    statusCode: StatusCodes.Status429TooManyRequests),
                DevicePlayerSignInErrorCodeNames.SessionNotYours or DevicePlayerSignInErrorCodeNames.DeviceInMaintenance =>
                    Results.Conflict(new DevicePlayerSignInErrorDto(result.Error)),
                _ => Results.Json(
                    new DevicePlayerSignInErrorDto(result.Error),
                    statusCode: StatusCodes.Status401Unauthorized)
            };
        });

        // ПК забирает заявку на вход с телефона (спека оболочки, §5.4) и получает токены,
        // привязанные к себе. Ключом устройства: заявку может забрать только та машина, к монитору
        // которой человек поднёс телефон.
        app.MapPost("/api/devices/{deviceId:guid}/sign-in-claims/{claimId:guid}/redeem", async (
            Guid deviceId,
            Guid claimId,
            DeviceRedeemSignInClaimRequest request,
            HttpContext httpContext,
            PlatformDbContext dbContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            PlayerSignInClaimService claims,
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

            var device = await dbContext.Devices.SingleOrDefaultAsync(
                candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var result = await claims.RedeemAsync(device, claimId, cancellationToken);
            return result.Error switch
            {
                null => Results.Ok(result.Session),
                PlayerSignInClaimErrorCodeNames.NotFound => Results.NotFound(new { error = result.Error }),
                _ => Results.Conflict(new { error = result.Error })
            };
        });

        // Оператор подошёл — вызов снят. Право то же, что у «отдать заказ»: это работа зала.
        organizations.MapPost("devices/{deviceId:guid}/assistance-request/resolve", async (
            Guid deviceId,
            DeviceStateChangeRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var scope = await LoadDeviceMutationScopeAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                auditRecordWriter,
                deviceId,
                OrganizationPermissionNames.ResolveAssistanceRequest,
                AuditActionNames.ResolveAssistanceRequest,
                new { request.OrganizationId },
                cancellationToken);

            if (scope.ErrorResult is not null)
            {
                return scope.ErrorResult;
            }

            var device = scope.Device!;
            var authorization = scope.Authorization!;
            var organizationValidation = ValidateDeviceMutationOrganization(request.OrganizationId, authorization, device);
            if (organizationValidation is not null)
            {
                return organizationValidation;
            }

            var observedAtUtc = timeProvider.GetUtcNow();
            var waitedSeconds = device.AssistanceRequestedAtUtc is { } requestedAt
                ? (int)Math.Max(0, (observedAtUtc - requestedAt).TotalSeconds)
                : 0;

            device.AssistanceRequestedAtUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                device.OrganizationId,
                device.BranchId,
                authorization.StaffContext!.StaffUserId,
                AuditActionNames.ResolveAssistanceRequest,
                "Device",
                deviceId.ToString("D"),
                AuditOutcome.Succeeded,
                new { WaitedSeconds = waitedSeconds },
                cancellationToken);

            await NotifyDeviceChangesAsync(hubContext, dbContext, [deviceId], observedAtUtc, cancellationToken);

            return Results.Ok(new DeviceAssistanceStateDto(deviceId, null));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ResolveAssistanceRequest);

        organizations.MapGet("branches/{branchId:guid}/devices", async (
            Guid branchId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewDeviceDetail,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var devices = await LoadBranchDeviceInventoryAsync(
                dbContext,
                authorization.StaffContext!.OrganizationId,
                branchId,
                enrollmentState: null,
                OnlineWindow(timeProvider, diagnosticsOptions),
                cancellationToken);

            return Results.Ok(devices);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceDetail);

        organizations.MapGet("branches/{branchId:guid}/devices/pending", async (
            Guid branchId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewDeviceDetail,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var devices = await LoadBranchDeviceInventoryAsync(
                dbContext,
                authorization.StaffContext!.OrganizationId,
                branchId,
                DeviceEnrollmentStateNames.Pending,
                OnlineWindow(timeProvider, diagnosticsOptions),
                cancellationToken);

            return Results.Ok(devices);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceDetail);

        organizations.MapGet("devices/{deviceId:guid}", async (
            Guid deviceId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                OrganizationPermissionNames.ViewDeviceDetail,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var assignment = await dbContext.DeviceSeatAssignments
                .AsNoTracking()
                .Where(candidate => candidate.DeviceId == deviceId && candidate.DetachedAtUtc == null)
                .OrderByDescending(candidate => candidate.AttachedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            SeatEntity? seat = null;
            ZoneEntity? zone = null;
            if (assignment is not null)
            {
                seat = await dbContext.Seats
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.SeatId == assignment.SeatId, cancellationToken);

                if (seat is not null)
                {
                    zone = await dbContext.Zones
                        .AsNoTracking()
                        .SingleOrDefaultAsync(candidate => candidate.ZoneId == seat.ZoneId, cancellationToken);
                }
            }

            var activeCredentialCount = await dbContext.DeviceCredentials
                .AsNoTracking()
                .CountAsync(
                    credential => credential.DeviceId == deviceId && credential.RevokedAtUtc == null,
                    cancellationToken);
            var installedAppCount = await dbContext.DeviceInstalledApps
                .AsNoTracking()
                .CountAsync(app => app.DeviceId == deviceId, cancellationToken);
            var recentCommands = await dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => command.DeviceId == deviceId)
                .OrderByDescending(command => command.CreatedAtUtc)
                .Take(5)
                .Select(command => new DeviceCommandStatusDto(
                    command.DeviceId,
                    command.CommandId,
                    command.Type,
                    command.Status,
                    command.Message,
                    command.CreatedAtUtc,
                    command.UpdatedAtUtc,
                    command.Outcome))
                .ToListAsync(cancellationToken);

            return Results.Ok(new DeviceDetailDto(
                OrganizationId: device.OrganizationId,
                BranchId: device.BranchId,
                DeviceId: device.DeviceId,
                MachineName: device.MachineName,
                AgentVersion: device.AgentVersion,
                ShellVersion: device.ShellVersion,
                EnrolledAtUtc: device.EnrolledAtUtc,
                LastHeartbeatAtUtc: device.LastHeartbeatAtUtc,
                IsOnline: IsDeviceOnline(device, timeProvider.GetUtcNow(), diagnosticsOptions.Value.StaleHeartbeatSeconds),
                IsLocked: device.IsLocked,
                SeatId: seat?.SeatId,
                SeatName: seat?.Name,
                ZoneId: zone?.ZoneId,
                ZoneName: zone?.Name,
                ActiveCredentialCount: activeCredentialCount,
                InstalledAppCount: installedAppCount,
                RecentCommands: recentCommands,
                DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
                Role: device.Role,
                EnrollmentState: device.EnrollmentState));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceDetail);

        organizations.MapPost("devices/{deviceId:guid}/approve", async (
            Guid deviceId,
            DeviceStateChangeRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await LoadDeviceMutationScopeAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                auditRecordWriter,
                deviceId,
                OrganizationPermissionNames.AssignDeviceSeat,
                AuditActionNames.ApprovePendingDevice,
                new
                {
                    request.OrganizationId,
                    request.Reason
                },
                cancellationToken);

            if (scope.ErrorResult is not null)
            {
                return scope.ErrorResult;
            }

            var device = scope.Device!;
            var authorization = scope.Authorization!;
            var organizationValidation = ValidateDeviceMutationOrganization(request.OrganizationId, authorization, device);
            if (organizationValidation is not null)
            {
                return organizationValidation;
            }

            if (device.EnrollmentState is DeviceEnrollmentStateNames.Rejected or DeviceEnrollmentStateNames.Removed)
            {
                return Results.Conflict(new { Error = "Rejected or removed devices cannot be approved." });
            }

            var previousState = device.EnrollmentState;
            if (device.EnrollmentState == DeviceEnrollmentStateNames.Pending)
            {
                device.EnrollmentState = DeviceEnrollmentStateNames.Approved;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                device.OrganizationId,
                device.BranchId,
                authorization.StaffContext!.StaffUserId,
                AuditActionNames.ApprovePendingDevice,
                "Device",
                device.DeviceId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    PreviousEnrollmentState = previousState,
                    device.EnrollmentState,
                    request.Reason
                },
                cancellationToken);

            var observedAtUtc = timeProvider.GetUtcNow();
            await NotifyDeviceChangesAsync(hubContext, dbContext, [device.DeviceId], observedAtUtc, cancellationToken);

            return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, OnlineWindow(timeProvider, diagnosticsOptions), cancellationToken));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.AssignDeviceSeat);

        organizations.MapPost("devices/{deviceId:guid}/reject", async (
            Guid deviceId,
            DeviceStateChangeRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            IDeviceBoundPlayerTokens deviceTokens,
            CancellationToken cancellationToken) =>
        {
            var scope = await LoadDeviceMutationScopeAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                auditRecordWriter,
                deviceId,
                OrganizationPermissionNames.AssignDeviceSeat,
                AuditActionNames.RejectPendingDevice,
                new
                {
                    request.OrganizationId,
                    request.Reason
                },
                cancellationToken);

            if (scope.ErrorResult is not null)
            {
                return scope.ErrorResult;
            }

            var device = scope.Device!;
            var authorization = scope.Authorization!;
            var organizationValidation = ValidateDeviceMutationOrganization(request.OrganizationId, authorization, device);
            if (organizationValidation is not null)
            {
                return organizationValidation;
            }

            if (device.EnrollmentState == DeviceEnrollmentStateNames.Approved)
            {
                return Results.Conflict(new { Error = "Approved devices must be removed instead of rejected." });
            }

            if (device.EnrollmentState == DeviceEnrollmentStateNames.Removed)
            {
                return Results.Conflict(new { Error = "Removed devices cannot be rejected." });
            }

            var now = timeProvider.GetUtcNow();
            var previousState = device.EnrollmentState;
            device.EnrollmentState = DeviceEnrollmentStateNames.Rejected;
            device.IsOnline = false;

            var changedDeviceIds = await DetachActiveDeviceAssignmentsAsync(dbContext, device, now, cancellationToken);
            var revokedCredentialCount = await RevokeActiveDeviceCredentialsAsync(dbContext, device, now, cancellationToken);
            // Машины больше нет в зале — и вход игрока на ней не должен пережить её.
            await deviceTokens.RevokeForDeviceAsync(device.DeviceId, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                device.OrganizationId,
                device.BranchId,
                authorization.StaffContext!.StaffUserId,
                AuditActionNames.RejectPendingDevice,
                "Device",
                device.DeviceId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    PreviousEnrollmentState = previousState,
                    device.EnrollmentState,
                    RevokedCredentialCount = revokedCredentialCount,
                    request.Reason
                },
                cancellationToken);

            await NotifyDeviceChangesAsync(hubContext, dbContext, changedDeviceIds, now, cancellationToken);

            return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, OnlineWindow(timeProvider, diagnosticsOptions), cancellationToken));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.AssignDeviceSeat);

        organizations.MapPost("devices/{deviceId:guid}/rename", async (
            Guid deviceId,
            RenameDeviceRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await LoadDeviceMutationScopeAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                auditRecordWriter,
                deviceId,
                OrganizationPermissionNames.AssignDeviceSeat,
                AuditActionNames.RenameDevice,
                new
                {
                    request.OrganizationId,
                    request.DisplayName
                },
                cancellationToken);

            if (scope.ErrorResult is not null)
            {
                return scope.ErrorResult;
            }

            var device = scope.Device!;
            var authorization = scope.Authorization!;
            var organizationValidation = ValidateDeviceMutationOrganization(request.OrganizationId, authorization, device);
            if (organizationValidation is not null)
            {
                return organizationValidation;
            }

            var displayName = request.DisplayName.Trim();
            if (displayName.Length == 0)
            {
                return Results.BadRequest(new { Error = "DisplayName is required." });
            }

            if (displayName.Length > 80)
            {
                return Results.BadRequest(new { Error = "DisplayName must be 80 characters or fewer." });
            }

            var previousDisplayName = device.DisplayName;
            device.DisplayName = displayName;
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                device.OrganizationId,
                device.BranchId,
                authorization.StaffContext!.StaffUserId,
                AuditActionNames.RenameDevice,
                "Device",
                device.DeviceId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    PreviousDisplayName = previousDisplayName,
                    device.DisplayName
                },
                cancellationToken);

            var observedAtUtc = timeProvider.GetUtcNow();
            await NotifyDeviceChangesAsync(hubContext, dbContext, [device.DeviceId], observedAtUtc, cancellationToken);

            return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, OnlineWindow(timeProvider, diagnosticsOptions), cancellationToken));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.AssignDeviceSeat);

        organizations.MapPost("devices/{deviceId:guid}/remove", async (
            Guid deviceId,
            DeviceStateChangeRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            IDeviceBoundPlayerTokens deviceTokens,
            CancellationToken cancellationToken) =>
        {
            var scope = await LoadDeviceMutationScopeAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                auditRecordWriter,
                deviceId,
                OrganizationPermissionNames.RevokeDeviceCredential,
                AuditActionNames.RemoveDevice,
                new
                {
                    request.OrganizationId,
                    request.Reason
                },
                cancellationToken);

            if (scope.ErrorResult is not null)
            {
                return scope.ErrorResult;
            }

            var device = scope.Device!;
            var authorization = scope.Authorization!;
            var organizationValidation = ValidateDeviceMutationOrganization(request.OrganizationId, authorization, device);
            if (organizationValidation is not null)
            {
                return organizationValidation;
            }

            var hasActiveSession = await HasActiveDeviceSessionAsync(dbContext, device, cancellationToken);
            if (hasActiveSession)
            {
                return Results.Conflict(new { Error = "Device has an active, paused, or ending session." });
            }

            var now = timeProvider.GetUtcNow();
            var previousState = device.EnrollmentState;
            device.EnrollmentState = DeviceEnrollmentStateNames.Removed;
            device.IsOnline = false;

            var changedDeviceIds = await DetachActiveDeviceAssignmentsAsync(dbContext, device, now, cancellationToken);
            var revokedCredentialCount = await RevokeActiveDeviceCredentialsAsync(dbContext, device, now, cancellationToken);
            // Машины больше нет в зале — и вход игрока на ней не должен пережить её.
            await deviceTokens.RevokeForDeviceAsync(device.DeviceId, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                device.OrganizationId,
                device.BranchId,
                authorization.StaffContext!.StaffUserId,
                AuditActionNames.RemoveDevice,
                "Device",
                device.DeviceId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    PreviousEnrollmentState = previousState,
                    device.EnrollmentState,
                    RevokedCredentialCount = revokedCredentialCount,
                    request.Reason
                },
                cancellationToken);

            await NotifyDeviceChangesAsync(hubContext, dbContext, changedDeviceIds, now, cancellationToken);

            return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, OnlineWindow(timeProvider, diagnosticsOptions), cancellationToken));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.RevokeDeviceCredential);

        organizations.MapPost("branches/{branchId:guid}/devices/{deviceId:guid}/seat-assignment", async (
            Guid branchId,
            Guid deviceId,
            AssignDeviceSeatRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceBoundPlayerTokens deviceTokens,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .SingleOrDefaultAsync(
                    candidate => candidate.DeviceId == deviceId && candidate.BranchId == branchId,
                    cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.AssignDeviceSeat,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: branchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.AssignDeviceSeat,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        request.SeatId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "OrganizationId is required." });
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId ||
                request.OrganizationId != device.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization and device." });
            }

            if (request.SeatId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "SeatId is required." });
            }

            var seat = await dbContext.Seats
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.SeatId == request.SeatId &&
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.BranchId == branchId,
                    cancellationToken);

            if (seat is null)
            {
                return Results.NotFound();
            }

            var hasActiveSession = await dbContext.Sessions
                .AsNoTracking()
                .AnyAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.BranchId == branchId &&
                        (candidate.SeatId == request.SeatId || candidate.DeviceId == deviceId) &&
                        (candidate.State == SessionStateNames.Active ||
                         candidate.State == SessionStateNames.Paused ||
                         candidate.State == SessionStateNames.Ending),
                    cancellationToken);

            if (hasActiveSession)
            {
                return Results.Conflict(new { Error = "Seat or device has an active, paused, or ending session." });
            }

            var now = timeProvider.GetUtcNow();
            var activeAssignments = await dbContext.DeviceSeatAssignments
                .Where(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.BranchId == branchId &&
                        candidate.DetachedAtUtc == null &&
                        (candidate.SeatId == request.SeatId || candidate.DeviceId == deviceId))
                .OrderByDescending(candidate => candidate.AttachedAtUtc)
                .ThenByDescending(candidate => candidate.DeviceSeatAssignmentId)
                .ToListAsync(cancellationToken);

            var currentAssignment = activeAssignments.FirstOrDefault(
                candidate => candidate.SeatId == request.SeatId && candidate.DeviceId == deviceId);

            if (currentAssignment is not null)
            {
                foreach (var assignment in activeAssignments.Where(candidate => candidate.DeviceSeatAssignmentId != currentAssignment.DeviceSeatAssignmentId))
                {
                    assignment.DetachedAtUtc = now;
                }
            }
            else
            {
                foreach (var assignment in activeAssignments)
                {
                    assignment.DetachedAtUtc = now;
                }

                currentAssignment = new DeviceSeatAssignmentEntity
                {
                    DeviceSeatAssignmentId = Guid.NewGuid(),
                    OrganizationId = request.OrganizationId,
                    BranchId = branchId,
                    SeatId = request.SeatId,
                    DeviceId = deviceId,
                    AttachedAtUtc = now
                };
                dbContext.DeviceSeatAssignments.Add(currentAssignment);
            }

            // Машина переехала на другое место (или место отдали другой машине): вход игрока,
            // сделанный у прежнего места, к новому не переезжает.
            var movedDeviceIds = activeAssignments
                .Where(assignment => assignment.DetachedAtUtc == now)
                .Select(assignment => assignment.DeviceId)
                .Append(currentAssignment.AttachedAtUtc == now ? deviceId : Guid.Empty)
                .Where(candidate => candidate != Guid.Empty)
                .Distinct();
            foreach (var movedDeviceId in movedDeviceIds)
            {
                await deviceTokens.RevokeForDeviceAsync(movedDeviceId, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: request.OrganizationId,
                BranchId: branchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.AssignDeviceSeat,
                TargetType: "DeviceSeatAssignment",
                TargetId: currentAssignment.DeviceSeatAssignmentId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    request.SeatId,
                    DeviceId = deviceId
                })),
                cancellationToken);

            return Results.Ok(ToDeviceSeatAssignmentDto(currentAssignment));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.AssignDeviceSeat);

        organizations.MapPost("devices/{deviceId:guid}/commands", async (
            Guid deviceId,
            CreateDeviceCommandRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceCommandDispatchService commandDispatchService,
            IDeviceBoundPlayerTokens deviceTokens,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            // Обслуживание — своим правом: оно закрывает машину для игроков, и решать это — не
            // каждому, кто может её перезапереть.
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                DeviceCommandPolicy.RequiredPermission(request.Type),
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.DispatchDeviceCommand,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        request.Type,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(request.Type))
            {
                return Results.BadRequest(new { Error = "Command type is required." });
            }

            if (request.Payload is null)
            {
                return Results.BadRequest(new { Error = "Command payload is required." });
            }

            // Опечатка в типе раньше доезжала до агента и возвращалась «не умею» — в журнале
            // команда висела отправленной.
            if (!DeviceCommandPolicy.IsStaffCommand(request.Type))
            {
                return Results.BadRequest(new
                {
                    Error = $"Unknown device command type '{request.Type}'.",
                    Code = DeviceCommandErrorCodeNames.UnknownType
                });
            }

            var payloadError = DeviceCommandPolicy.ValidatePayload(request.Type, request.Payload);
            if (payloadError is not null)
            {
                return Results.BadRequest(new { Error = payloadError, Code = DeviceCommandErrorCodeNames.InvalidPayload });
            }

            if (device.EnrollmentState != DeviceEnrollmentStateNames.Approved)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.DispatchDeviceCommand,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        request.Type,
                        device.EnrollmentState,
                        Reason = "Device enrollment is not approved."
                    })),
                    cancellationToken);

                return Results.Conflict(new { Error = "Device enrollment is not approved." });
            }

            // Чужую игру не выключают, не перезагружают и не уводят в обслуживание: сначала
            // закончить сессию, потом трогать машину.
            if (DeviceCommandPolicy.RequiresFreeDevice(request.Type)
                && await HasActiveDeviceSessionAsync(dbContext, device, cancellationToken))
            {
                return Results.Conflict(new
                {
                    Error = "Device has an active, paused, or ending session.",
                    Code = DeviceCommandErrorCodeNames.ActiveSession
                });
            }

            var targetDeviceId = deviceId;
            var commandRequest = request;
            if (request.Type == DeviceCommandTypeNames.Wake)
            {
                // Спящему ПК команду не отдать: будит сосед по подсети волшебным пакетом.
                var wake = await PlanWakeAsync(dbContext, device, timeProvider, cancellationToken);
                if (wake.Error is not null)
                {
                    return Results.Conflict(new { Error = wake.Error.Value.Message, Code = wake.Error.Value.Code });
                }

                targetDeviceId = wake.HelperDeviceId;
                commandRequest = wake.Command!;
            }

            // Обслуживание запоминает сервер: по нему стойка не начнёт сессию, карта покажет машину
            // закрытой, а агент, пропустивший команду, догонит по сердцебиению.
            if (request.Type == DeviceCommandTypeNames.MaintenanceOn && device.MaintenanceSinceUtc is null)
            {
                // Повторное «на обслуживание» не переписывает, кто и когда: полоса на ПК и восемь
                // часов до автоснятия считаются от первого нажатия.
                var staff = authorization.StaffContext!;
                device.MaintenanceSinceUtc = timeProvider.GetUtcNow();
                device.MaintenanceByStaffUserId = staff.StaffUserId == Guid.Empty ? null : staff.StaffUserId;
                device.MaintenanceByName = staff.DisplayName;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (request.Type == DeviceCommandTypeNames.MaintenanceOff)
            {
                DeviceMaintenance.Clear(device);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Выход игрока гарантирует сервер, а не хост: погасшие токены не откроют аккаунт, даже
            // если хост команду не получил или её проигнорировал.
            if (request.Type == DeviceCommandTypeNames.SignOut)
            {
                await deviceTokens.RevokeForDeviceAsync(deviceId, cancellationToken);
            }

            // Неповторяемые команды едут только сердцебиением: оно же помечает их отданными. Через
            // SignalR и сердцебиение сразу агент получил бы перезагрузку дважды.
            var command = DeviceCommandPolicy.IsOneShot(commandRequest.Type)
                ? await commandDispatchService.EnqueueAsync(targetDeviceId, commandRequest, cancellationToken)
                : await commandDispatchService.DispatchAsync(targetDeviceId, commandRequest, cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.DispatchDeviceCommand,
                TargetType: "DeviceCommand",
                TargetId: command.CommandId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    DeviceId = deviceId,
                    command.Type,
                    // У пробуждения команду исполняет сосед — в журнале видно, кто именно.
                    ExecutedByDeviceId = targetDeviceId == deviceId ? (Guid?)null : targetDeviceId
                })),
                cancellationToken);

            return Results.Ok(command);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.DispatchDeviceCommand);

        organizations.MapGet("devices/{deviceId:guid}/commands", async (
            Guid deviceId,
            int? limit,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.ViewDeviceCommandStatus,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var resultLimit = Math.Clamp(limit ?? 25, 1, 100);
            var commands = await dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => command.DeviceId == deviceId)
                .OrderByDescending(command => command.CreatedAtUtc)
                .Take(resultLimit)
                .Select(command => new DeviceCommandStatusDto(
                    command.DeviceId,
                    command.CommandId,
                    command.Type,
                    command.Status,
                    command.Message,
                    command.CreatedAtUtc,
                    command.UpdatedAtUtc,
                    command.Outcome))
                .ToListAsync(cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.ViewDeviceCommandStatus,
                TargetType: "Device",
                TargetId: deviceId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    ResultCount = commands.Count,
                    Limit = resultLimit
                })),
                cancellationToken);

            return Results.Ok(commands);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceCommandStatus);

        organizations.MapGet("branches/{branchId:guid}/device-commands", async (
            Guid branchId,
            int? limit,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: branchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.ViewDeviceCommandStatus,
                    TargetType: "Branch",
                    TargetId: branchId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var resultLimit = Math.Clamp(limit ?? 50, 1, 100);
            var deviceIds = await dbContext.Devices
                .AsNoTracking()
                .Where(device => device.BranchId == branchId)
                .Select(device => device.DeviceId)
                .ToListAsync(cancellationToken);
            IReadOnlyList<DeviceCommandStatusDto> commands = deviceIds.Count == 0
                ? []
                : await dbContext.DeviceCommands
                    .AsNoTracking()
                    .Where(command => deviceIds.Contains(command.DeviceId))
                    .OrderByDescending(command => command.CreatedAtUtc)
                    .Take(resultLimit)
                    .Select(command => new DeviceCommandStatusDto(
                        command.DeviceId,
                        command.CommandId,
                        command.Type,
                        command.Status,
                        command.Message,
                        command.CreatedAtUtc,
                        command.UpdatedAtUtc,
                        command.Outcome))
                    .ToListAsync(cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: branchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.ViewDeviceCommandStatus,
                TargetType: "Branch",
                TargetId: branchId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    ResultCount = commands.Count,
                    Limit = resultLimit
                })),
                cancellationToken);

            return Results.Ok(commands);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceCommandStatus);

        organizations.MapGet("devices/{deviceId:guid}/commands/{commandId:guid}/status", async (
            Guid deviceId,
            Guid commandId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceCommandStore commandStore,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.ViewDeviceCommandStatus,
                    TargetType: "DeviceCommand",
                    TargetId: commandId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        DeviceId = deviceId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var status = await commandStore.GetAsync(deviceId, commandId, cancellationToken);

            if (status is null)
            {
                return Results.NotFound();
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.ViewDeviceCommandStatus,
                TargetType: "DeviceCommand",
                TargetId: commandId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    DeviceId = deviceId,
                    status.Status
                })),
                cancellationToken);

            return Results.Ok(status);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDeviceCommandStatus);

        organizations.MapPost("devices/{deviceId:guid}/credentials/rotate", async (
            Guid deviceId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceCredentialLifecycleService credentialLifecycleService,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                OrganizationPermissionNames.RotateDeviceCredential,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.RotateDeviceCredential,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var rotated = await credentialLifecycleService.RotateAsync(deviceId, cancellationToken);

            if (rotated is null)
            {
                return Results.NotFound();
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.RotateDeviceCredential,
                TargetType: "DeviceCredential",
                TargetId: rotated.CredentialId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    DeviceId = deviceId
                })),
                cancellationToken);

            return Results.Ok(rotated);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.RotateDeviceCredential);

        // Гигиена: попросить машину сменить ключ самой. Ничего не отрезает — в отличие от
        // соседнего `rotate`, который для украденного ПК.
        organizations.MapPost("devices/{deviceId:guid}/credentials/request-rotation", async (
            Guid deviceId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceCredentialLifecycleService credentialLifecycleService,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var device = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
            if (device is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                device.BranchId,
                OrganizationPermissionNames.RotateDeviceCredential,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: device.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.RequestDeviceCredentialRotation,
                    TargetType: "Device",
                    TargetId: deviceId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!await credentialLifecycleService.RequestRotationAsync(deviceId, cancellationToken))
            {
                return Results.NotFound();
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.RequestDeviceCredentialRotation,
                TargetType: "Device",
                TargetId: deviceId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: "{}"),
                cancellationToken);

            return Results.Accepted();
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.RotateDeviceCredential);

        organizations.MapPost("devices/{deviceId:guid}/credentials/{credentialId:guid}/revoke", async (
            Guid deviceId,
            Guid credentialId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IDeviceCredentialLifecycleService credentialLifecycleService,
            CancellationToken cancellationToken) =>
        {
            if (staffContextAccessor.Current is null)
            {
                return Results.Unauthorized();
            }

            var credential = await dbContext.DeviceCredentials
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.DeviceId == deviceId && candidate.CredentialId == credentialId,
                    cancellationToken);

            if (credential is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                credential.BranchId,
                OrganizationPermissionNames.RevokeDeviceCredential,
                cancellationToken);

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: authorization.StaffContext!.OrganizationId,
                    BranchId: credential.BranchId,
                    ActorStaffUserId: authorization.StaffContext.StaffUserId,
                    Action: AuditActionNames.RevokeDeviceCredential,
                    TargetType: "DeviceCredential",
                    TargetId: credentialId.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        DeviceId = deviceId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var revoked = await credentialLifecycleService.RevokeAsync(deviceId, credentialId, cancellationToken);

            if (revoked is null)
            {
                return Results.NotFound();
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: authorization.StaffContext!.OrganizationId,
                BranchId: credential.BranchId,
                ActorStaffUserId: authorization.StaffContext.StaffUserId,
                Action: AuditActionNames.RevokeDeviceCredential,
                TargetType: "DeviceCredential",
                TargetId: credentialId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    DeviceId = deviceId
                })),
                cancellationToken);

            return Results.Ok(revoked);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.RevokeDeviceCredential);

    }

    private readonly record struct WakePlan(
        Guid HelperDeviceId,
        CreateDeviceCommandRequest? Command,
        (string Code, string Message)? Error);

    /// <summary>
    /// Кто разбудит спящий ПК: включённый сосед той же подсети, недавно подававший сердцебиение.
    /// Волшебный пакет не проходит маршрутизаторы — сосед из другой подсети не разбудит никого.
    /// </summary>
    private static async Task<WakePlan> PlanWakeAsync(
        PlatformDbContext dbContext,
        DeviceEntity target,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.NetworkMacAddress) || string.IsNullOrWhiteSpace(target.NetworkSubnet))
        {
            return new WakePlan(Guid.Empty, null, (DeviceCommandErrorCodeNames.WakeTargetUnknown,
                "This PC has never reported its network address, so it cannot be woken."));
        }

        var aliveSince = timeProvider.GetUtcNow().AddMinutes(-1);
        var helper = await dbContext.Devices
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == target.OrganizationId
                && candidate.BranchId == target.BranchId
                && candidate.DeviceId != target.DeviceId
                && candidate.EnrollmentState == DeviceEnrollmentStateNames.Approved
                && candidate.NetworkSubnet == target.NetworkSubnet
                && candidate.LastHeartbeatAtUtc != null
                && candidate.LastHeartbeatAtUtc >= aliveSince)
            .OrderByDescending(candidate => candidate.LastHeartbeatAtUtc)
            .Select(candidate => candidate.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (helper == Guid.Empty)
        {
            return new WakePlan(Guid.Empty, null, (DeviceCommandErrorCodeNames.NoWakeHelper,
                "No powered-on PC in the same network can wake this one."));
        }

        return new WakePlan(
            helper,
            new CreateDeviceCommandRequest(
                DeviceCommandTypeNames.WakeNeighbor,
                new Dictionary<string, string>
                {
                    ["mac"] = target.NetworkMacAddress!,
                    ["broadcast"] = target.NetworkBroadcastAddress ?? string.Empty,
                    ["targetDeviceId"] = target.DeviceId.ToString("D")
                }),
            null);
    }
}
