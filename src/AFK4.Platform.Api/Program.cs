using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tenancy;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
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
builder.Services.AddScoped<IDeviceCommandStore, EfDeviceCommandStore>();
builder.Services.AddSingleton<IDeviceConnectionRegistry, InMemoryDeviceConnectionRegistry>();
builder.Services.AddScoped<IDeviceCommandDispatchService, DeviceCommandDispatchService>();
builder.Services.AddScoped<IDeviceHeartbeatService, DeviceHeartbeatService>();
builder.Services.AddSingleton<IFloorMapReadService, InMemoryFloorMapReadService>();
builder.Services.AddScoped<IStaffTokenService, OpaqueStaffTokenService>();
builder.Services.AddScoped<IStaffCredentialService, PasswordHashingStaffCredentialService>();
builder.Services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
builder.Services.AddScoped<StaffAuthorizationService>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();

var app = builder.Build();

app.UseMiddleware<StaffAuthenticationMiddleware>();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow));
});

app.MapGet("/api/branches/{branchId:guid}/floor-map", (
    Guid branchId,
    IFloorMapReadService floorMapReadService) =>
{
    return Results.Ok(floorMapReadService.GetFloorMap(branchId));
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

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
