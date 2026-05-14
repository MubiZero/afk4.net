using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class ReportEndpointTests
{
    private static readonly Guid ShiftId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SaleId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset ReportDay = DateTimeOffset.Parse("2026-05-14T00:00:00Z");

    [Theory]
    [InlineData("/api/branches/{0}/reports/shifts")]
    [InlineData("/api/branches/{0}/reports/sales")]
    public async Task GetReport_WithoutStaffToken_ReturnsUnauthorized(string routeTemplate)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(string.Format(routeTemplate, TestIds.BranchId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/branches/{0}/reports/shifts", AuditActionNames.ViewShiftReport)]
    [InlineData("/api/branches/{0}/reports/sales", AuditActionNames.ViewSalesReport)]
    public async Task GetReport_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit(
        string routeTemplate,
        string expectedAction)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync(string.Format(routeTemplate, TestIds.BranchId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(expectedAction, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task GetShiftReport_WithAuditorRole_ReturnsRowsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);
        await SeedShiftReportDataAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/reports/shifts?limit=1");
        var result = await response.Content.ReadFromJsonAsync<ShiftReportResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.Limit);
        var row = Assert.Single(result.Rows);
        Assert.Equal(ShiftId, row.ShiftId);
        Assert.Equal(52400, row.ExpectedCash.MinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewShiftReport, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task GetSalesReport_WithAuditorRole_ReturnsRowsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);
        await SeedSalesReportDataAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/reports/sales?limit=1");
        var result = await response.Content.ReadFromJsonAsync<SalesReportResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.Limit);
        var row = Assert.Single(result.Rows);
        Assert.Equal(SaleId, row.PosSaleId);
        Assert.Equal(2400, result.NetSalesTotal.MinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewSalesReport, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    private static async Task SeedShiftReportDataAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        SeedShift(dbContext);
        dbContext.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PosSaleId = SaleId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            PaymentKind = "payment",
            Provider = "manual",
            PaymentMethod = PaymentMethodNames.Cash,
            CurrencyCode = "TJS",
            AmountMinorUnits = 2400,
            Note = "sale",
            CreatedAtUtc = ReportDay.AddHours(12)
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSalesReportDataAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        SeedShift(dbContext);
        dbContext.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = SaleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = "TJS",
            TotalMinorUnits = 2400,
            RefundReason = string.Empty,
            VoidReason = string.Empty,
            CreatedAtUtc = ReportDay.AddHours(12),
            PaidAtUtc = ReportDay.AddHours(12).AddMinutes(1),
            RefundedAtUtc = null,
            VoidedAtUtc = null
        });
        dbContext.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = SaleId,
            ProductId = Guid.NewGuid(),
            ProductName = "Energy drink",
            Quantity = 2,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = 1200,
            LineTotalMinorUnits = 2400,
            TrackStock = true,
            AllowNegativeStock = false
        });
        dbContext.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PosSaleId = SaleId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            PaymentKind = "payment",
            Provider = "manual",
            PaymentMethod = PaymentMethodNames.Cash,
            CurrencyCode = "TJS",
            AmountMinorUnits = 2400,
            Note = "sale",
            CreatedAtUtc = ReportDay.AddHours(12)
        });
        await dbContext.SaveChangesAsync();
    }

    private static void SeedShift(PlatformDbContext dbContext)
    {
        dbContext.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            ClosedByStaffUserId = null,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "front register",
            ClosingNote = string.Empty,
            OpenedAtUtc = ReportDay.AddHours(9),
            ClosedAtUtc = null
        });
    }
}
