using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Reports;

public sealed class OrganizationAdminReportEndpointTests
{
    [Fact]
    public async Task Summary_WithoutStaffSession_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reports/workspace/summary?fromDate=2026-07-29&toDate=2026-07-29");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_WithoutReportsPermission_ReturnsForbiddenAndAuditsDenial()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reports/workspace/summary?fromDate=2026-07-29&toDate=2026-07-29");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await db.AuditRecords.SingleAsync(row => row.Action == AuditActionNames.ViewOrganizationAdminReports);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task Revenue_WithManagerPermission_UsesBranchTimezone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var branch = await db.Branches.SingleAsync(row => row.BranchId == TestIds.BranchId);
            branch.PreferredTimeZone = "Asia/Dushanbe";
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reports/workspace/revenue?fromDate=2026-07-29&toDate=2026-07-29");
        var report = await response.Content.ReadFromJsonAsync<OrganizationAdminRevenueReportDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(report);
        Assert.Equal(DateTimeOffset.Parse("2026-07-28T19:00:00Z"), report.Period.FromUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-29T19:00:00Z"), report.Period.ToUtc);
    }

    [Fact]
    public async Task RevenueCsv_WithManagerPermission_ReturnsOneUnifiedCsv()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reports/workspace/revenue/export.csv?fromDate=2026-07-29&toDate=2026-07-29");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, csv.Split("section,source,currency,amount_minor_units,from_date,to_date").Length - 1);
        Assert.Contains("\"total\",\"net\"", csv);
        Assert.Contains("\"source\",\"gameplay\"", csv);
    }
}
