using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Предел неверных кодов посадки — на игрока и на весь клуб (спека оболочки, §5.4).
///
/// Раньше перебор упирался только в общий предел запросов игрока — 60 в минуту. Живых кодов у
/// клуба столько, сколько свободных ПК, из миллиона вариантов: перебор безнадёжен, пока его
/// считают. Предел на клуб держит того, кто перебирает с нескольких аккаунтов.
/// </summary>
public sealed class SeatingCodeAttemptGuard(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    public const int MaxPlayerFailures = 10;

    public const int MaxOrganizationFailures = 100;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>До какого времени ввод кодов закрыт этому игроку в этом клубе; null — открыт.</summary>
    public async Task<DateTimeOffset?> BlockedUntilAsync(
        Guid playerScopeId, Guid organizationId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var counters = await dbContext.SeatingCodeAttemptCounters
            .AsNoTracking()
            .Where(counter =>
                (counter.Scope == SeatingCodeAttemptScopes.Player && counter.ScopeId == playerScopeId)
                || (counter.Scope == SeatingCodeAttemptScopes.Organization && counter.ScopeId == organizationId))
            .ToListAsync(cancellationToken);

        DateTimeOffset? blockedUntil = null;
        foreach (var counter in counters)
        {
            var limit = counter.Scope == SeatingCodeAttemptScopes.Player ? MaxPlayerFailures : MaxOrganizationFailures;
            var windowEnds = counter.WindowStartedAtUtc.Add(Window);
            if (windowEnds > now && counter.FailedCount >= limit && (blockedUntil is null || windowEnds > blockedUntil))
            {
                blockedUntil = windowEnds;
            }
        }

        return blockedUntil;
    }

    public async Task RecordFailureAsync(Guid playerScopeId, Guid organizationId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await CountAsync(SeatingCodeAttemptScopes.Player, playerScopeId, now, cancellationToken);
        await CountAsync(SeatingCodeAttemptScopes.Organization, organizationId, now, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Два первых промаха разом завели одну и ту же строку — один из них не засчитан. Это
            // счёт попыток, а не деньги: промах на единицу лучше пятисотки человеку, который ошибся
            // в цифре.
            foreach (var entry in dbContext.ChangeTracker.Entries<SeatingCodeAttemptCounterEntity>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task CountAsync(string scope, Guid scopeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var counter = await dbContext.SeatingCodeAttemptCounters
            .SingleOrDefaultAsync(candidate => candidate.Scope == scope && candidate.ScopeId == scopeId, cancellationToken);
        if (counter is null)
        {
            dbContext.SeatingCodeAttemptCounters.Add(new SeatingCodeAttemptCounterEntity
            {
                Scope = scope,
                ScopeId = scopeId,
                FailedCount = 1,
                WindowStartedAtUtc = now
            });
            return;
        }

        if (now - counter.WindowStartedAtUtc >= Window)
        {
            counter.FailedCount = 1;
            counter.WindowStartedAtUtc = now;
            return;
        }

        counter.FailedCount++;
    }
}
