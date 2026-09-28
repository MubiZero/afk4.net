using AFK4.Platform.Api.Endpoints;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Техник открывает карту, чтобы найти сломанный ПК, — это его единственный вход в приложение.
/// Право «видеть карту» раньше отдавало ему имя каждого играющего гостя, тариф и набежавшую сумму,
/// хотя каталог ролей нарочно не даёт технику ни игроков, ни счёта.
/// </summary>
public sealed class FloorMapVisibilityTests
{
    private static readonly Guid Branch = Guid.NewGuid();

    private static readonly FloorMapDto Map = new(Branch, "На Рудаки",
    [
        new SeatStatusDto(Guid.NewGuid(), "ПК 07", Guid.NewGuid(), "Общий", 1, SeatStateNames.Active,
            Guid.NewGuid(), "PC-07", true, false, null, null, null, Guid.NewGuid(), null,
            AccruedCostMinorUnits: 1_250, CurrencyCode: "TJS", PlayerDisplayName: "Фаррух", TariffName: "Вечер")
    ]);

    private static StaffContext StaffWith(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Сотрудник", new HashSet<Guid> { Branch }, new HashSet<string>(permissions));

    [Fact]
    public void Technician_SeesTheSeat_ButNotThePlayerOrTheMoney()
    {
        var seat = Assert.Single(FloorMapEndpoints.VisibleTo(StaffWith(OrganizationPermissionNames.ViewFloorMap), Branch, Map).Seats);

        Assert.Equal("ПК 07", seat.SeatName);
        Assert.Equal(SeatStateNames.Active, seat.State);
        Assert.Null(seat.PlayerDisplayName);
        Assert.Null(seat.TariffName);
        Assert.Null(seat.AccruedCostMinorUnits);
    }

    [Fact]
    public void Operator_SeesEverythingAsBefore()
    {
        var staff = StaffWith(
            OrganizationPermissionNames.ViewFloorMap,
            OrganizationPermissionNames.ViewPlayers,
            OrganizationPermissionNames.ViewBilling);

        Assert.Same(Map, FloorMapEndpoints.VisibleTo(staff, Branch, Map));
    }
}
