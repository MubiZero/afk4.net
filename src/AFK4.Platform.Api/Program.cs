using System.Text.Json;
using System.Text;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
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
builder.Services.AddScoped<IAuditSearchService, EfAuditSearchService>();
builder.Services.AddSingleton(new BranchDiagnosticsOptions());
builder.Services.AddScoped<IBranchDiagnosticsService, EfBranchDiagnosticsService>();
builder.Services.AddScoped<EfShiftService>();
builder.Services.AddScoped<IShiftService>(provider => provider.GetRequiredService<EfShiftService>());
builder.Services.AddScoped<IOpenShiftResolver>(provider => provider.GetRequiredService<EfShiftService>());
builder.Services.AddScoped<IInventoryService, EfInventoryService>();
builder.Services.AddScoped<IPosService, EfPosService>();
builder.Services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
builder.Services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
builder.Services.AddScoped<IReportService, EfReportService>();
builder.Services.AddScoped<IOperatorDashboardService, EfOperatorDashboardService>();
builder.Services.AddScoped<IReservationService, EfReservationService>();
builder.Services.Configure<SessionLeaseOptions>(builder.Configuration.GetSection("Sessions"));
builder.Services.AddScoped<ISessionLeaseSigner, EcdsaSessionLeaseSigner>();
builder.Services.AddScoped<IHeartbeatSessionCommandPlanner, EfHeartbeatSessionCommandPlanner>();
builder.Services.AddScoped<ISessionCommandService, EfSessionCommandService>();
builder.Services.AddScoped<ISessionCommandResultProcessor, EfSessionCommandResultProcessor>();
builder.Services.AddScoped<IBillingCommandService, EfBillingCommandService>();
builder.Services.AddScoped<ITariffService, EfTariffService>();
builder.Services.AddScoped<IPackageService, EfPackageService>();
builder.Services.AddScoped<ISessionBillingService, SessionBillingService>();
builder.Services.AddScoped<IOperatorReferenceDataService, EfOperatorReferenceDataService>();
builder.Services.AddScoped<IUpdateService, EfUpdateService>();

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

app.MapGet("/api/branches/{branchId:guid}/staff", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageBranchStaff,
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
            AuditActionNames.ViewStaffUsers,
            "StaffUser",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var organizationId = authorization.StaffContext!.OrganizationId;
    var roleAssignments = await dbContext.StaffRoleAssignments
        .AsNoTracking()
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == organizationId &&
            roleAssignment.BranchId == branchId)
        .OrderBy(roleAssignment => roleAssignment.RoleName)
        .ToListAsync(cancellationToken);
    var staffUserIds = roleAssignments.Select(roleAssignment => roleAssignment.StaffUserId).ToHashSet();
    var staffUsers = await dbContext.StaffUsers
        .AsNoTracking()
        .Where(staffUser =>
            staffUser.OrganizationId == organizationId &&
            staffUserIds.Contains(staffUser.StaffUserId))
        .OrderBy(staffUser => staffUser.DisplayName)
        .ToListAsync(cancellationToken);
    var rolesByStaffUserId = roleAssignments
        .GroupBy(roleAssignment => roleAssignment.StaffUserId)
        .ToDictionary(
            group => group.Key,
            group => group.Select(roleAssignment => roleAssignment.RoleName).ToList() as IReadOnlyList<string>);
    var response = staffUsers
        .Select(staffUser => ToStaffUserDto(
            staffUser,
            rolesByStaffUserId.GetValueOrDefault(staffUser.StaffUserId) ?? []))
        .ToList();

    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewStaffUsers,
        "StaffUser",
        null,
        AuditOutcome.Succeeded,
        new { Count = response.Count },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/branches/{branchId:guid}/staff", async (
    Guid branchId,
    CreateStaffUserRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageBranchStaff,
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
            AuditActionNames.CreateStaffUser,
            "StaffUser",
            null,
            AuditOutcome.Denied,
            new { request.UserName, request.RoleNames, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateCreateStaffUserRequest(request);
    if (validation is not null)
    {
        return Results.BadRequest(new { Error = validation });
    }

    var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
    var roleNames = request.RoleNames
        .Select(roleName => roleName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(roleName => roleName, StringComparer.Ordinal)
        .ToList();
    var createdAtUtc = timeProvider.GetUtcNow();
    var staffUser = await dbContext.StaffUsers
        .SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == request.OrganizationId &&
                candidate.NormalizedUserName == normalizedUserName,
            cancellationToken);

    if (staffUser is null)
    {
        staffUser = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            CreatedAtUtc = createdAtUtc
        };
        var hasher = new PasswordHasher<StaffUserEntity>();
        staffUser.PasswordHash = hasher.HashPassword(staffUser, request.Password);
        dbContext.StaffUsers.Add(staffUser);
    }

    var existingRoleNames = await dbContext.StaffRoleAssignments
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == request.OrganizationId &&
            roleAssignment.BranchId == branchId &&
            roleAssignment.StaffUserId == staffUser.StaffUserId)
        .Select(roleAssignment => roleAssignment.RoleName)
        .ToListAsync(cancellationToken);
    var existingRoleSet = existingRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var roleName in roleNames.Where(roleName => !existingRoleSet.Contains(roleName)))
    {
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUser.StaffUserId,
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            RoleName = roleName
        });
        existingRoleNames.Add(roleName);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToStaffUserDto(
        staffUser,
        existingRoleNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleName => roleName, StringComparer.Ordinal)
            .ToList());

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateStaffUser,
        "StaffUser",
        staffUser.StaffUserId.ToString("D"),
        AuditOutcome.Succeeded,
        new { staffUser.UserName, response.RoleNames },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPatch("/api/branches/{branchId:guid}/staff/{staffUserId:guid}/roles", async (
    Guid branchId,
    Guid staffUserId,
    UpdateStaffUserRolesRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageRoles,
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
            AuditActionNames.UpdateStaffRoles,
            "StaffUser",
            staffUserId.ToString("D"),
            AuditOutcome.Denied,
            new { request.RoleNames, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateStaffRoleNames(request.RoleNames);
    if (validation is not null)
    {
        return Results.BadRequest(new { Error = validation });
    }

    var staffUser = await dbContext.StaffUsers
        .SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == request.OrganizationId &&
                candidate.StaffUserId == staffUserId,
            cancellationToken);

    if (staffUser is null)
    {
        return Results.NotFound();
    }

    var existingAssignments = await dbContext.StaffRoleAssignments
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == request.OrganizationId &&
            roleAssignment.BranchId == branchId &&
            roleAssignment.StaffUserId == staffUserId)
        .ToListAsync(cancellationToken);

    if (existingAssignments.Count == 0)
    {
        return Results.NotFound();
    }

    var roleNames = request.RoleNames
        .Select(roleName => roleName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(roleName => roleName, StringComparer.Ordinal)
        .ToList();
    var requestedRoleSet = roleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var assignmentsToRemove = existingAssignments
        .Where(roleAssignment => !requestedRoleSet.Contains(roleAssignment.RoleName))
        .ToList();

    dbContext.StaffRoleAssignments.RemoveRange(assignmentsToRemove);

    var existingRoleSet = existingAssignments
        .Where(roleAssignment => requestedRoleSet.Contains(roleAssignment.RoleName))
        .Select(roleAssignment => roleAssignment.RoleName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var roleName in roleNames.Where(roleName => !existingRoleSet.Contains(roleName)))
    {
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            RoleName = roleName
        });
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToStaffUserDto(staffUser, roleNames);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateStaffRoles,
        "StaffUser",
        staffUserId.ToString("D"),
        AuditOutcome.Succeeded,
        new { staffUser.UserName, response.RoleNames },
        cancellationToken);

    return Results.Ok(response);
});

app.MapGet("/api/branches/{branchId:guid}/profile", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageLayout,
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
            AuditActionNames.ViewBranchProfile,
            "Branch",
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

    var response = ToBranchProfileDto(branch);
    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewBranchProfile,
        "Branch",
        branchId.ToString("D"),
        AuditOutcome.Succeeded,
        new { branch.Name, branch.City },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPatch("/api/branches/{branchId:guid}/profile", async (
    Guid branchId,
    UpdateBranchProfileRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageLayout,
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
            AuditActionNames.UpdateBranchProfile,
            "Branch",
            branchId.ToString("D"),
            AuditOutcome.Denied,
            new { request.Name, request.City, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateUpdateBranchProfileRequest(request);
    if (validation is not null)
    {
        return Results.BadRequest(new { Error = validation });
    }

    var branch = await dbContext.Branches
        .SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == request.OrganizationId && candidate.BranchId == branchId,
            cancellationToken);

    if (branch is null)
    {
        return Results.NotFound();
    }

    branch.Name = request.Name.Trim();
    branch.City = request.City.Trim();
    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToBranchProfileDto(branch);
    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateBranchProfile,
        "Branch",
        branchId.ToString("D"),
        AuditOutcome.Succeeded,
        new { branch.Name, branch.City },
        cancellationToken);

    return Results.Ok(response);
});

app.MapGet("/api/branches/{branchId:guid}/layout/zones", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageLayout,
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
            AuditActionNames.ViewLayout,
            "Layout",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var organizationId = authorization.StaffContext!.OrganizationId;
    var zones = await dbContext.Zones
        .AsNoTracking()
        .Where(zone => zone.OrganizationId == organizationId && zone.BranchId == branchId)
        .OrderBy(zone => zone.SortOrder)
        .ThenBy(zone => zone.Name)
        .ToListAsync(cancellationToken);
    var zoneIds = zones.Select(zone => zone.ZoneId).ToHashSet();
    var seats = await dbContext.Seats
        .AsNoTracking()
        .Where(seat =>
            seat.OrganizationId == organizationId &&
            seat.BranchId == branchId &&
            zoneIds.Contains(seat.ZoneId))
        .OrderBy(seat => seat.SortOrder)
        .ThenBy(seat => seat.Name)
        .ToListAsync(cancellationToken);
    var seatsByZoneId = seats
        .GroupBy(seat => seat.ZoneId)
        .ToDictionary(group => group.Key, group => group.ToList() as IReadOnlyList<SeatEntity>);
    var response = zones
        .Select(zone => ToZoneDto(zone, seatsByZoneId.GetValueOrDefault(zone.ZoneId) ?? []))
        .ToList();

    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewLayout,
        "Layout",
        null,
        AuditOutcome.Succeeded,
        new { ZoneCount = response.Count, SeatCount = seats.Count },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/branches/{branchId:guid}/layout/zones", async (
    Guid branchId,
    CreateZoneRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageLayout,
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
            AuditActionNames.CreateZone,
            "Zone",
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

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { Error = "Zone name is required." });
    }

    var normalizedName = request.Name.Trim().ToUpperInvariant();
    var zone = await dbContext.Zones.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.Name.ToUpper() == normalizedName,
        cancellationToken);

    if (zone is null)
    {
        zone = new ZoneEntity
        {
            ZoneId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.Zones.Add(zone);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    var response = ToZoneDto(zone, []);
    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateZone,
        "Zone",
        zone.ZoneId.ToString("D"),
        AuditOutcome.Succeeded,
        new { zone.Name, zone.SortOrder },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/branches/{branchId:guid}/layout/seats", async (
    Guid branchId,
    CreateSeatRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageLayout,
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
            AuditActionNames.CreateSeat,
            "Seat",
            null,
            AuditOutcome.Denied,
            new { request.ZoneId, request.Name, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    if (request.ZoneId == Guid.Empty)
    {
        return Results.BadRequest(new { Error = "ZoneId is required." });
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { Error = "Seat name is required." });
    }

    var zoneExists = await dbContext.Zones.AnyAsync(
        zone =>
            zone.OrganizationId == request.OrganizationId &&
            zone.BranchId == branchId &&
            zone.ZoneId == request.ZoneId,
        cancellationToken);
    if (!zoneExists)
    {
        return Results.NotFound(new { Error = "Zone was not found." });
    }

    var normalizedName = request.Name.Trim().ToUpperInvariant();
    var seat = await dbContext.Seats.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.ZoneId == request.ZoneId &&
            candidate.Name.ToUpper() == normalizedName,
        cancellationToken);

    if (seat is null)
    {
        seat = new SeatEntity
        {
            SeatId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            ZoneId = request.ZoneId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.Seats.Add(seat);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    var response = ToSeatDto(seat);
    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateSeat,
        "Seat",
        seat.SeatId.ToString("D"),
        AuditOutcome.Succeeded,
        new { seat.ZoneId, seat.Name, seat.SortOrder },
        cancellationToken);

    return Results.Ok(response);
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

app.MapPost("/api/devices/{deviceId:guid}/commands/{commandId:guid}/result", async (
    Guid deviceId,
    Guid commandId,
    DeviceCommandResultDto result,
    HttpContext httpContext,
    IDeviceCredentialValidator credentialValidator,
    IDeviceCommandStore commandStore,
    ISessionCommandResultProcessor sessionCommandResultProcessor,
    IHubContext<DeviceHub> hubContext,
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
    if (!credentialValidator.Validate(result.OrganizationId, result.BranchId, deviceId, credentialSecret))
    {
        return Results.Unauthorized();
    }

    await commandStore.ApplyResultAsync(result, cancellationToken);
    await sessionCommandResultProcessor.ProcessAsync(result, cancellationToken);
    await hubContext.Clients.All.SendAsync(DeviceRealtimeEvents.DeviceCommandResult, result, cancellationToken);

    return Results.Ok();
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

app.MapPost("/api/branches/{branchId:guid}/devices/{deviceId:guid}/seat-assignment", async (
    Guid branchId,
    Guid deviceId,
    AssignDeviceSeatRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
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
        StaffPermissionNames.AssignDeviceSeat,
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

app.MapPost("/api/branches/{branchId:guid}/players", async (
    Guid branchId,
    CreatePlayerAccountRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IBillingCommandService billingCommandService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.CreatePlayerAccount,
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

app.MapGet("/api/branches/{branchId:guid}/players", async (
    Guid branchId,
    string? query,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IOperatorReferenceDataService referenceDataService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewPlayers,
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
        cancellationToken);

    return Results.Ok(players);
});

app.MapGet("/api/players/{playerAccountId:guid}/wallet-summary", async (
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

    if (!player.Authorization!.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerAccountId, cancellationToken);

    return summary is null
        ? Results.NotFound()
        : Results.Ok(summary);
});

app.MapPost("/api/players/{playerAccountId:guid}/wallet/top-ups", async (
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
        StaffPermissionNames.TopUpWallet,
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

app.MapPost("/api/players/{playerAccountId:guid}/ledger/{ledgerEntryId:guid}/refunds", async (
    Guid playerAccountId,
    Guid ledgerEntryId,
    RefundLedgerEntryRequest request,
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
        StaffPermissionNames.RefundLedgerEntry,
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
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/players/{playerAccountId:guid}/ledger/manual-corrections", async (
    Guid playerAccountId,
    ManualLedgerCorrectionRequest request,
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
        StaffPermissionNames.ManualLedgerCorrection,
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
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/players/{playerAccountId:guid}/debts/payments", async (
    Guid playerAccountId,
    PayDebtRequest request,
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
        StaffPermissionNames.PayDebt,
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
            AuditActionNames.PayDebt,
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

    var result = await billingCommandService.PayDebtAsync(
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
        AuditActionNames.PayDebt,
        "PlayerAccount",
        playerAccountId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Amount },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/tariffs", async (
    Guid branchId,
    CreateTariffRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ITariffService tariffService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageTariffs,
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
            AuditActionNames.CreateTariff,
            "Tariff",
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

    var result = await tariffService.CreateTariffAsync(
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
        AuditActionNames.CreateTariff,
        "Tariff",
        result.Response!.TariffId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Name },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/tariffs/{tariffId:guid}/versions", async (
    Guid branchId,
    Guid tariffId,
    CreateTariffVersionRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ITariffService tariffService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageTariffs,
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
            AuditActionNames.CreateTariffVersion,
            "Tariff",
            tariffId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    if (request.TariffId != tariffId)
    {
        return Results.BadRequest(new { Error = "Route tariffId must match request TariffId." });
    }

    var result = await tariffService.CreateTariffVersionAsync(
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
        AuditActionNames.CreateTariffVersion,
        "TariffVersion",
        result.Response!.TariffVersionId.ToString("D"),
        AuditOutcome.Succeeded,
        new { tariffId, result.Response.VersionNumber },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/tariffs/calculate", async (
    Guid branchId,
    CalculateTariffRequest request,
    StaffAuthorizationService authorizationService,
    ITariffService tariffService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewBilling,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    if (request.DurationMinutes <= 0)
    {
        return Results.BadRequest(new { Error = "DurationMinutes must be positive." });
    }

    var calculation = await tariffService.CalculateAsync(branchId, request, cancellationToken);

    return calculation is null
        ? Results.NotFound()
        : Results.Ok(calculation);
});

app.MapGet("/api/branches/{branchId:guid}/tariffs/options", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IOperatorReferenceDataService referenceDataService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewTariffs,
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
            AuditActionNames.ViewTariffs,
            "Tariff",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var options = await referenceDataService.GetTariffOptionsAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    return Results.Ok(options);
});

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

app.MapPost("/api/branches/{branchId:guid}/shifts/open", async (
    Guid branchId,
    OpenShiftRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IShiftService shiftService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.OpenShift,
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
            AuditActionNames.OpenShift,
            "Shift",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await shiftService.OpenShiftAsync(
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
        AuditActionNames.OpenShift,
        "Shift",
        result.Response!.ShiftId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.StartingCash },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/shifts/current", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IShiftService shiftService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewShift,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await shiftService.GetCurrentShiftAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    return result.Response is null
        ? Results.NotFound()
        : Results.Ok(result.Response);
});

app.MapPost("/api/shifts/{shiftId:guid}/cash-movements", async (
    Guid shiftId,
    RecordCashMovementRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IShiftService shiftService,
    CancellationToken cancellationToken) =>
{
    var shift = await LoadShiftScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        shiftId,
        StaffPermissionNames.ManageShiftCash,
        cancellationToken);
    if (shift.Result is not null)
    {
        return shift.Result;
    }

    var authorization = shift.Authorization!;
    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            shift.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.RecordCashMovement,
            "Shift",
            shiftId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await shiftService.RecordCashMovementAsync(
        shiftId,
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
        shift.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.RecordCashMovement,
        "CashMovement",
        result.Response!.CashMovementId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.MovementType, request.Amount },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/shifts/{shiftId:guid}/close", async (
    Guid shiftId,
    CloseShiftRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IShiftService shiftService,
    CancellationToken cancellationToken) =>
{
    var shift = await LoadShiftScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        shiftId,
        StaffPermissionNames.CloseShift,
        cancellationToken);
    if (shift.Result is not null)
    {
        return shift.Result;
    }

    var authorization = shift.Authorization!;
    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            shift.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.CloseShift,
            "Shift",
            shiftId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await shiftService.CloseShiftAsync(
        shiftId,
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
        shift.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CloseShift,
        "Shift",
        shiftId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.CountedCash },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/dashboard/summary", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IOperatorDashboardService dashboardService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewDashboardSummary,
            "Dashboard",
            "summary",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new DashboardSummaryQuery(fromUtc, toUtc, limit);
    var result = await dashboardService.GetSummaryAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewDashboardSummary,
        "Dashboard",
        "summary",
        AuditOutcome.Succeeded,
        new
        {
            FocusQueueCount = result.FocusQueue.Count,
            RecentPaymentCount = result.RecentPayments.Count,
            result.AlertPressure.TotalAlerts,
            fromUtc,
            toUtc,
            limit
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reservations", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    string? state,
    string? source,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReservations,
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
            AuditActionNames.ViewReservations,
            "Reservation",
            null,
            AuditOutcome.Denied,
            new { fromUtc, toUtc, state, source, limit, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await reservationService.SearchAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        new ReservationSearchQuery(fromUtc, toUtc, state, source, limit),
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewReservations,
        "Reservation",
        null,
        AuditOutcome.Succeeded,
        new { fromUtc, toUtc, state, source, limit, ResultCount = result.Reservations.Count },
        cancellationToken);

    return Results.Ok(result);
});

app.MapPost("/api/branches/{branchId:guid}/reservations", async (
    Guid branchId,
    CreateReservationRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageReservations,
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
            AuditActionNames.CreateReservation,
            "Reservation",
            null,
            AuditOutcome.Denied,
            new { request.CustomerName, request.StartsAtUtc, request.SeatId, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await reservationService.CreateAsync(
        branchId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);
    if (!result.Succeeded)
    {
        return ToReservationHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateReservation,
        "Reservation",
        result.Response!.ReservationId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.CustomerName, request.StartsAtUtc, request.SeatId, result.Response.State, result.Response.Source },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPatch("/api/reservations/{reservationId:guid}", async (
    Guid reservationId,
    UpdateReservationRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var scoped = await LoadReservationForStaffAsync(
        dbContext,
        staffContextAccessor,
        reservationId,
        cancellationToken);
    if (scoped.Result is not null)
    {
        return scoped.Result;
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        scoped.Reservation!.BranchId,
        StaffPermissionNames.ManageReservations,
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
            scoped.Reservation.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.UpdateReservation,
            "Reservation",
            reservationId.ToString("D"),
            AuditOutcome.Denied,
            new { request.SeatId, request.StartsAtUtc, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await reservationService.UpdateAsync(
        reservationId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);
    if (!result.Succeeded)
    {
        return ToReservationHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        result.Response!.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateReservation,
        "Reservation",
        reservationId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.SeatId, result.Response.StartsAtUtc, result.Response.State },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/reservations/{reservationId:guid}/confirm", async (
    Guid reservationId,
    ConfirmReservationRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
    if (scoped.Result is not null)
    {
        return scoped.Result;
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        scoped.Reservation!.BranchId,
        StaffPermissionNames.ManageReservations,
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
            scoped.Reservation.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.ConfirmReservation,
            "Reservation",
            reservationId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await reservationService.ConfirmAsync(
        reservationId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);
    if (!result.Succeeded)
    {
        return ToReservationHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        result.Response!.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ConfirmReservation,
        "Reservation",
        reservationId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.State },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/reservations/{reservationId:guid}/seat", async (
    Guid reservationId,
    SeatReservationRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
    if (scoped.Result is not null)
    {
        return scoped.Result;
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        scoped.Reservation!.BranchId,
        StaffPermissionNames.ManageReservations,
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
            scoped.Reservation.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.SeatReservation,
            "Reservation",
            reservationId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await reservationService.SeatAsync(
        reservationId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);
    if (!result.Succeeded)
    {
        return ToReservationHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        result.Response!.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.SeatReservation,
        "Reservation",
        reservationId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.SeatId, result.Response.State },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/reservations/{reservationId:guid}/cancel", async (
    Guid reservationId,
    CancelReservationRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
    if (scoped.Result is not null)
    {
        return scoped.Result;
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        scoped.Reservation!.BranchId,
        StaffPermissionNames.ManageReservations,
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
            scoped.Reservation.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.CancelReservation,
            "Reservation",
            reservationId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await reservationService.CancelAsync(
        reservationId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);
    if (!result.Succeeded)
    {
        return ToReservationHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        result.Response!.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CancelReservation,
        "Reservation",
        reservationId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.State, request.Reason },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/reports/shifts", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewShiftReport,
            "Report",
            "shifts",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetShiftReportAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewShiftReport,
        "Report",
        "shifts",
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Rows.Count,
            result.Limit,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reports/sales", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewSalesReport,
            "Report",
            "sales",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetSalesReportAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewSalesReport,
        "Report",
        "sales",
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Rows.Count,
            result.Limit,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reports/gameplay-time", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewGameplayTimeReport,
            "Report",
            "gameplay-time",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetGameplayTimeReportAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewGameplayTimeReport,
        "Report",
        "gameplay-time",
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Rows.Count,
            result.Limit,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reports/cash-operations", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewCashOperationReport,
            "Report",
            "cash-operations",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetCashOperationReportAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewCashOperationReport,
        "Report",
        "cash-operations",
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Rows.Count,
            result.Limit,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reports/operator-actions", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            AuditActionNames.ViewOperatorActionReport,
            "Report",
            "operator-actions",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetOperatorActionReportAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewOperatorActionReport,
        "Report",
        "operator-actions",
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Rows.Count,
            result.Limit,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/reports/shifts/export.csv", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    return await ExportReportCsvAsync(
        branchId,
        fromUtc,
        toUtc,
        limit,
        authorizationService,
        auditRecordWriter,
        reportService,
        AuditActionNames.ViewShiftReport,
        "shifts",
        "afk4-shifts-report.csv",
        static (service, organizationId, scopedBranchId, query, token) =>
            service.GetShiftReportAsync(organizationId, scopedBranchId, query, token),
        ReportCsvExporter.ExportShiftReport,
        cancellationToken);
});

app.MapGet("/api/branches/{branchId:guid}/reports/sales/export.csv", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    return await ExportReportCsvAsync(
        branchId,
        fromUtc,
        toUtc,
        limit,
        authorizationService,
        auditRecordWriter,
        reportService,
        AuditActionNames.ViewSalesReport,
        "sales",
        "afk4-sales-report.csv",
        static (service, organizationId, scopedBranchId, query, token) =>
            service.GetSalesReportAsync(organizationId, scopedBranchId, query, token),
        ReportCsvExporter.ExportSalesReport,
        cancellationToken);
});

app.MapGet("/api/branches/{branchId:guid}/reports/gameplay-time/export.csv", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    return await ExportReportCsvAsync(
        branchId,
        fromUtc,
        toUtc,
        limit,
        authorizationService,
        auditRecordWriter,
        reportService,
        AuditActionNames.ViewGameplayTimeReport,
        "gameplay-time",
        "afk4-gameplay-time-report.csv",
        static (service, organizationId, scopedBranchId, query, token) =>
            service.GetGameplayTimeReportAsync(organizationId, scopedBranchId, query, token),
        ReportCsvExporter.ExportGameplayTimeReport,
        cancellationToken);
});

app.MapGet("/api/branches/{branchId:guid}/reports/cash-operations/export.csv", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    return await ExportReportCsvAsync(
        branchId,
        fromUtc,
        toUtc,
        limit,
        authorizationService,
        auditRecordWriter,
        reportService,
        AuditActionNames.ViewCashOperationReport,
        "cash-operations",
        "afk4-cash-operations-report.csv",
        static (service, organizationId, scopedBranchId, query, token) =>
            service.GetCashOperationReportAsync(organizationId, scopedBranchId, query, token),
        ReportCsvExporter.ExportCashOperationReport,
        cancellationToken);
});

app.MapGet("/api/branches/{branchId:guid}/reports/operator-actions/export.csv", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    return await ExportReportCsvAsync(
        branchId,
        fromUtc,
        toUtc,
        limit,
        authorizationService,
        auditRecordWriter,
        reportService,
        AuditActionNames.ViewOperatorActionReport,
        "operator-actions",
        "afk4-operator-actions-report.csv",
        static (service, organizationId, scopedBranchId, query, token) =>
            service.GetOperatorActionReportAsync(organizationId, scopedBranchId, query, token),
        ReportCsvExporter.ExportOperatorActionReport,
        cancellationToken);
});

app.MapGet("/api/branches/{branchId:guid}/diagnostics", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IBranchDiagnosticsService diagnosticsService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewDiagnostics,
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
            AuditActionNames.ViewDiagnostics,
            "Diagnostics",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await diagnosticsService.GetAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewDiagnostics,
        "Diagnostics",
        null,
        AuditOutcome.Succeeded,
        new
        {
            result.DeviceSummary.TotalDevices,
            result.DeviceSummary.StaleDevices,
            result.CommandSummary.PendingCommands,
            result.CommandSummary.FailedCommands,
            result.UpdateSummary.ActiveRollouts,
            result.UpdateSummary.FailedDevices
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapPost("/api/branches/{branchId:guid}/pos/categories", async (
    Guid branchId,
    CreateProductCategoryRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IInventoryService inventoryService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManagePosCatalog,
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
            AuditActionNames.CreateProductCategory,
            "PosProductCategory",
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

    var result = await inventoryService.CreateCategoryAsync(
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
        AuditActionNames.CreateProductCategory,
        "PosProductCategory",
        result.Response!.CategoryId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Name },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/pos/products", async (
    Guid branchId,
    CreateProductRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IInventoryService inventoryService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManagePosCatalog,
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
            AuditActionNames.CreateProduct,
            "PosProduct",
            null,
            AuditOutcome.Denied,
            new { request.Sku, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await inventoryService.CreateProductAsync(
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
        AuditActionNames.CreateProduct,
        "PosProduct",
        result.Response!.ProductId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Sku, request.Price },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/pos/catalog", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IInventoryService inventoryService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewInventory,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await inventoryService.GetCatalogAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    return ToHttpResult(result);
});

app.MapGet("/api/branches/{branchId:guid}/inventory/stock-movements", async (
    Guid branchId,
    Guid? productId,
    int? limit,
    StaffAuthorizationService authorizationService,
    IInventoryService inventoryService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewInventory,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await inventoryService.GetStockMovementsAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        productId,
        Math.Clamp(limit ?? 50, 1, 200),
        cancellationToken);

    return ToHttpResult(result);
});

app.MapPost("/api/branches/{branchId:guid}/inventory/stock-movements", async (
    Guid branchId,
    CreateStockMovementRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IInventoryService inventoryService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageInventoryStock,
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
            AuditActionNames.CreateStockMovement,
            "StockMovement",
            null,
            AuditOutcome.Denied,
            new { request.ProductId, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await inventoryService.CreateStockMovementAsync(
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
        AuditActionNames.CreateStockMovement,
        "StockMovement",
        result.Response!.StockMovementId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.ProductId, request.MovementType, request.QuantityDelta },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/pos/sales", async (
    Guid branchId,
    CreatePosSaleRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IPosService posService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.CreatePosSale,
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
            AuditActionNames.CreatePosSale,
            "PosSale",
            null,
            AuditOutcome.Denied,
            new { request.ShiftId, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await posService.CreateSaleAsync(
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
        AuditActionNames.CreatePosSale,
        "PosSale",
        result.Response!.PosSaleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.ShiftId, LineCount = request.Lines.Count },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/pos/sales/{saleId:guid}/payments/manual", async (
    Guid saleId,
    ManualPaymentRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IPosService posService,
    CancellationToken cancellationToken) =>
{
    var sale = await LoadPosSaleScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        saleId,
        StaffPermissionNames.PayPosSale,
        cancellationToken);
    if (sale.Result is not null)
    {
        return sale.Result;
    }

    var authorization = sale.Authorization!;
    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            sale.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.PayPosSale,
            "PosSale",
            saleId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await posService.PaySaleAsync(
        saleId,
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
        sale.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.PayPosSale,
        "PosSale",
        saleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.PaymentMethod, request.Amount },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/pos/sales/{saleId:guid}/refunds", async (
    Guid saleId,
    RefundPosSaleRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IPosService posService,
    CancellationToken cancellationToken) =>
{
    var sale = await LoadPosSaleScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        saleId,
        StaffPermissionNames.RefundPosSale,
        cancellationToken);
    if (sale.Result is not null)
    {
        return sale.Result;
    }

    var authorization = sale.Authorization!;
    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            sale.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.RefundPosSale,
            "PosSale",
            saleId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await posService.RefundSaleAsync(
        saleId,
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
        sale.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.RefundPosSale,
        "PosSale",
        saleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Reason },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/pos/sales/{saleId:guid}/void", async (
    Guid saleId,
    VoidPosSaleRequest request,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IPosService posService,
    CancellationToken cancellationToken) =>
{
    var sale = await LoadPosSaleScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        saleId,
        StaffPermissionNames.VoidPosSale,
        cancellationToken);
    if (sale.Result is not null)
    {
        return sale.Result;
    }

    var authorization = sale.Authorization!;
    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            sale.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.VoidPosSale,
            "PosSale",
            saleId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await posService.VoidSaleAsync(
        saleId,
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
        sale.BranchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.VoidPosSale,
        "PosSale",
        saleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Reason },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/pos/sales/{saleId:guid}", async (
    Guid saleId,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IPosService posService,
    CancellationToken cancellationToken) =>
{
    var sale = await LoadPosSaleScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        saleId,
        StaffPermissionNames.ViewReceipt,
        cancellationToken);
    if (sale.Result is not null)
    {
        return sale.Result;
    }

    var authorization = sale.Authorization!;
    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await posService.GetSaleAsync(
        authorization.StaffContext!.OrganizationId,
        saleId,
        cancellationToken);

    return ToHttpResult(result);
});

app.MapGet("/api/receipts/{receiptId:guid}", async (
    Guid receiptId,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    CancellationToken cancellationToken) =>
{
    var receipt = await LoadReceiptScopedEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        receiptId,
        StaffPermissionNames.ViewReceipt,
        cancellationToken);
    if (receipt.Result is not null)
    {
        return receipt.Result;
    }

    if (!receipt.Authorization!.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Ok(ToDto(receipt.Entity!));
});

app.MapPost("/api/branches/{branchId:guid}/updates/packages", async (
    Guid branchId,
    CreateUpdatePackageRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageUpdatePackages,
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
            AuditActionNames.RegisterUpdatePackage,
            "UpdatePackage",
            null,
            AuditOutcome.Denied,
            new { request.Component, request.Version, request.Channel, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await updateService.RegisterPackageAsync(
        branchId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.RegisterUpdatePackage,
        "UpdatePackage",
        result.Response!.UpdatePackageId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.Component, result.Response.Version, result.Response.Channel },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/updates/packages/{packageId:guid}/state", async (
    Guid branchId,
    Guid packageId,
    UpdatePackageStateChangeRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageUpdatePackages,
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
            AuditActionNames.ChangeUpdatePackageState,
            "UpdatePackage",
            packageId.ToString("D"),
            AuditOutcome.Denied,
            new { request.State, request.Reason, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await updateService.ChangePackageStateAsync(
        authorization.StaffContext.OrganizationId,
        branchId,
        packageId,
        request,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ChangeUpdatePackageState,
        "UpdatePackage",
        packageId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response!.State, request.Reason },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/updates/rollouts", async (
    Guid branchId,
    CreateUpdateRolloutRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageUpdateRollouts,
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
            AuditActionNames.CreateUpdateRollout,
            "UpdateRollout",
            null,
            AuditOutcome.Denied,
            new { request.UpdatePackageId, request.Channel, request.TargetKind, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await updateService.CreateRolloutAsync(
        branchId,
        authorization.StaffContext.StaffUserId,
        request,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateUpdateRollout,
        "UpdateRollout",
        result.Response!.UpdateRolloutId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response.UpdatePackageId, result.Response.Channel, result.Response.TargetKind },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPost("/api/branches/{branchId:guid}/updates/rollouts/{rolloutId:guid}/state", async (
    Guid branchId,
    Guid rolloutId,
    UpdateRolloutStateChangeRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageUpdateRollouts,
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
            AuditActionNames.ChangeUpdateRolloutState,
            "UpdateRollout",
            rolloutId.ToString("D"),
            AuditOutcome.Denied,
            new { request.State, request.Reason, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await updateService.ChangeRolloutStateAsync(
        authorization.StaffContext.OrganizationId,
        branchId,
        rolloutId,
        request,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ChangeUpdateRolloutState,
        "UpdateRollout",
        rolloutId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response!.State, request.Reason },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/updates/rollouts", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewUpdateStatus,
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
            AuditActionNames.ViewUpdateRollout,
            "UpdateRollout",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await updateService.ListRolloutStatusesAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewUpdateRollout,
        "UpdateRollout",
        null,
        AuditOutcome.Succeeded,
        new { Count = result.Response!.Count },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/updates/rollouts/{rolloutId:guid}", async (
    Guid branchId,
    Guid rolloutId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IUpdateService updateService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewUpdateStatus,
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
            AuditActionNames.ViewUpdateRollout,
            "UpdateRollout",
            rolloutId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await updateService.GetRolloutAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        rolloutId,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToUpdateHttpResult(result);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewUpdateRollout,
        "UpdateRollout",
        rolloutId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response!.State },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/branches/{branchId:guid}/audit", async (
    Guid branchId,
    string? action,
    string? outcome,
    string? targetType,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IAuditSearchService auditSearchService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewAudit,
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
            AuditActionNames.ViewAudit,
            "AuditRecord",
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit);
    var result = await auditSearchService.SearchAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewAudit,
        "AuditRecord",
        null,
        AuditOutcome.Succeeded,
        new
        {
            Count = result.Records.Count,
            result.Limit,
            action,
            outcome,
            targetType,
            fromUtc,
            toUtc
        },
        cancellationToken);

    return Results.Ok(result);
});

app.MapPost("/api/devices/{deviceId:guid}/updates/check", async (
    Guid deviceId,
    DeviceUpdateCheckRequest request,
    HttpContext httpContext,
    IDeviceCredentialValidator credentialValidator,
    IUpdateService updateService,
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

    var result = await updateService.CheckForUpdatesAsync(request, cancellationToken);

    return ToUpdateHttpResult(result);
});

app.MapPost("/api/devices/{deviceId:guid}/updates/status", async (
    Guid deviceId,
    DeviceUpdateStatusReportRequest request,
    HttpContext httpContext,
    IDeviceCredentialValidator credentialValidator,
    IUpdateService updateService,
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

    var result = await updateService.ReportStatusAsync(request, cancellationToken);

    return ToUpdateHttpResult(result);
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

static IResult ToHttpResult<TResponse>(BillingCommandServiceResult<TResponse> result)
{
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

    return Results.Ok(result.Response);
}

static IResult ToUpdateHttpResult<TResponse>(UpdateServiceResult<TResponse> result)
{
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

    return Results.Ok(result.Response);
}

static IResult ToReservationHttpResult<TResponse>(ReservationServiceResult<TResponse> result)
{
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

    return Results.Ok(result.Response);
}

static async Task WriteAuditAsync(
    IAuditRecordWriter auditRecordWriter,
    Guid organizationId,
    Guid branchId,
    Guid actorStaffUserId,
    string action,
    string targetType,
    string? targetId,
    string outcome,
    object details,
    CancellationToken cancellationToken)
{
    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        organizationId,
        branchId,
        actorStaffUserId,
        action,
        targetType,
        targetId,
        outcome,
        "PlatformApi",
        JsonSerializer.Serialize(details)),
        cancellationToken);
}

static async Task<IResult> ExportReportCsvAsync<TReport>(
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    string auditAction,
    string targetId,
    string fileName,
    Func<IReportService, Guid, Guid, ReportSearchQuery, CancellationToken, Task<TReport>> loadReportAsync,
    Func<TReport, string> exportCsv,
    CancellationToken cancellationToken)
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewReports,
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
            "Report",
            targetId,
            AuditOutcome.Denied,
            new { Format = "csv", authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await loadReportAsync(
        reportService,
        authorization.StaffContext!.OrganizationId,
        branchId,
        query,
        cancellationToken);
    var csv = exportCsv(result);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        auditAction,
        "Report",
        targetId,
        AuditOutcome.Succeeded,
        new
        {
            Format = "csv",
            Count = GetReportRowCount(result),
            fromUtc,
            toUtc,
            limit
        },
        cancellationToken);

    return Results.File(
        Encoding.UTF8.GetBytes(csv),
        "text/csv; charset=utf-8",
        fileDownloadName: fileName);
}

static int GetReportRowCount<TReport>(TReport report)
{
    return report switch
    {
        ShiftReportResultDto shiftReport => shiftReport.Rows.Count,
        SalesReportResultDto salesReport => salesReport.Rows.Count,
        GameplayTimeReportResultDto gameplayTimeReport => gameplayTimeReport.Rows.Count,
        CashOperationReportResultDto cashOperationReport => cashOperationReport.Rows.Count,
        OperatorActionReportResultDto operatorActionReport => operatorActionReport.Rows.Count,
        _ => 0
    };
}

static async Task<PlayerAccountEntity?> LoadPlayerForStaffAsync(
    PlatformDbContext dbContext,
    Guid playerAccountId,
    Guid organizationId,
    CancellationToken cancellationToken)
{
    return await dbContext.PlayerAccounts
        .AsNoTracking()
        .SingleOrDefaultAsync(
            player =>
                player.OrganizationId == organizationId &&
                player.PlayerAccountId == playerAccountId,
            cancellationToken);
}

static async Task<ReservationScopedEndpointResult> LoadReservationForStaffAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    Guid reservationId,
    CancellationToken cancellationToken)
{
    if (staffContextAccessor.Current is null)
    {
        return new ReservationScopedEndpointResult(null, Results.Unauthorized());
    }

    var reservation = await dbContext.Reservations
        .AsNoTracking()
        .SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == staffContextAccessor.Current.OrganizationId &&
                candidate.ReservationId == reservationId,
            cancellationToken);

    return reservation is null
        ? new ReservationScopedEndpointResult(null, Results.NotFound())
        : new ReservationScopedEndpointResult(reservation, null);
}

static async Task<PlayerScopedEndpointResult> LoadPlayerScopedEndpointAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    Guid playerAccountId,
    string permission,
    CancellationToken cancellationToken)
{
    if (staffContextAccessor.Current is null)
    {
        return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.Unauthorized());
    }

    var staffContext = staffContextAccessor.Current;
    var player = await LoadPlayerForStaffAsync(
        dbContext,
        playerAccountId,
        staffContext.OrganizationId,
        cancellationToken);

    if (player is not null && !staffContext.BranchIds.Contains(player.HomeBranchId))
    {
        var fallbackBranchId = staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
        if (fallbackBranchId == Guid.Empty)
        {
            return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var fallbackAuthorization = await authorizationService.RequireBranchPermissionAsync(
            fallbackBranchId,
            permission,
            cancellationToken);

        if (!fallbackAuthorization.IsAuthenticated)
        {
            return new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, Results.Unauthorized());
        }

        return fallbackAuthorization.IsAllowed
            ? new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, Results.NotFound())
            : new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, null);
    }

    var branchId = player?.HomeBranchId ?? staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
    if (branchId == Guid.Empty)
    {
        return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.StatusCode(StatusCodes.Status403Forbidden));
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        permission,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return new PlayerScopedEndpointResult(player, branchId, authorization, Results.Unauthorized());
    }

    if (!authorization.IsAllowed)
    {
        return new PlayerScopedEndpointResult(player, branchId, authorization, null);
    }

    return player is null
        ? new PlayerScopedEndpointResult(null, branchId, authorization, Results.NotFound())
        : new PlayerScopedEndpointResult(player, branchId, authorization, null);
}

static Task<ScopedEntityEndpointResult<ShiftEntity>> LoadShiftScopedEndpointAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    Guid shiftId,
    string permission,
    CancellationToken cancellationToken)
{
    return LoadScopedEntityEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        permission,
        (organizationId, token) => dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                shift => shift.OrganizationId == organizationId && shift.ShiftId == shiftId,
                token),
        shift => shift.BranchId,
        cancellationToken);
}

static Task<ScopedEntityEndpointResult<PosSaleEntity>> LoadPosSaleScopedEndpointAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    Guid saleId,
    string permission,
    CancellationToken cancellationToken)
{
    return LoadScopedEntityEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        permission,
        (organizationId, token) => dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(
                sale => sale.OrganizationId == organizationId && sale.PosSaleId == saleId,
                token),
        sale => sale.BranchId,
        cancellationToken);
}

static Task<ScopedEntityEndpointResult<ReceiptEntity>> LoadReceiptScopedEndpointAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    Guid receiptId,
    string permission,
    CancellationToken cancellationToken)
{
    return LoadScopedEntityEndpointAsync(
        dbContext,
        staffContextAccessor,
        authorizationService,
        permission,
        (organizationId, token) => dbContext.Receipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.OrganizationId == organizationId && receipt.ReceiptId == receiptId,
                token),
        receipt => receipt.BranchId,
        cancellationToken);
}

static async Task<ScopedEntityEndpointResult<TEntity>> LoadScopedEntityEndpointAsync<TEntity>(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    string permission,
    Func<Guid, CancellationToken, Task<TEntity?>> loadEntityAsync,
    Func<TEntity, Guid> getBranchId,
    CancellationToken cancellationToken)
    where TEntity : class
{
    if (staffContextAccessor.Current is null)
    {
        return new ScopedEntityEndpointResult<TEntity>(null, Guid.Empty, null, Results.Unauthorized());
    }

    var staffContext = staffContextAccessor.Current;
    var entity = await loadEntityAsync(staffContext.OrganizationId, cancellationToken);

    if (entity is not null && !staffContext.BranchIds.Contains(getBranchId(entity)))
    {
        var fallbackBranchId = staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
        if (fallbackBranchId == Guid.Empty)
        {
            return new ScopedEntityEndpointResult<TEntity>(
                null,
                Guid.Empty,
                null,
                Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var fallbackAuthorization = await authorizationService.RequireBranchPermissionAsync(
            fallbackBranchId,
            permission,
            cancellationToken);

        if (!fallbackAuthorization.IsAuthenticated)
        {
            return new ScopedEntityEndpointResult<TEntity>(
                null,
                fallbackBranchId,
                fallbackAuthorization,
                Results.Unauthorized());
        }

        return fallbackAuthorization.IsAllowed
            ? new ScopedEntityEndpointResult<TEntity>(null, fallbackBranchId, fallbackAuthorization, Results.NotFound())
            : new ScopedEntityEndpointResult<TEntity>(null, fallbackBranchId, fallbackAuthorization, null);
    }

    var branchId = entity is null
        ? staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault()
        : getBranchId(entity);
    if (branchId == Guid.Empty)
    {
        return new ScopedEntityEndpointResult<TEntity>(
            null,
            Guid.Empty,
            null,
            Results.StatusCode(StatusCodes.Status403Forbidden));
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        permission,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, Results.Unauthorized());
    }

    if (!authorization.IsAllowed)
    {
        return new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, null);
    }

    return entity is null
        ? new ScopedEntityEndpointResult<TEntity>(null, branchId, authorization, Results.NotFound())
        : new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, null);
}

static ReceiptDto ToDto(ReceiptEntity receipt)
{
    return new ReceiptDto(
        receipt.ReceiptId,
        receipt.OrganizationId,
        receipt.BranchId,
        receipt.PosSaleId,
        receipt.ReceiptNumber,
        receipt.ReceiptType,
        new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
        receipt.CreatedAtUtc);
}

static DeviceSeatAssignmentDto ToDeviceSeatAssignmentDto(DeviceSeatAssignmentEntity assignment)
{
    return new DeviceSeatAssignmentDto(
        assignment.DeviceSeatAssignmentId,
        assignment.OrganizationId,
        assignment.BranchId,
        assignment.SeatId,
        assignment.DeviceId,
        assignment.AttachedAtUtc,
        assignment.DetachedAtUtc);
}

static StaffUserDto ToStaffUserDto(StaffUserEntity staffUser, IReadOnlyList<string> roleNames)
{
    return new StaffUserDto(
        staffUser.StaffUserId,
        staffUser.OrganizationId,
        staffUser.UserName,
        staffUser.DisplayName,
        staffUser.IsActive,
        roleNames,
        staffUser.CreatedAtUtc);
}

static BranchProfileDto ToBranchProfileDto(BranchEntity branch)
{
    return new BranchProfileDto(
        branch.OrganizationId,
        branch.BranchId,
        branch.Name,
        branch.City,
        branch.CreatedAtUtc);
}

static ZoneDto ToZoneDto(ZoneEntity zone, IReadOnlyList<SeatEntity> seats)
{
    return new ZoneDto(
        zone.ZoneId,
        zone.OrganizationId,
        zone.BranchId,
        zone.Name,
        zone.SortOrder,
        zone.CreatedAtUtc,
        seats.Select(ToSeatDto).ToList());
}

static SeatDto ToSeatDto(SeatEntity seat)
{
    return new SeatDto(
        seat.SeatId,
        seat.OrganizationId,
        seat.BranchId,
        seat.ZoneId,
        seat.Name,
        seat.SortOrder,
        seat.CreatedAtUtc);
}

static string? ValidateCreateStaffUserRequest(CreateStaffUserRequest request)
{
    if (request.OrganizationId == Guid.Empty)
    {
        return "OrganizationId is required.";
    }

    if (string.IsNullOrWhiteSpace(request.UserName))
    {
        return "UserName is required.";
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return "DisplayName is required.";
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
    {
        return "Password must contain at least 8 characters.";
    }

    return ValidateStaffRoleNames(request.RoleNames);
}

static string? ValidateStaffRoleNames(IReadOnlyList<string> roleNames)
{
    if (roleNames.Count == 0)
    {
        return "At least one role is required.";
    }

    return roleNames.All(IsAssignableBranchStaffRole)
        ? null
        : "Unsupported branch staff role name.";
}

static bool IsAssignableBranchStaffRole(string roleName)
{
    return roleName.Trim() is
        StaffRoleNames.BranchManager or
        StaffRoleNames.ShiftSupervisor or
        StaffRoleNames.CashierOperator or
        StaffRoleNames.Technician or
        StaffRoleNames.AccountantAuditor;
}

static string? ValidateUpdateBranchProfileRequest(UpdateBranchProfileRequest request)
{
    if (request.OrganizationId == Guid.Empty)
    {
        return "OrganizationId is required.";
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return "Name is required.";
    }

    if (request.Name.Trim().Length > 160)
    {
        return "Name must contain 160 characters or fewer.";
    }

    if (string.IsNullOrWhiteSpace(request.City))
    {
        return "City is required.";
    }

    return request.City.Trim().Length <= 120
        ? null
        : "City must contain 120 characters or fewer.";
}

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

    var shouldDispatchCommand = dispatchCommand &&
        (action != "lock" ||
            sessionId is null ||
            !await HasInFlightOrAcceptedLockCommandAsync(
                dbContext,
                request.DeviceId,
                sessionId.Value,
                cancellationToken));

    if (shouldDispatchCommand)
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

static async Task<bool> HasInFlightOrAcceptedLockCommandAsync(
    PlatformDbContext dbContext,
    Guid deviceId,
    Guid sessionId,
    CancellationToken cancellationToken)
{
    var commands = await dbContext.DeviceCommands
        .AsNoTracking()
        .Where(command =>
            command.DeviceId == deviceId &&
            command.Type == DeviceCommandTypeNames.Lock &&
            (command.Status == "Pending" ||
                command.Status == "Accepted" ||
                command.Status == "Completed"))
        .Select(command => command.PayloadJson)
        .ToListAsync(cancellationToken);

    return commands.Any(payloadJson => TryReadCommandSessionId(payloadJson) == sessionId);
}

static Guid? TryReadCommandSessionId(string payloadJson)
{
    try
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("sessionId", out var sessionIdElement) &&
            sessionIdElement.ValueKind == JsonValueKind.String &&
            Guid.TryParse(sessionIdElement.GetString(), out var sessionId)
                ? sessionId
                : null;
    }
    catch (JsonException)
    {
        return null;
    }
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

public sealed record PlayerScopedEndpointResult(
    PlayerAccountEntity? Player,
    Guid BranchId,
    StaffAuthorizationResult? Authorization,
    IResult? Result);

public sealed record ReservationScopedEndpointResult(
    ReservationEntity? Reservation,
    IResult? Result);

public sealed record ScopedEntityEndpointResult<TEntity>(
    TEntity? Entity,
    Guid BranchId,
    StaffAuthorizationResult? Authorization,
    IResult? Result)
    where TEntity : class;

public partial class Program;
