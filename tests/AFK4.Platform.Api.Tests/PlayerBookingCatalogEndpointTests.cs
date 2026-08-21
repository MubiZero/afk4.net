using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Что нужно приложению, чтобы игрок мог выбрать тариф: собственный филиал и цена выбора до того,
/// как бронь подтверждена. До этого филиал сервер знал про себя молча, и спросить прайс было не по
/// чему.
/// </summary>
public class PlayerBookingCatalogEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone, Guid TariffVersionId);

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        string pin,
        string branchName = "CyberX на Рудаки",
        int minimumBillableMinutes = 0,
        int roundingIncrementMinutes = 1)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var tariffId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        var phone = TestPhones.Next();

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Booking Test Org", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = branchName,
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = org, HomeBranchId = branch, DisplayName = "Игрок",
            PhoneNumber = phone, PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Now
        });
        // Бронь с выбранным тарифом замораживает деньги, поэтому у игрока должен быть кошелёк:
        // без него сервер справедливо откажет «бронь только с деньгами».
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, PlayerAccountId = player,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 50_000, CurrencyCode = "TJS", CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId, OrganizationId = org, BranchId = branch, Name = "Ночной", IsActive = true, CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId, TariffId = tariffId, OrganizationId = org, BranchId = branch,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = minimumBillableMinutes, RoundingIncrementMinutes = roundingIncrementMinutes,
            EffectiveFromUtc = Now.AddYears(-1)
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new Seeded(org, branch, player, phone, tariffVersionId);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync("/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task Profile_CarriesTheHomeBranch_SoTheAppCanAskForThePriceList()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");

        Assert.Equal(seeded.BranchId, profile!.HomeBranchId);
        Assert.Equal("CyberX на Рудаки", profile.HomeBranchName);
    }

    [Fact]
    public async Task Quote_PricesTheBookingBeforeItIsMade()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(seeded.TariffVersionId, start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<ReservationQuoteDto>();
        Assert.Equal(120, quote!.RequestedMinutes);
        Assert.Equal(120, quote.BillableMinutes);
        Assert.Equal(3_000, quote.AmountMinorUnits);
        Assert.Equal("TJS", quote.CurrencyCode);
        Assert.Equal("Ночной", quote.TariffName);
    }

    // Ради этого расчёт и живёт на сервере: минимум и округление меняют сумму, а приложение,
    // умножающее минуты на цену, показало бы другую.
    [Fact]
    public async Task Quote_ShowsWhatTheMinimumAndRoundingDoToThePrice()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", minimumBillableMinutes: 60, roundingIncrementMinutes: 30);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(seeded.TariffVersionId, start, start.AddMinutes(70)));

        var quote = await response.Content.ReadFromJsonAsync<ReservationQuoteDto>();
        Assert.Equal(70, quote!.RequestedMinutes);
        Assert.Equal(90, quote.BillableMinutes);
        Assert.Equal(2_250, quote.AmountMinorUnits);
    }

    [Fact]
    public async Task Quote_RejectsATariffFromAnotherOrganization()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(stranger.TariffVersionId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Booking_KeepsTheChosenTariffAndItsPrice()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, start, start.AddHours(1), null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.Equal(seeded.TariffVersionId, created!.TariffVersionId);
        Assert.Equal(1_500, created.EstimatedCostMinorUnits);

        // И в списке броней цена та же — игрок видит, на что согласился, а не пересчёт по прайсу.
        var mine = await client.GetFromJsonAsync<List<PlayerReservationDto>>("/api/me/reservations");
        Assert.Equal(1_500, Assert.Single(mine!).EstimatedCostMinorUnits);
        Assert.Equal("Ночной", mine![0].TariffName);
    }

    /// <summary>Место с привязанным принятым компьютером — то, за что игрок может сесть сам.</summary>
    private static async Task<(Guid SeatId, Guid DeviceId)> SeedSeatAsync(
        PlatformApiFactory factory,
        Seeded seeded,
        string name,
        bool online = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = await db.Zones
            .Where(zone => zone.BranchId == seeded.BranchId)
            .Select(zone => zone.ZoneId)
            .FirstOrDefaultAsync();
        if (zoneId == Guid.Empty)
        {
            zoneId = Guid.NewGuid();
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = zoneId, OrganizationId = seeded.OrgId, BranchId = seeded.BranchId,
                Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now
            });
        }

        var seatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId, OrganizationId = seeded.OrgId, BranchId = seeded.BranchId, ZoneId = zoneId,
            Name = name, SortOrder = 10, CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId, OrganizationId = seeded.OrgId, BranchId = seeded.BranchId,
            MachineName = name, DisplayName = name, DevicePublicKey = $"key-{deviceId:N}",
            Role = DeviceRoleNames.GamingPc, EnrollmentState = DeviceEnrollmentStateNames.Approved,
            IsOnline = online, EnrolledAtUtc = Now
        });
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(), OrganizationId = seeded.OrgId, BranchId = seeded.BranchId,
            SeatId = seatId, DeviceId = deviceId, AttachedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (seatId, deviceId);
    }

    [Fact]
    public async Task Seats_ListWhatThePlayerCanSitAt()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        await SeedSeatAsync(factory, seeded, "PC-01");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var seats = await client.GetFromJsonAsync<List<PlayerSeatDto>>(
            $"/api/me/branches/{seeded.BranchId}/seats");

        var seat = Assert.Single(seats!);
        Assert.Equal("PC-01", seat.SeatName);
        Assert.Equal("Зал A", seat.ZoneName);
        Assert.True(seat.IsAvailable);
    }

    // Занятое место остаётся в списке с причиной: пропавшее место выглядит как сбой приложения,
    // а «PC-01 занят» — это ответ.
    [Fact]
    public async Task Seats_ShowBusyOnesWithAReason()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        var seat = await SeedSeatAsync(factory, seeded, "PC-01");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = seeded.OrgId, BranchId = seeded.BranchId,
                SeatId = seat.SeatId, DeviceId = seat.DeviceId, State = SessionStateNames.Active,
                RequestedAtUtc = Now, StartedAtUtc = Now,
                TariffRuleVersionId = seeded.TariffVersionId.ToString("D"),
                BillingMode = BillingModeNames.PrepaidWallet
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var seats = await client.GetFromJsonAsync<List<PlayerSeatDto>>(
            $"/api/me/branches/{seeded.BranchId}/seats");

        var listed = Assert.Single(seats!);
        Assert.False(listed.IsAvailable);
        Assert.Equal(PlayerSeatUnavailableReasons.Session, listed.UnavailableReason);
    }

    // Выключенный компьютер сесть не даст, и сказать об этом надо до, а не после списания денег.
    [Fact]
    public async Task Seats_MarkOfflineMachines()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        await SeedSeatAsync(factory, seeded, "PC-02", online: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var seats = await client.GetFromJsonAsync<List<PlayerSeatDto>>(
            $"/api/me/branches/{seeded.BranchId}/seats");

        Assert.Equal(PlayerSeatUnavailableReasons.Offline, Assert.Single(seats!).UnavailableReason);
    }

    [Fact]
    public async Task Seats_AreScopedToTheCallersOrganization()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        await SeedSeatAsync(factory, stranger, "PC-99");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var response = await client.GetAsync($"/api/me/branches/{stranger.BranchId}/seats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Booking_RejectsATariffFromAnotherOrganization()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, start, start.AddHours(1), null, stranger.TariffVersionId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.Reservations.AnyAsync(r => r.PlayerAccountId == mine.PlayerId));
    }
}
