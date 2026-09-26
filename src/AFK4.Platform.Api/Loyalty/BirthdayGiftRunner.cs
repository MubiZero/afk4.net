using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Loyalty;

/// <summary>Правила подарка на день рождения, общие для настроек и задания.</summary>
public static class BirthdayGifts
{
    /// <summary>Подарок только тем, кто был в клубе за полгода, — так он зовёт своих, а не всех.</summary>
    public const int DefaultRecentVisitDays = 180;

    public const int MaxRecentVisitDays = 730;

    /// <summary>Предел подарка — от опечатки в сумме, а не от щедрости: 10 000 сомони.</summary>
    public const long MaxAmountMinorUnits = 1_000_000;

    /// <summary>
    /// Дату нужно ввести хотя бы за столько дней до дня рождения. Иначе её вписали бы сегодняшней
    /// ради подарка: «с днём рождения» тому, кто его не празднует, клуб оплачивать не должен.
    /// </summary>
    public const int BirthDateKnownDays = 30;

    /// <summary>Не раньше этого часа по времени клуба: поздравление в полночь будит, а не радует.</summary>
    public const int GrantFromLocalHour = 10;
}

/// <summary>
/// Вручает подарки на день рождения (владелец, 2026-09-26). Раз в прогон ищет тех, у кого сегодня —
/// по календарю их клуба — день рождения, кладёт подарок на кошелёк и шлёт поздравление.
///
/// Повтор не страшен: отметка «вручено в этом году» ставится вместе с проводкой, а уникальный индекс
/// не даст второй — даже если два прогона столкнутся.
/// </summary>
public sealed class BirthdayGiftRunner(
    PlatformDbContext dbContext,
    IOrganizationEntitlements entitlements,
    PlayerPushNotifier push,
    IOptions<BillingOptions> billingOptions,
    TimeProvider timeProvider,
    ILogger<BirthdayGiftRunner> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var programmes = await dbContext.OrganizationBirthdayGiftSettings
            .AsNoTracking()
            .Where(settings => settings.Enabled && settings.AmountMinorUnits > 0)
            .ToListAsync(cancellationToken);

        var granted = 0;
        foreach (var settings in programmes)
        {
            // Общий рубильник тот же, что у кешбэка: выключенная тарифом лояльность не раздаёт деньги.
            if (!await entitlements.IsEnabledAsync(settings.OrganizationId, PlatformFeatureNames.Loyalty, cancellationToken))
            {
                continue;
            }

            granted += await GrantForOrganizationAsync(settings, now, cancellationToken);
        }

        return granted;
    }

    private async Task<int> GrantForOrganizationAsync(
        OrganizationBirthdayGiftSettingsEntity settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Кандидаты — по месяцу и дню во всех поясах сразу: у клуба в Душанбе «сегодня» наступает
        // раньше, чем в UTC. Точный день каждого сверяется ниже, по поясу его клуба.
        var days = Enumerable.Range(-1, 3)
            .Select(offset => DateOnly.FromDateTime(now.UtcDateTime.AddDays(offset)))
            .ToList();
        var months = days.Select(day => day.Month).Distinct().ToList();
        var candidates = await (
                from account in dbContext.PlayerAccounts.AsNoTracking()
                join person in dbContext.PlatformPersons.AsNoTracking()
                    on account.PlatformPersonId equals (Guid?)person.PlatformPersonId
                join branch in dbContext.Branches.AsNoTracking() on account.HomeBranchId equals branch.BranchId
                where account.OrganizationId == settings.OrganizationId
                      && account.IsActive
                      && person.IsActive
                      && person.BirthDate != null
                      && months.Contains(person.BirthDate.Value.Month)
                select new
                {
                    account.PlayerAccountId,
                    account.HomeBranchId,
                    BirthDate = person.BirthDate!.Value,
                    person.BirthDateSetAtUtc,
                    branch.PreferredTimeZone
                })
            .ToListAsync(cancellationToken);

        var granted = 0;
        foreach (var candidate in candidates)
        {
            var localNow = TimeZoneInfo.ConvertTime(now, ZoneOf(candidate.PreferredTimeZone));
            var today = DateOnly.FromDateTime(localNow.DateTime);
            var birthday = PlayerBirthdays.BirthdayIn(today.Year, candidate.BirthDate);
            if (birthday != today || localNow.Hour < BirthdayGifts.GrantFromLocalHour)
            {
                continue;
            }

            if (candidate.BirthDateSetAtUtc is not { } setAt
                || setAt > now - TimeSpan.FromDays(BirthdayGifts.BirthDateKnownDays))
            {
                continue;
            }

            if (settings.RecentVisitDays > 0)
            {
                var since = now - TimeSpan.FromDays(settings.RecentVisitDays);
                var visited = await dbContext.Sessions.AsNoTracking().AnyAsync(
                    session => session.PlayerAccountId == candidate.PlayerAccountId && session.StartedAtUtc >= since,
                    cancellationToken);
                if (!visited)
                {
                    continue;
                }
            }

            if (await dbContext.PlayerBirthdayGifts.AnyAsync(
                    gift => gift.PlayerAccountId == candidate.PlayerAccountId && gift.Year == today.Year, cancellationToken))
            {
                continue;
            }

            if (await GrantAsync(settings, candidate.PlayerAccountId, candidate.HomeBranchId, today.Year, now, cancellationToken) is { } gift)
            {
                granted++;
                await push.BirthdayGiftAsync(
                    candidate.PlayerAccountId, settings.OrganizationId, candidate.HomeBranchId,
                    gift.AmountMinorUnits, gift.CurrencyCode, gift.GiftId, cancellationToken);
            }
        }

        return granted;
    }

    private async Task<(Guid GiftId, long AmountMinorUnits, string CurrencyCode)?> GrantAsync(
        OrganizationBirthdayGiftSettingsEntity settings, Guid playerAccountId, Guid branchId, int year,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Валюта — та, в которой игрок уже платил в этом клубе; нет проводок — валюта платформы.
        var currency = await dbContext.LedgerEntries.AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => entry.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken) ?? billingOptions.Value.DefaultCurrencyCode;

        // Подарок — начисление самой системы (actor = Guid.Empty), не касса смены: ShiftId пуст.
        var entry = BillingEntryFactory.Create(
            settings.OrganizationId,
            branchId,
            playerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.BirthdayBonus,
            LedgerAccountTypeNames.Wallet,
            settings.AmountMinorUnits,
            quantitySeconds: 0,
            currency,
            description: LedgerEntryTypeNames.BirthdayBonus,
            reason: $"birthday {year}",
            reversesLedgerEntryId: null,
            actorStaffUserId: Guid.Empty,
            createdAtUtc: now);
        var gift = new PlayerBirthdayGiftEntity
        {
            PlayerBirthdayGiftId = Guid.NewGuid(),
            OrganizationId = settings.OrganizationId,
            PlayerAccountId = playerAccountId,
            Year = year,
            AmountMinorUnits = settings.AmountMinorUnits,
            LedgerEntryId = entry.LedgerEntryId,
            GrantedAtUtc = now
        };

        dbContext.LedgerEntries.Add(entry);
        dbContext.PlayerBirthdayGifts.Add(gift);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (RelationalFailureClassifier.IsUniqueViolation(exception))
        {
            // Соседний прогон успел первым — подарок уже вручён, второй не нужен.
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Birthday gift for {PlayerAccountId} in {Year} was already granted.", playerAccountId, year);
            return null;
        }

        return (gift.PlayerBirthdayGiftId, gift.AmountMinorUnits, currency);
    }

    private static TimeZoneInfo ZoneOf(string? timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

/// <summary>Тикает <see cref="BirthdayGiftRunner"/> раз в полчаса: подарок уходит утром дня рождения.</summary>
public sealed class BirthdayGiftHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<BirthdayGiftHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(30);

    protected override string JobName => PlatformJobNames.BirthdayGifts;

    protected override TimeSpan Interval => TickInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var granted = await scopedServices.GetRequiredService<BirthdayGiftRunner>().RunAsync(cancellationToken);
        if (granted > 0)
        {
            Log.LogInformation("Birthday gift tick granted {Count} gift(s).", granted);
        }

        return granted;
    }
}
