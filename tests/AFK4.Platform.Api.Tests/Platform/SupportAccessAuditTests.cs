using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessAuditTests
{
    [Fact]
    public async Task ReadUnderSupport_RecordsPlatformAdminAsActor()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);
        await client.GetAsync($"/api/organizations/{organizationId}/branches/{branchId}/settings");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstAsync(candidate => candidate.OrganizationId == organizationId);

        Assert.NotNull(record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);
    }
}
