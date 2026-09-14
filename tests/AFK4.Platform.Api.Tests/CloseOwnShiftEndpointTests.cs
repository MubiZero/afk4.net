using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Кассир закрывает СВОЮ смену. Без этого гейт после входа заставлял его смену открыть, а
/// закрыть её обязан был кто-то другой: ночной оператор в шесть утра один.
///
/// Контроль при этом не ослаб, и это здесь же проверяется: сверка кассы обязательна, а
/// расхождение сверх допуска филиала по-прежнему требует подписи второго человека.
/// </summary>
public sealed class CloseOwnShiftEndpointTests
{
    private static string ShiftsPath => $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/shifts/open";

    private static string ClosePath(Guid shiftId) => $"/api/organizations/{TestIds.OrganizationId:D}/shifts/{shiftId:D}/close";

    private static async Task<ShiftDto> OpenShiftAsync(HttpClient client, long startingCashMinorUnits = 0)
    {
        var response = await client.PostAsJsonAsync(
            ShiftsPath,
            new OpenShiftRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", startingCashMinorUnits),
                "смена открыта",
                $"open-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShiftDto>())!;
    }

    [Fact]
    public async Task Operator_ClosesTheShiftTheyOpened()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var shift = await OpenShiftAsync(client);

        var response = await client.PostAsJsonAsync(
            ClosePath(shift.ShiftId),
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "сдал", "close-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await response.Content.ReadFromJsonAsync<ShiftDto>();
        Assert.Equal(ShiftStateNames.Closed, closed!.State);
    }

    // Узкое право — именно «свою». Чужую смену кассир не закрывает: он не знает, что в ней было.
    [Fact]
    public async Task Operator_CannotCloseSomeoneElsesShift()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var shift = await OpenShiftAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var entity = await db.Shifts.SingleAsync(candidate => candidate.ShiftId == shift.ShiftId);
            entity.OpenedByStaffUserId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            ClosePath(shift.ShiftId),
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "сдал", "close-002"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Главная проверка, ради которой это вообще можно было разрешить: «закрыть поверх недостачи»
    // в одиночку нельзя было и не стало можно.
    [Fact]
    public async Task Operator_CannotCloseOverADiscrepancyAlone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var shift = await OpenShiftAsync(client, startingCashMinorUnits: 100_000);

        // Пересчитали на 100 сомони меньше, чем должно быть.
        var response = await client.PostAsJsonAsync(
            ClosePath(shift.ShiftId),
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "не сходится", "close-003"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("sign-off", body, StringComparison.OrdinalIgnoreCase);
    }

    // Управляющий закрывает любую смену: его право шире, и правило «только своя» к нему не
    // применяется.
    [Fact]
    public async Task BranchManager_ClosesAnyShift()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var shift = await OpenShiftAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var entity = await db.Shifts.SingleAsync(candidate => candidate.ShiftId == shift.ShiftId);
            entity.OpenedByStaffUserId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            ClosePath(shift.ShiftId),
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "сдал", "close-004"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
