using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Tests;

public sealed class ReportScheduleEndpointTests
{
    [Fact]
    public async Task Create_Then_List_Then_Delete_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var create = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<ReportScheduleDto>();
        Assert.NotNull(dto);
        Assert.Equal(ScheduledReportTypeNames.Sales, dto!.ReportType);
        Assert.Equal(ReportScheduleFrequencyNames.Daily, dto.Frequency);
        Assert.True(dto.IsActive);

        var list = await client.GetFromJsonAsync<List<ReportScheduleDto>>(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules");
        Assert.NotNull(list);
        Assert.Contains(list!, s => s.ReportScheduleId == dto.ReportScheduleId);

        var delete = await client.DeleteAsync(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules/{dto.ReportScheduleId:D}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<List<ReportScheduleDto>>(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules");
        Assert.DoesNotContain(afterDelete!, s => s.ReportScheduleId == dto.ReportScheduleId);
    }

    [Fact]
    public async Task Create_InvalidReportType_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, "not_a_report", ReportScheduleFrequencyNames.Daily));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownSchedule_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.DeleteAsync(
            $"/api/branches/{TestIds.BranchId:D}/report-schedules/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
