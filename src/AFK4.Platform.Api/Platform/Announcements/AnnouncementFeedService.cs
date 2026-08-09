using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Announcements;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Announcements;

/// <summary>
/// Что видит клуб. Аудитория считается ЗДЕСЬ, при каждой выдаче, а не разворачивается в список
/// получателей при публикации: клуб, заведённый завтра, тоже должен увидеть «плановые работы».
/// </summary>
public sealed class AnnouncementFeedService(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AnnouncementFeedItemDto>> ListAsync(
        Guid organizationId,
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var published = await dbContext.PlatformAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.Status == AnnouncementStatus.Published
                && announcement.ShowFromUtc <= now
                && announcement.ShowUntilUtc > now)
            .ToArrayAsync(cancellationToken);

        if (published.Length == 0)
        {
            return [];
        }

        var planCode = await ResolvePlanCodeAsync(organizationId, cancellationToken);

        var mine = published
            .Where(announcement => AnnouncementAudience.Matches(announcement, organizationId, planCode))
            .ToArray();
        if (mine.Length == 0)
        {
            return [];
        }

        var ids = mine.Select(announcement => announcement.PlatformAnnouncementId).ToArray();
        var readIds = await dbContext.AnnouncementReads
            .AsNoTracking()
            .Where(read => read.StaffUserId == staffUserId && ids.Contains(read.PlatformAnnouncementId))
            .Select(read => read.PlatformAnnouncementId)
            .ToArrayAsync(cancellationToken);
        var read = new HashSet<Guid>(readIds);

        // Порядок — тот, в котором это читает человек: сначала самое важное, внутри важности
        // сначала свежее.
        return mine
            .OrderByDescending(announcement => AnnouncementSeverity.Rank(announcement.Severity))
            .ThenByDescending(announcement => announcement.PublishedAtUtc ?? announcement.CreatedAtUtc)
            .Select(announcement => new AnnouncementFeedItemDto(
                announcement.PlatformAnnouncementId,
                announcement.Title,
                announcement.Body,
                announcement.Severity,
                announcement.ShowFromUtc,
                announcement.ShowUntilUtc,
                announcement.PublishedAtUtc ?? announcement.CreatedAtUtc,
                read.Contains(announcement.PlatformAnnouncementId)))
            .ToArray();
    }

    /// <summary>
    /// Отметка прочтения идемпотентна: оператор дважды нажал «прочитано» — это не ошибка, а
    /// дрогнувшая рука. Отметка привязана к сотруднику, поэтому его коллеги полосу ещё увидят.
    /// </summary>
    public async Task<bool> MarkReadAsync(
        Guid organizationId,
        Guid staffUserId,
        Guid announcementId,
        CancellationToken cancellationToken)
    {
        var announcement = await dbContext.PlatformAnnouncements
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlatformAnnouncementId == announcementId, cancellationToken);
        if (announcement is null || announcement.Status != AnnouncementStatus.Published)
        {
            return false;
        }

        var planCode = await ResolvePlanCodeAsync(organizationId, cancellationToken);

        // Клуб не может пометить прочитанным то, что ему не адресовано: иначе чужой id в теле
        // запроса создаёт строку про сообщение, которого этот клуб никогда не видел.
        if (!AnnouncementAudience.Matches(announcement, organizationId, planCode))
        {
            return false;
        }

        var already = await dbContext.AnnouncementReads.AnyAsync(
            read => read.PlatformAnnouncementId == announcementId && read.StaffUserId == staffUserId,
            cancellationToken);
        if (already)
        {
            return true;
        }

        dbContext.AnnouncementReads.Add(new AnnouncementReadEntity
        {
            PlatformAnnouncementId = announcementId,
            StaffUserId = staffUserId,
            ReadAtUtc = timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (RelationalFailureClassifier.IsUniqueViolation(exception))
        {
            // Две вкладки нажали «прочитано» одновременно — обе правы. Первичный ключ держит
            // единственность, проигравшая сторона получает тот же успех.
            dbContext.ChangeTracker.Clear();
        }

        return true;
    }

    /// <summary>
    /// Тариф читается из строки организации — из того же места, откуда его берут лимиты
    /// (<c>EfPlanLimitGuard</c>) и фичи (<c>EfOrganizationEntitlements</c>). Второй источник
    /// означал бы, что «клуб на тарифе X» для анонса и для фичи — разные утверждения.
    /// </summary>
    private Task<string?> ResolvePlanCodeAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.PlanCode)
            .FirstOrDefaultAsync(cancellationToken)!;
}
