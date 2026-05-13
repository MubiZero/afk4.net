using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfTariffServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T09:00:00Z");
    private static readonly DateTimeOffset FirstEffectiveFromUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task CreateTariffAsync_CreatesBranchTariffAndStoresIdempotency()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreateTariffRequest(TestIds.OrganizationId, "Standard", "tariff-create-001");

        var first = await service.CreateTariffAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreateTariffAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.TariffId, second.Response.TariffId);

        var tariff = await db.Tariffs.SingleAsync();
        Assert.Equal(first.Response.TariffId, tariff.TariffId);
        Assert.Equal(TestIds.BranchId, tariff.BranchId);
        Assert.True(tariff.IsActive);
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task CreateTariffAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreateTariffRequest(TestIds.OrganizationId, "Standard", "tariff-create-001");
        var differentRequest = request with { Name = "VIP" };

        await service.CreateTariffAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.CreateTariffAsync(TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.Tariffs.ToListAsync());
    }

    [Fact]
    public async Task CreateTariffVersionAsync_AssignsVersionNumberOneForFirstVersion()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var service = CreateService(db);
        var request = CreateVersionRequest(tariff.TariffId, idempotencyKey: "tariff-version-001");

        var result = await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(1, result.Response.VersionNumber);
        Assert.Null(result.Response.RetiredAtUtc);
        Assert.Equal("TJS", result.Response.CurrencyCode);
        Assert.Equal(50, result.Response.PricePerMinuteMinorUnits);
        Assert.Equal(30, result.Response.MinimumBillableMinutes);
        Assert.Equal(15, result.Response.RoundingIncrementMinutes);
        Assert.Equal(FirstEffectiveFromUtc, result.Response.EffectiveFromUtc);

        var version = await db.TariffVersions.SingleAsync();
        Assert.Equal(result.Response.TariffVersionId, version.TariffVersionId);
        Assert.Single(await db.BillingCommandIdempotency.Where(record => record.Operation == "tariff-version-create").ToListAsync());
    }

    [Fact]
    public async Task CreateTariffVersionAsync_RetiresPreviousActiveVersionWhenNewVersionStarts()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var service = CreateService(db);
        var firstRequest = CreateVersionRequest(tariff.TariffId, idempotencyKey: "tariff-version-001");
        var secondRequest = CreateVersionRequest(
            tariff.TariffId,
            pricePerMinuteMinorUnits: 60,
            effectiveFromUtc: DateTimeOffset.Parse("2026-05-13T12:00:00Z"),
            idempotencyKey: "tariff-version-002");

        var first = await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, firstRequest, CancellationToken.None);
        var second = await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, secondRequest, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var firstVersion = await db.TariffVersions.SingleAsync(version => version.TariffVersionId == first.Response!.TariffVersionId);
        var secondVersion = await db.TariffVersions.SingleAsync(version => version.TariffVersionId == second.Response!.TariffVersionId);
        Assert.Equal(secondRequest.EffectiveFromUtc, firstVersion.RetiredAtUtc);
        Assert.Equal(2, secondVersion.VersionNumber);
        Assert.Null(secondVersion.RetiredAtUtc);
    }

    [Fact]
    public async Task CreateTariffVersionAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var service = CreateService(db);
        var request = CreateVersionRequest(tariff.TariffId, idempotencyKey: "tariff-version-001");
        var differentRequest = request with { PricePerMinuteMinorUnits = 60 };

        await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.TariffVersions.ToListAsync());
    }

    [Fact]
    public async Task CalculateAsync_PreservesVersionAndRoundsBillableMinutes()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var version = await SeedTariffVersionAsync(db, tariff.TariffId);
        var service = CreateService(db);

        var exact = await service.CalculateAsync(
            TestIds.BranchId,
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 75),
            CancellationToken.None);
        var rounded = await service.CalculateAsync(
            TestIds.BranchId,
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 76),
            CancellationToken.None);

        Assert.NotNull(exact);
        Assert.Equal(75, exact.DurationMinutes);
        Assert.Equal(75, exact.BillableMinutes);
        Assert.Equal(3750, exact.Amount.MinorUnits);
        Assert.Equal(version.TariffVersionId.ToString("D"), exact.TariffRuleVersionId);

        Assert.NotNull(rounded);
        Assert.Equal(76, rounded.DurationMinutes);
        Assert.Equal(90, rounded.BillableMinutes);
        Assert.Equal(4500, rounded.Amount.MinorUnits);
        Assert.Equal(version.TariffVersionId.ToString("D"), rounded.TariffRuleVersionId);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsNullForUnknownOrRetiredVersion()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var version = await SeedTariffVersionAsync(
            db,
            tariff.TariffId,
            retiredAtUtc: DateTimeOffset.Parse("2026-05-13T12:00:00Z"));
        var service = CreateService(db);

        var unknown = await service.CalculateAsync(
            TestIds.BranchId,
            new CalculateTariffRequest(TestIds.OrganizationId, Guid.NewGuid(), 30),
            CancellationToken.None);
        var retired = await service.CalculateAsync(
            TestIds.BranchId,
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 30),
            CancellationToken.None);

        Assert.Null(unknown);
        Assert.Null(retired);
    }

    [Fact]
    public async Task CreateTariffAsync_RejectsEmptyName()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.CreateTariffAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateTariffRequest(TestIds.OrganizationId, "   ", "tariff-create-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.Tariffs.ToListAsync());
    }

    [Theory]
    [InlineData(0, 30, 15, "TJS")]
    [InlineData(50, 0, 15, "TJS")]
    [InlineData(50, 30, 0, "TJS")]
    [InlineData(50, 30, 15, "   ")]
    public async Task CreateTariffVersionAsync_RejectsInvalidValues(
        long pricePerMinuteMinorUnits,
        int minimumBillableMinutes,
        int roundingIncrementMinutes,
        string currencyCode)
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var service = CreateService(db);
        var request = CreateVersionRequest(
            tariff.TariffId,
            currencyCode,
            pricePerMinuteMinorUnits,
            minimumBillableMinutes,
            roundingIncrementMinutes,
            FirstEffectiveFromUtc,
            "tariff-version-001");

        var result = await service.CreateTariffVersionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.TariffVersions.ToListAsync());
    }

    [Fact]
    public async Task CreateTariffVersionAsync_RejectsUnknownTariff()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.CreateTariffVersionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateVersionRequest(Guid.NewGuid(), idempotencyKey: "tariff-version-001"),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Empty(await db.TariffVersions.ToListAsync());
    }

    [Fact]
    public async Task CalculateAsync_ReturnsNullForNonPositiveDuration()
    {
        await using var db = CreateDbContext();
        var tariff = await SeedTariffAsync(db);
        var version = await SeedTariffVersionAsync(db, tariff.TariffId);
        var service = CreateService(db);

        var result = await service.CalculateAsync(
            TestIds.BranchId,
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 0),
            CancellationToken.None);

        Assert.Null(result);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfTariffService CreateService(PlatformDbContext db)
    {
        return new EfTariffService(db, new FixedTimeProvider(Now));
    }

    private static async Task<TariffEntity> SeedTariffAsync(PlatformDbContext db)
    {
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };

        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();

        return tariff;
    }

    private static async Task<TariffVersionEntity> SeedTariffVersionAsync(
        PlatformDbContext db,
        Guid tariffId,
        DateTimeOffset? retiredAtUtc = null)
    {
        var version = new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = FirstEffectiveFromUtc,
            RetiredAtUtc = retiredAtUtc,
            CreatedAtUtc = Now
        };

        db.TariffVersions.Add(version);
        await db.SaveChangesAsync();

        return version;
    }

    private static CreateTariffVersionRequest CreateVersionRequest(
        Guid tariffId,
        string currencyCode = "TJS",
        long pricePerMinuteMinorUnits = 50,
        int minimumBillableMinutes = 30,
        int roundingIncrementMinutes = 15,
        DateTimeOffset? effectiveFromUtc = null,
        string idempotencyKey = "tariff-version-001")
    {
        return new CreateTariffVersionRequest(
            TestIds.OrganizationId,
            tariffId,
            currencyCode,
            pricePerMinuteMinorUnits,
            minimumBillableMinutes,
            roundingIncrementMinutes,
            effectiveFromUtc ?? FirstEffectiveFromUtc,
            idempotencyKey);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
