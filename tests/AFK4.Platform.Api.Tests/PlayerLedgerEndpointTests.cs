using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Выписка по кошельку — глазами игрока.
///
/// Три ленты истории, которые у него уже есть, построены не на деньгах: визиты берутся из сессий,
/// покупки — из чеков магазина. Ни одна не смотрит в журнал, поэтому пополнение, кешбэк,
/// реферальный бонус, ручная правка оператора и погашение долга не видны нигде. Человек видит, за
/// что списали, и не видит, откуда пришло, — кошелёк у него не сходится, и объяснить нечем.
///
/// Это не тот же список, что у стойки. Оператор разбирает спорную ситуацию и должен видеть каждую
/// проводку; игроку проводки, между которыми ничего не произошло, только мешают.
/// </summary>
public sealed class PlayerLedgerEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T12:00:00Z");

    [Fact]
    public async Task Ledger_ShowsWhereTheMoneyCameFromAndWhereItWent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.TopUp, 20_000, Now.AddHours(-5));
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.Cashback, 500, Now.AddHours(-4));
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.GameplayCharge, -3_000, Now.AddHours(-3));
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.ReservationNoShowFee, -1_500, Now.AddHours(-2));

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.NotNull(page);
        Assert.Equal(
            new[]
            {
                LedgerEntryTypeNames.ReservationNoShowFee,
                LedgerEntryTypeNames.GameplayCharge,
                LedgerEntryTypeNames.Cashback,
                LedgerEntryTypeNames.TopUp
            },
            page!.Items.Select(item => item.EntryType).ToArray());
        Assert.Equal(20_000, page.Items.Last().Amount.MinorUnits);
    }

    /// <summary>
    /// Сумма без состава — половина ответа на вопрос «за что». Чек визита у игрока есть и давно
    /// работает; строке выписки не хватало только ссылки на него.
    /// </summary>
    [Fact]
    public async Task Ledger_PointsAtTheReceiptOfTheVisitItCameFrom()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        var sessionId = Guid.NewGuid();
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.GameplayCharge, -4_500, Now.AddHours(-2),
            sessionId: sessionId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Receipts.Add(new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(),
                OrganizationId = p.OrgId,
                BranchId = p.BranchId,
                SessionId = sessionId,
                ReceiptNumber = "Ч-000777",
                TotalMinorUnits = 4_500,
                CurrencyCode = "TJS",
                CreatedAtUtc = Now.AddHours(-2)
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(sessionId, page!.Items.Single().ReceiptSessionId);
    }

    /// <summary>
    /// Ссылка, ведущая в «чека нет», хуже её отсутствия: визит без выбитого чека остаётся без
    /// ссылки, и строка просто не нажимается.
    /// </summary>
    [Fact]
    public async Task Ledger_LeavesAVisitWithoutAReceiptUnlinked()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.GameplayCharge, -4_500, Now.AddHours(-2),
            sessionId: Guid.NewGuid());

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Null(page!.Items.Single().ReceiptSessionId);
    }

    /// <summary>
    /// Удержание под бронь видно, и видно, почему деньги вернулись. Раньше обе строки прятались как
    /// «событие без итога» — и остаток кошелька не складывался из видимых строк: в баланс удержание
    /// входит, а в выписку не входило.
    /// </summary>
    [Fact]
    public async Task Ledger_ShowsTheHoldAndWhyTheMoneyCameBack()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        var reservationId = Guid.NewGuid();
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.TopUp, 20_000, Now.AddHours(-5));
        var holdId = await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.ReservationHold, -1_500, Now.AddHours(-4),
            reason: ReservationHold.Reason(reservationId));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.Reversal, 1_500, Now.AddHours(-3), reverses: holdId,
            reason: ReservationHold.ReleaseReason(reservationId, ReservationHoldCauses.Seated));

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(
            new[] { LedgerEntryTypeNames.Reversal, LedgerEntryTypeNames.ReservationHold, LedgerEntryTypeNames.TopUp },
            page!.Items.Select(item => item.EntryType).ToArray());
        Assert.Equal(ReservationHoldCauses.Seated, page.Items[0].HoldReleaseCause);
        Assert.Null(page.Items[1].HoldReleaseCause);
        Assert.Equal(
            new long[] { 20_000, 18_500, 20_000 },
            page.Items.Select(item => item.WalletBalanceAfter!.MinorUnits).ToArray());
    }

    /// <summary>
    /// Неявка: снятие удержания и удержание за неявку пишутся одним моментом. Остаток у пары
    /// должен сходиться с порядком строк, иначе выписка покажет число, которого не было.
    /// </summary>
    [Fact]
    public async Task Ledger_BalanceFollowsRowsWrittenAtTheSameMoment()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        var reservationId = Guid.NewGuid();
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.TopUp, 10_000, Now.AddHours(-5));
        var holdId = await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.ReservationHold, -2_000, Now.AddHours(-4),
            reason: ReservationHold.Reason(reservationId));
        var moment = Now.AddHours(-2);
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.Reversal, 2_000, moment, reverses: holdId,
            reason: ReservationHold.ReleaseReason(reservationId, ReservationHoldCauses.NoShow));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.ReservationNoShowFee, -2_000, moment,
            reason: ReservationHold.NoShowFeeReason(reservationId));

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        AssertBalancesAddUp(page!.Items, expectedNewest: 8_000);
        Assert.Contains(page.Items, item => item.HoldReleaseCause == ReservationHoldCauses.NoShow);
    }

    /// <summary>
    /// Пакетное и бонусное время остаток кошелька не двигает: у такой строки остатка нет, а соседние
    /// строки кошелька сходятся через неё.
    /// </summary>
    [Fact]
    public async Task Ledger_TimeRowsCarryNoWalletBalance()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.TopUp, 5_000, Now.AddHours(-3));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.BonusGrant, 0, Now.AddHours(-2),
            accountType: LedgerAccountTypeNames.BonusTime);
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.GameplayCharge, -1_000, Now.AddHours(-1));

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(4_000, page!.Items[0].WalletBalanceAfter!.MinorUnits);
        Assert.Null(page.Items[1].WalletBalanceAfter);
        Assert.Equal(5_000, page.Items[2].WalletBalanceAfter!.MinorUnits);
    }

    /// <summary>Незнакомый повод наружу не уходит: причина записи — служебная строка.</summary>
    [Fact]
    public async Task Ledger_DoesNotLeakAnUnknownReleaseCause()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        var reservationId = Guid.NewGuid();
        var holdId = await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.ReservationHold, -1_000, Now.AddHours(-2),
            reason: ReservationHold.Reason(reservationId));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.Reversal, 1_000, Now.AddHours(-1), reverses: holdId,
            reason: ReservationHold.ReleaseReason(reservationId, "operator_typo_42"));

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var body = await (await client.GetAsync("/api/me/wallet/ledger")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("operator_typo_42", body, StringComparison.Ordinal);
    }

    private static void AssertBalancesAddUp(IReadOnlyList<PlayerLedgerEntryDto> rows, long expectedNewest)
    {
        var wallet = rows.Where(row => row.WalletBalanceAfter is not null).ToList();
        Assert.Equal(expectedNewest, wallet[0].WalletBalanceAfter!.MinorUnits);
        for (var index = 0; index < wallet.Count - 1; index++)
        {
            Assert.Equal(
                wallet[index].WalletBalanceAfter!.MinorUnits - wallet[index].Amount.MinorUnits,
                wallet[index + 1].WalletBalanceAfter!.MinorUnits);
        }
    }

    /// <summary>
    /// Возврат настоящего списания — событие с итогом, и прятать его нельзя: человеку вернули
    /// деньги, и он вправе это видеть. Скрывается только реверс заморозки.
    /// </summary>
    [Fact]
    public async Task Ledger_KeepsAReversalOfARealCharge()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        var chargeId = await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.GameplayCharge, -3_000, Now.AddHours(-4));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.Reversal, 3_000, Now.AddHours(-3), reverses: chargeId);

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(
            new[] { LedgerEntryTypeNames.Reversal, LedgerEntryTypeNames.GameplayCharge },
            page!.Items.Select(item => item.EntryType).ToArray());
    }

    /// <summary>
    /// Остаток не рвётся на границе страниц: первая строка второй страницы продолжает последнюю
    /// строку первой, а самая новая строка сходится с балансом кошелька.
    /// </summary>
    [Fact]
    public async Task Ledger_BalanceContinuesAcrossPages()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        for (var index = 0; index < 4; index++)
        {
            var reservationId = Guid.NewGuid();
            var holdId = await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.ReservationHold, -100, Now.AddMinutes(-index * 10 - 5),
                reason: ReservationHold.Reason(reservationId));
            await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.Reversal, 100, Now.AddMinutes(-index * 10 - 4), reverses: holdId,
                reason: ReservationHold.ReleaseReason(reservationId, ReservationHoldCauses.Cancelled));
            await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.TopUp, 1_000, Now.AddMinutes(-index * 10));
        }

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var rows = new List<PlayerLedgerEntryDto>();
        string? cursor = null;
        do
        {
            var url = cursor is null
                ? "/api/me/wallet/ledger?limit=5"
                : $"/api/me/wallet/ledger?limit=5&cursor={Uri.EscapeDataString(cursor)}";
            var page = await (await client.GetAsync(url)).Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();
            rows.AddRange(page!.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(12, rows.Count);
        Assert.Equal(12, rows.Select(row => row.LedgerEntryId).Distinct().Count());
        AssertBalancesAddUp(rows, expectedNewest: 4_000);
    }

    /// <summary>Чужой выписки не существует: маршрут отвечает только про своего владельца.</summary>
    [Fact]
    public async Task Ledger_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/wallet/ledger");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Кто именно из сотрудников провёл запись — внутреннее дело клуба. Игроку важно, что
    /// случилось с его деньгами, а не табельный номер кассира.
    /// </summary>
    [Fact]
    public async Task Ledger_DoesNotCarryStaffIdentifiers()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.ManualCorrection, 700, Now);

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var body = await (await client.GetAsync("/api/me/wallet/ledger")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("createdByStaffUserId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reason", body, StringComparison.OrdinalIgnoreCase);
    }
}
