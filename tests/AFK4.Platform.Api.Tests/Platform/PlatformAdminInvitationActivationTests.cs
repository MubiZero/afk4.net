using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminInvitationActivationTests
{
    [Fact]
    public async Task ValidCode_CreatesActiveAdminWithInvitedRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();

        using var anonymous = factory.CreateClient();
        var accepted = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await db.PlatformAdminUsers.SingleAsync(x => x.NormalizedUserName == "SUPPORT1");
        Assert.True(user.IsActive);
        Assert.Contains(PlatformAdminRoleNames.PlatformSupport, user.RolesJson);
    }

    [Fact]
    public async Task CodeCannotBeUsedTwice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        using var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        var second = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation.Code, "support2", "Вторая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ExpiredCode_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.PlatformAdminInvitations.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Regression for a code-review finding: the AcceptInvitationAsync username-taken check
    // (AnyAsync over NormalizedUserName) is read-then-write. Two concurrent accepts for TWO
    // DIFFERENT valid invitations that both request the same login can each pass that check before
    // either commits, so only the unique index on NormalizedUserName actually stops the second write
    // — as a raw DbUpdateException unless RelationalFailureClassifier.IsUniqueViolation catches it.
    // The InMemory provider used by every other test in this file has no real unique-constraint
    // enforcement under concurrent writers, so this needs real PostgreSQL with the same deterministic
    // overlap gate as
    // PlatformAdminDirectoryServiceTests.ConcurrentUpdate_DisablingTwoDifferentFullAdmins_LeavesAtLeastOneActiveFullAdmin.
    // The endpoint (/api/account-activation/platform-admin, see PlatformAdminDirectoryEndpoints)
    // maps PlatformAdminDirectoryError.UserNameTaken to HTTP 409 — asserted separately by
    // Invitation_ReturnsCodeOnceAndStoresOnlyHash's sibling HTTP-level tests in this file, so here
    // we assert directly on the service-level error the endpoint switches on.
    [PlatformAdminPostgresFact]
    public async Task ConcurrentAccept_TwoDifferentInvitationsRequestingSameUserName_OnlyOneSucceeds()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_admin_invitation_race_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(rootBuilder.ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var gate = new SaveOverlapGate();
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .AddInterceptors(gate)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var now = DateTimeOffset.Parse("2026-08-04T08:00:00Z");
            var creatorId = Guid.NewGuid();
            string codeForFirst;
            string codeForSecond;
            await using (var seedDb = new PlatformDbContext(options))
            {
                // InviteAsync validates the role against the database now, not a static catalog —
                // this raw-context test bypasses PlatformApiFactory's seeding, so seed the declared
                // roles here the same way PlatformRoleSeedHostedService would on a real startup.
                foreach (var declaration in PlatformRoleCatalog.Declared)
                {
                    seedDb.PlatformRoles.Add(new PlatformRoleEntity
                    {
                        RoleName = declaration.RoleName,
                        DisplayName = declaration.DisplayName,
                        Description = declaration.Description,
                        IsBuiltIn = true,
                        GrantsAllPermissions = declaration.GrantsAllPermissions,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                    foreach (var permissionName in declaration.Permissions)
                    {
                        seedDb.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
                        {
                            PlatformRolePermissionId = Guid.NewGuid(),
                            RoleName = declaration.RoleName,
                            PermissionName = permissionName
                        });
                    }
                }
                await seedDb.SaveChangesAsync();

                var seedService = new PlatformAdminDirectoryService(
                    seedDb, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(seedDb));
                var (first, firstError) = await seedService.InviteAsync(creatorId,
                    new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72), CancellationToken.None);
                var (second, secondError) = await seedService.InviteAsync(creatorId,
                    new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72), CancellationToken.None);
                Assert.Equal(PlatformAdminDirectoryError.None, firstError);
                Assert.Equal(PlatformAdminDirectoryError.None, secondError);
                codeForFirst = first!.Code;
                codeForSecond = second!.Code;
            }

            gate.Arm();
            await using var dbForFirst = new PlatformDbContext(options);
            await using var dbForSecond = new PlatformDbContext(options);
            var serviceForFirst = new PlatformAdminDirectoryService(
                dbForFirst, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbForFirst));
            var serviceForSecond = new PlatformAdminDirectoryService(
                dbForSecond, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbForSecond));

            var results = await Task.WhenAll(
                serviceForFirst.AcceptInvitationAsync(
                    new AcceptPlatformAdminInvitationRequest(codeForFirst, "racer", "Гонщик 1", "S3cret!passphrase"),
                    CancellationToken.None),
                serviceForSecond.AcceptInvitationAsync(
                    new AcceptPlatformAdminInvitationRequest(codeForSecond, "racer", "Гонщик 2", "S3cret!passphrase"),
                    CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(results, result => result.Error == PlatformAdminDirectoryError.None);
            Assert.Single(results, result => result.Error == PlatformAdminDirectoryError.UserNameTaken);

            await using var verifyDb = new PlatformDbContext(options);
            var matchingUsers = await verifyDb.PlatformAdminUsers.AsNoTracking()
                .Where(admin => admin.NormalizedUserName == "RACER")
                .ToArrayAsync();
            Assert.Single(matchingUsers);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together — the same pattern as
    // PlatformAdminDirectoryServiceTests.SaveOverlapGate, duplicated here because that one is
    // private to its own test class. Forces both AcceptInvitationAsync calls to have already run
    // their AnyAsync(NormalizedUserName) check (both seeing the login as free) before either is
    // allowed to flush its INSERT, instead of hoping Task.WhenAll happens to interleave two real
    // round trips unluckily.
    private sealed class SaveOverlapGate : SaveChangesInterceptor
    {
        private TaskCompletionSource? bothSavesReached;
        private int saveCount;
        private int armed;

        public void Arm()
        {
            bothSavesReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref saveCount, 0);
            Volatile.Write(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 0)
            {
                return result;
            }

            var reached = Interlocked.Increment(ref saveCount);
            if (reached == 2)
            {
                Volatile.Write(ref armed, 0);
                bothSavesReached!.TrySetResult();
            }

            await bothSavesReached!.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
