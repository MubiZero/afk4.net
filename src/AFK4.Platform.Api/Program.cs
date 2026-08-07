using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Configuration;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Shop;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Platform.Api.Platform.Pulse;
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
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.News;
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
using AFK4.Platform.Api.Endpoints;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

const string OperatorWebCorsPolicyName = "operator-web";
const string PlatformWebCorsPolicyName = "platform-control";
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

var organizationAdminWebOrigins = ResolveCorsOrigins(
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
        "https://player.afk4.local",
        "http://localhost:5175",
        "http://127.0.0.1:5175",
        "http://localhost:4175",
        "http://127.0.0.1:4175"
    ]);

var combinedWebOrigins = organizationAdminWebOrigins
    .Concat(platformWebOrigins)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        OperatorWebCorsPolicyName,
        policy => policy
            .WithOrigins(organizationAdminWebOrigins)
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
builder.Services.AddScoped<ISessionTimelineReadService, EfSessionTimelineReadService>();
builder.Services.AddScoped<IStaffTokenService, OpaqueStaffTokenService>();
builder.Services.AddScoped<IPlayerTokenService, OpaquePlayerTokenService>();
builder.Services.AddScoped<IPlayerCredentialService, PlayerCredentialService>();
builder.Services.AddScoped<IPlayerContextAccessor, PlayerContextAccessor>();
builder.Services.AddScoped<IStaffCredentialService, PasswordHashingStaffCredentialService>();
builder.Services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
builder.Services.AddScoped<StaffAuthorizationService>();
builder.Services.AddScoped<IPlatformAdminTokenService, OpaquePlatformAdminTokenService>();
builder.Services.AddScoped<IPlatformAdminCredentialService, PasswordHashingPlatformAdminCredentialService>();
builder.Services.AddScoped<PlatformAdminTwoFactorService>();
builder.Services.AddScoped<IPlatformAdminContextAccessor, PlatformAdminContextAccessor>();
builder.Services.AddScoped<PlatformAdminAuthorizationService>();
builder.Services.AddScoped<PlatformAdminDirectoryService>();
builder.Services.AddScoped<PlatformSupportAccessGrantService>();
builder.Services.AddScoped<IPlatformSupportContextAccessor, PlatformSupportContextAccessor>();
builder.Services.Configure<SupportAccessOptions>(
    builder.Configuration.GetSection(SupportAccessOptions.SectionName));
builder.Services.Configure<PlatformAdminBootstrapOptions>(
    builder.Configuration.GetSection(PlatformAdminBootstrapOptions.ConfigurationSection));
builder.Services.AddHostedService<PlatformAdminBootstrapHostedService>();
builder.Services.Configure<PlatformOrganizationOptions>(
    builder.Configuration.GetSection(PlatformOrganizationOptions.ConfigurationSection));
builder.Services.Configure<OrganizationAdminCompatibilityOptions>(
    builder.Configuration.GetSection(OrganizationAdminCompatibilityOptions.SectionName));
builder.Services.AddSingleton<IOrganizationOwnerInviteCodeGenerator, RandomOrganizationOwnerInviteCodeGenerator>();
builder.Services.Configure<InstallOptions>(
    builder.Configuration.GetSection(InstallOptions.SectionName));
builder.Services.AddScoped<IInstallService, EfInstallService>();
builder.Services.AddSingleton<IInstallRequestThrottle, InMemoryInstallRequestThrottle>();
builder.Services.AddScoped<IPlatformOrganizationService, EfPlatformOrganizationService>();
builder.Services.AddScoped<IPlatformSupportNoteService, EfPlatformSupportNoteService>();
builder.Services.AddScoped<IPlatformIdempotencyStore, EfPlatformIdempotencyStore>();
builder.Services.AddScoped<IPlatformOrganizationHealthService, EfPlatformOrganizationHealthService>();
builder.Services.Configure<PlatformPulseOptions>(
    builder.Configuration.GetSection(PlatformPulseOptions.SectionName));
builder.Services.AddScoped<IPlatformPulseService, EfPlatformPulseService>();
builder.Services.AddScoped<IPlanCatalogService, EfPlanCatalogService>();
builder.Services.AddScoped<IOrganizationSubscriptionService, EfOrganizationSubscriptionService>();
builder.Services.AddScoped<IOrganizationOwnerResolver, EfOrganizationOwnerResolver>();
builder.Services.AddScoped<IInvoiceNotifier, EfInvoiceNotifier>();
builder.Services.AddScoped<IInvoiceGenerationRunner, EfInvoiceGenerationRunner>();
builder.Services.AddScoped<IDunningRunner, EfDunningRunner>();
builder.Services.AddScoped<IInvoiceService, EfInvoiceService>();
builder.Services.AddScoped<IBillingMetricsService, EfBillingMetricsService>();
builder.Services.AddScoped<IDebtOverviewService, EfDebtOverviewService>();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.ConfigurationSection));
builder.Services.AddHostedService<BillingPlanSeedHostedService>();
builder.Services.AddScoped<IJobRunRecorder, EfJobRunRecorder>();
builder.Services.AddScoped<IPlatformIncidentService, EfPlatformIncidentService>();
builder.Services.Configure<PlatformAlertOptions>(
    builder.Configuration.GetSection(PlatformAlertOptions.ConfigurationSection));
builder.Services.AddScoped<IPlatformAlertNotifier, PlatformAlertNotifier>();
builder.Services.AddHostedService<InvoiceGenerationHostedService>();
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.ConfigurationSection));
builder.Services.AddSingleton<INotificationRenderer, NotificationRenderer>();
builder.Services.AddSingleton<ITemplateProvider>(provider =>
    new EmbeddedTemplateProvider(provider.GetRequiredService<IOptions<NotificationOptions>>().Value.DefaultLocale));
builder.Services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
builder.Services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
builder.Services.Configure<SmsOptions>(
    builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.AddHttpClient(SmsClientRegistration.HttpClientName, (provider, http) =>
{
    var smsOptions = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(smsOptions.BaseUrl))
    {
        http.BaseAddress = new Uri(smsOptions.BaseUrl);
    }

    http.Timeout = TimeSpan.FromSeconds(smsOptions.TimeoutSeconds);
});
builder.Services.AddSingleton<ISmsTransport>(provider =>
{
    var smsOptions = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    return new PayomSmsTransport(
        factory.CreateClient(SmsClientRegistration.HttpClientName),
        smsOptions.ApiToken,
        smsOptions.SenderName);
});
builder.Services.AddSingleton<INotificationChannel, SmsChannel>();
builder.Services.AddScoped<INotificationOutbox, EfNotificationOutbox>();
builder.Services.AddScoped<INotificationPreferenceService, EfNotificationPreferenceService>();
builder.Services.AddScoped<NotificationDispatchRunner>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStaffPasswordResetService, EfStaffPasswordResetService>();
builder.Services.Configure<PhoneOtpOptions>(
    builder.Configuration.GetSection(PhoneOtpOptions.SectionName));
builder.Services.AddSingleton<IPhoneOtpHasher, Sha256PhoneOtpHasher>();
builder.Services.AddSingleton<IPhoneOtpGenerator, RandomPhoneOtpGenerator>();
builder.Services.AddScoped<IStaffPhoneVerificationService, EfStaffPhoneVerificationService>();
builder.Services.AddScoped<IStaffPhonePasswordResetService, EfStaffPhonePasswordResetService>();
builder.Services.AddScoped<IStaffInviteService, EfStaffInviteService>();
builder.Services.AddScoped<IDailySummaryRunner, EfDailySummaryRunner>();
builder.Services.Configure<AFK4.Platform.Api.Reports.BusinessDayOptions>(builder.Configuration.GetSection("BusinessDay"));
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
builder.Services.AddScoped<IOrganizationStatusGuard, EfOrganizationStatusGuard>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IAuditRecordStager, AuditRecordStager>();
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
builder.Services.AddScoped<IInventoryCostService, EfInventoryCostService>();
builder.Services.AddScoped<IPosSettlementService, EfPosSettlementService>();
builder.Services.AddScoped<IPosService, EfPosService>();
builder.Services.AddScoped<IShopPosSettlementService, EfShopPosSettlementService>();
builder.Services.AddScoped<AFK4.Platform.Api.Commerce.IShopCommerceCoordinator, AFK4.Platform.Api.Commerce.EfShopCommerceCoordinator>();
builder.Services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
builder.Services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
builder.Services.AddScoped<IReportService, EfReportService>();
builder.Services.AddScoped<IOrganizationAdminReportService, OrganizationAdminReportService>();
builder.Services.AddScoped<IReportScheduleService, EfReportScheduleService>();
builder.Services.AddScoped<IOperatorDashboardService, EfOperatorDashboardService>();
builder.Services.AddScoped<IReservationService, EfReservationService>();
builder.Services.AddScoped<IReservationSessionCoordinator, EfReservationSessionCoordinator>();
builder.Services.Configure<SessionLeaseOptions>(builder.Configuration.GetSection("Sessions"));
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.ConfigurationSection));
builder.Services.AddScoped<ISessionLeaseSigner, EcdsaSessionLeaseSigner>();
builder.Services.AddScoped<IHeartbeatSessionCommandPlanner, EfHeartbeatSessionCommandPlanner>();
builder.Services.AddScoped<ISessionLifecycleNotifier, SignalRSessionLifecycleNotifier>();
builder.Services.AddScoped<IShopOrderService, EfShopOrderService>();
builder.Services.AddScoped<IShopOrderWorkflow, EfShopOrderWorkflow>();
builder.Services.AddScoped<IShopOrderNotifier, SignalRShopOrderNotifier>();
builder.Services.AddScoped<ISessionStartWorkflow, EfSessionStartWorkflow>();
builder.Services.AddScoped<ISessionCommandService, EfSessionCommandService>();
builder.Services.AddScoped<ISessionCheckoutService, EfSessionCheckoutService>();
builder.Services.AddSingleton(new AutoProtectionOptions());
builder.Services.AddScoped<AutoProtectionRunner>();
builder.Services.AddScoped<ISessionCommandResultProcessor, EfSessionCommandResultProcessor>();
builder.Services.AddScoped<IBillingCommandService, EfBillingCommandService>();
builder.Services.AddScoped<IWalletSettlementService, EfWalletSettlementService>();
builder.Services.AddScoped<ILoyaltyAccrualService, LoyaltyAccrualService>();
builder.Services.AddScoped<INewsService, EfNewsService>();
builder.Services.AddScoped<IMoneyActionPolicyResolver, EfMoneyActionPolicyResolver>();
builder.Services.AddScoped<IMoneyActionExecutor, EfMoneyActionExecutor>();
builder.Services.AddScoped<IMoneyActionApprovalService, MoneyActionApprovalService>();
builder.Services.AddScoped<ITariffService, EfTariffService>();
builder.Services.AddScoped<IPackageService, EfPackageService>();
builder.Services.AddScoped<ISessionBillingService, SessionBillingService>();
builder.Services.AddScoped<IOperatorReferenceDataService, EfOperatorReferenceDataService>();
builder.Services.AddScoped<IUpdateService, EfUpdateService>();
builder.Services.AddScoped<IPlatformUpdateReleaseService, EfPlatformUpdateReleaseService>();

builder.Services.Configure<SecretProtectionOptions>(
    builder.Configuration.GetSection(SecretProtectionOptions.SectionName));
builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

var mediaSection = builder.Configuration.GetSection(MediaOptions.SectionName);
builder.Services.Configure<MediaOptions>(mediaSection);
// Singleton: AmazonS3Client is thread-safe and owns an HttpClient/connection pool; a per-request
// (Scoped) client would leak handlers/sockets under load. MediaOptions is read once at construction.
builder.Services.AddSingleton<IMediaStorage, MinioMediaStorage>();
builder.Services.AddScoped<IMediaService, EfMediaService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    // Track Media:MaxBytes (not a hardcoded literal) + headroom for multipart framing, so raising
    // MaxBytes via config actually widens uploads instead of hitting a stale framework-level 400
    // before EfMediaService's own size check. Kestrel's MaxRequestBodySize (30 MB default) stays
    // above this for the current default; revisit if MaxBytes is configured near/above ~28 MB.
    var maxBytes = mediaSection.Get<MediaOptions>()?.MaxBytes ?? new MediaOptions().MaxBytes;
    options.MultipartBodyLengthLimit = maxBytes + 2 * 1024 * 1024;
});

builder.Services.AddHttpClient(EskhataMerchantClientFactory.HttpClientName);
builder.Services.AddScoped<IEskhataMerchantClientFactory, EskhataMerchantClientFactory>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("player-public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("player-me", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers.Authorization.ToString() is { Length: > 0 } auth
                ? auth
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("staff-reset", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

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
app.UseRateLimiter();
app.UseMiddleware<StaffAuthenticationMiddleware>();
app.UseMiddleware<PlatformAdminAuthenticationMiddleware>();
app.UseMiddleware<PlayerAuthenticationMiddleware>();
app.UseMiddleware<PlatformSupportSessionMiddleware>();
app.UseMiddleware<OrganizationAdminCompatibilityMiddleware>();
app.UseMiddleware<AuthenticationDomainEnforcementMiddleware>();
app.UseMiddleware<OrganizationSuspensionMiddleware>();

// Endpoint registrations are grouped by domain in Endpoints/*Endpoints.cs
var organizations = app.MapGroup("/api/organizations/{organizationId:guid}")
    .RequireOrganizationDomain();

app.MapHealthEndpoints();
organizations.MapFloorMapEndpoints();
organizations.MapBranchSettingsEndpoints();
organizations.MapMediaEndpoints();
app.MapAuthEndpoints(organizations);
organizations.MapEskhataConfigEndpoints();
app.MapEskhataPaymentEndpoints();
organizations.MapDcConfigEndpoints();
organizations.MapDcTopUpEndpoints();
organizations.MapLoyaltySettingsEndpoints();
organizations.MapNewsEndpoints();
app.MapPlayerSelfServiceEndpoints();
app.MapPlayerCatalogEndpoints();
app.MapPlayerShopEndpoints();
app.MapPlayerLoyaltyEndpoints();
app.MapPlayerNewsEndpoints();
organizations.MapShopOrderEndpoints();
organizations.MapWalletEndpoints();
app.MapStaffOnboardingEndpoints(organizations);
organizations.MapReportScheduleEndpoints();
app.MapPlatformOrganizationEndpoints();
app.MapPlatformAdminDirectoryEndpoints();
app.MapPlatformAdminTwoFactorEndpoints();
app.MapPlatformBillingEndpoints(organizations);
app.MapPlatformDebtEndpoints();
app.MapPlatformSupportAccessEndpoints();
app.MapSupportAccessSessionEndpoints();
app.MapPlatformUpdateEndpoints();
app.MapPlatformAuditEndpoints();
app.MapPlatformSearchEndpoints();
app.MapPlatformPulseEndpoints();
organizations.MapOrganizationAuditEndpoints();
organizations.MapStaffEndpoints();
organizations.MapBranchProfileLayoutEndpoints();
organizations.MapSessionEndpoints();
app.MapDeviceEndpoints(organizations);
organizations.MapPlayerManagementEndpoints();
organizations.MapMoneyActionEndpoints();
organizations.MapTariffEndpoints();
organizations.MapPackageEndpoints();
organizations.MapShiftEndpoints();
organizations.MapDashboardEndpoints();
organizations.MapReservationEndpoints();
organizations.MapReportEndpoints();
organizations.MapOrganizationAdminReportEndpoints();
organizations.MapDiagnosticsEndpoints();
organizations.MapPosEndpoints();
app.MapUpdateEndpoints(organizations);

app.Run();

public partial class Program;
