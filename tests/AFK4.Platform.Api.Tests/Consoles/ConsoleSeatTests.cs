using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Consoles;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Consoles;

/// <summary>Консольное место без агента: администратор сам начинает и заканчивает сессию, остальное — как у ПК.</summary>
public sealed class ConsoleSeatTests
{
    [Fact]
    public async Task AConsoleSeat_StartsAndEndsASession_WithoutAnAgent()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var seatId = await fixture.AddSeatAsync("PS5 · 1");

        var created = await fixture.Client.PostAsJsonAsync(ConsolesRoute(fixture), new CreateConsoleSeatRequest(fixture.Device.OrganizationId, seatId, "PS5 · 1"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var console = (await created.Content.ReadFromJsonAsync<DeviceInventoryItemDto>())!;
        Assert.Equal(DeviceRoleNames.Console, console.Role);

        // Свободна, хотя не выходила на связь ни разу: у консоли нет «офлайн».
        var seat = await SeatAsync(fixture, seatId);
        Assert.Equal(SeatStateNames.Free, seat.State);
        Assert.True(seat.IsConsole);
        Assert.Null(seat.IsDeviceOnline);

        var started = await fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{fixture.Device.OrganizationId:D}/branches/{fixture.Device.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(fixture.Device.OrganizationId, seatId, "manual-v1", $"start-{Guid.NewGuid():N}",
                DurationMode: SessionDurationModes.Fixed, DurationMinutes: 60));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var session = (await started.Content.ReadFromJsonAsync<SessionCommandResponse>())!.Session;
        Assert.Equal(SeatStateNames.Active, (await SeatAsync(fixture, seatId)).State);

        var ended = await fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{fixture.Device.OrganizationId:D}/sessions/{session.SessionId:D}/end",
            new EndSessionRequest("operator-end", $"end-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Запирать некому: сессия закрыта сразу, команды не висят в очереди.
        Assert.Equal(SessionStateNames.Ended, (await db.Sessions.SingleAsync(candidate => candidate.SessionId == session.SessionId)).State);
        Assert.All(await db.DeviceCommands.Where(command => command.DeviceId == console.DeviceId).ToListAsync(),
            command => Assert.Equal(DeviceCommandStatusNames.Completed, command.Status));
        Assert.Equal(SeatStateNames.Free, (await SeatAsync(fixture, seatId)).State);
    }

    [Fact]
    public async Task ASeatWithAPc_CannotBecomeAConsole()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var pcSeat = await SeatOfTheFixturePcAsync(fixture);

        var response = await fixture.Client.PostAsJsonAsync(ConsolesRoute(fixture), new CreateConsoleSeatRequest(fixture.Device.OrganizationId, pcSeat, "PS5"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static string ConsolesRoute(DevicePlayerFixture fixture) =>
        $"/api/organizations/{fixture.Device.OrganizationId:D}/branches/{fixture.Device.BranchId:D}/consoles";

    private static async Task<SeatStatusDto> SeatAsync(DevicePlayerFixture fixture, Guid seatId)
    {
        var map = await fixture.Client.GetFromJsonAsync<FloorMapDto>(
            $"/api/organizations/{fixture.Device.OrganizationId:D}/branches/{fixture.Device.BranchId:D}/floor-map");
        return map!.Seats.Single(seat => seat.SeatId == seatId);
    }

    private static async Task<Guid> SeatOfTheFixturePcAsync(DevicePlayerFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.DeviceSeatAssignments
            .Where(assignment => assignment.DeviceId == fixture.Device.DeviceId && assignment.DetachedAtUtc == null)
            .Select(assignment => assignment.SeatId)
            .SingleAsync();
    }
}
