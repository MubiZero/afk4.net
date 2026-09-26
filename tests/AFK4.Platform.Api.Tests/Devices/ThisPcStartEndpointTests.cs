using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Оболочка садит игрока без кода с монитора: её токен привязан к ПК и сам доказывает, где человек
/// сидит. Код ей не годится — вход по QR его гасит, а новый приходит со следующим сердцебиением, и
/// каждый такой вход стоил бы игроку неверной попытки.
/// </summary>
public sealed class ThisPcStartEndpointTests
{
    [Fact]
    public async Task ThePcItself_SeesThePrices_WithoutTheCode()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await SeedTariffAsync(fixture, walletMinorUnits: 100_000);
        using var pc = await PcClientAsync(fixture);

        var response = await pc.GetAsync("/api/me/this-pc/start-offers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var offers = await response.Content.ReadFromJsonAsync<PlayerStartOffersDto>();
        Assert.Equal("ПК 07", offers!.SeatLabel);
        Assert.Single(offers.Tariffs);
        Assert.Equal(100_000, offers.Balance.MinorUnits);
    }

    [Fact]
    public async Task APhone_CannotAskForThisPc()
    {
        // У телефона нет «этого ПК»: его пропуск — код с монитора.
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();

        var response = await phone.GetAsync("/api/me/this-pc/start-offers");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(SeatingCodeErrorCodeNames.Invalid, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePcItself_StartsASession_WithoutTheCode_AndCountsNoWrongAttempt()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var tariffVersionId = await SeedTariffAsync(fixture, walletMinorUnits: 100_000);
        using var pc = await PcClientAsync(fixture);

        var response = await pc.PostAsJsonAsync(
            "/api/me/sessions/start",
            new PlayerSelfStartRequest(string.Empty, tariffVersionId.ToString("D"), 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(fixture.Device.DeviceId, session.DeviceId);
        Assert.Equal(fixture.PlayerAccountId, session.PlayerAccountId);
        Assert.Empty(await db.SeatingCodeAttemptCounters.ToListAsync());
    }

    [Fact]
    public async Task APhone_WithoutACode_StartsNothing()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var tariffVersionId = await SeedTariffAsync(fixture, walletMinorUnits: 100_000);
        using var phone = await fixture.PhoneClientAsync();

        var response = await phone.PostAsJsonAsync(
            "/api/me/sessions/start",
            new PlayerSelfStartRequest(string.Empty, tariffVersionId.ToString("D"), 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpClient> PcClientAsync(DevicePlayerFixture fixture)
    {
        var signIn = await fixture.SignInAsync(fixture.Pin);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = await signIn.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    /// <summary>Тариф 10 с./мин, открытая смена и деньги на кошельке игрока.</summary>
    private static async Task<Guid> SeedTariffAsync(DevicePlayerFixture fixture, long walletMinorUnits)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var start = DevicePlayerFixture.Start;
        var tariffId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Стандарт",
            IsActive = true,
            CreatedAtUtc = start.AddYears(-1)
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId,
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 1000,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = start.AddYears(-1),
            CreatedAtUtc = start.AddYears(-1)
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = Guid.NewGuid(),
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = start.AddHours(-1)
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = fixture.PlayerAccountId,
            EntryType = "top_up",
            AccountType = "wallet",
            AmountMinorUnits = walletMinorUnits,
            CurrencyCode = "TJS",
            Description = "test top_up",
            Reason = "test seed",
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = start.AddMinutes(-5)
        });
        await db.SaveChangesAsync();
        return tariffVersionId;
    }
}
