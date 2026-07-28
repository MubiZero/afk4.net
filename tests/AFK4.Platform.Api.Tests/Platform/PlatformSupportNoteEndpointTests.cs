using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
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
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Owner called about login issue"));
        var body = await response.Content.ReadFromJsonAsync<OrganizationSupportNoteDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organizationId, body.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, body.AuthorPlatformAdminId);
        Assert.Equal("Owner called about login issue", body.Body);
        Assert.False(string.IsNullOrWhiteSpace(body.AuthorDisplayName));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var note = await dbContext.OrganizationSupportNotes.SingleAsync(n => n.OrganizationId == organizationId);
        Assert.Equal("Owner called about login issue", note.Body);
        Assert.Equal(admin.PlatformAdminId, note.AuthorPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(record =>
            record.Action == AuditActionNames.CreateOrganizationSupportNote &&
            record.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(note.OrganizationSupportNoteId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostSupportNote_WithEmptyBody_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_OnUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Stray note"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/support-notes",
            new CreateOrganizationSupportNoteRequest("hi"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSupportNote_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/support-notes",
            new CreateOrganizationSupportNoteRequest("hi"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSupportNotes_ReturnsNotesDescendingByCreatedAt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);

        var first = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("First note"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Second note"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var listResponse = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/support-notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<List<OrganizationSupportNoteDto>>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(notes);
        Assert.Equal(2, notes.Count);
        Assert.True(notes[0].CreatedAtUtc >= notes[1].CreatedAtUtc);
    }

    [Fact]
    public async Task GetSupportNotes_OnUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/support-notes");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_UpdatesBodyAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Initial"));
        var created = await create.Content.ReadFromJsonAsync<OrganizationSupportNoteDto>();
        Assert.NotNull(created);

        var update = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes/{created.OrganizationSupportNoteId:D}",
            new UpdateOrganizationSupportNoteRequest("Updated body with more details"));
        var updated = await update.Content.ReadFromJsonAsync<OrganizationSupportNoteDto>();

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Updated body with more details", updated.Body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var note = await dbContext.OrganizationSupportNotes.SingleAsync(n => n.OrganizationSupportNoteId == created.OrganizationSupportNoteId);
        Assert.Equal("Updated body with more details", note.Body);
        var audit = await dbContext.AuditRecords.SingleAsync(record =>
            record.Action == AuditActionNames.UpdateOrganizationSupportNote &&
            record.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(note.OrganizationSupportNoteId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PatchSupportNote_WithUnknownNote_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes/{Guid.NewGuid():D}",
            new UpdateOrganizationSupportNoteRequest("Edit"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_RejectsCrossOrganizationNote()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationA = await CreateOrganizationAsync(client, orgSlug: "club-a", branchSlug: "main");
        var organizationB = await CreateOrganizationAsync(client, orgSlug: "club-b", branchSlug: "main");

        var create = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationA:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Organization A note"));
        var created = await create.Content.ReadFromJsonAsync<OrganizationSupportNoteDto>();
        Assert.NotNull(created);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationB:D}/support-notes/{created.OrganizationSupportNoteId:D}",
            new UpdateOrganizationSupportNoteRequest("Should fail"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchSupportNote_WithEmptyBody_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes",
            new CreateOrganizationSupportNoteRequest("Some note"));
        var created = await create.Content.ReadFromJsonAsync<OrganizationSupportNoteDto>();
        Assert.NotNull(created);

        var update = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/support-notes/{created.OrganizationSupportNoteId:D}",
            new UpdateOrganizationSupportNoteRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string orgSlug = "demo-club", string branchSlug = "main")
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            new CreateOrganizationRequest(
                OrganizationSlug: orgSlug,
                OrganizationName: "Demo Club",
                BranchSlug: branchSlug,
                BranchName: "Main Branch",
                BranchCity: "Dushanbe",
                PlanCode: OrganizationPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new OrganizationLimitsDto(1, 20, 30, 5),
                OwnerUserName: null,
                OwnerDisplayName: null,
                OrganizationOwnerInviteLifetime: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }
}
