using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessPersistenceTests
{
    [Fact]
    public async Task Grant_RoundTripsTicketAndSessionHashes()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var grantId = Guid.NewGuid();
        db.PlatformSupportAccessGrants.Add(new PlatformSupportAccessGrantEntity
        {
            GrantId = grantId,
            PlatformAdminUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Reason = "Клуб сообщает, что не открывается смена",
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            TicketHash = [1, 2, 3],
            SessionTokenHash = [4, 5, 6]
        });
        await db.SaveChangesAsync();

        var stored = await db.PlatformSupportAccessGrants.SingleAsync(g => g.GrantId == grantId);

        Assert.Equal(new byte[] { 1, 2, 3 }, stored.TicketHash);
        Assert.Equal(new byte[] { 4, 5, 6 }, stored.SessionTokenHash);
        Assert.Null(stored.TicketUsedAtUtc);
    }
}
