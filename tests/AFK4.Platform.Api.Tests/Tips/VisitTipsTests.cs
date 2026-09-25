using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Tips;

/// <summary>Чаевые администратору смены с экрана итога (P8).</summary>
public sealed class VisitTipsTests
{
    private static MoneyDto Tjs(long minorUnits) => new("TJS", minorUnits);

    [Fact]
    public async Task APlayer_TipsTheShiftAdministrator_OnceAVisit_AndTheShiftSeesIt()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var club = await ClubAsync(fixture, enabled: true, walletMinorUnits: 5000);
        using var phone = await fixture.PhoneClientAsync();

        var offer = (await phone.GetFromJsonAsync<PlayerTipOfferDto>(TipRoutes.Visit(club.SessionId)))!;
        Assert.True(offer.Available);
        Assert.Equal([Tjs(500), Tjs(1000), Tjs(2000)], offer.Presets);
        Assert.Equal("Шерзод", offer.RecipientName);

        var request = new PlayerTipRequest(Tjs(1000), "tip-1");
        var given = await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), request);
        Assert.Equal(HttpStatusCode.OK, given.StatusCode);
        var response = (await given.Content.ReadFromJsonAsync<PlayerTipResponse>())!;
        Assert.Equal(Tjs(4000), response.BalanceAfter);

        // Повтор того же нажатия не списывает второй раз; новое нажатие — отказ «уже оставлены».
        Assert.Equal(HttpStatusCode.OK, (await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), request)).StatusCode);
        var again = await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), new PlayerTipRequest(Tjs(500), "tip-2"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var after = (await phone.GetFromJsonAsync<PlayerTipOfferDto>(TipRoutes.Visit(club.SessionId)))!;
        Assert.Equal(TipUnavailableReasonNames.AlreadyTipped, after.UnavailableReason);
        Assert.Equal(Tjs(1000), after.Given);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var tip = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.Tip);
            Assert.Equal(-1000, tip.AmountMinorUnits);
            Assert.Equal(club.ShiftId, tip.ShiftId);
            Assert.Equal(club.SessionId, tip.SessionId);
        }

        var shift = (await fixture.Client.GetFromJsonAsync<ShiftTipsDto>(ShiftTipsRoute(fixture, club.ShiftId)))!;
        Assert.Equal(Tjs(1000), shift.Total);
        Assert.Equal("Шерзод", shift.RecipientName);
        Assert.Equal("ПК 07", Assert.Single(shift.Tips).SeatLabel);
    }

    [Fact]
    public async Task AClubThatDidNotTurnTipsOn_OrHasNoOpenShift_OffersNone()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var club = await ClubAsync(fixture, enabled: false, walletMinorUnits: 5000);
        using var phone = await fixture.PhoneClientAsync();

        var offer = (await phone.GetFromJsonAsync<PlayerTipOfferDto>(TipRoutes.Visit(club.SessionId)))!;
        Assert.Equal(TipUnavailableReasonNames.Disabled, offer.UnavailableReason);
        Assert.Equal(HttpStatusCode.Conflict,
            (await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), new PlayerTipRequest(Tjs(500), "k"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(SettingsRoute(fixture), new UpdateTipSettingsRequest(true))).StatusCode);
        await CloseShiftAsync(fixture, club.ShiftId);
        var noShift = (await phone.GetFromJsonAsync<PlayerTipOfferDto>(TipRoutes.Visit(club.SessionId)))!;
        Assert.Equal(TipUnavailableReasonNames.NoShift, noShift.UnavailableReason);
    }

    [Fact]
    public async Task AnOddAmount_OrMoreThanTheBalance_IsRefused_AndAStrangersVisitIsInvisible()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var club = await ClubAsync(fixture, enabled: true, walletMinorUnits: 700);
        using var phone = await fixture.PhoneClientAsync();

        Assert.Equal(TipUnavailableReasonNames.InvalidAmount, await RefusalAsync(phone, club.SessionId, Tjs(700)));
        Assert.Equal(TipUnavailableReasonNames.NotEnoughBalance, await RefusalAsync(phone, club.SessionId, Tjs(1000)));

        var stranger = await fixture.AddPlayerAsync();
        using var strangerPhone = await fixture.PhoneClientAsync(stranger.Phone);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerPhone.GetAsync(TipRoutes.Visit(club.SessionId))).StatusCode);
    }

    [Fact]
    public async Task TheManager_ReturnsATip_WhileTheShiftIsOpen()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var club = await ClubAsync(fixture, enabled: true, walletMinorUnits: 5000);
        using var phone = await fixture.PhoneClientAsync();
        await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), new PlayerTipRequest(Tjs(2000), "tip-1"));
        var tipId = (await fixture.Client.GetFromJsonAsync<ShiftTipsDto>(ShiftTipsRoute(fixture, club.ShiftId)))!.Tips[0].LedgerEntryId;
        var reverse = $"{ShiftTipsRoute(fixture, club.ShiftId)}/{tipId:D}/reverse";

        var reversed = await fixture.Client.PostAsync(reverse, content: null);
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        Assert.Equal(Tjs(0), (await reversed.Content.ReadFromJsonAsync<ShiftTipsDto>())!.Total);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsync(reverse, content: null)).StatusCode);

        var offer = (await phone.GetFromJsonAsync<PlayerTipOfferDto>(TipRoutes.Visit(club.SessionId)))!;
        Assert.Equal(Tjs(5000), offer.Balance);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Возврат — «отмена операции», а не «возврат»: тот уменьшил бы выручку за игру.
        Assert.False(await db.LedgerEntries.AnyAsync(entry => entry.EntryType == LedgerEntryTypeNames.Refund));
    }

    // Выдача из кассы — обычная выдача наличных; выданное второй раз не выдаётся.
    [Fact]
    public async Task TheShiftPaysTipsOutOfTheDrawer_Once()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var club = await ClubAsync(fixture, enabled: true, walletMinorUnits: 5000);
        using var phone = await fixture.PhoneClientAsync();
        await phone.PostAsJsonAsync(TipRoutes.Visit(club.SessionId), new PlayerTipRequest(Tjs(1000), "tip-1"));
        var payout = $"{ShiftTipsRoute(fixture, club.ShiftId)}/payout";

        var paid = await fixture.Client.PostAsJsonAsync(payout, new PayOutShiftTipsRequest("payout-1"));
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal(Tjs(1000), (await paid.Content.ReadFromJsonAsync<ShiftTipsDto>())!.PaidOut);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(payout, new PayOutShiftTipsRequest("payout-2"))).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var movement = await db.CashMovements.SingleAsync(row => row.ShiftId == club.ShiftId);
        Assert.Equal(CashMovementTypeNames.CashOut, movement.MovementType);
        Assert.Equal(1000, movement.AmountMinorUnits);
        Assert.Equal("Чаевые: Шерзод", movement.Reason);
    }

    private sealed record Club(Guid SessionId, Guid ShiftId);

    private static async Task<Club> ClubAsync(DevicePlayerFixture fixture, bool enabled, long walletMinorUnits)
    {
        if (enabled)
        {
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(SettingsRoute(fixture), new UpdateTipSettingsRequest(true))).StatusCode);
        }

        var sessionId = await fixture.StartSessionAsync(fixture.PlayerAccountId);
        await fixture.EndSessionAsync(sessionId);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await db.StaffUsers.FirstAsync(user => user.OrganizationId == fixture.Device.OrganizationId);
        staff.DisplayName = "Шерзод Каримов";
        var shiftId = Guid.NewGuid();
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId, OrganizationId = fixture.Device.OrganizationId, BranchId = fixture.Device.BranchId,
            OpenedByStaffUserId = staff.StaffUserId, State = ShiftStateNames.Open, CurrencyCode = "TJS",
            OpenedAtUtc = DevicePlayerFixture.Start
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = fixture.Device.OrganizationId, BranchId = fixture.Device.BranchId,
            PlayerAccountId = fixture.PlayerAccountId, EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = walletMinorUnits, CurrencyCode = "TJS", Description = "top_up", Reason = "test",
            CreatedByStaffUserId = staff.StaffUserId, CreatedAtUtc = DevicePlayerFixture.Start
        });
        await db.SaveChangesAsync();
        return new Club(sessionId, shiftId);
    }

    private static async Task CloseShiftAsync(DevicePlayerFixture fixture, Guid shiftId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var shift = await db.Shifts.SingleAsync(candidate => candidate.ShiftId == shiftId);
        shift.State = ShiftStateNames.Closed;
        await db.SaveChangesAsync();
    }

    private static async Task<string?> RefusalAsync(HttpClient phone, Guid sessionId, MoneyDto amount)
    {
        var response = await phone.PostAsJsonAsync(TipRoutes.Visit(sessionId), new PlayerTipRequest(amount, Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["error"];
    }

    private static string SettingsRoute(DevicePlayerFixture fixture) => $"/api/organizations/{fixture.Device.OrganizationId:D}/tip-settings";

    private static string ShiftTipsRoute(DevicePlayerFixture fixture, Guid shiftId) =>
        $"/api/organizations/{fixture.Device.OrganizationId:D}/shifts/{shiftId:D}/tips";
}
