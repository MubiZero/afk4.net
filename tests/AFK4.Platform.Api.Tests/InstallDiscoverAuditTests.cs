using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class InstallDiscoverAuditTests
{
    [Fact]
    public async Task POST_install_discover_writes_audit_record()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.PostAsync("/api/install/auth/discover", null);
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords.SingleAsync(r => r.Action == AuditActionNames.InstallDiscoverInvoked);
        Assert.Equal(TestIds.OrganizationId, record.OrganizationId);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
    }
}
