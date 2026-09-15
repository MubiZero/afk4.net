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
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var create = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<ReportScheduleDto>();
        Assert.NotNull(dto);
        Assert.Equal(ScheduledReportTypeNames.Sales, dto!.ReportType);
        Assert.Equal(ReportScheduleFrequencyNames.Daily, dto.Frequency);
        Assert.True(dto.IsActive);

        var list = await client.GetFromJsonAsync<List<ReportScheduleDto>>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules");
        Assert.NotNull(list);
        Assert.Contains(list!, s => s.ReportScheduleId == dto.ReportScheduleId);

        var delete = await client.DeleteAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules/{dto.ReportScheduleId:D}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<List<ReportScheduleDto>>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules");
        Assert.DoesNotContain(afterDelete!, s => s.ReportScheduleId == dto.ReportScheduleId);
    }

    [Fact]
    public async Task Create_InvalidReportType_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, "not_a_report", ReportScheduleFrequencyNames.Daily));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Вторая такая же рассылка означает два одинаковых письма владельцу каждый период. Стойка её
    // и не предлагает, но экран — не единственный вход: маршрут открыт любому, у кого есть право
    // видеть отчёты.
    [Fact]
    public async Task Create_SameReportAndFrequencyTwice_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var path = $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules";
        var request = new CreateReportScheduleRequest(
            TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);
        await client.PostAsJsonAsync(path, request);

        var second = await client.PostAsJsonAsync(path, request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var list = await client.GetFromJsonAsync<List<ReportScheduleDto>>(path);
        Assert.Single(list!);
    }

    // А та же рассылка с другой частотой — это другая рассылка, и запрещать её не за что.
    [Fact]
    public async Task Create_SameReportDifferentFrequency_IsAllowed()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var path = $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules";
        await client.PostAsJsonAsync(path, new CreateReportScheduleRequest(
            TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily));

        var second = await client.PostAsJsonAsync(path, new CreateReportScheduleRequest(
            TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Monthly));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var list = await client.GetFromJsonAsync<List<ReportScheduleDto>>(path);
        Assert.Equal(2, list!.Count);
    }

    // Пауза, а не удаление: запись остаётся, рассылка молчит, возобновление не требует заводить заново.
    [Fact]
    public async Task Pause_KeepsTheScheduleAndStopsItFromRunning()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var dto = await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);

        var paused = await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, IsActive: false));

        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        Assert.False((await paused.Content.ReadFromJsonAsync<ReportScheduleDto>())!.IsActive);
        var list = await client.GetFromJsonAsync<List<ReportScheduleDto>>(SchedulesPath);
        var listed = Assert.Single(list!);
        Assert.False(listed.IsActive);
        Assert.Equal(ReportScheduleFrequencyNames.Daily, listed.Frequency);
    }

    // Пауза не сбрасывает частоту, смена частоты не снимает паузу: пропущенное поле — это
    // «не трогай», а не «сбрось в умолчание».
    [Fact]
    public async Task FrequencyChange_DoesNotResumeAPausedSchedule()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var dto = await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);
        await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, IsActive: false));

        var changed = await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, Frequency: ReportScheduleFrequencyNames.Weekly));

        var updated = await changed.Content.ReadFromJsonAsync<ReportScheduleDto>();
        Assert.Equal(ReportScheduleFrequencyNames.Weekly, updated!.Frequency);
        Assert.False(updated.IsActive);
    }

    // Смена частоты на уже занятую — та же пара одинаковых писем, только через другую дверь.
    [Fact]
    public async Task FrequencyChange_ToAnAlreadyScheduledPair_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var daily = await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);
        await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Weekly);

        var conflict = await client.PatchAsJsonAsync(
            SchedulePath(daily.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, Frequency: ReportScheduleFrequencyNames.Weekly));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    // Возобновлённая рассылка не должна показывать дату в прошлом: владелец прочтёт это как поломку.
    [Fact]
    public async Task Resume_MovesTheNextRunForward()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var dto = await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);
        await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, IsActive: false));

        var resumed = await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, IsActive: true));

        var updated = await resumed.Content.ReadFromJsonAsync<ReportScheduleDto>();
        Assert.True(updated!.IsActive);
        Assert.True(updated.NextRunUtc >= dto.NextRunUtc);
    }

    // Пустая правка — не «готово».
    [Fact]
    public async Task Update_WithNeitherFrequencyNorPause_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var dto = await CreateAsync(client, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily);

        var response = await client.PatchAsJsonAsync(
            SchedulePath(dto.ReportScheduleId),
            new UpdateReportScheduleRequest(TestIds.OrganizationId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownSchedule_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PatchAsJsonAsync(
            SchedulePath(Guid.NewGuid()),
            new UpdateReportScheduleRequest(TestIds.OrganizationId, IsActive: false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string SchedulesPath =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules";

    private static string SchedulePath(Guid scheduleId) => $"{SchedulesPath}/{scheduleId:D}";

    private static async Task<ReportScheduleDto> CreateAsync(HttpClient client, string reportType, string frequency)
    {
        var response = await client.PostAsJsonAsync(
            SchedulesPath,
            new CreateReportScheduleRequest(TestIds.OrganizationId, reportType, frequency));
        return (await response.Content.ReadFromJsonAsync<ReportScheduleDto>())!;
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules",
            new CreateReportScheduleRequest(TestIds.OrganizationId, ScheduledReportTypeNames.Sales, ReportScheduleFrequencyNames.Daily));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownSchedule_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.DeleteAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/report-schedules/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
