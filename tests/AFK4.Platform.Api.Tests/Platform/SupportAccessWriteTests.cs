using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessWriteTests
{
    [Fact]
    public async Task BranchSettings_AreWritableUnderSupport()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId, _) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings",
            new UpdateBranchSettingsRequest(organizationId, true, "ru"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("pos/sales")]
    [InlineData("tariffs")]
    public async Task MoneyEndpoints_StayClosedUnderSupport(string suffix)
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId, _) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/{suffix}",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // До сих пор атрибуция аудита под поддержкой (ActorPlatformAdminUserId вместо ActorStaffUserId)
    // проверялась только юнит-тестом стейджера (SupportAccessAuditTests), в обход middleware. Этот
    // тест проходит настоящий HTTP-запрос под сессией поддержки и проверяет, что PUT .../settings —
    // эндпоинт, который и так пишет аудит на успехе — атрибутирует запись именно платформенному
    // администратору, выдавшему grant.
    [Fact]
    public async Task BranchSettingsUpdate_UnderSupport_AttributesAuditToPlatformAdmin()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId, platformAdminUserId) =
            await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings",
            new UpdateBranchSettingsRequest(organizationId, true, "ru"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await dbContext.AuditRecords
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstAsync();

        Assert.Equal(platformAdminUserId, record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);
    }
}
