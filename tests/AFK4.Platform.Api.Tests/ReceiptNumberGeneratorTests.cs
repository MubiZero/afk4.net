using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Receipts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class ReceiptNumberGeneratorTests
{
    private static readonly Guid SaleId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T14:30:00Z");

    [Fact]
    public async Task GenerateAsync_CreatesBranchDailySaleSequence()
    {
        await using var db = CreateDbContext();
        db.Receipts.Add(CreateReceipt("POS-20260513-0001", "sale"));
        await db.SaveChangesAsync();
        var generator = new ReceiptNumberGenerator(db);

        var number = await generator.GenerateAsync(TestIds.OrganizationId, TestIds.BranchId, "sale", Now, CancellationToken.None);

        Assert.Equal("POS-20260513-0002", number);
    }

    [Fact]
    public async Task GenerateAsync_CreatesIndependentRefundSequence()
    {
        await using var db = CreateDbContext();
        db.Receipts.Add(CreateReceipt("POS-20260513-0001", "sale"));
        db.Receipts.Add(CreateReceipt("REF-20260513-0001", "refund"));
        await db.SaveChangesAsync();
        var generator = new ReceiptNumberGenerator(db);

        var number = await generator.GenerateAsync(TestIds.OrganizationId, TestIds.BranchId, "refund", Now, CancellationToken.None);

        Assert.Equal("REF-20260513-0002", number);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static ReceiptEntity CreateReceipt(string receiptNumber, string receiptType)
    {
        return new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PosSaleId = SaleId,
            ReceiptNumber = receiptNumber,
            ReceiptType = receiptType,
            CurrencyCode = "TJS",
            TotalMinorUnits = 2400,
            CreatedAtUtc = Now
        };
    }
}
