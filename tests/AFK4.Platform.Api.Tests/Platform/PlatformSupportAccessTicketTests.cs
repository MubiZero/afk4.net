using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessTicketTests
{
    [Fact]
    public async Task RedeemTicket_Twice_SucceedsOnlyOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            Guid.NewGuid(),
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Смена не открывается у клуба", 30),
            CancellationToken.None);

        Assert.NotNull(issue);

        var first = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);
        var second = await service.RedeemTicketAsync(issue.Ticket, CancellationToken.None);

        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.SessionToken));
        Assert.Null(second);
    }

    [Fact]
    public async Task AuthenticateSession_AfterRevocation_Fails()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var adminId = Guid.NewGuid();
        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            adminId,
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Устройство не видно в списке", 30),
            CancellationToken.None);
        var session = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);

        var before = await service.AuthenticateSessionAsync(
            session!.SessionToken, "organization.branch_settings.manage", CancellationToken.None);
        await service.RevokeAsync(issue.Grant.GrantId, adminId, CancellationToken.None);
        var after = await service.AuthenticateSessionAsync(
            session.SessionToken, "organization.branch_settings.manage", CancellationToken.None);

        Assert.NotNull(before);
        Assert.Null(after);
    }

    private static async Task<Guid> SeedOrganizationAsync(PlatformDbContext db)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = $"club-{organizationId:N}",
            Name = "Тестовый клуб",
            Status = "active",
            PlanCode = "starter",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return organizationId;
    }
}
