using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Health;

public sealed class EfPlatformIncidentService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlatformIncidentService
{
    /// <summary>Пока инцидент открыт, напоминание уходит не чаще раза в сутки.</summary>
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(1);

    public async Task<IncidentTransition> OpenOrTouchAsync(
        string kind, string dedupKey, string severity, string detailsJson, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.PlatformIncidents
            .SingleOrDefaultAsync(incident => incident.DedupKey == dedupKey && incident.ResolvedAtUtc == null, cancellationToken);

        if (existing is not null)
        {
            existing.LastSeenAtUtc = now;
            existing.DetailsJson = detailsJson;
            // Ухудшение серьёзности повышаем, обратно НЕ понижаем: инцидент, разово скатившийся
            // из critical в warning, не должен тихо терять приоритет до закрытия.
            if (severity == PlatformIncidentSeverityNames.Critical)
                existing.Severity = PlatformIncidentSeverityNames.Critical;

            var shouldRemind = existing.LastNotifiedAtUtc is null
                || now - existing.LastNotifiedAtUtc.Value >= ReminderInterval;
            if (shouldRemind) existing.LastNotifiedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            return new IncidentTransition(existing, IsNew: false, ShouldRemind: shouldRemind);
        }

        var incident = new PlatformIncidentEntity
        {
            PlatformIncidentId = Guid.NewGuid(),
            Kind = kind,
            DedupKey = dedupKey,
            Severity = severity,
            DetailsJson = detailsJson,
            OpenedAtUtc = now,
            LastSeenAtUtc = now,
            LastNotifiedAtUtc = now
        };
        dbContext.PlatformIncidents.Add(incident);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IncidentTransition(incident, IsNew: true, ShouldRemind: true);
        }
        catch (DbUpdateException) when (IsDuplicateOpenIncident(dbContext, incident))
        {
            // Гонка двух наблюдателей: частичный уникальный индекс отклонил вторую вставку.
            // Это НЕ ошибка вызывающего — инцидент уже заведён, письмо уже ушло.
            dbContext.Entry(incident).State = EntityState.Detached;
            var winner = await dbContext.PlatformIncidents
                .SingleAsync(row => row.DedupKey == dedupKey && row.ResolvedAtUtc == null, cancellationToken);
            return new IncidentTransition(winner, IsNew: false, ShouldRemind: false);
        }
    }

    private static bool IsDuplicateOpenIncident(PlatformDbContext dbContext, PlatformIncidentEntity incident) =>
        dbContext.Entry(incident).State == EntityState.Added;

    public async Task<IReadOnlyList<PlatformIncidentEntity>> ResolveMissingAsync(
        IReadOnlyCollection<string> stillOpenKeys, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var open = await dbContext.PlatformIncidents
            .Where(incident => incident.ResolvedAtUtc == null)
            .ToListAsync(cancellationToken);

        var resolved = open.Where(incident => !stillOpenKeys.Contains(incident.DedupKey)).ToList();
        foreach (var incident in resolved) incident.ResolvedAtUtc = now;

        if (resolved.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return resolved;
    }

    public async Task<IReadOnlyList<PlatformIncidentEntity>> ListOpenAsync(CancellationToken cancellationToken) =>
        await dbContext.PlatformIncidents
            .AsNoTracking()
            .Where(incident => incident.ResolvedAtUtc == null)
            .OrderByDescending(incident => incident.Severity == PlatformIncidentSeverityNames.Critical)
            .ThenBy(incident => incident.OpenedAtUtc)
            .ToListAsync(cancellationToken);
}
