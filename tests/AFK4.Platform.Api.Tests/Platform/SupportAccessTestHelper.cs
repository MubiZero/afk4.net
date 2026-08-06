using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

// Shared by every test that needs a live platform-support session token: seeds a throwaway
// organization + branch, issues a grant, and redeems its ticket for a session token — the same
// two-step exchange PlatformSupportAccessGrantService.IssueAsync/RedeemTicketAsync perform for a
// real support agent switching tabs.
internal static class SupportAccessTestHelper
{
    public static async Task<(string SessionToken, Guid OrganizationId, Guid BranchId, Guid PlatformAdminUserId)> OpenSessionAsync(
        PlatformApiFactory factory,
        string reason = "Проверка настроек филиала по обращению клиента")
    {
        var (organizationId, branchId, platformAdminUserId, issue) = await IssueGrantAsync(factory, reason);

        await using var issueScope = factory.Services.CreateAsyncScope();
        var supportAccessService = issueScope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();
        var session = await supportAccessService.RedeemTicketAsync(issue.Ticket, CancellationToken.None);
        Assert.NotNull(session);

        return (session!.SessionToken, organizationId, branchId, platformAdminUserId);
    }

    // For tests exercising the redemption endpoint itself (POST /api/public/support-access/sessions):
    // returns just the one-time ticket, unclaimed.
    public static async Task<string> IssueTicketAsync(
        PlatformApiFactory factory,
        string reason = "Проверка настроек филиала по обращению клиента")
    {
        var (_, _, _, issue) = await IssueGrantAsync(factory, reason);
        return issue.Ticket;
    }

    private static async Task<(Guid OrganizationId, Guid BranchId, Guid PlatformAdminUserId, PlatformSupportAccessGrantIssue Issue)> IssueGrantAsync(
        PlatformApiFactory factory,
        string reason)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var platformAdminUserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = organizationId,
                Slug = $"club-{organizationId:N}",
                Name = "Тестовый клуб поддержки",
                Status = "active",
                PlanCode = "starter",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = branchId,
                OrganizationId = organizationId,
                Slug = $"branch-{branchId:N}",
                Name = "Тестовый филиал",
                City = "Душанбе",
                CreatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        await using var issueScope = factory.Services.CreateAsyncScope();
        var supportAccessService = issueScope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var issue = await supportAccessService.IssueAsync(
            platformAdminUserId,
            new CreatePlatformSupportAccessGrantRequest(organizationId, reason, 30),
            CancellationToken.None);
        Assert.NotNull(issue);

        return (organizationId, branchId, platformAdminUserId, issue!);
    }
}
