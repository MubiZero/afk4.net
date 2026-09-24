using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PlayerSelfSessionEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SelfStartContext(
        Guid OrgId, Guid BranchId, Guid PlayerId, string Phone,
        Guid DeviceId, Guid SeatId, string TariffRuleVersionId);

    /// <summary>
    /// Seeds: player + PIN credential + wallet ledger entry + seat + approved device
    /// + active DeviceSeatAssignment + TariffVersion.
    /// The tariff: 1000 minor/min, min=1 min. At 60 min the charge = 60_000 minor.
    /// walletMinorUnits=100_000 comfortably covers it; 100 does not.
    /// </summary>
    /// <summary>
    /// Код, который сейчас показал бы монитор этой машины. Тест им и представляется: человек,
    /// стоящий перед экраном, набирает то же самое.
    /// </summary>
    private static async Task<string> SeatingCodeAsync(PlatformApiFactory factory, SelfStartContext ctx)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var issued = await new EfSeatingCodeService(db, TimeProvider.System)
            .IssueAsync(ctx.OrgId, ctx.DeviceId, CancellationToken.None);
        return issued!.Code;
    }

    private static async Task<SelfStartContext> SeedSelfStartContextAsync(
        PlatformApiFactory factory, long walletMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var phone = TestPhones.Next();
        var deviceId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();

        // Player account
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Self-Start Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });

        // Wallet ledger entry (top-up)
        if (walletMinorUnits > 0)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(),
                OrganizationId = orgId,
                BranchId = branchId,
                PlayerAccountId = playerId,
                EntryType = "top_up",
                AccountType = "wallet",
                AmountMinorUnits = walletMinorUnits,
                CurrencyCode = "TJS",
                Description = "test top_up",
                Reason = "test seed",
                CreatedByStaffUserId = Guid.NewGuid(),
                CreatedAtUtc = Now
            });
        }

        // Seat
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = Guid.NewGuid(),
            Name = "PC-Self",
            SortOrder = 10,
            CreatedAtUtc = Now
        });

        // Approved device
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = orgId,
            BranchId = branchId,
            MachineName = "PC-SELF-01",
            DisplayName = "Self-Start PC",
            DevicePublicKey = "test-key",
            Role = "gaming_pc",
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            AgentVersion = "1.0.0",
            ShellVersion = "1.0.0",
            EnrolledAtUtc = Now.AddDays(-30)
        });

        // Active seat assignment
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = deviceId,
            AttachedAtUtc = Now.AddDays(-1),
            DetachedAtUtc = null
        });

        // Tariff version: 1000 minor/min, no minimum, no rounding
        // => 60 min => 60_000 minor. 100_000 covers it, 100 does not.
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId,
            TariffId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 1000,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = Now.AddYears(-1),
            CreatedAtUtc = Now.AddYears(-1)
        });

        // Open shift is required by session billing validation
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            OpenedByStaffUserId = Guid.NewGuid(),
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 0,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now.AddHours(-1)
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, playerId, phone, "1234");

        return new SelfStartContext(
            orgId, branchId, playerId, phone,
            deviceId, seatId, tariffVersionId.ToString("D"));
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    /// <summary>
    /// Код с чужого монитора или снятый час назад — не «почти годный». Иначе фотография экрана
    /// работает вечно, и вся затея с кодом теряет смысл.
    /// </summary>
    [Fact]
    public async Task SelfStart_WithADeadCode_IsRefused()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var code = await SeatingCodeAsync(factory, ctx);
        await ExpireSeatingCodesAsync(factory);

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(code, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("seating_code_invalid", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.Sessions.AnyAsync(session => session.PlayerAccountId == ctx.PlayerId));
    }

    /// <summary>
    /// Код одноразовый: сели — и подсмотревший его через плечо уже не воспользуется им, даже
    /// если сессия кончится раньше, чем код истёк бы сам.
    /// </summary>
    [Fact]
    public async Task SelfStart_ConsumesTheCode()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var code = await SeatingCodeAsync(factory, ctx);

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(code, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Null(await new EfSeatingCodeService(db, TimeProvider.System)
            .FindDeviceAsync(ctx.OrgId, code, CancellationToken.None));
    }

    /// <summary>
    /// Отказ по деньгам код не сжигает: человек пополнит счёт или выберет меньше времени и
    /// попробует тем же кодом, не дожидаясь нового.
    /// </summary>
    [Fact]
    public async Task SelfStart_RefusedForMoney_KeepsTheCode()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var code = await SeatingCodeAsync(factory, ctx);

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(code, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(ctx.DeviceId, await new EfSeatingCodeService(db, TimeProvider.System)
            .FindDeviceAsync(ctx.OrgId, code, CancellationToken.None));
    }

    /// <summary>
    /// Ответ потерялся в сети, приложение повторило старт с тем же ключом. Код уже погашен
    /// удачным стартом — повтор всё равно получает свою сессию, а не «код неверен» и не вторую.
    /// </summary>
    [Fact]
    public async Task SelfStart_RepeatedWithTheSameKey_ReturnsTheSameSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var request = new PlayerSelfStartRequest(
            await SeatingCodeAsync(factory, ctx), ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N"));

        var first = await client.PostAsJsonAsync("/api/me/sessions/start", request);
        var repeat = await client.PostAsJsonAsync("/api/me/sessions/start", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var firstSession = (await first.Content.ReadFromJsonAsync<SessionCommandResponse>())!.Session.SessionId;
        var repeatSession = (await repeat.Content.ReadFromJsonAsync<SessionCommandResponse>())!.Session.SessionId;
        Assert.Equal(firstSession, repeatSession);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.Sessions.CountAsync(session => session.PlayerAccountId == ctx.PlayerId));
        // Денег списано один раз: повтор не прошёл второй раз мимо кассы.
        Assert.Equal(100_000 - 60_000, await WalletBalanceAsync(factory, ctx.PlayerId));
    }

    /// <summary>Код живёт минуты — состарить его в тесте дешевле, чем ждать.</summary>
    private static async Task ExpireSeatingCodesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var code in await db.DeviceSeatingCodes.ToListAsync())
        {
            code.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SelfStart_WithSufficientWallet_StartsSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(await SeatingCodeAsync(factory, ctx), ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId);
        Assert.Equal("active", session.State);
        Assert.Equal(ctx.SeatId, session.SeatId);
        // Человек сел сам — и назвать это посадкой оператора нельзя: у смены и у отчётов
        // «кто посадил» разные ответы на эти два случая.
        Assert.Equal(SessionOriginNames.SelfService, session.Origin);
    }

    [Fact]
    public async Task SelfStart_WithInsufficientWallet_Returns409InsufficientBalance()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(await SeatingCodeAsync(factory, ctx), ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_balance", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SelfStart_WithoutToken_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest("000000", Guid.NewGuid().ToString("D"), 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SelfExtend_OwnedActiveSession_WithWallet_Extends()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var start = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(await SeatingCodeAsync(factory, ctx), ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));
        start.EnsureSuccessStatusCode();
        Guid sessionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            sessionId = (await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId)).SessionId;
        }

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Ответ на продление потерялся, приложение повторило с тем же ключом. Денег было ровно на
    /// одно продление — повтор получает свой ответ, а не «не хватает денег», и второй раз не платит.
    /// </summary>
    [Fact]
    public async Task SelfExtend_RepeatedWithTheSameKey_ReturnsTheSameAnswer()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 120_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        var extend = new PlayerSelfExtendRequest(60, Guid.NewGuid().ToString("N"));

        var first = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/extend", extend);
        var repeat = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/extend", extend);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(0, await WalletBalanceAsync(factory, ctx.PlayerId));
    }

    [Fact]
    public async Task SelfExtend_ForeignSession_Returns404()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{Guid.NewGuid()}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Игрок сам встаёт из-за ПК ─────────────────────────────────────────────────────────
    //
    // Предоплаченная сессия списывается ЦЕЛИКОМ при старте, поэтому ранний выход — это возврат
    // переплаты, а не доплата. Тариф здесь 1000 дирамов за минуту при минимуме в минуту, так что
    // час стоит 60 000, а «встал сразу» стоит минимум — одну минуту.

    private static async Task<Guid> StartHourSessionAsync(PlatformApiFactory factory, HttpClient client, SelfStartContext ctx)
    {
        var start = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(await SeatingCodeAsync(factory, ctx), ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));
        start.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId)).SessionId;
    }

    private static async Task<long> WalletBalanceAsync(PlatformApiFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerId
                            && entry.AccountType == AFK4.Shared.Contracts.Billing.LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => entry.AmountMinorUnits);
    }

    /// Включает кешбэк за игровое время в этом клубе: без него возврат нечего разматывать, и
    /// дыра «оплатить восемь часов, встать через пять минут, оставить себе кешбэк за восемь»
    /// осталась бы непроверенной.
    private static async Task EnableSessionCashbackAsync(PlatformApiFactory factory, Guid orgId, int basisPoints)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Кешбэк проходит через лестницу фич, а она начинается с существования организации:
        // без строки Organizations ответ «нет», и настройки лояльности читаться не будут вовсе.
        if (!await db.Organizations.AnyAsync(organization => organization.OrganizationId == orgId))
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId,
                Name = "Self-End Club",
                CreatedAtUtc = Now
            });
        }

        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = orgId,
            FeatureKey = AFK4.Shared.Contracts.Platform.Features.PlatformFeatureNames.Loyalty,
            IsEnabled = true,
            Reason = "тест: включаем кешбэк за игровое время",
            SetByPlatformAdminUserId = Guid.Empty,
            SetAtUtc = Now
        });

        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = orgId,
            SessionEnabled = true,
            SessionPercentBasisPoints = basisPoints,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<long> SumByTypeAsync(PlatformApiFactory factory, Guid sessionId, string entryType)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.LedgerEntries
            .Where(entry => entry.SessionId == sessionId && entry.EntryType == entryType)
            .SumAsync(entry => entry.AmountMinorUnits);
    }

    // Кешбэк начисляется при старте на ВСЮ предоплаченную сумму. Вернуть деньги и оставить
    // кешбэк за неигранные часы — это дыра, а не щедрость.
    [Fact]
    public async Task SelfEnd_UnwindsCashbackInProportionToTheRefund()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        await EnableSessionCashbackAsync(factory, ctx.OrgId, basisPoints: 1000); // 10%
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var sessionId = await StartHourSessionAsync(factory, client, ctx);

        // Час стоит 60 000, кешбэк 10% = 6 000.
        Assert.Equal(6_000, await SumByTypeAsync(factory, sessionId, AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.Cashback));

        (await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        // Вернулось 59 000 из 60 000, значит снимается 59/60 кешбэка = 5 900.
        Assert.Equal(59_000, await SumByTypeAsync(factory, sessionId, AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.Refund));
        Assert.Equal(-5_900, await SumByTypeAsync(factory, sessionId, AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.Reversal));
    }

    // Клуб без кешбэка: разматывать нечего, и лишней записи в леджере появиться не должно.
    [Fact]
    public async Task SelfEnd_WithoutCashback_WritesNoReversal()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        (await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        Assert.Equal(0, await SumByTypeAsync(factory, sessionId, AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.Reversal));
    }

    [Fact]
    public async Task SelfEnd_RefundsTheUnplayedTime()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        var afterStart = await WalletBalanceAsync(factory, ctx.PlayerId);

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlayerSelfEndSessionResponse>();
        Assert.NotNull(body);

        // Встал сразу — списывается минимальная тарифицируемая длительность, остальное вернулось.
        Assert.Equal(1, body!.BilledMinutes);
        Assert.Equal(59_000, body.Refunded.MinorUnits);
        Assert.Equal(afterStart + 59_000, await WalletBalanceAsync(factory, ctx.PlayerId));
    }

    /// <summary>
    /// Пауза не игра. Её ставит администратор (#279), и раньше расчёт брал «сейчас минус старт» —
    /// игрок платил за время, когда ПК стоял запертым.
    /// </summary>
    [Fact]
    public async Task SelfEnd_DoesNotChargeForTimeOnPause()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        // Полчаса назад сел, двадцать минут из них ПК стоял на паузе: сыграно десять.
        await ShiftSessionAsync(factory, sessionId, startedMinutesAgo: 30, pausedMinutes: 20);

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));

        var body = await response.Content.ReadFromJsonAsync<PlayerSelfEndSessionResponse>();
        Assert.Equal(10, body!.BilledMinutes);
        Assert.Equal(50_000, body.Refunded.MinorUnits);
    }

    /// <summary>Экран обещает ровно то, что вернёт выход, — и ничего не пишет, пока не нажали.</summary>
    [Fact]
    public async Task EndQuote_PromisesWhatTheExitWillReturn_AndMovesNoMoney()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        await ShiftSessionAsync(factory, sessionId, startedMinutesAgo: 30, pausedMinutes: 20);
        var beforeQuote = await WalletBalanceAsync(factory, ctx.PlayerId);

        var quote = await client.GetFromJsonAsync<PlayerEndQuoteDto>($"/api/me/sessions/{sessionId}/end-quote");

        Assert.Equal(10, quote!.BilledMinutes);
        Assert.Equal(50_000, quote.Refund.MinorUnits);
        Assert.Equal(beforeQuote, await WalletBalanceAsync(factory, ctx.PlayerId));

        var end = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));
        var ended = await end.Content.ReadFromJsonAsync<PlayerSelfEndSessionResponse>();
        Assert.Equal(quote.Refund.MinorUnits, ended!.Refunded.MinorUnits);
    }

    [Fact]
    public async Task EndQuote_ForeignSession_Returns404()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.GetAsync($"/api/me/sessions/{Guid.NewGuid()}/end-quote");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task ShiftSessionAsync(PlatformApiFactory factory, Guid sessionId, int startedMinutesAgo, int pausedMinutes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(candidate => candidate.SessionId == sessionId);
        // Чуть меньше названного: часы идут, пока тест дойдёт до выхода, а тариф округляет минуты
        // вверх — без запаса «десять минут» превратились бы в одиннадцать.
        session.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-startedMinutesAgo).AddSeconds(30);
        session.TotalPausedSeconds = pausedMinutes * 60;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Экран выбора получает готовые суммы одним запросом: тарифы филиала с вариантами и пакеты
    /// игрока с остатком. Клиент цену не считает.
    /// </summary>
    [Fact]
    public async Task StartOffers_PriceTheHours_AndListThePlayersPackages()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        await AddTariffCardAsync(factory, ctx, "Общий");
        var packageId = await SeedPackageAsync(factory, ctx, includedSeconds: 3 * 3600);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var offers = await client.GetFromJsonAsync<PlayerStartOffersDto>(
            $"/api/me/devices/{await SeatingCodeAsync(factory, ctx)}/start-offers");

        Assert.Equal(100_000, offers!.Balance.MinorUnits);
        var tariff = Assert.Single(offers.Tariffs);
        Assert.Equal("Общий", tariff.Name);
        Assert.Equal(60_000, tariff.PricePerHour.MinorUnits);
        var hour = tariff.Options.Single(option => option.Minutes == 60);
        Assert.Equal(60_000, hour.Amount.MinorUnits);
        Assert.Equal(40_000, hour.BalanceAfter.MinorUnits);
        Assert.False(tariff.Options.Single(option => option.Minutes == 120).Affordable);
        var package = Assert.Single(offers.Packages);
        Assert.Equal(packageId, package.PlayerPackageId);
        Assert.Equal(180, package.RemainingMinutes);
    }

    [Fact]
    public async Task StartOffers_WithAWrongCode_CountLikeAWrongStart()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.GetAsync("/api/me/devices/000000/start-offers");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.SeatingCodeAttemptCounters
            .Where(counter => counter.Scope == SeatingCodeAttemptScopes.Player)
            .Select(counter => counter.FailedCount)
            .SingleAsync());
    }

    [Fact]
    public async Task ExtendOffers_MoveTheEndFromTheCurrentEnd()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");
        var sessionId = await StartHourSessionAsync(factory, client, ctx);

        var offers = await client.GetFromJsonAsync<PlayerExtendOffersDto>($"/api/me/sessions/{sessionId}/extend-offers");

        Assert.Null(offers!.UnavailableReason);
        var halfHour = offers.Options.Single(option => option.Minutes == 30);
        Assert.Equal(30_000, halfHour.Amount.MinorUnits);
        Assert.Equal(10_000, halfHour.BalanceAfter.MinorUnits);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var endsAt = (await db.Sessions.SingleAsync(session => session.SessionId == sessionId)).EndsAtUtc!.Value;
        Assert.Equal(endsAt.AddMinutes(30), halfHour.EndsAtUtc);
    }

    /// <summary>
    /// Сесть по своему пакету: минуты уходят из пакета, кошелёк не трогается. Встал раньше —
    /// неиграное возвращается в пакет, а не сгорает, как раньше.
    /// </summary>
    [Fact]
    public async Task SelfStart_ByPackage_ThenAnEarlyExit_ReturnsTheUnplayedMinutesToThePackage()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        var packageId = await SeedPackageAsync(factory, ctx, includedSeconds: 3 * 3600);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var start = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(await SeatingCodeAsync(factory, ctx), string.Empty, 120, Guid.NewGuid().ToString("N"), packageId));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var sessionId = (await start.Content.ReadFromJsonAsync<SessionCommandResponse>())!.Session.SessionId;
        Assert.Equal(100_000, await WalletBalanceAsync(factory, ctx.PlayerId));
        Assert.Equal(60, await PackageRemainingMinutesAsync(factory, packageId));

        // Сорок минут сыграно, из них десять — на паузе: списывается тридцать.
        await ShiftSessionAsync(factory, sessionId, startedMinutesAgo: 40, pausedMinutes: 10);
        var quote = await client.GetFromJsonAsync<PlayerEndQuoteDto>($"/api/me/sessions/{sessionId}/end-quote");
        Assert.Equal(90, quote!.PackageMinutesReturned);

        var end = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));
        var ended = await end.Content.ReadFromJsonAsync<PlayerSelfEndSessionResponse>();
        Assert.Equal(90, ended!.PackageMinutesReturned);
        Assert.Equal(150, await PackageRemainingMinutesAsync(factory, packageId));
        Assert.Equal(100_000, await WalletBalanceAsync(factory, ctx.PlayerId));
    }

    /// <summary>Карточка тарифа к версии из сида: без неё тариф не попадает в список для игрока.</summary>
    private static async Task AddTariffCardAsync(PlatformApiFactory factory, SelfStartContext ctx, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var version = await db.TariffVersions.SingleAsync(
            candidate => candidate.TariffVersionId == Guid.Parse(ctx.TariffRuleVersionId));
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = version.TariffId,
            OrganizationId = ctx.OrgId,
            BranchId = ctx.BranchId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = Now.AddYears(-1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedPackageAsync(PlatformApiFactory factory, SelfStartContext ctx, int includedSeconds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var packageId = Guid.NewGuid();
        db.PlayerPackages.Add(new PlayerPackageEntity
        {
            PlayerPackageId = packageId,
            PackageDefinitionId = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            BranchId = ctx.BranchId,
            PlayerAccountId = ctx.PlayerId,
            Name = "Пакет 3 часа",
            CurrencyCode = "TJS",
            PurchasedPriceMinorUnits = 100_000,
            IncludedSeconds = includedSeconds,
            PurchasedAtUtc = Now,
            ExpiresAtUtc = Now.AddDays(30)
        });
        db.LedgerEntries.Add(AFK4.Platform.Api.Billing.BillingEntryFactory.Create(
            ctx.OrgId,
            ctx.BranchId,
            ctx.PlayerId,
            sessionId: null,
            packageId,
            AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.PackagePurchase,
            AFK4.Shared.Contracts.Billing.LedgerAccountTypeNames.PackageTime,
            amountMinorUnits: 0,
            includedSeconds,
            "TJS",
            "package purchase",
            "package purchase",
            reversesLedgerEntryId: null,
            Guid.Empty,
            Now));
        await db.SaveChangesAsync();
        return packageId;
    }

    private static async Task<int> PackageRemainingMinutesAsync(PlatformApiFactory factory, Guid packageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var remaining = await AFK4.Platform.Api.Billing.LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
            db, packageId, CancellationToken.None);
        return (remaining.IncludedSeconds + remaining.BonusSeconds) / 60;
    }

    [Fact]
    public async Task SelfEnd_ClosesTheSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        (await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(s => s.SessionId == sessionId);
        Assert.NotEqual(SessionStateNames.Active, session.State);
    }

    // Что именно защищает от второго возврата: после первого закрытия сессия уже не Active, и
    // повторный вызов не доходит до денег вовсе — ни по тому же ключу, ни по новому. (От
    // ОДНОВРЕМЕННЫХ вызовов защищает оптимистичная версия сессии: записи возврата попадают в ту
    // же транзакцию, что и смена состояния. Гонку здесь не воспроизвести — PlatformApiFactory
    // работает на in-memory провайдере, — поэтому она подтверждается кодом, а не этим тестом.)
    [Fact]
    public async Task SelfEnd_AfterTheSessionIsClosed_TouchesNoMoney()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var sessionId = await StartHourSessionAsync(factory, client, ctx);
        var key = Guid.NewGuid().ToString("N");

        (await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end", new PlayerSelfEndSessionRequest(key)))
            .EnsureSuccessStatusCode();
        var afterFirst = await WalletBalanceAsync(factory, ctx.PlayerId);

        await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end", new PlayerSelfEndSessionRequest(key));
        await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(afterFirst, await WalletBalanceAsync(factory, ctx.PlayerId));
    }

    // Чужую сессию не закончить — тем же правилом, что и продление.
    [Fact]
    public async Task SelfEnd_ForeignSession_Returns404()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{Guid.NewGuid()}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SelfEnd_WithoutToken_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{Guid.NewGuid()}/end",
            new PlayerSelfEndSessionRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
