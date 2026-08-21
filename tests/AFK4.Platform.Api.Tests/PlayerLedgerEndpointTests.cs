using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
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
    /// Заморозка под бронь и её снятие — это одно событие, у которого нет денежного итога.
    /// Показать их значит выдать «−15 c» и «+15 c», между которыми ничего не произошло, и
    /// заставить человека искать пропажу, которой нет. Придержанное объясняет третье число
    /// кошелька, а не выписка.
    /// </summary>
    [Fact]
    public async Task Ledger_HidesTheHoldAndItsRelease()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        await PlayerLedgerTestData.AddAsync(factory, p, LedgerEntryTypeNames.TopUp, 20_000, Now.AddHours(-5));
        var holdId = await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.ReservationHold, -1_500, Now.AddHours(-4));
        await PlayerLedgerTestData.AddAsync(
            factory, p, LedgerEntryTypeNames.Reversal, 1_500, Now.AddHours(-3), reverses: holdId);

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var page = await (await client.GetAsync("/api/me/wallet/ledger"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal([LedgerEntryTypeNames.TopUp], page!.Items.Select(item => item.EntryType).ToArray());
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
    /// Страница отбирается до нарезки, а не после. Отфильтруй мы скрытое из уже набранной
    /// страницы — она приходила бы короче обещанного, а часть событий не показалась бы вовсе.
    /// </summary>
    [Fact]
    public async Task Ledger_PagesOverVisibleEntriesOnly()
    {
        await using var factory = new PlatformApiFactory();
        var p = await PlayerLedgerTestData.SeedPlayerAsync(factory);
        for (var index = 0; index < 4; index++)
        {
            var holdId = await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.ReservationHold, -100, Now.AddMinutes(-index * 10 - 5));
            await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.Reversal, 100, Now.AddMinutes(-index * 10 - 4), reverses: holdId);
            await PlayerLedgerTestData.AddAsync(
                factory, p, LedgerEntryTypeNames.TopUp, 1_000, Now.AddMinutes(-index * 10));
        }

        using var client = factory.CreateClient();
        await PlayerLedgerTestData.AuthenticateAsync(client, p);

        var first = await (await client.GetAsync("/api/me/wallet/ledger?limit=2"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(2, first!.Items.Count);
        Assert.All(first.Items, item => Assert.Equal(LedgerEntryTypeNames.TopUp, item.EntryType));
        Assert.NotNull(first.NextCursor);

        var second = await (await client.GetAsync($"/api/me/wallet/ledger?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerLedgerEntryDto>>();

        Assert.Equal(2, second!.Items.Count);
        Assert.All(second.Items, item => Assert.Equal(LedgerEntryTypeNames.TopUp, item.EntryType));
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
