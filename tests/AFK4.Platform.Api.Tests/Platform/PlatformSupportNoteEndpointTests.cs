using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportNoteEndpointTests
{
    [Fact]
    public async Task PostSupportNote_WithValidBody_PersistsAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest("Owner called about login issue"));
        var body = await response.Content.ReadFromJsonAsync<TenantSupportNoteDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organizationId, body.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, body.AuthorPlatformAdminId);
        Assert.Equal("Owner called about login issue", body.Body);
        Assert.False(string.IsNullOrWhiteSpace(body.AuthorDisplayName));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var note = await dbContext.TenantSupportNotes.SingleAsync(n => n.OrganizationId == organizationId);
        Assert.Equal("Owner called about login issue", note.Body);
        Assert.Equal(admin.PlatformAdminId, note.AuthorPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(record =>
            record.Action == AuditActionNames.CreateTenantSupportNote &&
            record.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(note.TenantSupportNoteId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostSupportNote_WithEmptyBody_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_OnUnknownTenant_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/support-notes",
            new CreateTenantSupportNoteRequest("Stray note"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/support-notes",
            new CreateTenantSupportNoteRequest("hi"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/support-notes",
            new CreateTenantSupportNoteRequest("hi"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSupportNotes_ReturnsNotesDescendingByCreatedAt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var first = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest("First note"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest("Second note"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var listResponse = await client.GetAsync($"/api/platform/tenants/{organizationId:D}/support-notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<List<TenantSupportNoteDto>>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(notes);
        Assert.Equal(2, notes.Count);
        Assert.True(notes[0].CreatedAtUtc >= notes[1].CreatedAtUtc);
    }

    [Fact]
    public async Task GetSupportNotes_OnUnknownTenant_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/support-notes");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_UpdatesBodyAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest("Initial"));
        var created = await create.Content.ReadFromJsonAsync<TenantSupportNoteDto>();
        Assert.NotNull(created);

        var update = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes/{created.TenantSupportNoteId:D}",
            new UpdateTenantSupportNoteRequest("Updated body with more details"));
        var updated = await update.Content.ReadFromJsonAsync<TenantSupportNoteDto>();

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Updated body with more details", updated.Body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var note = await dbContext.TenantSupportNotes.SingleAsync(n => n.TenantSupportNoteId == created.TenantSupportNoteId);
        Assert.Equal("Updated body with more details", note.Body);
        var audit = await dbContext.AuditRecords.SingleAsync(record =>
            record.Action == AuditActionNames.UpdateTenantSupportNote &&
            record.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(note.TenantSupportNoteId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PatchSupportNote_WithUnknownNote_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes/{Guid.NewGuid():D}",
            new UpdateTenantSupportNoteRequest("Edit"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_RejectsCrossTenantNote()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var tenantA = await CreateTenantAsync(client, orgSlug: "club-a", branchSlug: "main");
        var tenantB = await CreateTenantAsync(client, orgSlug: "club-b", branchSlug: "main");

        var create = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantA:D}/support-notes",
            new CreateTenantSupportNoteRequest("Tenant A note"));
        var created = await create.Content.ReadFromJsonAsync<TenantSupportNoteDto>();
        Assert.NotNull(created);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{tenantB:D}/support-notes/{created.TenantSupportNoteId:D}",
            new UpdateTenantSupportNoteRequest("Should fail"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_WithEmptyBody_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes",
            new CreateTenantSupportNoteRequest("Some note"));
        var created = await create.Content.ReadFromJsonAsync<TenantSupportNoteDto>();
        Assert.NotNull(created);

        var update = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/support-notes/{created.TenantSupportNoteId:D}",
            new UpdateTenantSupportNoteRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string orgSlug = "demo-club", string branchSlug = "main")
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantRequest(
                OrganizationSlug: orgSlug,
                OrganizationName: "Demo Club",
                BranchSlug: branchSlug,
                BranchName: "Main Branch",
                BranchCity: "Dushanbe",
                PlanCode: TenantPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new TenantLimitsDto(1, 20, 30, 5),
                OwnerUserName: null,
                OwnerDisplayName: null,
                OrganizationOwnerInviteLifetime: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(body);
        return body.Tenant.OrganizationId;
    }
}
