using System.Globalization;
using System.Net;
using System.Net.Sockets;
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
using AFK4.Platform.Api.Identity.OwnerCodes;
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
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

const string OperatorWebCorsPolicyName = "operator-web";
const string PlatformWebCorsPolicyName = "platform-web";
const string CombinedWebCorsPolicyName = "afk4-web";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

string[] ResolveCorsOrigins(string configurationKey, string[] defaults)
{
    var configured = builder.Configuration
        .GetSection(configurationKey)
        .Get<string[]>();
    if (configured is null || configured.Length == 0)
    {
        return defaults;
    }

    return defaults
        .Concat(configured)
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

var operatorWebOrigins = ResolveCorsOrigins(
    "Cors:OperatorWebOrigins",
    [
        "https://operator.afk4.local",
        "http://localhost:5174",
        "http://127.0.0.1:5174",
        "http://localhost:4174",
        "http://127.0.0.1:4174"
    ]);

var platformWebOrigins = ResolveCorsOrigins(
    "Cors:PlatformWebOrigins",
    [
        "https://platform.afk4.local",
        "http://localhost:5175",
        "http://127.0.0.1:5175",
        "http://localhost:4175",
        "http://127.0.0.1:4175"
    ]);

var combinedWebOrigins = operatorWebOrigins
    .Concat(platformWebOrigins)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        OperatorWebCorsPolicyName,
        policy => policy
            .WithOrigins(operatorWebOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    options.AddPolicy(
        PlatformWebCorsPolicyName,
        policy => policy
            .WithOrigins(platformWebOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    options.AddPolicy(
        CombinedWebCorsPolicyName,
        policy => policy
            .WithOrigins(combinedWebOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
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
builder.Services.AddScoped<IFloorMapEditService, EfFloorMapEditService>();
builder.Services.AddScoped<IStaffTokenService, OpaqueStaffTokenService>();
builder.Services.AddScoped<IStaffCredentialService, PasswordHashingStaffCredentialService>();
builder.Services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
builder.Services.AddScoped<StaffAuthorizationService>();
builder.Services.AddScoped<IPlatformAdminTokenService, OpaquePlatformAdminTokenService>();
builder.Services.AddScoped<IPlatformAdminCredentialService, PasswordHashingPlatformAdminCredentialService>();
builder.Services.AddScoped<IPlatformAdminContextAccessor, PlatformAdminContextAccessor>();
builder.Services.AddScoped<PlatformAdminAuthorizationService>();
builder.Services.Configure<PlatformAdminBootstrapOptions>(
    builder.Configuration.GetSection(PlatformAdminBootstrapOptions.ConfigurationSection));
builder.Services.AddHostedService<PlatformAdminBootstrapHostedService>();
builder.Services.Configure<PlatformTenantOptions>(
    builder.Configuration.GetSection(PlatformTenantOptions.ConfigurationSection));
builder.Services.AddSingleton<IOwnerInviteCodeGenerator, RandomOwnerInviteCodeGenerator>();
builder.Services.Configure<OwnerCodeOptions>(
    builder.Configuration.GetSection(OwnerCodeOptions.SectionName));
builder.Services.AddSingleton<IOwnerCodeGenerator, RandomOwnerCodeGenerator>();
builder.Services.AddSingleton<IOwnerCodeHasher, Sha256OwnerCodeHasher>();
builder.Services.AddScoped<IOwnerCodeService, OwnerCodeService>();
builder.Services.Configure<InstallOptions>(
    builder.Configuration.GetSection(InstallOptions.SectionName));
builder.Services.AddScoped<IInstallService, EfInstallService>();
builder.Services.AddSingleton<IInstallRequestThrottle, InMemoryInstallRequestThrottle>();
builder.Services.AddScoped<IPlatformTenantService, EfPlatformTenantService>();
builder.Services.AddScoped<IPlatformSupportNoteService, EfPlatformSupportNoteService>();
builder.Services.AddScoped<IPlatformIdempotencyStore, EfPlatformIdempotencyStore>();
builder.Services.AddScoped<IPlatformTenantHealthService, EfPlatformTenantHealthService>();
builder.Services.AddScoped<IPlanCatalogService, EfPlanCatalogService>();
builder.Services.AddScoped<ITenantSubscriptionService, EfTenantSubscriptionService>();
builder.Services.AddScoped<IOrganizationOwnerResolver, EfOrganizationOwnerResolver>();
builder.Services.AddScoped<IInvoiceNotifier, EfInvoiceNotifier>();
builder.Services.AddScoped<IInvoiceGenerationRunner, EfInvoiceGenerationRunner>();
builder.Services.AddScoped<IInvoiceService, EfInvoiceService>();
builder.Services.AddScoped<IBillingMetricsService, EfBillingMetricsService>();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.ConfigurationSection));
builder.Services.AddHostedService<BillingPlanSeedHostedService>();
builder.Services.AddHostedService<InvoiceGenerationHostedService>();
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.ConfigurationSection));
builder.Services.AddSingleton<INotificationRenderer, NotificationRenderer>();
builder.Services.AddSingleton<ITemplateProvider>(provider =>
    new EmbeddedTemplateProvider(provider.GetRequiredService<IOptions<NotificationOptions>>().Value.DefaultLocale));
builder.Services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
builder.Services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
builder.Services.AddScoped<INotificationOutbox, EfNotificationOutbox>();
builder.Services.AddScoped<INotificationPreferenceService, EfNotificationPreferenceService>();
builder.Services.AddScoped<NotificationDispatchRunner>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStaffPasswordResetService, EfStaffPasswordResetService>();
builder.Services.AddScoped<IStaffInviteService, EfStaffInviteService>();
builder.Services.AddScoped<IDailySummaryRunner, EfDailySummaryRunner>();
builder.Services.AddScoped<IScheduledReportRunner, EfScheduledReportRunner>();
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.ConfigurationSection));
builder.Services.AddScoped<IBillingOutbox, EfBillingOutbox>();
builder.Services.AddScoped<IOutboxMessageHandler, SessionCheckoutOutboxHandler>();
builder.Services.AddScoped<OutboxDispatchRunner>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<NotificationDispatcher>();
builder.Services.AddHostedService<DailySummaryHostedService>();
builder.Services.AddHostedService<AutoProtectionHostedService>();
builder.Services.AddHostedService<ScheduledReportHostedService>();
builder.Services.AddScoped<IOperatorConnectionResolver, EfOperatorConnectionResolver>();
builder.Services.AddScoped<ITenantStatusGuard, EfTenantStatusGuard>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();
builder.Services.AddScoped<IAuditSearchService, EfAuditSearchService>();
builder.Services.AddSingleton(new BranchDiagnosticsOptions());
builder.Services.AddScoped<IBranchDiagnosticsService, EfBranchDiagnosticsService>();
builder.Services.AddScoped<IShiftDiscrepancyNotifier, EfShiftDiscrepancyNotifier>();
builder.Services.AddScoped<EfShiftService>();
builder.Services.AddScoped<IShiftService>(provider => provider.GetRequiredService<EfShiftService>());
builder.Services.AddScoped<IOpenShiftResolver>(provider => provider.GetRequiredService<EfShiftService>());
builder.Services.AddScoped<ILowStockNotifier, EfLowStockNotifier>();
builder.Services.AddScoped<IInventoryService, EfInventoryService>();
builder.Services.AddScoped<IPosService, EfPosService>();
builder.Services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
builder.Services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
builder.Services.AddScoped<IReportService, EfReportService>();
builder.Services.AddScoped<IReportScheduleService, EfReportScheduleService>();
builder.Services.AddScoped<IOperatorDashboardService, EfOperatorDashboardService>();
builder.Services.AddScoped<IReservationService, EfReservationService>();
builder.Services.Configure<SessionLeaseOptions>(builder.Configuration.GetSection("Sessions"));
builder.Services.AddScoped<ISessionLeaseSigner, EcdsaSessionLeaseSigner>();
builder.Services.AddScoped<IHeartbeatSessionCommandPlanner, EfHeartbeatSessionCommandPlanner>();
builder.Services.AddScoped<ISessionCommandService, EfSessionCommandService>();
builder.Services.AddScoped<ISessionCheckoutService, EfSessionCheckoutService>();
builder.Services.AddSingleton(new AutoProtectionOptions());
builder.Services.AddScoped<AutoProtectionRunner>();
builder.Services.AddScoped<ISessionCommandResultProcessor, EfSessionCommandResultProcessor>();
builder.Services.AddScoped<IBillingCommandService, EfBillingCommandService>();
builder.Services.AddScoped<IMoneyActionPolicyResolver, EfMoneyActionPolicyResolver>();
builder.Services.AddScoped<IMoneyActionExecutor, EfMoneyActionExecutor>();
builder.Services.AddScoped<IMoneyActionApprovalService, MoneyActionApprovalService>();
builder.Services.AddScoped<ITariffService, EfTariffService>();
builder.Services.AddScoped<IPackageService, EfPackageService>();
builder.Services.AddScoped<ISessionBillingService, SessionBillingService>();
builder.Services.AddScoped<IOperatorReferenceDataService, EfOperatorReferenceDataService>();
builder.Services.AddScoped<IUpdateService, EfUpdateService>();

var app = builder.Build();

// Fail fast at startup if any registered notification template key is missing its file (§8),
// rather than discovering it when a send is attempted at runtime.
app.Services.GetRequiredService<ITemplateProvider>().EnsureKeysPresent(NotificationTemplateKeys.All);

// Single UseCors call so the CORS middleware emits Access-Control-Allow-*
// headers on preflight OPTIONS as well as the mainline request. The combined
// policy unions OperatorWebOrigins and PlatformWebOrigins so both SPAs share
// one preflight handler; the per-SPA named policies remain registered for
// endpoint-scoped RequireCors usage if we ever need to split them again.
app.UseCors(CombinedWebCorsPolicyName);
app.Use(async (httpContext, next) =>
{
    if (httpContext.Request.Path.StartsWithSegments("/api/install") &&
        HttpMethods.IsPost(httpContext.Request.Method))
    {
        var sourceIp = GetSourceIp(httpContext);
        var throttle = httpContext.RequestServices.GetRequiredService<IInstallRequestThrottle>();
        var decision = await throttle.ApplyAsync(sourceIp, httpContext.RequestAborted);
        if (decision.IsRejected)
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(decision.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await httpContext.Response.WriteAsJsonAsync(
                new { Error = "Too many install requests from this source IP." },
                httpContext.RequestAborted);
            return;
        }
    }

    await next(httpContext);
});
app.UseMiddleware<StaffAuthenticationMiddleware>();
app.UseMiddleware<PlatformAdminAuthenticationMiddleware>();
app.UseMiddleware<TenantSuspensionMiddleware>();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow));
});

app.MapGet("/api/branches/{branchId:guid}/floor-map", async (
    Guid branchId,
    HttpContext httpContext,
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

    var result = await floorMapReadService.GetFloorMapAsync(branchId, cancellationToken);

    if (result is null)
    {
        return Results.NotFound();
    }

    httpContext.Response.Headers.ETag = result.ETag;
    return Results.Ok(result.FloorMap);
});

app.MapPut("/api/branches/{branchId:guid}/floor-map", async (
    Guid branchId,
    FloorMapBulkUpdateRequest request,
    HttpContext httpContext,
    IFloorMapEditService floorMapEditService,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
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
            AuditActionNames.UpdateFloorMap,
            "FloorMap",
            branchId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
    var result = await floorMapEditService.BulkUpdateAsync(
        request.OrganizationId,
        branchId,
        ifMatch,
        request,
        cancellationToken);

    switch (result.Status)
    {
        case FloorMapBulkUpdateStatus.PreconditionRequired:
            return Results.Json(new { Error = result.Error }, statusCode: StatusCodes.Status428PreconditionRequired);
        case FloorMapBulkUpdateStatus.PreconditionFailed:
            if (!string.IsNullOrEmpty(result.CurrentETag))
            {
                httpContext.Response.Headers.ETag = result.CurrentETag;
            }
            return Results.Json(new { Error = result.Error }, statusCode: StatusCodes.Status412PreconditionFailed);
        case FloorMapBulkUpdateStatus.BadRequest:
            return Results.BadRequest(new { Error = result.Error });
        case FloorMapBulkUpdateStatus.Conflict:
            return Results.Conflict(new { Error = result.Error });
        case FloorMapBulkUpdateStatus.NotFound:
            return Results.NotFound();
        case FloorMapBulkUpdateStatus.Success:
            httpContext.Response.Headers.ETag = result.Response!.ETag;
            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateFloorMap,
                "FloorMap",
                branchId.ToString("D"),
                AuditOutcome.Succeeded,
                new { ZoneCount = result.Response.Zones.Count, SeatCount = result.Response.Seats.Count },
                cancellationToken);
            return Results.Ok(result.Response);
        default:
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/branches/{branchId:guid}/settings", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageBranchSettings,
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
        branch.RequireManualDeviceApproval);

    return Results.Ok(response);
});

app.MapPut("/api/branches/{branchId:guid}/settings", async (
    Guid branchId,
    UpdateBranchSettingsRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ManageBranchSettings,
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

    var branch = await dbContext.Branches
        .SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == request.OrganizationId && candidate.BranchId == branchId,
            cancellationToken);

    if (branch is null)
    {
        return Results.NotFound();
    }

    var changed = branch.RequireManualDeviceApproval != request.RequireManualDeviceApproval;
    if (changed)
    {
        branch.RequireManualDeviceApproval = request.RequireManualDeviceApproval;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    var response = new BranchSettingsDto(
        branch.OrganizationId,
        branch.BranchId,
        branch.RequireManualDeviceApproval);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateBranchSettings,
        "BranchSettings",
        branchId.ToString("D"),
        AuditOutcome.Succeeded,
        new { branch.RequireManualDeviceApproval, Changed = changed },
        cancellationToken);

    return Results.Ok(response);
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

app.MapPost("/api/auth/staff/sign-in-by-tenant-key", async (
    StaffSignInByTenantKeyRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var response = await credentialService.SignInByTenantKeyAsync(request, cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

app.MapPost("/api/auth/staff/sign-in-by-login", async (
    StaffSignInByLoginRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var resolution = await credentialService.SignInByLoginAsync(request, cancellationToken);

    if (resolution.SignedIn is not null)
    {
        return Results.Ok(resolution.SignedIn);
    }

    return resolution.Clubs.Count > 0
        ? Results.Json(
            new StaffSignInChooseClubResponse(resolution.Clubs),
            statusCode: StatusCodes.Status409Conflict)
        : Results.Unauthorized();
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

app.MapPost("/api/auth/staff/forgot-password", async (
    StaffForgotPasswordRequest request,
    IStaffPasswordResetService passwordResetService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.UserNameOrEmail))
    {
        return Results.BadRequest(new { error = "UserNameOrEmail is required." });
    }

    // Anti-enumeration: always report acceptance regardless of whether the account exists.
    await passwordResetService.RequestResetAsync(request.UserNameOrEmail, cancellationToken);
    return Results.Ok(new { message = "If the account exists, a reset email has been sent." });
});

app.MapPost("/api/auth/staff/reset-password", async (
    StaffResetPasswordRequest request,
    IStaffPasswordResetService passwordResetService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return Results.BadRequest(new { error = "Token is required." });
    }

    var passwordValidation = ValidateStaffPassword(request.NewPassword);
    if (passwordValidation is not null)
    {
        return Results.BadRequest(new { error = passwordValidation });
    }

    var reset = await passwordResetService.CompleteResetAsync(request.Token, request.NewPassword, cancellationToken);
    return reset
        ? Results.Ok(new { message = "Password updated." })
        : Results.BadRequest(new { error = "The reset link is invalid or has expired." });
});

app.MapPost("/api/branches/{branchId:guid}/staff/invites", async (
    Guid branchId,
    CreateStaffInviteRequest request,
    StaffAuthorizationService authorizationService,
    IStaffInviteService staffInviteService,
    IAuditRecordWriter auditRecordWriter,
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
            AuditActionNames.CreateStaffInvite,
            "StaffInvite",
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

    var validation = ValidateCreateStaffInviteRequest(request);
    if (validation is not null)
    {
        return Results.BadRequest(new { Error = validation });
    }

    var result = await staffInviteService.CreateInviteAsync(
        request.OrganizationId,
        branchId,
        request.UserName,
        request.DisplayName,
        request.Email,
        request.RoleNames,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            request.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.CreateStaffInvite,
            "StaffInvite",
            null,
            AuditOutcome.Denied,
            new { request.UserName, Error = result.Error },
            cancellationToken);

        return Results.BadRequest(new { Error = result.Error });
    }

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateStaffInvite,
        "StaffInvite",
        result.StaffInviteId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.UserName, request.RoleNames },
        cancellationToken);

    return Results.Ok(new StaffInviteDto(result.StaffInviteId, result.Code, result.ExpiresAtUtc));
});

app.MapPost("/api/staff/invites/accept", async (
    AcceptStaffInviteRequest request,
    IStaffInviteService staffInviteService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return Results.BadRequest(new { error = "Token is required." });
    }

    var passwordValidation = ValidateStaffPassword(request.Password);
    if (passwordValidation is not null)
    {
        return Results.BadRequest(new { error = passwordValidation });
    }

    var result = await staffInviteService.AcceptInviteAsync(request.Token, request.Password, cancellationToken);
    return result.Succeeded
        ? Results.Ok(new AcceptStaffInviteResponse(result.OrganizationId, result.UserName))
        : Results.BadRequest(new { error = result.Error });
});

app.MapPost("/api/branches/{branchId:guid}/report-schedules", async (
    Guid branchId,
    CreateReportScheduleRequest request,
    StaffAuthorizationService authorizationService,
    IReportScheduleService reportScheduleService,
    IAuditRecordWriter auditRecordWriter,
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
            AuditActionNames.CreateReportSchedule,
            "ReportSchedule",
            null,
            AuditOutcome.Denied,
            new { request.ReportType, request.Frequency, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateCreateReportScheduleRequest(request);
    if (validation is not null)
    {
        return Results.BadRequest(new { Error = validation });
    }

    var dto = await reportScheduleService.CreateAsync(
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        request.ReportType,
        request.Frequency,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.CreateReportSchedule,
        "ReportSchedule",
        dto.ReportScheduleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.ReportType, request.Frequency },
        cancellationToken);

    return Results.Ok(dto);
});

app.MapGet("/api/branches/{branchId:guid}/report-schedules", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IReportScheduleService reportScheduleService,
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
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var schedules = await reportScheduleService.ListAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        cancellationToken);

    return Results.Ok(schedules);
});

app.MapDelete("/api/branches/{branchId:guid}/report-schedules/{scheduleId:guid}", async (
    Guid branchId,
    Guid scheduleId,
    StaffAuthorizationService authorizationService,
    IReportScheduleService reportScheduleService,
    IAuditRecordWriter auditRecordWriter,
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
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var deleted = await reportScheduleService.DeleteAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        scheduleId,
        cancellationToken);

    if (!deleted)
    {
        return Results.NotFound();
    }

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.DeleteReportSchedule,
        "ReportSchedule",
        scheduleId.ToString("D"),
        AuditOutcome.Succeeded,
        new { scheduleId },
        cancellationToken);

    return Results.Ok(new { message = "Report schedule deleted." });
});

app.MapGet("/api/staff/me/owner-code", async (
    StaffAuthorizationService authorizationService,
    IOwnerCodeService ownerCodeService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var summary = await ownerCodeService.GetActiveSummaryAsync(
        authorization.StaffContext!.StaffUserId,
        cancellationToken);

    if (summary is null)
    {
        return Results.NoContent();
    }

    return Results.Ok(new OwnerCodeSummaryResponse(
        summary.CodeSuffix,
        summary.ExpiresAtUtc,
        summary.LastUsedAtUtc,
        summary.FailedAttemptCount));
});

app.MapPost("/api/staff/me/owner-code/generate", async (
    StaffAuthorizationService authorizationService,
    IOwnerCodeService ownerCodeService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WriteOwnerCodeAuditAsync(
            auditRecordWriter,
            authorization.StaffContext?.OrganizationId,
            authorization.StaffContext?.StaffUserId,
            AuditActionNames.GenerateOwnerCode,
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    var result = await ownerCodeService.GenerateAsync(staffContext.StaffUserId, cancellationToken);

    if (!result.Succeeded)
    {
        await WriteOwnerCodeAuditAsync(
            auditRecordWriter,
            staffContext.OrganizationId,
            staffContext.StaffUserId,
            AuditActionNames.GenerateOwnerCode,
            null,
            AuditOutcome.Denied,
            new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            OwnerCodeOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            OwnerCodeOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var issued = result.Value!;
    await WriteOwnerCodeAuditAsync(
        auditRecordWriter,
        staffContext.OrganizationId,
        staffContext.StaffUserId,
        AuditActionNames.GenerateOwnerCode,
        issued.CodeSuffix,
        AuditOutcome.Succeeded,
        new { issued.CodeSuffix, issued.ExpiresAtUtc },
        cancellationToken);

    return Results.Ok(new OwnerCodeIssuedResponse(
        issued.PlaintextCode,
        issued.CodeSuffix,
        issued.ExpiresAtUtc));
});

app.MapPost("/api/staff/me/owner-code/rotate", async (
    RotateOwnerCodeRequest request,
    StaffAuthorizationService authorizationService,
    IOwnerCodeService ownerCodeService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WriteOwnerCodeAuditAsync(
            auditRecordWriter,
            authorization.StaffContext?.OrganizationId,
            authorization.StaffContext?.StaffUserId,
            AuditActionNames.RotateOwnerCode,
            null,
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    var result = await ownerCodeService.RotateAsync(
        staffContext.StaffUserId,
        request.Reason,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WriteOwnerCodeAuditAsync(
            auditRecordWriter,
            staffContext.OrganizationId,
            staffContext.StaffUserId,
            AuditActionNames.RotateOwnerCode,
            null,
            AuditOutcome.Denied,
            new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            OwnerCodeOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            OwnerCodeOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var issued = result.Value!;
    await WriteOwnerCodeAuditAsync(
        auditRecordWriter,
        staffContext.OrganizationId,
        staffContext.StaffUserId,
        AuditActionNames.RotateOwnerCode,
        issued.CodeSuffix,
        AuditOutcome.Succeeded,
        new { issued.CodeSuffix, issued.ExpiresAtUtc, request.Reason },
        cancellationToken);

    return Results.Ok(new OwnerCodeIssuedResponse(
        issued.PlaintextCode,
        issued.CodeSuffix,
        issued.ExpiresAtUtc));
});

app.MapPost("/api/platform/auth/sign-in", async (
    PlatformAdminSignInRequest request,
    IPlatformAdminCredentialService credentialService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var response = await credentialService.SignInAsync(request, cancellationToken);

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        OrganizationId: Guid.Empty,
        BranchId: null,
        ActorStaffUserId: null,
        Action: AuditActionNames.PlatformAdminSignIn,
        TargetType: "PlatformAdminUser",
        TargetId: response?.PlatformAdminId.ToString("D") ?? request.UserName,
        Outcome: response is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
        SourceApp: "PlatformApi",
        DetailsJson: JsonSerializer.Serialize(new { request.UserName })),
        cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

app.MapPost("/api/platform/auth/refresh", async (
    PlatformAdminRefreshTokenRequest request,
    IPlatformAdminTokenService tokenService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var response = await tokenService.RefreshAsync(request, cancellationToken);

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        OrganizationId: Guid.Empty,
        BranchId: null,
        ActorStaffUserId: null,
        Action: AuditActionNames.PlatformAdminRefresh,
        TargetType: "PlatformAdminUser",
        TargetId: response?.PlatformAdminId.ToString("D"),
        Outcome: response is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
        SourceApp: "PlatformApi",
        DetailsJson: "{}"),
        cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

app.MapPost("/api/platform/auth/sign-out", async (
    PlatformAdminSignOutRequest request,
    IPlatformAdminTokenService tokenService,
    PlatformAdminAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireAuthenticated();
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    var revoked = await tokenService.RevokeAsync(request, cancellationToken);

    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        OrganizationId: Guid.Empty,
        BranchId: null,
        ActorStaffUserId: null,
        Action: AuditActionNames.PlatformAdminSignOut,
        TargetType: "PlatformAdminUser",
        TargetId: authorization.PlatformAdminContext!.PlatformAdminUserId.ToString("D"),
        Outcome: revoked ? AuditOutcome.Succeeded : AuditOutcome.Denied,
        SourceApp: "PlatformApi",
        DetailsJson: "{}"),
        cancellationToken);

    return revoked ? Results.NoContent() : Results.Unauthorized();
});

app.MapPost("/api/platform/tenants", async (
    CreateTenantRequest request,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.CreateTenant);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.CreateTenant,
            targetType: "Tenant",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { request.OrganizationSlug, request.BranchSlug, authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(request);
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        if (idempotencyKey.Length > 128)
        {
            return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
        }

        var prior = await idempotencyStore.TryReadAsync(
            scope: "platform.tenants.create",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            cancellationToken);
        if (prior.RequestHashMismatch)
        {
            return Results.Json(
                new { Error = "Idempotency-Key was reused with a different request body." },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await tenantService.CreateAsync(
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.CreateTenant,
            targetType: "Tenant",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { request.OrganizationSlug, request.BranchSlug, Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var created = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: created.Tenant.OrganizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.CreateTenant,
        targetType: "Tenant",
        targetId: created.Tenant.OrganizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            created.Tenant.Slug,
            BranchSlug = created.Tenant.Branches.First().Slug,
            created.Tenant.PlanCode,
            created.Tenant.SubscriptionStatus,
            OwnerInviteId = created.OwnerInvite.OwnerInviteId
        },
        cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = JsonSerializer.Serialize(created, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync(
            scope: "platform.tenants.create",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            statusCode: StatusCodes.Status200OK,
            responseBody: responseBody,
            retention: TimeSpan.FromHours(24),
            cancellationToken);
    }

    return Results.Ok(created);
});

app.MapGet("/api/platform/tenants", async (
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenants);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenant,
            targetType: "Tenant",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var summaries = await tenantService.ListAsync(cancellationToken);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: Guid.Empty,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ViewTenant,
        targetType: "Tenant",
        targetId: null,
        outcome: AuditOutcome.Succeeded,
        details: new { Count = summaries.Count },
        cancellationToken);

    return Results.Ok(summaries);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenants);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenant,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var detail = await tenantService.GetAsync(organizationId, cancellationToken);
    if (detail is null)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenant,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = "Tenant was not found." },
            cancellationToken);
        return Results.NotFound(new { Error = "Tenant was not found." });
    }

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ViewTenant,
        targetType: "Tenant",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { detail.Slug, BranchCount = detail.Branches.Count },
        cancellationToken);

    return Results.Ok(detail);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}/owner-invites", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewOwnerInvites,
            targetType: "OwnerInvite",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await tenantService.ListOwnerInvitesAsync(organizationId, cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewOwnerInvites,
            targetType: "OwnerInvite",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var invites = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ViewOwnerInvites,
        targetType: "OwnerInvite",
        targetId: null,
        outcome: AuditOutcome.Succeeded,
        details: new { Count = invites.Count },
        cancellationToken);

    return Results.Ok(invites);
});

app.MapPost("/api/platform/tenants/{organizationId:guid}/owner-invites", async (
    Guid organizationId,
    CreateOwnerInviteRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.CreateOwnerInvite,
            targetType: "OwnerInvite",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { request.BranchId, authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await tenantService.CreateOrRotateOwnerInviteAsync(
        organizationId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.CreateOwnerInvite,
            targetType: "OwnerInvite",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { request.BranchId, Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var invite = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.CreateOwnerInvite,
        targetType: "OwnerInvite",
        targetId: invite.OwnerInviteId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            invite.BranchId,
            invite.OwnerUserName,
            invite.ExpiresAtUtc
        },
        cancellationToken);

    return Results.Ok(invite);
});

app.MapPost("/api/platform/owner-invites/{ownerInviteId:guid}/resend", async (
    Guid ownerInviteId,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    var organizationId = await dbContext.OwnerInvites
        .AsNoTracking()
        .Where(invite => invite.OwnerInviteId == ownerInviteId)
        .Select(invite => (Guid?)invite.OrganizationId)
        .SingleOrDefaultAsync(cancellationToken) ?? Guid.Empty;

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ResendOwnerInvite,
            targetType: "OwnerInvite",
            targetId: ownerInviteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await tenantService.ResendOwnerInviteAsync(ownerInviteId, cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ResendOwnerInvite,
            targetType: "OwnerInvite",
            targetId: ownerInviteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ResendOwnerInvite,
        targetType: "OwnerInvite",
        targetId: ownerInviteId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value!.BranchId },
        cancellationToken);

    return Results.Ok(result.Value);
});

app.MapPost("/api/platform/owner-invites/{ownerInviteId:guid}/revoke", async (
    Guid ownerInviteId,
    RevokeOwnerInviteRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    var orgIdForAudit = await dbContext.OwnerInvites
        .AsNoTracking()
        .Where(invite => invite.OwnerInviteId == ownerInviteId)
        .Select(invite => (Guid?)invite.OrganizationId)
        .SingleOrDefaultAsync(cancellationToken) ?? Guid.Empty;

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: orgIdForAudit,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.RevokeOwnerInvite,
            targetType: "OwnerInvite",
            targetId: ownerInviteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await tenantService.RevokeOwnerInviteAsync(
        ownerInviteId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: orgIdForAudit,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.RevokeOwnerInvite,
            targetType: "OwnerInvite",
            targetId: ownerInviteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var invite = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: invite.OrganizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.RevokeOwnerInvite,
        targetType: "OwnerInvite",
        targetId: invite.OwnerInviteId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { invite.OwnerInviteId, Reason = request.Reason },
        cancellationToken);

    return Results.Ok(invite);
});

app.MapPost("/api/platform/owner-invites/accept", async (
    AcceptOwnerInviteRequest request,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var result = await tenantService.AcceptOwnerInviteAsync(request, cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: null,
            action: AuditActionNames.AcceptOwnerInvite,
            targetType: "OwnerInvite",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { request.UserName, Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var signIn = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: signIn.OrganizationId,
        actorPlatformAdminUserId: null,
        action: AuditActionNames.AcceptOwnerInvite,
        targetType: "StaffUser",
        targetId: signIn.StaffUserId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { signIn.DisplayName, request.UserName },
        cancellationToken);

    return Results.Ok(signIn);
});

app.MapPost("/api/operator-connections/resolve", async (
    ResolveOperatorConnectionRequest request,
    IOperatorConnectionResolver resolver,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var result = await resolver.ResolveAsync(request, cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: null,
            action: AuditActionNames.ResolveOperatorConnection,
            targetType: "OperatorConnection",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new
            {
                HasSlugPair = !string.IsNullOrWhiteSpace(request.OrganizationSlug)
                    || !string.IsNullOrWhiteSpace(request.BranchSlug),
                HasSetupCode = !string.IsNullOrWhiteSpace(request.SetupCode),
                Error = result.Error
            },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var resolution = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: resolution.OrganizationId,
        actorPlatformAdminUserId: null,
        action: AuditActionNames.ResolveOperatorConnection,
        targetType: "OperatorConnection",
        targetId: resolution.BranchId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            resolution.Source,
            resolution.OrganizationSlug,
            resolution.BranchSlug,
            resolution.OrganizationStatus
        },
        cancellationToken);

    return Results.Ok(resolution);
});

app.MapPatch("/api/platform/tenants/{organizationId:guid}/status", async (
    Guid organizationId,
    UpdateTenantStatusRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UpdateTenantStatus);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantStatus,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { request.Status, authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var previousStatus = await dbContext.Organizations
        .AsNoTracking()
        .Where(org => org.OrganizationId == organizationId)
        .Select(org => new { org.Status, org.StatusReason })
        .SingleOrDefaultAsync(cancellationToken);

    var result = await tenantService.UpdateStatusAsync(
        organizationId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantStatus,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { request.Status, Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var detail = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.UpdateTenantStatus,
        targetType: "Tenant",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            PreviousStatus = previousStatus?.Status,
            PreviousReason = previousStatus?.StatusReason,
            NewStatus = detail.Status,
            NewReason = detail.StatusReason
        },
        cancellationToken);

    return Results.Ok(detail);
});

app.MapPatch("/api/platform/tenants/{organizationId:guid}/limits", async (
    Guid organizationId,
    UpdateTenantLimitsRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantService tenantService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UpdateTenantLimits);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantLimits,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await tenantService.UpdateLimitsAsync(
        organizationId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantLimits,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);

        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var detail = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.UpdateTenantLimits,
        targetType: "Tenant",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            detail.Limits.MaxBranches,
            detail.Limits.MaxDevicesPerBranch,
            detail.Limits.MaxConcurrentSessions,
            detail.Limits.MaxStaffUsersPerBranch
        },
        cancellationToken);

    return Results.Ok(detail);
});

app.MapGet("/api/platform/plans", async (
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    IAuditRecordWriter auditRecordWriter,
    bool? includeInactive,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "SubscriptionPlan",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var plans = await planCatalogService.ListAsync(includeInactive ?? true, cancellationToken);
    return Results.Ok(plans);
});

app.MapPost("/api/platform/plans", async (
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    IAuditRecordWriter auditRecordWriter,
    CreatePlanRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.CreatePlan,
            targetType: "SubscriptionPlan",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await planCatalogService.CreateAsync(request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: Guid.Empty,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.CreatePlan,
        targetType: "SubscriptionPlan",
        targetId: result.Value!.PlanCode,
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value.PlanCode, result.Value.PriceMinorUnits, result.Value.BillingInterval },
        cancellationToken);
    return Results.Ok(result.Value);
});

app.MapPatch("/api/platform/plans/{planCode}", async (
    string planCode,
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    IAuditRecordWriter auditRecordWriter,
    UpdatePlanRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.UpdatePlan,
            targetType: "SubscriptionPlan",
            targetId: planCode,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await planCatalogService.UpdateAsync(planCode, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: Guid.Empty,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.UpdatePlan,
        targetType: "SubscriptionPlan",
        targetId: planCode,
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value!.PlanCode, result.Value.PriceMinorUnits, result.Value.IsActive },
        cancellationToken);
    return Results.Ok(result.Value);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}/subscription", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "TenantSubscription",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await subscriptionService.GetAsync(organizationId, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapPatch("/api/platform/tenants/{organizationId:guid}/subscription", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    IAuditRecordWriter auditRecordWriter,
    UpdateSubscriptionRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageSubscriptions);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.UpdateSubscription,
            targetType: "TenantSubscription",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await subscriptionService.UpdateAsync(organizationId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.UpdateSubscription,
        targetType: "TenantSubscription",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value!.PlanCode, result.Value.Status, result.Value.CancelAtPeriodEnd },
        cancellationToken);
    return Results.Ok(result.Value);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}/invoices", async (
    Guid organizationId,
    string? status,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "Invoice",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await invoiceService.ListForTenantAsync(organizationId, status, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

// --- Club-side (owner) read-only billing (SP3 Plan 7) ---
app.MapGet("/api/organizations/{organizationId:guid}/subscription", async (
    Guid organizationId,
    StaffAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewSubscription);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (organizationId != authorization.StaffContext!.OrganizationId)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await subscriptionService.GetAsync(authorization.StaffContext!.OrganizationId, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapGet("/api/organizations/{organizationId:guid}/invoices", async (
    Guid organizationId,
    StaffAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewSubscription);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (organizationId != authorization.StaffContext!.OrganizationId)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await invoiceService.ListForTenantAsync(authorization.StaffContext!.OrganizationId, status: null, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapGet("/api/platform/subscriptions", async (
    string? status,
    string? planCode,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "TenantSubscription",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await subscriptionService.ListAsync(status, planCode, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapGet("/api/platform/invoices", async (
    string? status,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "Invoice",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await invoiceService.ListAllAsync(status, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapGet("/api/platform/metrics", async (
    PlatformAdminAuthorizationService authorizationService,
    IBillingMetricsService metricsService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "BillingMetrics",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var metrics = await metricsService.GetAsync(cancellationToken);
    return Results.Ok(metrics);
});

app.MapPost("/api/platform/tenants/{organizationId:guid}/invoices/generate", async (
    Guid organizationId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.GenerateInvoice,
            targetType: "Invoice",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { organizationId });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        if (idempotencyKey.Length > 128)
        {
            return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
        }

        var prior = await idempotencyStore.TryReadAsync(
            scope: "platform.invoices.generate",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.GenerateAsync(organizationId, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.GenerateInvoice,
        targetType: "Invoice",
        targetId: result.Value!.InvoiceId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value.Number, result.Value.AmountMinorUnits },
        cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync(
            scope: "platform.invoices.generate",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            statusCode: StatusCodes.Status200OK,
            responseBody: responseBody,
            retention: TimeSpan.FromHours(24),
            cancellationToken);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/platform/invoices/{invoiceId:guid}/mark-paid", async (
    Guid invoiceId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    MarkInvoicePaidRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.MarkInvoicePaid,
            targetType: "Invoice",
            targetId: invoiceId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        if (idempotencyKey.Length > 128)
        {
            return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
        }

        var prior = await idempotencyStore.TryReadAsync(
            scope: "platform.invoices.mark_paid",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.MarkPaidAsync(invoiceId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: result.Value!.OrganizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.MarkInvoicePaid,
        targetType: "Invoice",
        targetId: invoiceId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value.Number, request.Reference },
        cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync(
            scope: "platform.invoices.mark_paid",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            statusCode: StatusCodes.Status200OK,
            responseBody: responseBody,
            retention: TimeSpan.FromHours(24),
            cancellationToken);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/platform/invoices/{invoiceId:guid}/void", async (
    Guid invoiceId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    VoidInvoiceRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.VoidInvoice,
            targetType: "Invoice",
            targetId: invoiceId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        if (idempotencyKey.Length > 128)
        {
            return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
        }

        var prior = await idempotencyStore.TryReadAsync(
            scope: "platform.invoices.void",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.VoidAsync(invoiceId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: result.Value!.OrganizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.VoidInvoice,
        targetType: "Invoice",
        targetId: invoiceId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value.Number, request.Reason },
        cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync(
            scope: "platform.invoices.void",
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            statusCode: StatusCodes.Status200OK,
            responseBody: responseBody,
            retention: TimeSpan.FromHours(24),
            cancellationToken);
    }

    return Results.Ok(result.Value);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}/health", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformTenantHealthService healthService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenantHealth);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenantHealth,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var health = await healthService.GetAsync(organizationId, cancellationToken);
    if (health is null)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenantHealth,
            targetType: "Tenant",
            targetId: organizationId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = "Tenant was not found." },
            cancellationToken);
        return Results.NotFound(new { Error = "Tenant was not found." });
    }

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ViewTenantHealth,
        targetType: "Tenant",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new
        {
            health.BranchCount,
            health.DeviceCount,
            health.ActiveStaffUserCount,
            health.RecentErrorCount
        },
        cancellationToken);

    return Results.Ok(health);
});

app.MapGet("/api/platform/tenants/{organizationId:guid}/support-notes", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformSupportNoteService supportNoteService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenantSupportNotes);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenantSupportNotes,
            targetType: "TenantSupportNote",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await supportNoteService.ListAsync(organizationId, cancellationToken);
    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewTenantSupportNotes,
            targetType: "TenantSupportNote",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);
        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var notes = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.ViewTenantSupportNotes,
        targetType: "TenantSupportNote",
        targetId: null,
        outcome: AuditOutcome.Succeeded,
        details: new { Count = notes.Count },
        cancellationToken);

    return Results.Ok(notes);
});

app.MapPost("/api/platform/tenants/{organizationId:guid}/support-notes", async (
    Guid organizationId,
    CreateTenantSupportNoteRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformSupportNoteService supportNoteService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageTenantSupportNotes);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.CreateTenantSupportNote,
            targetType: "TenantSupportNote",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await supportNoteService.CreateAsync(
        organizationId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.CreateTenantSupportNote,
            targetType: "TenantSupportNote",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);
        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var note = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.CreateTenantSupportNote,
        targetType: "TenantSupportNote",
        targetId: note.TenantSupportNoteId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { note.TenantSupportNoteId, BodyLength = note.Body.Length },
        cancellationToken);

    return Results.Ok(note);
});

app.MapPatch("/api/platform/tenants/{organizationId:guid}/support-notes/{tenantSupportNoteId:guid}", async (
    Guid organizationId,
    Guid tenantSupportNoteId,
    UpdateTenantSupportNoteRequest request,
    PlatformAdminAuthorizationService authorizationService,
    IPlatformSupportNoteService supportNoteService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageTenantSupportNotes);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantSupportNote,
            targetType: "TenantSupportNote",
            targetId: tenantSupportNoteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await supportNoteService.UpdateAsync(
        organizationId,
        tenantSupportNoteId,
        request,
        authorization.PlatformAdminContext!.PlatformAdminUserId,
        cancellationToken);

    if (!result.Succeeded)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
            action: AuditActionNames.UpdateTenantSupportNote,
            targetType: "TenantSupportNote",
            targetId: tenantSupportNoteId.ToString("D"),
            outcome: AuditOutcome.Denied,
            details: new { Error = result.Error },
            cancellationToken);
        return result.Status switch
        {
            PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error })
        };
    }

    var note = result.Value!;
    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
        action: AuditActionNames.UpdateTenantSupportNote,
        targetType: "TenantSupportNote",
        targetId: note.TenantSupportNoteId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { note.TenantSupportNoteId, BodyLength = note.Body.Length },
        cancellationToken);

    return Results.Ok(note);
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

app.MapPatch("/api/branches/{branchId:guid}/staff/{staffUserId:guid}/profile", async (
    Guid branchId,
    Guid staffUserId,
    UpdateStaffUserProfileRequest request,
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
            AuditActionNames.UpdateStaffProfile,
            "StaffUser",
            staffUserId.ToString("D"),
            AuditOutcome.Denied,
            new { request.UserName, request.DisplayName, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateUpdateStaffUserProfileRequest(request);
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

    var roleNames = await dbContext.StaffRoleAssignments
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == request.OrganizationId &&
            roleAssignment.BranchId == branchId &&
            roleAssignment.StaffUserId == staffUserId)
        .Select(roleAssignment => roleAssignment.RoleName)
        .OrderBy(roleName => roleName)
        .ToListAsync(cancellationToken);

    if (roleNames.Count == 0)
    {
        return Results.NotFound();
    }

    var userName = request.UserName.Trim();
    var normalizedUserName = userName.ToUpperInvariant();
    var displayName = request.DisplayName.Trim();
    var duplicateUserNameExists = await dbContext.StaffUsers
        .AnyAsync(
            candidate =>
                candidate.OrganizationId == request.OrganizationId &&
                candidate.StaffUserId != staffUserId &&
                candidate.NormalizedUserName == normalizedUserName,
            cancellationToken);

    if (duplicateUserNameExists)
    {
        return Results.Conflict(new { Error = "Staff user name already exists in the organization." });
    }

    var previousUserName = staffUser.UserName;
    var previousDisplayName = staffUser.DisplayName;
    staffUser.UserName = userName;
    staffUser.NormalizedUserName = normalizedUserName;
    staffUser.DisplayName = displayName;

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToStaffUserDto(staffUser, roleNames);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateStaffProfile,
        "StaffUser",
        staffUserId.ToString("D"),
        AuditOutcome.Succeeded,
        new { PreviousUserName = previousUserName, PreviousDisplayName = previousDisplayName, response.UserName, response.DisplayName },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPatch("/api/branches/{branchId:guid}/staff/{staffUserId:guid}/state", async (
    Guid branchId,
    Guid staffUserId,
    UpdateStaffUserStateRequest request,
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
            AuditActionNames.UpdateStaffState,
            "StaffUser",
            staffUserId.ToString("D"),
            AuditOutcome.Denied,
            new { request.IsActive, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    if (!request.IsActive && staffUserId == authorization.StaffContext.StaffUserId)
    {
        return Results.BadRequest(new { Error = "Staff user cannot deactivate the current authenticated account." });
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

    var roleNames = await dbContext.StaffRoleAssignments
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == request.OrganizationId &&
            roleAssignment.BranchId == branchId &&
            roleAssignment.StaffUserId == staffUserId)
        .Select(roleAssignment => roleAssignment.RoleName)
        .OrderBy(roleName => roleName)
        .ToListAsync(cancellationToken);

    if (roleNames.Count == 0)
    {
        return Results.NotFound();
    }

    var previousIsActive = staffUser.IsActive;
    staffUser.IsActive = request.IsActive;

    if (!request.IsActive)
    {
        await RevokeStaffTokensAsync(dbContext, request.OrganizationId, staffUserId, timeProvider.GetUtcNow(), cancellationToken);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToStaffUserDto(staffUser, roleNames);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateStaffState,
        "StaffUser",
        staffUserId.ToString("D"),
        AuditOutcome.Succeeded,
        new { staffUser.UserName, PreviousIsActive = previousIsActive, response.IsActive },
        cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/branches/{branchId:guid}/staff/{staffUserId:guid}/password-reset", async (
    Guid branchId,
    Guid staffUserId,
    ResetStaffUserPasswordRequest request,
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
            AuditActionNames.ResetStaffPassword,
            "StaffUser",
            staffUserId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var validation = ValidateStaffPassword(request.NewPassword);
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

    var roleNames = await dbContext.StaffRoleAssignments
        .Where(roleAssignment =>
            roleAssignment.OrganizationId == request.OrganizationId &&
            roleAssignment.BranchId == branchId &&
            roleAssignment.StaffUserId == staffUserId)
        .Select(roleAssignment => roleAssignment.RoleName)
        .OrderBy(roleName => roleName)
        .ToListAsync(cancellationToken);

    if (roleNames.Count == 0)
    {
        return Results.NotFound();
    }

    var hasher = new PasswordHasher<StaffUserEntity>();
    staffUser.PasswordHash = hasher.HashPassword(staffUser, request.NewPassword);
    await RevokeStaffTokensAsync(dbContext, request.OrganizationId, staffUserId, timeProvider.GetUtcNow(), cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToStaffUserDto(staffUser, roleNames);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ResetStaffPassword,
        "StaffUser",
        staffUserId.ToString("D"),
        AuditOutcome.Succeeded,
        new { staffUser.UserName, TokensRevoked = true },
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

app.MapPatch("/api/branches/{branchId:guid}/layout/zones/{zoneId:guid}", async (
    Guid branchId,
    Guid zoneId,
    UpdateZoneRequest request,
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
            AuditActionNames.UpdateZone,
            "Zone",
            zoneId.ToString("D"),
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

    var zone = await dbContext.Zones.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.ZoneId == zoneId,
        cancellationToken);

    if (zone is null)
    {
        return Results.NotFound(new { Error = "Zone was not found." });
    }

    var trimmedName = request.Name.Trim();
    var normalizedName = trimmedName.ToUpperInvariant();
    var duplicateName = await dbContext.Zones.AnyAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.ZoneId != zoneId &&
            candidate.Name.ToUpper() == normalizedName,
        cancellationToken);

    if (duplicateName)
    {
        return Results.Conflict(new { Error = "Zone name already exists." });
    }

    zone.Name = trimmedName;
    zone.SortOrder = request.SortOrder;
    await dbContext.SaveChangesAsync(cancellationToken);

    var seats = await dbContext.Seats
        .AsNoTracking()
        .Where(seat =>
            seat.OrganizationId == request.OrganizationId &&
            seat.BranchId == branchId &&
            seat.ZoneId == zoneId)
        .OrderBy(seat => seat.SortOrder)
        .ThenBy(seat => seat.Name)
        .ToListAsync(cancellationToken);
    var response = ToZoneDto(zone, seats);

    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateZone,
        "Zone",
        zone.ZoneId.ToString("D"),
        AuditOutcome.Succeeded,
        new { zone.Name, zone.SortOrder },
        cancellationToken);

    return Results.Ok(response);
});

app.MapDelete("/api/branches/{branchId:guid}/layout/zones/{zoneId:guid}", async (
    Guid branchId,
    Guid zoneId,
    Guid organizationId,
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
            AuditActionNames.DeleteZone,
            "Zone",
            zoneId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (organizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var zone = await dbContext.Zones.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == organizationId &&
            candidate.BranchId == branchId &&
            candidate.ZoneId == zoneId,
        cancellationToken);

    if (zone is null)
    {
        return Results.NotFound(new { Error = "Zone was not found." });
    }

    var hasSeats = await dbContext.Seats.AnyAsync(
        seat =>
            seat.OrganizationId == organizationId &&
            seat.BranchId == branchId &&
            seat.ZoneId == zoneId,
        cancellationToken);

    if (hasSeats)
    {
        return Results.Conflict(new { Error = "Zone must be empty before deletion." });
    }

    var zoneName = zone.Name;
    var sortOrder = zone.SortOrder;
    dbContext.Zones.Remove(zone);
    await dbContext.SaveChangesAsync(cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.DeleteZone,
        "Zone",
        zoneId.ToString("D"),
        AuditOutcome.Succeeded,
        new { Name = zoneName, SortOrder = sortOrder },
        cancellationToken);

    return Results.NoContent();
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

app.MapPatch("/api/branches/{branchId:guid}/layout/seats/{seatId:guid}", async (
    Guid branchId,
    Guid seatId,
    UpdateSeatRequest request,
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
            AuditActionNames.UpdateSeat,
            "Seat",
            seatId.ToString("D"),
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

    var seat = await dbContext.Seats.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.SeatId == seatId,
        cancellationToken);

    if (seat is null)
    {
        return Results.NotFound(new { Error = "Seat was not found." });
    }

    var trimmedName = request.Name.Trim();
    var normalizedName = trimmedName.ToUpperInvariant();
    var duplicateName = await dbContext.Seats.AnyAsync(
        candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == branchId &&
            candidate.ZoneId == request.ZoneId &&
            candidate.SeatId != seatId &&
            candidate.Name.ToUpper() == normalizedName,
        cancellationToken);

    if (duplicateName)
    {
        return Results.Conflict(new { Error = "Seat name already exists in the target zone." });
    }

    seat.ZoneId = request.ZoneId;
    seat.Name = trimmedName;
    seat.SortOrder = request.SortOrder;
    await dbContext.SaveChangesAsync(cancellationToken);

    var response = ToSeatDto(seat);
    await WriteAuditAsync(
        auditRecordWriter,
        request.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.UpdateSeat,
        "Seat",
        seat.SeatId.ToString("D"),
        AuditOutcome.Succeeded,
        new { seat.ZoneId, seat.Name, seat.SortOrder },
        cancellationToken);

    return Results.Ok(response);
});

app.MapDelete("/api/branches/{branchId:guid}/layout/seats/{seatId:guid}", async (
    Guid branchId,
    Guid seatId,
    Guid organizationId,
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
            AuditActionNames.DeleteSeat,
            "Seat",
            seatId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (organizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var seat = await dbContext.Seats.SingleOrDefaultAsync(
        candidate =>
            candidate.OrganizationId == organizationId &&
            candidate.BranchId == branchId &&
            candidate.SeatId == seatId,
        cancellationToken);

    if (seat is null)
    {
        return Results.NotFound(new { Error = "Seat was not found." });
    }

    var hasActiveAssignment = await dbContext.DeviceSeatAssignments.AnyAsync(
        assignment =>
            assignment.OrganizationId == organizationId &&
            assignment.BranchId == branchId &&
            assignment.SeatId == seatId &&
            assignment.DetachedAtUtc == null,
        cancellationToken);

    if (hasActiveAssignment)
    {
        return Results.Conflict(new { Error = "Seat has an active device assignment." });
    }

    var hasSessionHistory = await dbContext.Sessions.AnyAsync(
        session =>
            session.OrganizationId == organizationId &&
            session.BranchId == branchId &&
            session.SeatId == seatId,
        cancellationToken);

    if (hasSessionHistory)
    {
        return Results.Conflict(new { Error = "Seat has session history and cannot be deleted." });
    }

    var zoneId = seat.ZoneId;
    var seatName = seat.Name;
    var sortOrder = seat.SortOrder;
    dbContext.Seats.Remove(seat);
    await dbContext.SaveChangesAsync(cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.DeleteSeat,
        "Seat",
        seatId.ToString("D"),
        AuditOutcome.Succeeded,
        new { ZoneId = zoneId, Name = seatName, SortOrder = sortOrder },
        cancellationToken);

    return Results.NoContent();
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
        cancellationToken,
        actorCanApproveComp: authorization.StaffContext.Permissions.Contains(StaffPermissionNames.ApproveMoneyAction));

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

    // §5.4: a comp (free session) is audited as a first-class session.comp with its reason and its
    // assessed value, so the owner summary / Review screen can surface free sessions in money terms.
    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        request.IsComp ? AuditActionNames.SessionComp : AuditActionNames.StartSession,
        "Session",
        result.Response!.Session.SessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        request.IsComp
            ? JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes, request.CompReason, CompValueMinorUnits = result.Response.CompValueMinorUnits })
            : JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes }))
    {
        AmountMinorUnits = request.IsComp ? result.Response.CompValueMinorUnits : null
    },
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

app.MapPost("/api/sessions/{sessionId:guid}/checkout", async (
    Guid sessionId,
    SessionCheckoutRequest request,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    ISessionCheckoutService sessionCheckoutService,
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
            AuditActionNames.CheckoutSession,
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

    var result = await sessionCheckoutService.CheckoutAsync(
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
        AuditActionNames.CheckoutSession,
        "Session",
        sessionId.ToString("D"),
        AuditOutcome.Succeeded,
        "PlatformApi",
        JsonSerializer.Serialize(new
        {
            GrandTotal = result.Response!.GrandTotal.MinorUnits,
            result.Response.GrandTotal.CurrencyCode,
            PaymentParts = result.Response.Payments.Count
        })),
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapGet("/api/sessions/{sessionId:guid}/checkout/quote", async (
    Guid sessionId,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    ISessionCheckoutService sessionCheckoutService,
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
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await sessionCheckoutService.QuoteAsync(
        sessionId,
        authorization.StaffContext!.OrganizationId,
        cancellationToken);

    if (result.NotFound)
    {
        return Results.NotFound(new { Error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

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

app.MapPost("/api/install/discover", async (
    InstallDiscoverRequest request,
    HttpContext httpContext,
    IInstallService installService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var result = await installService.DiscoverAsync(request, cancellationToken);
    var sourceIp = GetSourceIp(httpContext);
    if (!result.Succeeded)
    {
        await WriteInstallAuditAsync(
            auditRecordWriter,
            result.OrganizationId ?? Guid.Empty,
            result.BranchId,
            AuditActionNames.InstallDiscoverInvoked,
            "OwnerCode",
            result.OwnerCodeId?.ToString("D"),
            AuditOutcome.Denied,
            new { result.Error, SourceIp = sourceIp },
            cancellationToken);
    }

    return ToInstallHttpResult(result);
});

app.MapPost("/api/install/enroll", async (
    InstallEnrollRequest request,
    HttpContext httpContext,
    IInstallService installService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var result = await installService.EnrollAsync(request, cancellationToken);
    var sourceIp = GetSourceIp(httpContext);
    if (result.Succeeded)
    {
        await WriteInstallAuditAsync(
            auditRecordWriter,
            result.OrganizationId!.Value,
            result.BranchId,
            AuditActionNames.InstallEnrollSucceeded,
            "Device",
            result.Value!.DeviceId.ToString("D"),
            AuditOutcome.Succeeded,
            new
            {
                request.SeatId,
                request.Role,
                request.DisplayName,
                result.Value.EnrollmentState,
                SourceIp = sourceIp
            },
            cancellationToken);
    }
    else
    {
        await WriteInstallAuditAsync(
            auditRecordWriter,
            result.OrganizationId ?? Guid.Empty,
            result.BranchId,
            AuditActionNames.InstallEnrollRejected,
            "OwnerCode",
            result.OwnerCodeId?.ToString("D"),
            AuditOutcome.Denied,
            new
            {
                request.BranchId,
                request.SeatId,
                request.Role,
                result.Error,
                SourceIp = sourceIp
            },
            cancellationToken);
    }

    return ToInstallHttpResult(result);
});

app.MapPost("/api/install/seats", async (
    InstallCreateSeatRequest request,
    HttpContext httpContext,
    IInstallService installService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var result = await installService.CreateSeatAsync(request, cancellationToken);
    var sourceIp = GetSourceIp(httpContext);
    if (result.Succeeded)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            result.OrganizationId!.Value,
            result.BranchId!.Value,
            result.StaffUserId!.Value,
            AuditActionNames.CreateSeat,
            "Seat",
            result.Value!.SeatId.ToString("D"),
            AuditOutcome.Succeeded,
            new
            {
                request.ZoneId,
                request.Name,
                SourceIp = sourceIp,
                Via = "owner_code_install"
            },
            cancellationToken);
    }
    else
    {
        await WriteInstallAuditAsync(
            auditRecordWriter,
            result.OrganizationId ?? Guid.Empty,
            result.BranchId,
            AuditActionNames.CreateSeat,
            "OwnerCode",
            result.OwnerCodeId?.ToString("D"),
            AuditOutcome.Denied,
            new
            {
                request.BranchId,
                request.ZoneId,
                request.Name,
                result.Error,
                SourceIp = sourceIp,
                Via = "owner_code_install"
            },
            cancellationToken);
    }

    return ToInstallHttpResult(result);
});

app.MapPost("/api/devices/enroll", async (
    DeviceEnrollmentRequest request,
    IDeviceEnrollmentService enrollmentService,
    ITenantStatusGuard tenantStatusGuard,
    CancellationToken cancellationToken) =>
{
    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
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
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
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

app.MapPost("/api/devices/{deviceId:guid}/commands/{commandId:guid}/result", async (
    Guid deviceId,
    Guid commandId,
    DeviceCommandResultDto result,
    HttpContext httpContext,
    IDeviceCredentialValidator credentialValidator,
    IDeviceCommandStore commandStore,
    ISessionCommandResultProcessor sessionCommandResultProcessor,
    IHubContext<DeviceHub> hubContext,
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(result.OrganizationId, cancellationToken);
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
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
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
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
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

app.MapGet("/api/branches/{branchId:guid}/devices", async (
    Guid branchId,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    CancellationToken cancellationToken) =>
{
    if (staffContextAccessor.Current is null)
    {
        return Results.Unauthorized();
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewDeviceDetail,
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
        cancellationToken);

    return Results.Ok(devices);
});

app.MapGet("/api/branches/{branchId:guid}/devices/pending", async (
    Guid branchId,
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    CancellationToken cancellationToken) =>
{
    if (staffContextAccessor.Current is null)
    {
        return Results.Unauthorized();
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.ViewDeviceDetail,
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
        cancellationToken);

    return Results.Ok(devices);
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
        RecentCommands: recentCommands,
        DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
        Role: device.Role,
        EnrollmentState: device.EnrollmentState));
});

app.MapPost("/api/devices/{deviceId:guid}/approve", async (
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
        StaffPermissionNames.AssignDeviceSeat,
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

    return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, cancellationToken));
});

app.MapPost("/api/devices/{deviceId:guid}/reject", async (
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
        StaffPermissionNames.AssignDeviceSeat,
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

    return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, cancellationToken));
});

app.MapPost("/api/devices/{deviceId:guid}/rename", async (
    Guid deviceId,
    RenameDeviceRequest request,
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
        StaffPermissionNames.AssignDeviceSeat,
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

    return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, cancellationToken));
});

app.MapPost("/api/devices/{deviceId:guid}/move-seat", async (
    Guid deviceId,
    MoveDeviceSeatRequest request,
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
        StaffPermissionNames.AssignDeviceSeat,
        AuditActionNames.MoveDeviceSeat,
        new
        {
            request.OrganizationId,
            request.SeatId
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
        return Results.Conflict(new { Error = "Rejected or removed devices cannot be moved." });
    }

    if (request.SeatId == Guid.Empty)
    {
        return Results.BadRequest(new { Error = "SeatId is required." });
    }

    var assignment = await ApplyDeviceSeatAssignmentAsync(
        dbContext,
        device,
        request.OrganizationId,
        request.SeatId,
        timeProvider.GetUtcNow(),
        cancellationToken);

    if (assignment.ErrorResult is not null)
    {
        return assignment.ErrorResult;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        device.OrganizationId,
        device.BranchId,
        authorization.StaffContext!.StaffUserId,
        AuditActionNames.MoveDeviceSeat,
        "DeviceSeatAssignment",
        assignment.Assignment!.DeviceSeatAssignmentId.ToString("D"),
        AuditOutcome.Succeeded,
        new
        {
            DeviceId = device.DeviceId,
            request.SeatId
        },
        cancellationToken);

    await NotifyDeviceChangesAsync(
        hubContext,
        dbContext,
        assignment.ChangedDeviceIds,
        assignment.ObservedAtUtc,
        cancellationToken);

    return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, cancellationToken));
});

app.MapPost("/api/devices/{deviceId:guid}/remove", async (
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
        StaffPermissionNames.RevokeDeviceCredential,
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

    return Results.Ok(await LoadDeviceInventoryItemAsync(dbContext, device.DeviceId, cancellationToken));
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

app.MapGet("/api/devices/{deviceId:guid}/commands", async (
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
        StaffPermissionNames.ViewDeviceCommandStatus,
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
            command.UpdatedAtUtc))
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
});

app.MapGet("/api/branches/{branchId:guid}/device-commands", async (
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
        StaffPermissionNames.ViewDeviceCommandStatus,
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
                command.UpdatedAtUtc))
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
    IMoneyActionPolicyResolver moneyActionPolicyResolver,
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

    // §5.2: gate the direct refund through the same guard as /money-actions. Over-threshold/over-cap
    // refunds cannot be pushed straight to the ledger here — they must go through the approval front door.
    var refundGate = await GuardLegacyMoneyActionAsync(
        dbContext,
        moneyActionPolicyResolver,
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        player.BranchId,
        authorization.StaffContext.StaffUserId,
        MoneyActionType.Refund,
        originalEntry.AccountType,
        -Math.Abs(request.Amount.MinorUnits),
        cancellationToken);
    if (refundGate is not null)
    {
        return refundGate;
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
        cancellationToken,
        amountMinorUnits: Math.Abs(request.Amount.MinorUnits));

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
    IMoneyActionPolicyResolver moneyActionPolicyResolver,
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

    // §5.2: gate the direct correction through the same guard as /money-actions. Over-threshold/over-cap
    // corrections (including debt write-offs) cannot bypass the approval front door here.
    var correctionGate = await GuardLegacyMoneyActionAsync(
        dbContext,
        moneyActionPolicyResolver,
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        player.BranchId,
        authorization.StaffContext.StaffUserId,
        MoneyActionType.ManualCorrection,
        request.AccountType,
        request.Amount.MinorUnits,
        cancellationToken);
    if (correctionGate is not null)
    {
        return correctionGate;
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
        cancellationToken,
        amountMinorUnits: Math.Abs(request.Amount.MinorUnits));

    return Results.Ok(result.Response);
});

// Anti-fraud control layer (§5.2): the guarded front door for high-risk money actions. The guard
// decides execute-now / hold-for-approval / refuse before any ledger write; approval replays the
// action through the verified billing path with a second pair of eyes.
app.MapPost("/api/branches/{branchId:guid}/money-actions", async (
    Guid branchId,
    MoneyActionSubmitRequest request,
    PlatformDbContext dbContext,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IOpenShiftResolver openShiftResolver,
    IMoneyActionApprovalService approvalService,
    CancellationToken cancellationToken) =>
{
    if (!TryParseMoneyActionType(request.ActionType, out var requestedType, out var requiredPermission))
    {
        return Results.BadRequest(new { Error = "ActionType must be 'refund' or 'manual_correction'." });
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, requiredPermission, cancellationToken);

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
            AuditActionNames.MoneyActionRequested,
            "MoneyAction",
            null,
            AuditOutcome.Denied,
            new { request.ActionType, request.SignedAmountMinorUnits, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    if (request.OrganizationId != staffContext.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
    {
        return Results.BadRequest(new { Error = "IdempotencyKey is required." });
    }

    if (requestedType == MoneyActionType.Refund && request.LedgerEntryId is null)
    {
        return Results.BadRequest(new { Error = "A refund requires the target LedgerEntryId." });
    }

    var openShift = await openShiftResolver.GetOpenShiftIdAsync(
        staffContext.OrganizationId, branchId, cancellationToken);
    if (!openShift.Succeeded || openShift.Response == Guid.Empty)
    {
        return Results.Conflict(new { Error = openShift.Error ?? "An open shift is required." });
    }

    var roleNames = await GetActorRoleNamesAsync(
        dbContext, staffContext.StaffUserId, staffContext.OrganizationId, cancellationToken);

    var command = new MoneyActionCommand(
        requestedType,
        request.PlayerAccountId,
        request.LedgerEntryId,
        request.AccountType,
        request.SignedAmountMinorUnits,
        request.CurrencyCode,
        request.QuantitySeconds,
        request.Reason,
        request.IdempotencyKey);

    var result = await approvalService.RequestAsync(
        staffContext.OrganizationId, branchId, openShift.Response, staffContext.StaffUserId,
        roleNames, command, cancellationToken);

    switch (result.Outcome)
    {
        case MoneyActionRequestOutcome.Executed:
            await WriteAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                branchId,
                staffContext.StaffUserId,
                AuditActionNames.MoneyActionExecuted,
                "MoneyAction",
                result.ResultingLedgerEntryId?.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.ActionType, request.SignedAmountMinorUnits, request.CurrencyCode },
                cancellationToken,
                amountMinorUnits: Math.Abs(request.SignedAmountMinorUnits));
            return Results.Ok(new MoneyActionSubmitResponse("executed", result.ResultingLedgerEntryId, null));

        case MoneyActionRequestOutcome.PendingApproval:
            await WriteAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                branchId,
                staffContext.StaffUserId,
                AuditActionNames.MoneyActionRequested,
                "MoneyAction",
                result.MoneyActionRequestId?.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.ActionType, request.SignedAmountMinorUnits, request.CurrencyCode },
                cancellationToken,
                amountMinorUnits: Math.Abs(request.SignedAmountMinorUnits));
            return Results.Json(
                new MoneyActionSubmitResponse("pending_approval", null, result.MoneyActionRequestId),
                statusCode: StatusCodes.Status202Accepted);

        default:
            if (result.NotFound)
            {
                return Results.NotFound(new { result.Error });
            }

            return result.Conflict
                ? Results.Conflict(new { result.Error })
                : Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
});

app.MapPost("/api/branches/{branchId:guid}/money-actions/{moneyActionRequestId:guid}/approve", async (
    Guid branchId,
    Guid moneyActionRequestId,
    MoneyActionDecisionRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IMoneyActionApprovalService approvalService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, StaffPermissionNames.ApproveMoneyAction, cancellationToken);

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
            AuditActionNames.MoneyActionApproved,
            "MoneyAction",
            moneyActionRequestId.ToString("D"),
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    var result = await approvalService.ApproveAsync(
        staffContext.OrganizationId, branchId, moneyActionRequestId,
        staffContext.StaffUserId, request.DecisionReason, cancellationToken);

    if (result.Forbidden)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            staffContext.OrganizationId,
            branchId,
            staffContext.StaffUserId,
            AuditActionNames.MoneyActionApproved,
            "MoneyAction",
            moneyActionRequestId.ToString("D"),
            AuditOutcome.Denied,
            new { result.Error },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { result.Error });
    }

    if (result.Conflict)
    {
        return Results.Conflict(new { result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        staffContext.OrganizationId,
        branchId,
        staffContext.StaffUserId,
        AuditActionNames.MoneyActionApproved,
        "MoneyAction",
        moneyActionRequestId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.ResultingLedgerEntryId },
        cancellationToken);

    return Results.Ok(new MoneyActionSubmitResponse("approved", result.ResultingLedgerEntryId, moneyActionRequestId));
});

app.MapPost("/api/branches/{branchId:guid}/money-actions/{moneyActionRequestId:guid}/reject", async (
    Guid branchId,
    Guid moneyActionRequestId,
    MoneyActionDecisionRequest request,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IMoneyActionApprovalService approvalService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, StaffPermissionNames.ApproveMoneyAction, cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    var result = await approvalService.RejectAsync(
        staffContext.OrganizationId, branchId, moneyActionRequestId,
        staffContext.StaffUserId, request.DecisionReason, cancellationToken);

    if (result.NotFound)
    {
        return Results.NotFound(new { result.Error });
    }

    if (result.Conflict)
    {
        return Results.Conflict(new { result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    await WriteAuditAsync(
        auditRecordWriter,
        staffContext.OrganizationId,
        branchId,
        staffContext.StaffUserId,
        AuditActionNames.MoneyActionRejected,
        "MoneyAction",
        moneyActionRequestId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.DecisionReason },
        cancellationToken);

    return Results.Ok(new MoneyActionSubmitResponse("rejected", null, moneyActionRequestId));
});

app.MapGet("/api/branches/{branchId:guid}/money-actions", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IMoneyActionApprovalService approvalService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, StaffPermissionNames.ApproveMoneyAction, cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var staffContext = authorization.StaffContext!;
    var pending = await approvalService.ListPendingAsync(
        staffContext.OrganizationId, branchId, cancellationToken);

    var dtos = pending
        .Select(request => new MoneyActionRequestDto(
            request.MoneyActionRequestId,
            request.OrganizationId,
            request.BranchId,
            request.ShiftId,
            request.ActionType,
            request.RequestedByStaffUserId,
            request.AmountMinorUnits,
            request.CurrencyCode,
            request.Reason,
            request.State,
            request.CreatedAtUtc,
            request.ExpiresAtUtc))
        .ToList();

    return Results.Ok(new MoneyActionRequestListResponse(dtos));
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

app.MapPatch("/api/branches/{branchId:guid}/tariffs/{tariffId:guid}", async (
    Guid branchId,
    Guid tariffId,
    UpdateTariffRequest request,
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
            AuditActionNames.UpdateTariff,
            "Tariff",
            tariffId.ToString("D"),
            AuditOutcome.Denied,
            new { request.Name, request.IsActive, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await tariffService.UpdateTariffAsync(
        branchId,
        tariffId,
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
        AuditActionNames.UpdateTariff,
        "Tariff",
        tariffId.ToString("D"),
        AuditOutcome.Succeeded,
        new { result.Response!.Name, result.Response.IsActive },
        cancellationToken);

    return Results.Ok(result.Response);
});

app.MapPatch("/api/branches/{branchId:guid}/tariffs/{tariffId:guid}/versions/{tariffVersionId:guid}", async (
    Guid branchId,
    Guid tariffId,
    Guid tariffVersionId,
    UpdateTariffVersionRequest request,
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
            AuditActionNames.UpdateTariffVersion,
            "TariffVersion",
            tariffVersionId.ToString("D"),
            AuditOutcome.Denied,
            new { tariffId, request.IsActive, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await tariffService.UpdateTariffVersionAsync(
        branchId,
        tariffId,
        tariffVersionId,
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
        AuditActionNames.UpdateTariffVersion,
        "TariffVersion",
        tariffVersionId.ToString("D"),
        AuditOutcome.Succeeded,
        new
        {
            tariffId,
            result.Response!.VersionNumber,
            result.Response.PricePerMinuteMinorUnits,
            result.Response.RetiredAtUtc
        },
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
        new { request.CountedCash, result.Response!.Difference },
        cancellationToken);

    // Anti-fraud §5.7: record the manager sign-off as its own audit fact when a discrepancy was cleared.
    if (result.Response.ManagerSignOffStaffUserId is { } signOffStaffUserId)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext.OrganizationId,
            shift.BranchId,
            authorization.StaffContext.StaffUserId,
            AuditActionNames.ShiftSignOff,
            "Shift",
            shiftId.ToString("D"),
            AuditOutcome.Succeeded,
            new { SignOffStaffUserId = signOffStaffUserId, result.Response.Difference, request.SignOffReason },
            cancellationToken);
    }

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
    Guid? actorStaffUserId,
    long? minAmountMinorUnits,
    long? maxAmountMinorUnits,
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

    var query = new ReportSearchQuery(fromUtc, toUtc, limit, actorStaffUserId, minAmountMinorUnits, maxAmountMinorUnits);
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
            toUtc,
            actorStaffUserId,
            minAmountMinorUnits,
            maxAmountMinorUnits
        },
        cancellationToken);

    return Results.Ok(result);
});

// Anti-fraud §5.6: on-demand owner daily summary (the report-endpoint fallback to the notification
// digest). Defaults to the most recently ended UTC day when no date is given.
app.MapGet("/api/branches/{branchId:guid}/reports/owner-daily-summary", async (
    Guid branchId,
    DateOnly? date,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IReportService reportService,
    TimeProvider timeProvider,
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
            AuditActionNames.ViewOwnerDailySummaryReport,
            "Report",
            "owner-daily-summary",
            AuditOutcome.Denied,
            new { authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var summaryDate = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
    var result = await reportService.GetOwnerDailySummaryAsync(
        authorization.StaffContext!.OrganizationId,
        branchId,
        summaryDate,
        cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        authorization.StaffContext.OrganizationId,
        branchId,
        authorization.StaffContext.StaffUserId,
        AuditActionNames.ViewOwnerDailySummaryReport,
        "Report",
        "owner-daily-summary",
        AuditOutcome.Succeeded,
        new
        {
            Date = summaryDate.ToString("yyyy-MM-dd"),
            ActorCount = result.Rows.Count
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
    Guid? actorStaffUserId,
    long? minAmountMinorUnits,
    long? maxAmountMinorUnits,
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
        cancellationToken,
        actorStaffUserId,
        minAmountMinorUnits,
        maxAmountMinorUnits);
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

app.MapPatch("/api/branches/{branchId:guid}/pos/products/{productId:guid}", async (
    Guid branchId,
    Guid productId,
    UpdateProductRequest request,
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
            AuditActionNames.UpdateProduct,
            "PosProduct",
            productId.ToString("D"),
            AuditOutcome.Denied,
            new { request.Sku, request.IsActive, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    }

    var result = await inventoryService.UpdateProductAsync(
        branchId,
        productId,
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
        AuditActionNames.UpdateProduct,
        "PosProduct",
        result.Response!.ProductId.ToString("D"),
        AuditOutcome.Succeeded,
        new { request.Sku, request.Price, request.IsActive },
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
    Guid? actorStaffUserId,
    long? minAmount,
    long? maxAmount,
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

    var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
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
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
    if (suspendedCheck is not null)
    {
        return suspendedCheck;
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
    ITenantStatusGuard tenantStatusGuard,
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

    var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
    if (suspendedCheck is not null)
    {
        return suspendedCheck;
    }

    var result = await updateService.ReportStatusAsync(request, cancellationToken);

    return ToUpdateHttpResult(result);
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

static bool TryParseMoneyActionType(string? actionType, out MoneyActionType requestedType, out string requiredPermission)
{
    switch (actionType?.Trim().ToLowerInvariant())
    {
        case MoneyActionTypeNames.Refund:
            requestedType = MoneyActionType.Refund;
            requiredPermission = StaffPermissionNames.RefundLedgerEntry;
            return true;
        case MoneyActionTypeNames.ManualCorrection:
            requestedType = MoneyActionType.ManualCorrection;
            requiredPermission = StaffPermissionNames.ManualLedgerCorrection;
            return true;
        default:
            requestedType = default;
            requiredPermission = string.Empty;
            return false;
    }
}

static async Task<IReadOnlyCollection<string>> GetActorRoleNamesAsync(
    PlatformDbContext dbContext,
    Guid staffUserId,
    Guid organizationId,
    CancellationToken cancellationToken) =>
    await dbContext.StaffRoleAssignments
        .AsNoTracking()
        .Where(role => role.StaffUserId == staffUserId && role.OrganizationId == organizationId)
        .Select(role => role.RoleName)
        .Distinct()
        .ToListAsync(cancellationToken);

// Anti-fraud §5.2 enforcement: the legacy direct ledger endpoints share the same MoneyActionGuard as
// the /money-actions front door. Returns null when the action may execute immediately (under threshold,
// under cap) so the caller proceeds with its direct ledger write; otherwise returns the blocking result
// (409 — must go through the approval front door; 422 — over cap) and writes the denied audit trail.
static async Task<IResult?> GuardLegacyMoneyActionAsync(
    PlatformDbContext dbContext,
    IMoneyActionPolicyResolver policyResolver,
    IAuditRecordWriter auditRecordWriter,
    Guid organizationId,
    Guid branchId,
    Guid actorStaffUserId,
    MoneyActionType requestedType,
    string accountType,
    long signedAmountMinorUnits,
    CancellationToken cancellationToken)
{
    var roleNames = await GetActorRoleNamesAsync(dbContext, actorStaffUserId, organizationId, cancellationToken);
    var assessment = await policyResolver.AssessAsync(
        organizationId, branchId, actorStaffUserId, roleNames,
        requestedType, accountType, signedAmountMinorUnits, cancellationToken);

    if (assessment.Decision == MoneyActionDecision.ExecuteNow)
    {
        return null;
    }

    var amount = Math.Abs(signedAmountMinorUnits);
    var requiresApproval = assessment.Decision == MoneyActionDecision.RequireApproval;
    var blockedReason = requiresApproval
        ? "Amount exceeds the approval threshold; submit via /money-actions for manager approval."
        : "Amount exceeds the configured per-transaction or daily cap.";

    await WriteAuditAsync(
        auditRecordWriter,
        organizationId,
        branchId,
        actorStaffUserId,
        AuditActionNames.MoneyActionRequested,
        "MoneyAction",
        null,
        AuditOutcome.Denied,
        new { Decision = assessment.Decision.ToString(), Amount = amount, Reason = blockedReason },
        cancellationToken,
        amountMinorUnits: amount);

    return requiresApproval
        ? Results.Conflict(new { Error = blockedReason, RequiresApproval = true })
        : Results.Json(new { Error = blockedReason }, statusCode: StatusCodes.Status422UnprocessableEntity);
}

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

static IResult ToInstallHttpResult<TResponse>(InstallOperationResult<TResponse> result)
{
    return result.Status switch
    {
        InstallOperationStatus.Succeeded => Results.Ok(result.Value),
        InstallOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
        InstallOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
        _ => Results.BadRequest(new { Error = result.Error })
    };
}

static string GetSourceIp(HttpContext httpContext)
{
    var remoteIp = httpContext.Connection.RemoteIpAddress;
    if (ShouldTrustForwardedFor(remoteIp))
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var firstForwardedFor = forwardedFor
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstForwardedFor))
        {
            return firstForwardedFor;
        }
    }

    return remoteIp?.ToString() ?? "unknown";
}

static bool ShouldTrustForwardedFor(IPAddress? remoteIp)
{
    if (remoteIp is null || IPAddress.IsLoopback(remoteIp))
    {
        return true;
    }

    if (remoteIp.IsIPv4MappedToIPv6)
    {
        remoteIp = remoteIp.MapToIPv4();
    }

    if (remoteIp.AddressFamily == AddressFamily.InterNetwork)
    {
        var bytes = remoteIp.GetAddressBytes();
        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
    }

    if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
    {
        var bytes = remoteIp.GetAddressBytes();
        return remoteIp.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
    }

    return false;
}

static async Task WriteInstallAuditAsync(
    IAuditRecordWriter auditRecordWriter,
    Guid organizationId,
    Guid? branchId,
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
        null,
        action,
        targetType,
        targetId,
        outcome,
        "PlatformApi",
        JsonSerializer.Serialize(details)),
        cancellationToken);
}

static async Task WriteOwnerCodeAuditAsync(
    IAuditRecordWriter auditRecordWriter,
    Guid? organizationId,
    Guid? actorStaffUserId,
    string action,
    string? targetId,
    string outcome,
    object details,
    CancellationToken cancellationToken)
{
    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        organizationId ?? Guid.Empty,
        null,
        actorStaffUserId,
        action,
        "OwnerCode",
        targetId,
        outcome,
        "PlatformApi",
        JsonSerializer.Serialize(details)),
        cancellationToken);
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
    CancellationToken cancellationToken,
    long? amountMinorUnits = null)
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
        JsonSerializer.Serialize(details))
    {
        AmountMinorUnits = amountMinorUnits
    },
        cancellationToken);
}

static async Task WritePlatformAuditAsync(
    IAuditRecordWriter auditRecordWriter,
    Guid organizationId,
    Guid? actorPlatformAdminUserId,
    string action,
    string targetType,
    string? targetId,
    string outcome,
    object details,
    CancellationToken cancellationToken)
{
    await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
        organizationId,
        null,
        null,
        action,
        targetType,
        targetId,
        outcome,
        "PlatformApi",
        JsonSerializer.Serialize(details))
    {
        ActorPlatformAdminUserId = actorPlatformAdminUserId
    },
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
    CancellationToken cancellationToken,
    Guid? actorStaffUserId = null,
    long? minAmountMinorUnits = null,
    long? maxAmountMinorUnits = null)
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

    var query = new ReportSearchQuery(fromUtc, toUtc, limit, actorStaffUserId, minAmountMinorUnits, maxAmountMinorUnits);
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

static async Task<IReadOnlyList<DeviceInventoryItemDto>> LoadBranchDeviceInventoryAsync(
    PlatformDbContext dbContext,
    Guid organizationId,
    Guid branchId,
    string? enrollmentState,
    CancellationToken cancellationToken)
{
    var query = dbContext.Devices
        .AsNoTracking()
        .Where(device => device.OrganizationId == organizationId && device.BranchId == branchId);

    query = enrollmentState is null
        ? query.Where(device => device.EnrollmentState != DeviceEnrollmentStateNames.Removed)
        : query.Where(device => device.EnrollmentState == enrollmentState);

    var devices = await query
        .OrderBy(device => device.MachineName)
        .ThenBy(device => device.DeviceId)
        .ToListAsync(cancellationToken);

    return await BuildDeviceInventoryAsync(dbContext, devices, cancellationToken);
}

static async Task<DeviceInventoryItemDto?> LoadDeviceInventoryItemAsync(
    PlatformDbContext dbContext,
    Guid deviceId,
    CancellationToken cancellationToken)
{
    var device = await dbContext.Devices
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

    if (device is null)
    {
        return null;
    }

    var items = await BuildDeviceInventoryAsync(dbContext, [device], cancellationToken);
    return items.SingleOrDefault();
}

static async Task<IReadOnlyList<DeviceInventoryItemDto>> BuildDeviceInventoryAsync(
    PlatformDbContext dbContext,
    IReadOnlyList<DeviceEntity> devices,
    CancellationToken cancellationToken)
{
    if (devices.Count == 0)
    {
        return [];
    }

    var deviceIds = devices.Select(device => device.DeviceId).ToList();
    var assignments = await dbContext.DeviceSeatAssignments
        .AsNoTracking()
        .Where(assignment => deviceIds.Contains(assignment.DeviceId) && assignment.DetachedAtUtc == null)
        .OrderByDescending(assignment => assignment.AttachedAtUtc)
        .ToListAsync(cancellationToken);
    var assignmentsByDevice = assignments
        .GroupBy(assignment => assignment.DeviceId)
        .ToDictionary(group => group.Key, group => group.First());
    var seatIds = assignmentsByDevice.Values
        .Select(assignment => assignment.SeatId)
        .Distinct()
        .ToList();
    var seats = seatIds.Count == 0
        ? []
        : await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seatIds.Contains(seat.SeatId))
            .ToListAsync(cancellationToken);
    var seatsById = seats.ToDictionary(seat => seat.SeatId);
    var zoneIds = seats
        .Select(seat => seat.ZoneId)
        .Distinct()
        .ToList();
    var zones = zoneIds.Count == 0
        ? []
        : await dbContext.Zones
            .AsNoTracking()
            .Where(zone => zoneIds.Contains(zone.ZoneId))
            .ToListAsync(cancellationToken);
    var zonesById = zones.ToDictionary(zone => zone.ZoneId);
    var activeCredentialCounts = await dbContext.DeviceCredentials
        .AsNoTracking()
        .Where(credential => deviceIds.Contains(credential.DeviceId) && credential.RevokedAtUtc == null)
        .GroupBy(credential => credential.DeviceId)
        .Select(group => new { DeviceId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
    var installedAppCounts = await dbContext.DeviceInstalledApps
        .AsNoTracking()
        .Where(app => deviceIds.Contains(app.DeviceId))
        .GroupBy(app => app.DeviceId)
        .Select(group => new { DeviceId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
    var pendingCommandCounts = await dbContext.DeviceCommands
        .AsNoTracking()
        .Where(command => deviceIds.Contains(command.DeviceId) && command.Status == "Pending")
        .GroupBy(command => command.DeviceId)
        .Select(group => new { DeviceId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
    var failedCommandCounts = await dbContext.DeviceCommands
        .AsNoTracking()
        .Where(command => deviceIds.Contains(command.DeviceId) && (command.Status == "Failed" || command.Status == "Rejected"))
        .GroupBy(command => command.DeviceId)
        .Select(group => new { DeviceId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);

    return devices.Select(device =>
    {
        assignmentsByDevice.TryGetValue(device.DeviceId, out var assignment);
        SeatEntity? seat = null;
        ZoneEntity? zone = null;
        if (assignment is not null && seatsById.TryGetValue(assignment.SeatId, out var assignedSeat))
        {
            seat = assignedSeat;
            zonesById.TryGetValue(assignedSeat.ZoneId, out zone);
        }

        return new DeviceInventoryItemDto(
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
            ActiveCredentialCount: activeCredentialCounts.GetValueOrDefault(device.DeviceId),
            InstalledAppCount: installedAppCounts.GetValueOrDefault(device.DeviceId),
            PendingCommandCount: pendingCommandCounts.GetValueOrDefault(device.DeviceId),
            FailedCommandCount: failedCommandCounts.GetValueOrDefault(device.DeviceId),
            DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
            Role: device.Role,
            EnrollmentState: device.EnrollmentState);
    }).ToList();
}

static async Task<DeviceMutationScope> LoadDeviceMutationScopeAsync(
    PlatformDbContext dbContext,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    Guid deviceId,
    string permission,
    string auditAction,
    object details,
    CancellationToken cancellationToken)
{
    var staffContext = staffContextAccessor.Current;
    if (staffContext is null)
    {
        return new DeviceMutationScope(null, null, Results.Unauthorized());
    }

    var device = await dbContext.Devices
        .SingleOrDefaultAsync(
            candidate =>
                candidate.DeviceId == deviceId &&
                candidate.OrganizationId == staffContext.OrganizationId,
            cancellationToken);

    if (device is null)
    {
        return new DeviceMutationScope(null, null, Results.NotFound());
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        device.BranchId,
        permission,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return new DeviceMutationScope(device, authorization, Results.Unauthorized());
    }

    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext!.OrganizationId,
            device.BranchId,
            authorization.StaffContext.StaffUserId,
            auditAction,
            "Device",
            device.DeviceId.ToString("D"),
            AuditOutcome.Denied,
            new
            {
                authorization.DenialReason,
                Details = details
            },
            cancellationToken);

        return new DeviceMutationScope(device, authorization, Results.StatusCode(StatusCodes.Status403Forbidden));
    }

    return new DeviceMutationScope(device, authorization, null);
}

static IResult? ValidateDeviceMutationOrganization(
    Guid requestOrganizationId,
    StaffAuthorizationResult authorization,
    DeviceEntity device)
{
    if (requestOrganizationId == Guid.Empty)
    {
        return Results.BadRequest(new { Error = "OrganizationId is required." });
    }

    if (requestOrganizationId != authorization.StaffContext!.OrganizationId ||
        requestOrganizationId != device.OrganizationId)
    {
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization and device." });
    }

    return null;
}

static async Task<DeviceSeatAssignmentOperationResult> ApplyDeviceSeatAssignmentAsync(
    PlatformDbContext dbContext,
    DeviceEntity device,
    Guid organizationId,
    Guid seatId,
    DateTimeOffset observedAtUtc,
    CancellationToken cancellationToken)
{
    var seat = await dbContext.Seats
        .SingleOrDefaultAsync(
            candidate =>
                candidate.SeatId == seatId &&
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == device.BranchId,
            cancellationToken);

    if (seat is null)
    {
        return new DeviceSeatAssignmentOperationResult(null, Results.NotFound(), [device.DeviceId], observedAtUtc);
    }

    var hasActiveSession = await dbContext.Sessions
        .AsNoTracking()
        .AnyAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == device.BranchId &&
                (candidate.SeatId == seatId || candidate.DeviceId == device.DeviceId) &&
                (candidate.State == SessionStateNames.Active ||
                 candidate.State == SessionStateNames.Paused ||
                 candidate.State == SessionStateNames.Ending),
            cancellationToken);

    if (hasActiveSession)
    {
        return new DeviceSeatAssignmentOperationResult(
            null,
            Results.Conflict(new { Error = "Seat or device has an active, paused, or ending session." }),
            [device.DeviceId],
            observedAtUtc);
    }

    var activeAssignments = await dbContext.DeviceSeatAssignments
        .Where(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == device.BranchId &&
                candidate.DetachedAtUtc == null &&
                (candidate.SeatId == seatId || candidate.DeviceId == device.DeviceId))
        .OrderByDescending(candidate => candidate.AttachedAtUtc)
        .ThenByDescending(candidate => candidate.DeviceSeatAssignmentId)
        .ToListAsync(cancellationToken);

    var changedDeviceIds = activeAssignments
        .Select(candidate => candidate.DeviceId)
        .Append(device.DeviceId)
        .Distinct()
        .ToArray();
    var currentAssignment = activeAssignments.FirstOrDefault(
        candidate => candidate.SeatId == seatId && candidate.DeviceId == device.DeviceId);

    if (currentAssignment is not null)
    {
        foreach (var assignment in activeAssignments.Where(candidate => candidate.DeviceSeatAssignmentId != currentAssignment.DeviceSeatAssignmentId))
        {
            assignment.DetachedAtUtc = observedAtUtc;
        }
    }
    else
    {
        foreach (var assignment in activeAssignments)
        {
            assignment.DetachedAtUtc = observedAtUtc;
        }

        currentAssignment = new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = device.BranchId,
            SeatId = seatId,
            DeviceId = device.DeviceId,
            AttachedAtUtc = observedAtUtc
        };
        dbContext.DeviceSeatAssignments.Add(currentAssignment);
    }

    return new DeviceSeatAssignmentOperationResult(currentAssignment, null, changedDeviceIds, observedAtUtc);
}

static async Task<IReadOnlyList<Guid>> DetachActiveDeviceAssignmentsAsync(
    PlatformDbContext dbContext,
    DeviceEntity device,
    DateTimeOffset detachedAtUtc,
    CancellationToken cancellationToken)
{
    var assignments = await dbContext.DeviceSeatAssignments
        .Where(
            assignment =>
                assignment.OrganizationId == device.OrganizationId &&
                assignment.BranchId == device.BranchId &&
                assignment.DeviceId == device.DeviceId &&
                assignment.DetachedAtUtc == null)
        .ToListAsync(cancellationToken);

    foreach (var assignment in assignments)
    {
        assignment.DetachedAtUtc = detachedAtUtc;
    }

    return assignments.Count == 0
        ? [device.DeviceId]
        : assignments
            .Select(assignment => assignment.DeviceId)
            .Append(device.DeviceId)
            .Distinct()
            .ToArray();
}

static async Task<int> RevokeActiveDeviceCredentialsAsync(
    PlatformDbContext dbContext,
    DeviceEntity device,
    DateTimeOffset revokedAtUtc,
    CancellationToken cancellationToken)
{
    var credentials = await dbContext.DeviceCredentials
        .Where(
            credential =>
                credential.OrganizationId == device.OrganizationId &&
                credential.BranchId == device.BranchId &&
                credential.DeviceId == device.DeviceId &&
                credential.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);

    foreach (var credential in credentials)
    {
        credential.RevokedAtUtc = revokedAtUtc;
    }

    return credentials.Count;
}

static async Task<bool> HasActiveDeviceSessionAsync(
    PlatformDbContext dbContext,
    DeviceEntity device,
    CancellationToken cancellationToken)
{
    return await dbContext.Sessions
        .AsNoTracking()
        .AnyAsync(
            session =>
                session.OrganizationId == device.OrganizationId &&
                session.BranchId == device.BranchId &&
                session.DeviceId == device.DeviceId &&
                (session.State == SessionStateNames.Active ||
                 session.State == SessionStateNames.Paused ||
                 session.State == SessionStateNames.Ending),
            cancellationToken);
}

static async Task NotifyDeviceChangesAsync(
    IHubContext<DeviceHub> hubContext,
    PlatformDbContext dbContext,
    IEnumerable<Guid> deviceIds,
    DateTimeOffset observedAtUtc,
    CancellationToken cancellationToken)
{
    var ids = deviceIds.Distinct().ToArray();
    if (ids.Length == 0)
    {
        return;
    }

    var devices = await dbContext.Devices
        .AsNoTracking()
        .Where(device => ids.Contains(device.DeviceId))
        .ToListAsync(cancellationToken);
    var assignmentRows = await dbContext.DeviceSeatAssignments
        .AsNoTracking()
        .Where(assignment => ids.Contains(assignment.DeviceId) && assignment.DetachedAtUtc == null)
        .OrderByDescending(assignment => assignment.AttachedAtUtc)
        .ThenByDescending(assignment => assignment.DeviceSeatAssignmentId)
        .ToListAsync(cancellationToken);
    var assignments = assignmentRows
        .GroupBy(assignment => assignment.DeviceId)
        .ToDictionary(group => group.Key, group => group.First().SeatId);

    foreach (var device in devices)
    {
        var seatId = assignments.TryGetValue(device.DeviceId, out var assignedSeatId)
            ? assignedSeatId
            : (Guid?)null;
        var status = new DeviceStatusChangedDto(
            OrganizationId: device.OrganizationId,
            BranchId: device.BranchId,
            DeviceId: device.DeviceId,
            MachineName: device.MachineName,
            IsOnline: device.IsOnline,
            IsLocked: device.IsLocked,
            ObservedAtUtc: observedAtUtc,
            DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
            Role: device.Role,
            EnrollmentState: device.EnrollmentState,
            SeatId: seatId);

        await hubContext.Clients
            .Group(DeviceHubGroups.Branch(device.BranchId))
            .SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);
    }
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

static async Task RevokeStaffTokensAsync(
    PlatformDbContext dbContext,
    Guid organizationId,
    Guid staffUserId,
    DateTimeOffset revokedAtUtc,
    CancellationToken cancellationToken)
{
    var accessTokens = await dbContext.StaffAccessTokens
        .Where(token =>
            token.OrganizationId == organizationId &&
            token.StaffUserId == staffUserId &&
            token.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);
    foreach (var token in accessTokens)
    {
        token.RevokedAtUtc = revokedAtUtc;
    }

    var refreshTokens = await dbContext.StaffRefreshTokens
        .Where(token =>
            token.OrganizationId == organizationId &&
            token.StaffUserId == staffUserId &&
            token.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);
    foreach (var token in refreshTokens)
    {
        token.RevokedAtUtc = revokedAtUtc;
    }
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

static string? ValidateCreateReportScheduleRequest(CreateReportScheduleRequest request)
{
    if (request.OrganizationId == Guid.Empty)
    {
        return "OrganizationId is required.";
    }

    if (!ScheduledReportTypeNames.All.Contains(request.ReportType))
    {
        return $"ReportType must be one of: {string.Join(", ", ScheduledReportTypeNames.All)}.";
    }

    if (!ReportScheduleFrequencyNames.All.Contains(request.Frequency))
    {
        return $"Frequency must be one of: {string.Join(", ", ReportScheduleFrequencyNames.All)}.";
    }

    return null;
}

static string? ValidateCreateStaffInviteRequest(CreateStaffInviteRequest request)
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

    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
    {
        return "A valid Email is required to send the invite.";
    }

    return ValidateStaffRoleNames(request.RoleNames);
}

static string? ValidateUpdateStaffUserProfileRequest(UpdateStaffUserProfileRequest request)
{
    if (request.OrganizationId == Guid.Empty)
    {
        return "OrganizationId is required.";
    }

    if (string.IsNullOrWhiteSpace(request.UserName))
    {
        return "UserName is required.";
    }

    if (request.UserName.Trim().Length > 256)
    {
        return "UserName must contain 256 characters or fewer.";
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return "DisplayName is required.";
    }

    return request.DisplayName.Trim().Length <= 160
        ? null
        : "DisplayName must contain 160 characters or fewer.";
}

static string? ValidateStaffPassword(string password)
{
    return string.IsNullOrWhiteSpace(password) || password.Length < 8
        ? "Password must contain at least 8 characters."
        : null;
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

public sealed record DeviceMutationScope(
    DeviceEntity? Device,
    StaffAuthorizationResult? Authorization,
    IResult? ErrorResult);

public sealed record DeviceSeatAssignmentOperationResult(
    DeviceSeatAssignmentEntity? Assignment,
    IResult? ErrorResult,
    IReadOnlyList<Guid> ChangedDeviceIds,
    DateTimeOffset ObservedAtUtc);

public partial class Program;
