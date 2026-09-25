using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Consoles;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Консольное место: устройство без агента на месте зала. Сессия, тарифы, касса и отчёты у него —
/// как у ПК; отпирать и запирать нечего, сердцебиения нет (план консолей).
/// </summary>
internal static class ConsoleSeatEndpoints
{
    public static void MapConsoleSeatEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/consoles", async (
            Guid branchId,
            CreateConsoleSeatRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IHubContext<DeviceHub> hubContext,
            TimeProvider timeProvider,
            IOptions<BranchDiagnosticsOptions> diagnosticsOptions,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.AssignDeviceSeat, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || request.OrganizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var name = request.DisplayName?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > ConsoleSeatLimits.DisplayNameMax)
                return Results.BadRequest(new { Error = $"Name the console in at most {ConsoleSeatLimits.DisplayNameMax} characters." });

            var seat = await dbContext.Seats.AsNoTracking().SingleOrDefaultAsync(candidate =>
                candidate.SeatId == request.SeatId && candidate.BranchId == branchId
                && candidate.OrganizationId == request.OrganizationId, cancellationToken);
            if (seat is null) return Results.NotFound(new { Error = "Seat was not found.", Code = ConsoleSeatErrorCodeNames.SeatNotFound });

            // Место с ПК консольным не становится: две машины на одном месте — две правды о сессии.
            var taken = await (
                    from assignment in dbContext.DeviceSeatAssignments
                    join device in dbContext.Devices on assignment.DeviceId equals device.DeviceId
                    where assignment.SeatId == seat.SeatId && assignment.DetachedAtUtc == null
                          && device.EnrollmentState != DeviceEnrollmentStateNames.Removed
                          && device.EnrollmentState != DeviceEnrollmentStateNames.Rejected
                    select assignment.DeviceSeatAssignmentId)
                .AnyAsync(cancellationToken);
            if (taken) return Results.Conflict(new { Error = "The seat already has a device.", Code = ConsoleSeatErrorCodeNames.SeatTaken });

            var now = timeProvider.GetUtcNow();
            var console = new DeviceEntity
            {
                DeviceId = Guid.NewGuid(),
                OrganizationId = seat.OrganizationId,
                BranchId = branchId,
                MachineName = name,
                DisplayName = name,
                // Ключа у консоли нет — она не входит в систему. Строка уникальна и ключом не является.
                DevicePublicKey = $"console:{Guid.NewGuid():N}",
                Role = DeviceRoleNames.Console,
                EnrollmentState = DeviceEnrollmentStateNames.Approved,
                IsLocked = true,
                EnrolledAtUtc = now
            };
            dbContext.Devices.Add(console);
            dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = seat.OrganizationId,
                BranchId = branchId,
                SeatId = seat.SeatId,
                DeviceId = console.DeviceId,
                AttachedAtUtc = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(auditRecordWriter, seat.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateConsoleSeat, "Device", console.DeviceId.ToString("D"), AuditOutcome.Succeeded,
                new { seat.SeatId, SeatName = seat.Name, name }, cancellationToken);
            await NotifyDeviceChangesAsync(hubContext, dbContext, [console.DeviceId], now, cancellationToken);

            return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, console.DeviceId,
                new DeviceOnlineWindow(now, diagnosticsOptions.Value.StaleHeartbeatSeconds), cancellationToken));
        });
    }
}
