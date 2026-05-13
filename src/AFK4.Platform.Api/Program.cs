using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Tenancy;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddDbContext<PlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PlatformDatabase")
        ?? "Host=localhost;Port=5432;Database=afk4_dev;Username=postgres";

    options.UseNpgsql(connectionString);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EfDeviceEnrollmentService>();
builder.Services.AddScoped<IDeviceEnrollmentService>(provider => provider.GetRequiredService<EfDeviceEnrollmentService>());
builder.Services.AddScoped<IDeviceCredentialValidator>(provider => provider.GetRequiredService<EfDeviceEnrollmentService>());
builder.Services.AddScoped<IDeviceCredentialLifecycleService, EfDeviceCredentialLifecycleService>();
builder.Services.AddScoped<IDeviceCommandStore, EfDeviceCommandStore>();
builder.Services.AddSingleton<IDeviceConnectionRegistry, InMemoryDeviceConnectionRegistry>();
builder.Services.AddScoped<IDeviceCommandDispatchService, DeviceCommandDispatchService>();
builder.Services.AddScoped<IDeviceHeartbeatService, DeviceHeartbeatService>();
builder.Services.AddScoped<IFloorMapReadService, EfFloorMapReadService>();
builder.Services.AddScoped<IStaffTokenService, OpaqueStaffTokenService>();
builder.Services.AddScoped<IStaffCredentialService, PasswordHashingStaffCredentialService>();
builder.Services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
builder.Services.AddScoped<StaffAuthorizationService>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();
builder.Services.Configure<SessionLeaseOptions>(builder.Configuration.GetSection("Sessions"));
builder.Services.AddScoped<ISessionLeaseSigner, EcdsaSessionLeaseSigner>();
builder.Services.AddScoped<ISessionCommandService, EfSessionCommandService>();

var app = builder.Build();

app.UseMiddleware<StaffAuthenticationMiddleware>();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow));
});

app.MapGet("/api/branches/{branchId:guid}/floor-map", async (
    Guid branchId,
    IFloorMapReadService floorMapReadService,
    StaffAuthorizationService authorizationService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewFloorMap,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var floorMap = await floorMapReadService.GetFloorMapAsync(branchId, cancellationToken);

    return floorMap is null
        ? Results.NotFound()
        : Results.Ok(floorMap);
});

app.MapPost("/api/auth/staff/sign-in", async (
    StaffSignInRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var response = await credentialService.SignInAsync(request, cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

app.MapPost("/api/auth/staff/refresh", async (
    StaffRefreshTokenRequest request,
    IStaffTokenService tokenService,
    CancellationToken cancellationToken) =>
{
    var response = await tokenService.RefreshAsync(request, cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

app.MapPost("/api/branches/{branchId:guid}/sessions/start", async (
    Guid branchId,
    StartGuestSessionRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.StartSession,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.StartSession,
            "Session",
            null,
            AuditOutcome.Denied,
            "PlatformApi",
            JsonSerializer.Serialize(new
            {
                request.SeatId,
                authorization.DenialReason
            })),
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await sessionCommandService.StartGuestSessionAsync(
        branchId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { Error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.StartSession,
        "Session",
        result.Response!.Session.SessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new
        {
            request.SeatId,
            request.DurationMinutes
        })),
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/sessions/{sessionId:guid}/extend", async (
    Guid sessionId,
    ExtendSessionRequest request,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var session = await dbContext.Sessions
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

    if (session is null)
    {
        return Results.NotFound();
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        session.BranchId,
        StaffPermissionNames.ExtendSession,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            session.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.ExtendSession,
            "Session",
            sessionId.ToString("D"),
            AuditOutcome.Denied,
            "PlatformApi",
            JsonSerializer.Serialize(new
            {
                authorization.DenialReason
            })),
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await sessionCommandService.ExtendSessionAsync(
        sessionId,
        authorization.StaffContext!.StaffUserId,
        request,
        cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { Error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        session.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ExtendSession,
        "Session",
        sessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new
        {
            request.AdditionalMinutes,
            request.TariffRuleVersionId
        })),
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/sessions/{sessionId:guid}/transfer", async (
    Guid sessionId,
    TransferSessionRequest request,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var session = await dbContext.Sessions
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

    if (session is null)
    {
        return Results.NotFound();
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        session.BranchId,
        StaffPermissionNames.TransferSession,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            session.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.TransferSession,
            "Session",
            sessionId.ToString("D"),
            AuditOutcome.Denied,
            "PlatformApi",
            JsonSerializer.Serialize(new
            {
                request.TargetSeatId,
                authorization.DenialReason
            })),
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await sessionCommandService.TransferSessionAsync(
        sessionId,
        authorization.StaffContext!.StaffUserId,
        request,
        cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { Error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        session.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.TransferSession,
        "Session",
        sessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new
        {
            request.TargetSeatId
        })),
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/sessions/{sessionId:guid}/end", async (
    Guid sessionId,
    EndSessionRequest request,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var session = await dbContext.Sessions
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

    if (session is null)
    {
        return Results.NotFound();
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        session.BranchId,
        StaffPermissionNames.EndSession,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            session.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.EndSession,
            "Session",
            sessionId.ToString("D"),
            AuditOutcome.Denied,
            "PlatformApi",
            JsonSerializer.Serialize(new
            {
                request.Reason,
                authorization.DenialReason
            })),
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await sessionCommandService.EndSessionAsync(
        sessionId,
        authorization.StaffContext!.StaffUserId,
        request,
        cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { Error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        session.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.EndSession,
        "Session",
        sessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new
        {
            request.Reason
        })),
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/device-enrollment-codes", async (
    Guid branchId,
    CreateDeviceEnrollmentCodeRequest request,
    IDeviceEnrollmentService enrollmentService,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.CreateDeviceEnrollmentCode,
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
});

app.MapPost("/api/devices/enroll", async (
    DeviceEnrollmentRequest request,
    IDeviceEnrollmentService enrollmentService,
    CancellationToken cancellationToken) =>
{
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

    var response = await heartbeatService.RecordHeartbeatAsync(deviceId, request, cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/devices/{deviceId:guid}/session-reconciliation", async (
    Guid deviceId,
    DeviceSessionSnapshotRequest request,
    HttpContext httpContext,
    PlatformDbContext dbContext,
    IDeviceCredentialValidator credentialValidator,
    IDeviceCommandDispatchService commandDispatchService,
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
    if (!credentialValidator.Validate(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
    {
        return Results.Unauthorized();
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
    if (!credentialValidator.Validate(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
    {
        return Results.Unauthorized();
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

app.MapGet("/api/devices/{deviceId:guid}", async (
    Guid deviceId,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
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
        StaffPermissionNames.ViewDeviceDetail,
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
            command.UpdatedAtUtc))
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
        IsOnline: device.IsOnline,
        IsLocked: device.IsLocked,
        SeatId: seat?.SeatId,
        SeatName: seat?.Name,
        ZoneId: zone?.ZoneId,
        ZoneName: zone?.Name,
        ActiveCredentialCount: activeCredentialCount,
        InstalledAppCount: installedAppCount,
        RecentCommands: recentCommands));
});

app.MapPost("/api/devices/{deviceId:guid}/commands", async (
    Guid deviceId,
    CreateDeviceCommandRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IDeviceCommandDispatchService commandDispatchService,
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
        StaffPermissionNames.DispatchDeviceCommand,
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

    var command = await commandDispatchService.DispatchAsync(deviceId, request, cancellationToken);

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
            command.Type
        })),
        cancellationToken);

    return Results.Ok(command);
});

app.MapGet("/api/devices/{deviceId:guid}/commands/{commandId:guid}/status", async (
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
        StaffPermissionNames.ViewDeviceCommandStatus,
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
});

app.MapPost("/api/devices/{deviceId:guid}/credentials/rotate", async (
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
        StaffPermissionNames.RotateDeviceCredential,
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
});

app.MapPost("/api/devices/{deviceId:guid}/credentials/{credentialId:guid}/revoke", async (
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
        StaffPermissionNames.RevokeDeviceCredential,
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
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

static async Task<IResult> CompleteReconciliationAsync(
    PlatformDbContext dbContext,
    IDeviceCommandDispatchService commandDispatchService,
    DeviceSessionSnapshotRequest request,
    string action,
    string reason,
    SessionEntity? session,
    SessionLeaseDto? lease,
    bool dispatchCommand,
    DateTimeOffset recordedAtUtc,
    CancellationToken cancellationToken)
{
    var sessionId = session?.SessionId ?? request.ActiveSessionId ?? request.ActiveLease?.SessionId;

    if (dispatchCommand)
    {
        var payload = CreateReconciliationCommandPayload(action, reason, sessionId, lease);
        await commandDispatchService.DispatchAsync(
            request.DeviceId,
            new CreateDeviceCommandRequest(action, payload),
            cancellationToken);
    }

    if (session is not null)
    {
        dbContext.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = "device-reconciled",
            ActorStaffUserId = null,
            DeviceId = request.DeviceId,
            CreatedAtUtc = recordedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                action,
                reason,
                request.ActiveSessionId,
                request.ObservedAtUtc,
                request.PendingLocalEventCount
            })
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new SessionReconciliationResponse(
        Action: action,
        Reason: reason,
        SessionId: sessionId,
        Lease: lease));
}

static Dictionary<string, string> CreateReconciliationCommandPayload(
    string action,
    string reason,
    Guid? sessionId,
    SessionLeaseDto? lease)
{
    var payload = new Dictionary<string, string>
    {
        ["reason"] = reason
    };

    if (sessionId is not null)
    {
        payload["sessionId"] = sessionId.Value.ToString("D");
    }

    if (action == "unlock" && lease is not null)
    {
        payload["sessionLease"] = JsonSerializer.Serialize(lease, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    return payload;
}

static bool LocalLeaseMatches(
    DeviceSessionSnapshotRequest request,
    SessionEntity cloudSession,
    SessionLeaseDto currentLease,
    DateTimeOffset now)
{
    var localLease = request.ActiveLease;

    return localLease is not null &&
        request.ActiveSessionId == cloudSession.SessionId &&
        localLease.SessionId == cloudSession.SessionId &&
        localLease.OrganizationId == cloudSession.OrganizationId &&
        localLease.BranchId == cloudSession.BranchId &&
        localLease.DeviceId == cloudSession.DeviceId &&
        localLease.SeatId == cloudSession.SeatId &&
        localLease.Sequence == currentLease.Sequence &&
        string.Equals(localLease.Signature, currentLease.Signature, StringComparison.Ordinal) &&
        localLease.ExpiresAtUtc > now;
}

static async Task<SessionLeaseDto?> LoadCurrentLeaseAsync(
    PlatformDbContext dbContext,
    SessionEntity session,
    CancellationToken cancellationToken)
{
    var leaseEntity = session.CurrentLeaseId is null
        ? await dbContext.SessionLeases
            .Where(lease => lease.SessionId == session.SessionId)
            .OrderByDescending(lease => lease.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
        : await dbContext.SessionLeases
            .SingleOrDefaultAsync(lease => lease.SessionLeaseId == session.CurrentLeaseId, cancellationToken);

    if (leaseEntity is null)
    {
        return null;
    }

    var lease = JsonSerializer.Deserialize<SessionLeaseDto>(
        leaseEntity.PayloadJson,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    return lease ?? new SessionLeaseDto(
        SessionId: leaseEntity.SessionId,
        OrganizationId: leaseEntity.OrganizationId,
        BranchId: leaseEntity.BranchId,
        SeatId: leaseEntity.SeatId,
        DeviceId: leaseEntity.DeviceId,
        State: leaseEntity.State,
        Sequence: leaseEntity.Sequence,
        IssuedAtUtc: leaseEntity.IssuedAtUtc,
        ExpiresAtUtc: leaseEntity.ExpiresAtUtc,
        SignatureAlgorithm: leaseEntity.SignatureAlgorithm,
        Signature: leaseEntity.Signature);
}

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
