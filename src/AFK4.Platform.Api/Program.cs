using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Shared.Contracts.Devices;
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

var app = builder.Build();

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

app.MapPost("/api/branches/{branchId:guid}/device-enrollment-codes", async (
    Guid branchId,
    CreateDeviceEnrollmentCodeRequest request,
    IDeviceEnrollmentService enrollmentService,
    CancellationToken cancellationToken) =>
{
    if (request.OrganizationId == Guid.Empty)
    {
        return Results.BadRequest(new { Error = "OrganizationId is required." });
    }

    if (request.ExpiresInSeconds <= 0)
    {
        return Results.BadRequest(new { Error = "Enrollment code lifetime must be positive." });
    }

    var code = await enrollmentService.CreateEnrollmentCodeAsync(branchId, request, cancellationToken);

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
    IDeviceCommandDispatchService commandDispatchService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Type))
    {
        return Results.BadRequest(new { Error = "Command type is required." });
    }

    if (request.Payload is null)
    {
        return Results.BadRequest(new { Error = "Command payload is required." });
    }

    var command = await commandDispatchService.DispatchAsync(deviceId, request, cancellationToken);

    return Results.Ok(command);
});

app.MapGet("/api/devices/{deviceId:guid}/commands/{commandId:guid}/status", async (
    Guid deviceId,
    Guid commandId,
    IDeviceCommandStore commandStore,
    CancellationToken cancellationToken) =>
{
    var status = await commandStore.GetAsync(deviceId, commandId, cancellationToken);

    return status is null
        ? Results.NotFound()
        : Results.Ok(status);
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
