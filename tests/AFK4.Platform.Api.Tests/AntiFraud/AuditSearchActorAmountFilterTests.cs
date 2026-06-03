using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class AuditSearchActorAmountFilterTests
{
    private static readonly Guid Alice = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    private static readonly Guid Bob = Guid.Parse("b2222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task Search_ByActor_ReturnsOnlyThatActorsRecords()
    {
        await using var db = CreateDbContext();
        SeedRefund(db, Alice, 6000);
        SeedRefund(db, Bob, 6000);
        await db.SaveChangesAsync();
        var service = new EfAuditSearchService(db);

        var result = await service.SearchAsync(
            TestIds.OrganizationId, TestIds.BranchId, Query(actor: Alice), CancellationToken.None);

        var record = Assert.Single(result.Records);
        Assert.Equal(Alice, record.ActorStaffUserId);
    }

    [Fact]
    public async Task Search_ByMinAmount_ExcludesSmallerAmounts()
    {
        await using var db = CreateDbContext();
        SeedRefund(db, Alice, 3000);
        SeedRefund(db, Alice, 7000);
        await db.SaveChangesAsync();
        var service = new EfAuditSearchService(db);

        var result = await service.SearchAsync(
            TestIds.OrganizationId, TestIds.BranchId, Query(minAmount: 5000), CancellationToken.None);

        var record = Assert.Single(result.Records);
        Assert.Equal(7000, record.AmountMinorUnits);
    }

    [Fact]
    public async Task Search_ByMaxAmount_ExcludesLargerAmounts()
    {
        await using var db = CreateDbContext();
        SeedRefund(db, Alice, 3000);
        SeedRefund(db, Alice, 7000);
        await db.SaveChangesAsync();
        var service = new EfAuditSearchService(db);

        var result = await service.SearchAsync(
            TestIds.OrganizationId, TestIds.BranchId, Query(maxAmount: 5000), CancellationToken.None);

        var record = Assert.Single(result.Records);
        Assert.Equal(3000, record.AmountMinorUnits);
    }

    [Fact]
    public async Task Search_RefundsByActorOverThreshold_AnswersTheOwnerQuestion()
    {
        // "Every refund Alice did this week over 50.00 TJS."
        await using var db = CreateDbContext();
        SeedRefund(db, Alice, 6000);   // Alice, over 5000 — included
        SeedRefund(db, Alice, 2000);   // Alice, under — excluded
        SeedRefund(db, Bob, 9000);     // Bob — excluded
        await db.SaveChangesAsync();
        var service = new EfAuditSearchService(db);

        var result = await service.SearchAsync(
            TestIds.OrganizationId, TestIds.BranchId,
            Query(actor: Alice, action: AuditActionNames.RefundLedgerEntry, minAmount: 5000),
            CancellationToken.None);

        var record = Assert.Single(result.Records);
        Assert.Equal(Alice, record.ActorStaffUserId);
        Assert.Equal(6000, record.AmountMinorUnits);
    }

    [Fact]
    public async Task Search_RecordsWithoutAmount_ExcludedByAmountFilter()
    {
        await using var db = CreateDbContext();
        SeedRefund(db, Alice, 6000);
        SeedRecord(db, Alice, AuditActionNames.StartSession, amountMinorUnits: null);
        await db.SaveChangesAsync();
        var service = new EfAuditSearchService(db);

        var result = await service.SearchAsync(
            TestIds.OrganizationId, TestIds.BranchId, Query(minAmount: 1), CancellationToken.None);

        Assert.Single(result.Records); // only the refund with an amount
    }

    private static AuditSearchQuery Query(
        Guid? actor = null, string? action = null, long? minAmount = null, long? maxAmount = null) =>
        new(
            Action: action,
            Outcome: null,
            TargetType: null,
            FromUtc: null,
            ToUtc: null,
            Limit: null,
            ActorStaffUserId: actor,
            MinAmountMinorUnits: minAmount,
            MaxAmountMinorUnits: maxAmount);

    private static void SeedRefund(PlatformDbContext db, Guid actor, long amount) =>
        SeedRecord(db, actor, AuditActionNames.RefundLedgerEntry, amount);

    private static void SeedRecord(PlatformDbContext db, Guid actor, string action, long? amountMinorUnits)
    {
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ActorStaffUserId = actor,
            Action = action,
            TargetType = "LedgerEntry",
            TargetId = Guid.NewGuid().ToString("D"),
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            AmountMinorUnits = amountMinorUnits,
            CreatedAtUtc = Now
        });
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
