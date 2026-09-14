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
        // Удаление собственной учётной записи. Требование App Store к любому приложению с
        // регистрацией — и до сих пор его не было ни на одном слое.
        //
        // Отказ при долге и при остатке — не перестраховка. Деньги на кошельке принадлежат
        // человеку, а долг он должен клубу: обнулить нажатием в телефоне и то и другое значит
        // решить чужой денежный вопрос молча и необратимо. Поэтому сначала касса, потом удаление.
        //
        // Записи журнала остаются: это бухгалтерия клуба, а не личные данные. Обезличивается
        // личность — имя, телефон, PIN, — и номер освобождается: иначе «удалить аккаунт» навсегда
        // забирало бы у человека его же телефон.
        app.MapDelete("/api/me", async (
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

            var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
                candidate => candidate.PlatformPersonId == context.PlatformPersonId,
                cancellationToken);
            if (person is null)
            {
                return Results.Unauthorized();
            }

            var accountIds = await dbContext.PlayerAccounts
                .Where(account => account.PlatformPersonId == person.PlatformPersonId)
                .Select(account => account.PlayerAccountId)
                .ToListAsync(cancellationToken);

            var liveStates = new[]
            {
                SessionStateNames.Requested,
                SessionStateNames.Active,
                SessionStateNames.Paused,
                SessionStateNames.Ending
            };
            if (await dbContext.Sessions.AnyAsync(
                session => session.PlayerAccountId != null
                    && accountIds.Contains(session.PlayerAccountId.Value)
                    && liveStates.Contains(session.State),
                cancellationToken))
            {
                return Results.Conflict(new { Error = "active_session" });
            }

            foreach (var accountId in accountIds)
            {
                var balances = await LedgerBalanceProjector.GetClubBalancesAsync(
                    dbContext, accountId, cancellationToken);
                if (balances.DebtMinorUnits > 0)
                {
                    return Results.Conflict(new { Error = "outstanding_debt" });
                }

                // Придержанное под брони — тоже деньги человека, просто обещанные клубу. Остаток
                // ноль при замороженной тысяче не значит «денег нет».
                if (balances.WalletMinorUnits + balances.HeldMinorUnits > 0)
                {
                    return Results.Conflict(new { Error = "remaining_balance" });
                }
            }

            var now = timeProvider.GetUtcNow();

            // Номер заменяется идентификатором самой личности: он уникален по построению, поэтому
            // уникальный индекс на телефон не подведёт, и на канонический номер («+» и цифры) не
            // похож ни одним символом — значит поиском по телефону эта запись больше не находится.
            person.PhoneNumber = person.PlatformPersonId.ToString("N");
            person.DisplayName = "Удалённая учётная запись";
            person.PreferredLocale = null;
            person.PhoneVerifiedAtUtc = null;
            person.PinHash = null;
            person.PinSetAtUtc = null;
            person.IsActive = false;
            person.UpdatedAtUtc = now;

            var accounts = await dbContext.PlayerAccounts
                .Where(account => account.PlatformPersonId == person.PlatformPersonId)
                .ToListAsync(cancellationToken);
            foreach (var account in accounts)
            {
                account.DisplayName = person.DisplayName;
                account.PhoneNumber = null;
                account.IsActive = false;
            }

            // Вход обрывается сразу: токены, выданные до удаления, иначе прожили бы свой срок.
            dbContext.PlatformPersonAccessTokens.RemoveRange(
                dbContext.PlatformPersonAccessTokens.Where(token => token.PlatformPersonId == person.PlatformPersonId));
            dbContext.PlatformPersonRefreshTokens.RemoveRange(
                dbContext.PlatformPersonRefreshTokens.Where(token => token.PlatformPersonId == person.PlatformPersonId));

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("player-me");

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
