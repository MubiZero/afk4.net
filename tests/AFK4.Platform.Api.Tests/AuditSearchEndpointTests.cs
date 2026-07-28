using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class AuditSearchEndpointTests
{
    [Fact]
    public async Task GetAuditRecords_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditRecords_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewAudit, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task GetAuditRecords_WithAuditorPermission_ReturnsFilteredBranchRecordsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);
        var older = await SeedAuditAsync(
            factory,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"));
        var newer = await SeedAuditAsync(
            factory,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            DateTimeOffset.Parse("2026-05-14T10:00:00Z"));
        await SeedAuditAsync(
            factory,
            AuditActionNames.EndSession,
            AuditOutcome.Succeeded,
            DateTimeOffset.Parse("2026-05-14T11:00:00Z"));
        await SeedAuditAsync(
            factory,
            AuditActionNames.StartSession,
            AuditOutcome.Denied,
            DateTimeOffset.Parse("2026-05-14T12:00:00Z"));

        var response = await client.GetAsync(
            $"/api/branches/{TestIds.BranchId}/audit?action={AuditActionNames.StartSession}&outcome={AuditOutcome.Succeeded}&limit=2");
        var result = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(2, result.Limit);
        Assert.Collection(
            result.Records,
            first => Assert.Equal(newer.AuditRecordId, first.AuditRecordId),
            second => Assert.Equal(older.AuditRecordId, second.AuditRecordId));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var viewAudit = await dbContext.AuditRecords
            .Where(audit => audit.Action == AuditActionNames.ViewAudit)
            .SingleAsync();
        Assert.Equal(AuditOutcome.Succeeded, viewAudit.Outcome);
    }

    private static async Task<AuditRecordEntity> SeedAuditAsync(
        PlatformApiFactory factory,
        string action,
        string outcome,
        DateTimeOffset createdAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ActorStaffUserId = TestIds.TechnicianStaffUserId,
            Action = action,
            TargetType = "Session",
            TargetId = Guid.NewGuid().ToString("D"),
            Outcome = outcome,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = createdAtUtc
        };

        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }
}
