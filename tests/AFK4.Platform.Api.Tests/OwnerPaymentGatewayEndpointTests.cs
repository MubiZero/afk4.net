using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.DcGate;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerPaymentGatewayEndpointTests
{
    // Seeds a gateway row directly (encrypting creds) for list/status tests.
    private static async Task<Guid> SeedGatewayAsync(
        PlatformApiFactory factory, Guid orgId, Guid? branchId, string projectId, string status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var id = Guid.NewGuid();
        db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
        {
            BranchPaymentGatewayId = id,
            OrganizationId = orgId,
            BranchId = branchId,
            DcgateProjectId = projectId,
            ApiKeyEncrypted = protector.Protect("key"),
            WebhookSecretEncrypted = protector.Protect("whsec"),
            CardLast4 = "4242",
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task List_returns_only_callers_org_gateways_for_owner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        await SeedGatewayAsync(factory, orgId, branchId: null, "proj_mine", "active");
        await SeedGatewayAsync(factory, Guid.NewGuid(), branchId: null, "proj_other", "active");

        var response = await owner.GetAsync("/api/owner/payment-gateways");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OwnerPaymentGatewayListResponse>();
        Assert.Single(body!.Gateways);
        Assert.Equal("proj_mine", body.Gateways[0].DcgateProjectId);
        Assert.Equal("4242", body.Gateways[0].CardLast4);
    }

    [Fact]
    public async Task List_returns_403_for_non_owner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var response = await nonOwner.GetAsync("/api/owner/payment-gateways");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Task 8: provision ----

    private sealed class FakeAdminClient : IDcGateAdminClient
    {
        public DcGateCreateProjectRequest? LastCreate;
        public DcGateAdminProjectResult CreateResult =
            new("proj_new", "pending_telegram", "4242", "key_live", "whsec_x", false);

        public Task<DcGateAdminProjectResult> CreateProjectAsync(DcGateCreateProjectRequest request, CancellationToken ct)
        { LastCreate = request; return Task.FromResult(CreateResult); }
        public Task<DcGateTelegramStartResult> StartTelegramAsync(string p, string phone, CancellationToken ct)
            => Task.FromResult(new DcGateTelegramStartResult("att", "code_required"));
        public Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(string p, string a, string c, CancellationToken ct)
            => Task.FromResult(new DcGateTelegramVerifyResult("attached"));
        public Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(string p, string a, string pw, CancellationToken ct)
            => Task.FromResult(new DcGateTelegramVerifyResult("attached"));
        public Task<DcGateProjectStatusResult> GetStatusAsync(string p, CancellationToken ct)
            => Task.FromResult(new DcGateProjectStatusResult("online", null, null, 0));
    }

    private static PlatformApiFactory FactoryWithAdmin(FakeAdminClient fake) =>
        new(extraServices: services =>
        {
            services.RemoveAll<IDcGateAdminClient>();
            services.AddSingleton<IDcGateAdminClient>(fake);
            ConfigureProvisioning(services);
        });

    // Provisioning is fail-safe off unless AdminSecret + WebhookUrl are set.
    private static void ConfigureProvisioning(IServiceCollection services) =>
        services.PostConfigure<DcGateOptions>(options =>
        {
            options.AdminSecret = "admin-secret-test";
            options.WebhookUrl = "https://afk4.test/api/public/payments/dcgate/webhook";
        });

    [Fact]
    public async Task Provision_persists_encrypted_creds_and_returns_pending_row()
    {
        var fake = new FakeAdminClient();
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
            new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OwnerPaymentGatewayDto>();
        Assert.Equal("pending_telegram", dto!.Status);
        Assert.Equal("4242", dto.CardLast4);
        Assert.Equal("proj_new", dto.DcgateProjectId);

        // externalId must equal the persisted PK (idempotency contract).
        Assert.Equal(dto.BranchPaymentGatewayId.ToString(), fake.LastCreate!.ExternalId);

        // creds stored encrypted, not plaintext.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.BranchPaymentGateways.FindAsync(dto.BranchPaymentGatewayId);
        Assert.NotNull(row);
        Assert.NotEqual("key_live", row!.ApiKeyEncrypted);
        Assert.NotEqual("whsec_x", row.WebhookSecretEncrypted);
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        Assert.Equal("key_live", protector.Unprotect(row.ApiKeyEncrypted));
        Assert.Equal("whsec_x", protector.Unprotect(row.WebhookSecretEncrypted));
    }

    [Fact]
    public async Task Provision_rejects_second_gateway_for_same_scope()
    {
        var fake = new FakeAdminClient();
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        await SeedGatewayAsync(factory, orgId, branchId: null, "proj_existing", "pending_telegram");

        var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
            new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Provision_relays_dcgate_4xx_and_persists_nothing()
    {
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IDcGateAdminClient>();
            services.AddSingleton<IDcGateAdminClient>(new ThrowingAdminClient());
            ConfigureProvisioning(services);
        });
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
            new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("card already in use", body);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(db.BranchPaymentGateways.ToList());
    }

    [Fact]
    public async Task Provision_503_when_provisioning_unconfigured()
    {
        // No ConfigureProvisioning() -> AdminSecret/WebhookUrl empty -> fail-safe off.
        // Still register the fake admin client so DI resolves the endpoint dependencies.
        var fake = new FakeAdminClient();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IDcGateAdminClient>();
            services.AddSingleton<IDcGateAdminClient>(fake);
        });
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
            new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class ThrowingAdminClient : IDcGateAdminClient
    {
        public Task<DcGateAdminProjectResult> CreateProjectAsync(DcGateCreateProjectRequest r, CancellationToken ct)
            => throw new DcGateAdminException(HttpStatusCode.BadRequest, "card already in use");
        public Task<DcGateTelegramStartResult> StartTelegramAsync(string p, string phone, CancellationToken ct) => throw new NotImplementedException();
        public Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(string p, string a, string c, CancellationToken ct) => throw new NotImplementedException();
        public Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(string p, string a, string pw, CancellationToken ct) => throw new NotImplementedException();
        public Task<DcGateProjectStatusResult> GetStatusAsync(string p, CancellationToken ct) => throw new NotImplementedException();
    }

    // ---- Task 9: telegram attach ----

    [Fact]
    public async Task TelegramStart_proxies_and_returns_attempt()
    {
        var fake = new FakeAdminClient();
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

        var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
            new TelegramStartRequest("+992900000000"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TelegramStartResponse>();
        Assert.Equal("code_required", body!.State);
    }

    [Fact]
    public async Task VerifyCode_attached_flips_gateway_to_active()
    {
        var fake = new FakeAdminClient(); // verify-code returns "attached"
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

        var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/verify-code",
            new TelegramVerifyCodeRequest("att", "12345"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TelegramVerifyResponse>();
        Assert.Equal("attached", body!.State);
        Assert.Equal("active", body.GatewayStatus);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.BranchPaymentGateways.FindAsync(id);
        Assert.Equal("active", row!.Status);
    }

    [Fact]
    public async Task VerifyPassword_attached_flips_gateway_to_active()
    {
        var fake = new FakeAdminClient(); // verify-password returns "attached"
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

        var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/verify-password",
            new TelegramVerifyPasswordRequest("att", "2fa-pass"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TelegramVerifyResponse>();
        Assert.Equal("attached", body!.State);
        Assert.Equal("active", body.GatewayStatus);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.BranchPaymentGateways.FindAsync(id);
        Assert.Equal("active", row!.Status);
    }

    [Fact]
    public async Task TelegramStart_404_for_other_orgs_gateway()
    {
        var fake = new FakeAdminClient();
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        var foreignId = await SeedGatewayAsync(factory, Guid.NewGuid(), null, "proj_foreign", "pending_telegram");

        var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{foreignId}/telegram/start",
            new TelegramStartRequest("+992900000000"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Task 10: status ----

    [Fact]
    public async Task Status_proxies_dcgate_session_health()
    {
        var fake = new FakeAdminClient(); // GetStatusAsync returns ("online", ...)
        await using var factory = FactoryWithAdmin(fake);
        var client = factory.CreateClient();
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "active");

        var response = await owner.GetAsync($"/api/owner/payment-gateways/{id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OwnerGatewayStatusResponse>();
        Assert.Equal("online", body!.SessionHealth);
        Assert.Equal("active", body.GatewayStatus);
    }
}
