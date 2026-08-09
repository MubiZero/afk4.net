using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminDirectoryServiceTests
{
    [Fact]
    public async Task DisablingLastFullAdmin_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (item, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(null, false), CancellationToken.None);

        Assert.Null(item);
        Assert.Equal(PlatformAdminDirectoryError.LastFullAdmin, error);
    }

    [Fact]
    public async Task DemotingSelf_IsRejectedEvenWhenAnotherAdminExists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformAdminUsers.Add(new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = "second",
            NormalizedUserName = "SECOND",
            DisplayName = "Второй админ",
            PasswordHash = "x",
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([PlatformAdminRoleNames.PlatformAdmin]),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(PlatformAdminRoleNames.PlatformSupport, null), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.SelfDemotion, error);
    }

    [Fact]
    public async Task Invitation_ReturnsCodeOnceAndStoresOnlyHash()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (response, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.False(string.IsNullOrWhiteSpace(response!.Code));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.DoesNotContain(response.Code, System.Text.Encoding.UTF8.GetString(stored.CodeHash));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvitationLifetime_OutOfRange_IsRejected(int lifetimeHours)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (response, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, lifetimeHours), CancellationToken.None);

        Assert.Null(response);
        Assert.Equal(PlatformAdminDirectoryError.InvalidInvitationLifetime, error);
    }

    [Theory]
    [InlineData(PlatformAdminDirectoryService.MinInvitationLifetimeHours)]
    [InlineData(PlatformAdminDirectoryService.MaxInvitationLifetimeHours)]
    public async Task InvitationLifetime_AtBoundary_IsAccepted(int lifetimeHours)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (response, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, lifetimeHours), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task UnknownRole_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest("platform_god", 24), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.UnknownRole, error);
    }

    [Fact]
    public async Task Update_AcceptsARoleCreatedInThePanel()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedRoleAsync(db, "billing_clerk", grantsAllPermissions: false,
            [PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ViewBilling]);
        var targetId = await SeedAdminAsync(db, "clerk", PlatformAdminRoleNames.PlatformSupport);
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (item, error) = await service.UpdateAsync(admin.PlatformAdminId, targetId,
            new UpdatePlatformAdminRequest("billing_clerk", null), CancellationToken.None);

        // Роль, заведённая в панели, обязана приниматься наравне со встроенной: проверка по
        // захардкоженному списку имён отвергла бы её.
        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.Equal("billing_clerk", item!.Role);
    }

    [Fact]
    public async Task Update_TreatsAnyRoleWithFullAccessAsFullAdmin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedRoleAsync(db, "owner", grantsAllPermissions: true, []);
        var ownerId = await SeedAdminAsync(db, "owner-user", "owner");
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        // Второй носитель полного доступа существует — значит отключение первого допустимо.
        var (item, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest("owner", null), CancellationToken.None);
        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.NotNull(item);

        // А теперь отключаем носителя роли owner, когда сам актор тоже owner: полный доступ
        // остаётся у актора, поэтому это разрешено.
        var (_, disableError) = await service.UpdateAsync(admin.PlatformAdminId, ownerId,
            new UpdatePlatformAdminRequest(null, false), CancellationToken.None);
        Assert.Equal(PlatformAdminDirectoryError.None, disableError);

        // Последний носитель полного доступа — теперь только актор; отключить его нельзя,
        // хотя его роль называется «owner», а не platform_admin.
        var (_, lastError) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(null, false), CancellationToken.None);
        Assert.Equal(PlatformAdminDirectoryError.LastFullAdmin, lastError);
    }

    [Fact]
    public async Task Update_DetectsSelfDemotionByLostPermissions_NotByCount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Две роли с ОДИНАКОВЫМ числом прав и разными наборами. Проверка по количеству прав
        // не увидит здесь понижения вовсе, хотя человек теряет управление администраторами.
        await SeedRoleAsync(db, "left", grantsAllPermissions: false,
            [PlatformAdminPermissionNames.ManagePlatformAdmins, PlatformAdminPermissionNames.ViewOrganizations]);
        await SeedRoleAsync(db, "right", grantsAllPermissions: false,
            [PlatformAdminPermissionNames.ViewBilling, PlatformAdminPermissionNames.ViewOrganizations]);
        await SeedRoleAsync(db, "left_plus", grantsAllPermissions: false,
            [
                PlatformAdminPermissionNames.ManagePlatformAdmins,
                PlatformAdminPermissionNames.ViewOrganizations,
                PlatformAdminPermissionNames.ViewBilling
            ]);
        // Второй полный админ, чтобы инвариант LastFullAdmin не срабатывал раньше самопонижения.
        await SeedAdminAsync(db, "spare", PlatformAdminRoleNames.PlatformAdmin);

        // Ставим актору роль «left» напрямую: перевести себя туда через UpdateAsync нельзя —
        // это и есть самопонижение с полного доступа, а нам нужна стартовая точка внутри
        // двух равных по размеру ролей.
        var actor = await db.PlatformAdminUsers.SingleAsync(row => row.PlatformAdminUserId == admin.PlatformAdminId);
        actor.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["left"]);
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, sidewaysError) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest("right", null), CancellationToken.None);
        Assert.Equal(PlatformAdminDirectoryError.SelfDemotion, sidewaysError);

        var (widened, widenError) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest("left_plus", null), CancellationToken.None);
        Assert.Equal(PlatformAdminDirectoryError.None, widenError);
        Assert.Equal("left_plus", widened!.Role);
    }

    private static async Task SeedRoleAsync(
        PlatformDbContext db,
        string roleName,
        bool grantsAllPermissions,
        IReadOnlyList<string> permissions)
    {
        var now = DateTimeOffset.Parse("2026-08-09T08:00:00Z");
        db.PlatformRoles.Add(new PlatformRoleEntity
        {
            RoleName = roleName,
            DisplayName = roleName,
            Description = roleName,
            IsBuiltIn = false,
            GrantsAllPermissions = grantsAllPermissions,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        foreach (var permission in permissions)
        {
            db.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
            {
                PlatformRolePermissionId = Guid.NewGuid(),
                RoleName = roleName,
                PermissionName = permission
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedAdminAsync(PlatformDbContext db, string userName, string roleName)
    {
        var now = DateTimeOffset.Parse("2026-08-09T08:00:00Z");
        var id = Guid.NewGuid();
        db.PlatformAdminUsers.Add(new PlatformAdminUserEntity
        {
            PlatformAdminUserId = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = userName,
            PasswordHash = "x",
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([roleName]),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        return id;
    }

    // Regression for a code-review finding: the LastFullAdmin check is read-then-write (read every
    // other admin's IsActive/role, then write the target row). Two concurrent UpdateAsync calls
    // disabling two DIFFERENT full admins could each read the other as still active before either
    // commits, both pass the check, and the panel ends up with zero active platform_admin. The
    // InMemory provider used by every other test in this file has no transactions and no snapshot
    // isolation, so it cannot exercise this race at all — it needs a real PostgreSQL instance with a
    // deterministic overlap (a SaveChangesInterceptor gate, same pattern as
    // Pos/PosSettlementPostgresFixture.SaveOverlapGate) so both transactions are guaranteed to have
    // taken their read snapshot before either one is allowed to flush its write.
    [PlatformAdminPostgresFact]
    public async Task ConcurrentUpdate_DisablingTwoDifferentFullAdmins_LeavesAtLeastOneActiveFullAdmin()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_admin_directory_race_{Guid.NewGuid():N}";
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
            var adminBId = Guid.NewGuid();
            var adminCId = Guid.NewGuid();
            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.PlatformAdminUsers.AddRange(
                    NewFullAdmin(adminBId, "b", now),
                    NewFullAdmin(adminCId, "c", now));
                await seedDb.SaveChangesAsync();
            }

            // The actor is a third, unrelated platform_admin acting through the API — deliberately
            // not one of the two targets, so the self-demotion rule never enters into it and the
            // only thing standing between the request and a full lockout is the LastFullAdmin check.
            var actorId = Guid.NewGuid();
            gate.Arm();
            await using var dbForB = new PlatformDbContext(options);
            await using var dbForC = new PlatformDbContext(options);
            var serviceForB = new PlatformAdminDirectoryService(
                dbForB, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbForB));
            var serviceForC = new PlatformAdminDirectoryService(
                dbForC, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbForC));

            var results = await Task.WhenAll(
                serviceForB.UpdateAsync(actorId, adminBId, new UpdatePlatformAdminRequest(null, false), CancellationToken.None),
                serviceForC.UpdateAsync(actorId, adminCId, new UpdatePlatformAdminRequest(null, false), CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            // Exactly one side must win (None) and the other must be denied. The loser's error is
            // pinned to Conflict specifically, not a Conflict-or-LastFullAdmin disjunction: under the
            // gate, both transactions read the other admin as still active before either writes, so
            // the app-level headcount check (stillHasFullAdmin) sees true in BOTH and never blocks
            // anything by itself — the only thing that can deny either side here is Postgres's
            // serializable isolation aborting the write-skew at commit time, which the service maps
            // to Conflict. The LastFullAdmin branch is unreachable in this specific, deterministic
            // race, so asserting the disjunction would let a regression that maps serialization
            // failures back to LastFullAdmin (the exact bug fixed in the previous round) slip through
            // as a false green. If this assertion ever starts failing with LastFullAdmin, the race is
            // not as deterministic as documented here — do not silently widen this back to a
            // disjunction; investigate why the app-level check is winning the race instead.
            Assert.Single(results, result => result.Error == PlatformAdminDirectoryError.None);
            Assert.Single(results, result => result.Error == PlatformAdminDirectoryError.Conflict);

            await using var verifyDb = new PlatformDbContext(options);
            var admins = await verifyDb.PlatformAdminUsers.AsNoTracking().ToArrayAsync();
            var activeFullAdmins = admins.Count(admin =>
                admin.IsActive && OpaquePlatformAdminTokenService.ParseRoles(admin.RolesJson)
                    .Contains(PlatformAdminRoleNames.PlatformAdmin));
            Assert.True(activeFullAdmins >= 1, "The invariant the whole service exists to protect was violated.");
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static PlatformAdminUserEntity NewFullAdmin(Guid id, string userNamePrefix, DateTimeOffset now)
    {
        return new PlatformAdminUserEntity
        {
            PlatformAdminUserId = id,
            UserName = $"{userNamePrefix}@platform.test",
            NormalizedUserName = $"{userNamePrefix}@PLATFORM.TEST".ToUpperInvariant(),
            DisplayName = userNamePrefix,
            PasswordHash = "x",
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([PlatformAdminRoleNames.PlatformAdmin]),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together. That forces both Serializable transactions to have already taken
    // their read snapshot (both saw the other admin as still active) before either is allowed to
    // flush its write — the only way to deterministically reproduce the race instead of hoping
    // Task.WhenAll happens to interleave two real network round trips unluckily.
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
