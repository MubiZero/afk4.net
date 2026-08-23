using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Localization;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// «Кто я и где у меня счета» — единственный маршрут игрока, которому клуб не нужен: он его как
/// раз и перечисляет. Общей суммы денег здесь нет: у каждого клуба своя касса, и число, которое
/// нельзя потратить ни в одном из них, врало бы человеку.
/// </summary>
internal static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", async (
            IPlatformPersonContextAccessor personContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var context = personContextAccessor.Current;
            if (context is null)
            {
                return Results.Unauthorized();
            }

            var person = await dbContext.PlatformPersons
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.PlatformPersonId == context.PlatformPersonId,
                    cancellationToken);
            if (person is null)
            {
                return Results.Unauthorized();
            }

            var accounts = await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(account => account.PlatformPersonId == person.PlatformPersonId && account.IsActive)
                .OrderBy(account => account.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var organizationIds = accounts.Select(account => account.OrganizationId).Distinct().ToList();
            var organizationNames = await dbContext.Organizations
                .AsNoTracking()
                .Where(organization => organizationIds.Contains(organization.OrganizationId))
                .ToDictionaryAsync(
                    organization => organization.OrganizationId,
                    organization => organization.Name,
                    cancellationToken);

            var visitCounts = await CountVisitsAsync(dbContext, accounts, cancellationToken);

            var clubs = new List<MyClubDto>(accounts.Count);
            foreach (var account in accounts)
            {
                var balances = await LedgerBalanceProjector.GetClubBalancesAsync(
                    dbContext, account.PlayerAccountId, cancellationToken);
                clubs.Add(new MyClubDto(
                    account.OrganizationId,
                    organizationNames.GetValueOrDefault(account.OrganizationId, string.Empty),
                    account.PlayerAccountId,
                    account.HomeBranchId,
                    await ResolveCurrencyAsync(dbContext, account.PlayerAccountId, cancellationToken),
                    balances.WalletMinorUnits,
                    balances.HeldMinorUnits,
                    balances.DebtMinorUnits,
                    visitCounts.GetValueOrDefault(account.PlayerAccountId, 0)));
            }

            return Results.Ok(new MeDto(
                new MePersonDto(
                    person.PlatformPersonId,
                    person.PhoneNumber,
                    person.DisplayName,
                    person.PreferredLocale,
                    person.PhoneVerifiedAtUtc is not null,
                    person.PinHash is not null,
                    person.NetworkBanAtUtc is not null,
                    // Причина едет самому человеку: запрет, о котором он не может узнать, за что,
                    // читается как поломка приложения — и он идёт спорить к стойке, которая его
                    // не ставила.
                    person.NetworkBanReason),
                clubs));
        }).RequireRateLimiting("player-me");

        // Имя и язык — ровно те два поля, которые спрашиваются при регистрации, и единственные,
        // которые человек про себя называет сам. PIN сюда не входит: он задаётся отдельно и в ту
        // секунду, когда впервые нужен.
        app.MapPatch("/api/me", async (
            UpdateMyProfileRequest request,
            IPlatformPersonContextAccessor personContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var context = personContextAccessor.Current;
            if (context is null)
            {
                return Results.Unauthorized();
            }

            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (displayName.Length is 0 or > MaxDisplayNameLength)
            {
                return Results.BadRequest(new { error = "invalid_display_name" });
            }

            // Язык, которого у нас нет, тихо превратился бы в русский — а человек думал бы, что
            // выбрал. Пустое значение — это «язык не выбран», и оно допустимо.
            var preferredLocale = string.IsNullOrWhiteSpace(request.PreferredLocale)
                ? null
                : request.PreferredLocale.Trim();
            if (preferredLocale is not null && !SupportedLocales.IsSupported(preferredLocale))
            {
                return Results.BadRequest(new { error = "unsupported_locale" });
            }

            var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
                candidate => candidate.PlatformPersonId == context.PlatformPersonId,
                cancellationToken);
            if (person is null)
            {
                return Results.Unauthorized();
            }

            person.DisplayName = displayName;
            person.PreferredLocale = preferredLocale;
            person.UpdatedAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new MePersonDto(
                person.PlatformPersonId,
                person.PhoneNumber,
                person.DisplayName,
                person.PreferredLocale,
                person.PhoneVerifiedAtUtc is not null,
                person.PinHash is not null,
                person.NetworkBanAtUtc is not null,
                person.NetworkBanReason));
        }).RequireRateLimiting("player-me");

        // PIN задаётся и меняется только здесь: человек уже вошёл в приложение, и это не стоит ни
        // одной SMS. Старый PIN не спрашивается — именно этот маршрут и есть ответ забывшему его.
        app.MapPut("/api/me/pin", async (
            SetMyPinRequest request,
            IPlatformPersonContextAccessor personContextAccessor,
            IPlatformPinService pinService,
            CancellationToken cancellationToken) =>
        {
            var context = personContextAccessor.Current;
            if (context is null)
            {
                return Results.Unauthorized();
            }

            var status = await pinService.SetAsync(
                context.PlatformPersonId, request.Pin, cancellationToken);

            return status switch
            {
                SetPinStatus.Updated => Results.NoContent(),
                SetPinStatus.InvalidPin => Results.BadRequest(new { error = "invalid_pin" }),
                _ => Results.Unauthorized(),
            };
        }).RequireRateLimiting("player-me");
    }

    /// <summary>Столько же, сколько отведено под имя в <c>platform_persons</c>.</summary>
    private const int MaxDisplayNameLength = 160;

    /// <summary>Стаж считается так же, как на экране достижений: по закрытым визитам.</summary>
    private static async Task<Dictionary<Guid, int>> CountVisitsAsync(
        PlatformDbContext dbContext,
        IReadOnlyCollection<PlayerAccountEntity> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return [];
        }

        var accountIds = accounts.Select(account => account.PlayerAccountId).ToList();
        return await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.PlayerAccountId != null
                && accountIds.Contains(session.PlayerAccountId!.Value)
                && session.State == SessionStateNames.Ended
                && session.StartedAtUtc != null
                && session.EndedAtUtc != null)
            .GroupBy(session => session.PlayerAccountId!.Value)
            .Select(group => new { PlayerAccountId = group.Key, Visits = group.Count() })
            .ToDictionaryAsync(row => row.PlayerAccountId, row => row.Visits, cancellationToken);
    }

    private static async Task<string> ResolveCurrencyAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var currencyCodes = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (currencyCodes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Cannot show club balances for player account '{playerAccountId}' because ledger entries contain multiple currencies.");
        }

        return currencyCodes.SingleOrDefault() ?? "TJS";
    }
}
