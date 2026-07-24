using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OrganizationAuditEndpointTests
{
    private static async Task SeedAuditAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var at = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        // branch-scoped record
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Action = "loyalty.settings.updated",
            TargetType = "LoyaltySettings",
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = at
        });
        // org-level record (BranchId == null) — currently unreadable via any endpoint
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = null,
            Action = "news.published",
            TargetType = "News",
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = at.AddMinutes(1)
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GET_org_audit_as_owner_includes_branch_and_org_level_records()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedAuditAsync(factory);

        var result = await client.GetFromJsonAsync<AuditSearchResultDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.NotNull(result);
        Assert.Contains(result!.Records, r => r.Action == "loyalty.settings.updated");
        Assert.Contains(result.Records, r => r.Action == "news.published" && r.BranchId == null);
    }

    [Fact]
    public async Task GET_org_audit_rejects_other_org_with_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_org_audit_as_cashier_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Regression: BranchManager holds branch-scoped audit.view (per-branch) but not the
    // owner-only audit.organization.view — must not be able to read org-wide audit across
    // all branches through this endpoint.
    [Fact]
    public async Task GET_org_audit_as_branch_manager_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
