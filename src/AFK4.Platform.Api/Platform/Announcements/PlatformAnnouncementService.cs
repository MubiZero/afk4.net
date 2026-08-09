using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Platform.Announcements;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Announcements;

public sealed record AnnouncementResult(PlatformAnnouncementDto? Value, string? ErrorCode)
{
    public bool Succeeded => ErrorCode is null;

    public static AnnouncementResult Ok(PlatformAnnouncementDto value) => new(value, null);

    public static AnnouncementResult Fail(string errorCode) => new(null, errorCode);
}

/// <summary>
/// Ведение анонсов платформы. Два инварианта стоят дороже остальных и держатся здесь:
/// письмо владельцу уходит ровно один раз за анонс, и аудитория опубликованного не меняется —
/// письма уже ушли по старому списку, и правка задним числом сделала бы след аудита ложью.
/// </summary>
public sealed class PlatformAnnouncementService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications)
{
    private const int MaxTitleLength = 200;
    private const int MaxBodyLength = 4000;

    public async Task<IReadOnlyList<PlatformAnnouncementDto>> ListAsync(CancellationToken cancellationToken)
    {
        var announcements = await dbContext.PlatformAnnouncements
            .AsNoTracking()
            .OrderByDescending(announcement => announcement.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var reads = await dbContext.AnnouncementReads
            .AsNoTracking()
            .GroupBy(read => read.PlatformAnnouncementId)
            .Select(group => new { AnnouncementId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var readCounts = reads.ToDictionary(entry => entry.AnnouncementId, entry => entry.Count);

        return announcements.Select(announcement => ToDto(announcement, readCounts)).ToArray();
    }

    public Task<AnnouncementResult> CreateAsync(
        Guid actorId,
        SavePlatformAnnouncementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => CreateCoreAsync(actorId, request, cancellationToken), cancellationToken);

    public Task<AnnouncementResult> UpdateAsync(
        Guid announcementId,
        SavePlatformAnnouncementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => UpdateCoreAsync(announcementId, request, cancellationToken), cancellationToken);

    public Task<AnnouncementResult> PublishAsync(Guid announcementId, CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => PublishCoreAsync(announcementId, cancellationToken), cancellationToken);

    public Task<AnnouncementResult> WithdrawAsync(Guid announcementId, CancellationToken cancellationToken) =>
        ExecuteInSerializableTransactionAsync(() => WithdrawCoreAsync(announcementId, cancellationToken), cancellationToken);

    private async Task<AnnouncementResult> CreateCoreAsync(
        Guid actorId,
        SavePlatformAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation is not null)
        {
            return AnnouncementResult.Fail(validation);
        }

        var now = timeProvider.GetUtcNow();
        var announcement = new PlatformAnnouncementEntity
        {
            PlatformAnnouncementId = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Severity = request.Severity,
            ShowFromUtc = request.ShowFromUtc,
            ShowUntilUtc = request.ShowUntilUtc,
            AudienceKind = request.AudienceKind,
            AudiencePlanCodesJson = JsonSerializer.Serialize(NormalizePlanCodes(request)),
            AudienceOrganizationIdsJson = JsonSerializer.Serialize(NormalizeOrganizationIds(request)),
            Status = AnnouncementStatus.Draft,
            CreatedByPlatformAdminUserId = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.PlatformAnnouncements.Add(announcement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AnnouncementResult.Ok(ToDto(announcement, readCount: 0));
    }

    private async Task<AnnouncementResult> UpdateCoreAsync(
        Guid announcementId,
        SavePlatformAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var announcement = await dbContext.PlatformAnnouncements
            .SingleOrDefaultAsync(candidate => candidate.PlatformAnnouncementId == announcementId, cancellationToken);
        if (announcement is null)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.InvalidAnnouncement);
        }

        // Снятый анонс — это законченная история. Править его текст значит переписывать то, что
        // клубы уже прочитали и чего больше не видят.
        if (announcement.Status == AnnouncementStatus.Withdrawn)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.WithdrawnAnnouncement);
        }

        var validation = await ValidateAsync(request, cancellationToken);
        if (validation is not null)
        {
            return AnnouncementResult.Fail(validation);
        }

        if (announcement.Status == AnnouncementStatus.Published && ChangesAudienceOrSeverity(announcement, request))
        {
            // Исправить опечатку в опубликованном — нормально. Сменить важность или аудиторию —
            // нет: письма уже ушли по старому списку, и запись «кому это было адресовано»
            // перестала бы соответствовать тому, что произошло.
            return AnnouncementResult.Fail(AnnouncementErrorCodes.PublishedAudienceLocked);
        }

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.Trim();
        announcement.Severity = request.Severity;
        announcement.ShowFromUtc = request.ShowFromUtc;
        announcement.ShowUntilUtc = request.ShowUntilUtc;
        announcement.AudienceKind = request.AudienceKind;
        announcement.AudiencePlanCodesJson = JsonSerializer.Serialize(NormalizePlanCodes(request));
        announcement.AudienceOrganizationIdsJson = JsonSerializer.Serialize(NormalizeOrganizationIds(request));
        announcement.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return AnnouncementResult.Ok(await LoadDtoAsync(announcementId, cancellationToken));
    }

    private async Task<AnnouncementResult> PublishCoreAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var announcement = await dbContext.PlatformAnnouncements
            .SingleOrDefaultAsync(candidate => candidate.PlatformAnnouncementId == announcementId, cancellationToken);
        if (announcement is null)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.InvalidAnnouncement);
        }

        if (announcement.Status != AnnouncementStatus.Draft)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.InvalidTransition);
        }

        var now = timeProvider.GetUtcNow();
        announcement.Status = AnnouncementStatus.Published;
        announcement.PublishedAtUtc = now;
        announcement.UpdatedAtUtc = now;

        // Сторож рассылки — отметка на самом анонсе, а не «статус только что сменился»: снять и
        // опубликовать заново значит вернуть то же сообщение, а не отправить второе письмо.
        if (AnnouncementSeverity.WarrantsEmail(announcement.Severity) && announcement.EmailDispatchedAtUtc is null)
        {
            await QueueOwnerEmailsAsync(announcement, cancellationToken);
            announcement.EmailDispatchedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return AnnouncementResult.Ok(await LoadDtoAsync(announcementId, cancellationToken));
    }

    private async Task<AnnouncementResult> WithdrawCoreAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var announcement = await dbContext.PlatformAnnouncements
            .SingleOrDefaultAsync(candidate => candidate.PlatformAnnouncementId == announcementId, cancellationToken);
        if (announcement is null)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.InvalidAnnouncement);
        }

        if (announcement.Status != AnnouncementStatus.Published)
        {
            return AnnouncementResult.Fail(AnnouncementErrorCodes.InvalidTransition);
        }

        announcement.Status = AnnouncementStatus.Withdrawn;
        announcement.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return AnnouncementResult.Ok(await LoadDtoAsync(announcementId, cancellationToken));
    }

    /// <summary>
    /// Получатели письма фиксируются составом аудитории НА МОМЕНТ ПУБЛИКАЦИИ. Полоса живёт иначе —
    /// она считает аудиторию при каждой выдаче, и клуб, заведённый завтра, увидит «плановые
    /// работы». Рассылать же письма вечно новым клубам было бы рассылкой без конца.
    /// </summary>
    private async Task QueueOwnerEmailsAsync(PlatformAnnouncementEntity announcement, CancellationToken cancellationToken)
    {
        var planCodes = AnnouncementAudience.ParsePlanCodes(announcement.AudiencePlanCodesJson);
        var organizationIds = AnnouncementAudience.ParseOrganizationIds(announcement.AudienceOrganizationIdsJson);

        // Тариф берётся из строки организации — из того же места, что у лимитов и фич.
        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Select(organization => new { organization.OrganizationId, organization.PlanCode })
            .ToArrayAsync(cancellationToken);

        foreach (var organization in organizations)
        {
            var matches = AnnouncementAudience.Matches(
                announcement.AudienceKind,
                planCodes,
                organizationIds,
                organization.OrganizationId,
                organization.PlanCode);
            if (!matches)
            {
                continue;
            }

            var owner = await ownerResolver.ResolveAsync(organization.OrganizationId, cancellationToken);
            if (owner is null)
            {
                continue;
            }

            await notifications.SendAsync(
                new NotificationRequest(
                    TemplateKey: NotificationTemplateKeys.PlatformAnnouncement,
                    Category: NotificationCategory.Transactional,
                    Recipient: new NotificationRecipient(
                        Locale: string.Empty,
                        EmailAddress: owner.Email,
                        StaffUserId: owner.StaffUserId),
                    Tokens: new Dictionary<string, string>
                    {
                        ["displayName"] = owner.DisplayName,
                        ["organizationName"] = owner.OrganizationName,
                        ["title"] = announcement.Title,
                        ["body"] = announcement.Body
                    },
                    // Второй слой защиты от дубля: даже если сторож на анонсе кто-то обойдёт,
                    // outbox схлопнет повтор по этому ключу.
                    IdempotencyKey: $"announcement:{announcement.PlatformAnnouncementId:N}:{organization.OrganizationId:N}",
                    OrganizationId: organization.OrganizationId),
                cancellationToken);
        }
    }

    private async Task<string?> ValidateAsync(SavePlatformAnnouncementRequest request, CancellationToken cancellationToken)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        if (title.Length is 0 or > MaxTitleLength || body.Length is 0 or > MaxBodyLength)
        {
            return AnnouncementErrorCodes.InvalidAnnouncement;
        }

        if (!AnnouncementSeverity.All.Contains(request.Severity)
            || !AnnouncementAudienceKinds.AllKinds.Contains(request.AudienceKind))
        {
            return AnnouncementErrorCodes.InvalidAnnouncement;
        }

        // Окно обязательно с обеих сторон: анонс без конца — это сообщение про субботние работы,
        // которое висит до декабря.
        if (request.ShowUntilUtc <= request.ShowFromUtc)
        {
            return AnnouncementErrorCodes.InvalidAnnouncement;
        }

        if (request.AudienceKind == AnnouncementAudienceKinds.Plans)
        {
            var planCodes = NormalizePlanCodes(request);
            if (planCodes.Count == 0)
            {
                return AnnouncementErrorCodes.InvalidAnnouncement;
            }

            var known = await dbContext.SubscriptionPlans
                .AsNoTracking()
                .Select(plan => plan.PlanCode)
                .ToArrayAsync(cancellationToken);
            var knownCodes = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
            if (!planCodes.All(knownCodes.Contains))
            {
                return AnnouncementErrorCodes.UnknownPlan;
            }
        }

        if (request.AudienceKind == AnnouncementAudienceKinds.Organizations)
        {
            var organizationIds = NormalizeOrganizationIds(request);
            if (organizationIds.Count == 0)
            {
                return AnnouncementErrorCodes.InvalidAnnouncement;
            }

            var knownCount = await dbContext.Organizations
                .CountAsync(organization => organizationIds.Contains(organization.OrganizationId), cancellationToken);
            if (knownCount != organizationIds.Count)
            {
                return AnnouncementErrorCodes.UnknownOrganization;
            }
        }

        return null;
    }

    private static bool ChangesAudienceOrSeverity(PlatformAnnouncementEntity announcement, SavePlatformAnnouncementRequest request) =>
        announcement.Severity != request.Severity
        || announcement.AudienceKind != request.AudienceKind
        || announcement.AudiencePlanCodesJson != JsonSerializer.Serialize(NormalizePlanCodes(request))
        || announcement.AudienceOrganizationIdsJson != JsonSerializer.Serialize(NormalizeOrganizationIds(request));

    private static List<string> NormalizePlanCodes(SavePlatformAnnouncementRequest request) =>
        request.AudienceKind == AnnouncementAudienceKinds.Plans
            ? (request.AudiencePlanCodes ?? [])
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList()
            : [];

    private static List<Guid> NormalizeOrganizationIds(SavePlatformAnnouncementRequest request) =>
        request.AudienceKind == AnnouncementAudienceKinds.Organizations
            ? (request.AudienceOrganizationIds ?? [])
                .Distinct()
                .OrderBy(id => id)
                .ToList()
            : [];

    private async Task<PlatformAnnouncementDto> LoadDtoAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var announcements = await ListAsync(cancellationToken);
        return announcements.Single(announcement => announcement.AnnouncementId == announcementId);
    }

    private static PlatformAnnouncementDto ToDto(PlatformAnnouncementEntity announcement, IReadOnlyDictionary<Guid, int> readCounts) =>
        ToDto(announcement, readCounts.GetValueOrDefault(announcement.PlatformAnnouncementId));

    private static PlatformAnnouncementDto ToDto(PlatformAnnouncementEntity announcement, int readCount) =>
        new(
            announcement.PlatformAnnouncementId,
            announcement.Title,
            announcement.Body,
            announcement.Severity,
            announcement.ShowFromUtc,
            announcement.ShowUntilUtc,
            announcement.AudienceKind,
            [.. AnnouncementAudience.ParsePlanCodes(announcement.AudiencePlanCodesJson)],
            [.. AnnouncementAudience.ParseOrganizationIds(announcement.AudienceOrganizationIdsJson)],
            announcement.Status,
            announcement.PublishedAtUtc,
            announcement.EmailDispatchedAtUtc is not null,
            readCount);

    private async Task<AnnouncementResult> ExecuteInSerializableTransactionAsync(
        Func<Task<AnnouncementResult>> action,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            // InMemory-провайдер не знает ни транзакций, ни изоляции — оборачивать нечего.
            // Настоящую гонку проверяет Postgres-тест.
            return await action();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (RelationalFailureClassifier.IsSerializationFailure(exception)
            || RelationalFailureClassifier.IsUniqueViolation(exception))
        {
            // Две причины, один честный ответ «повторите». Serializable отменяет и «невиновную»
            // сторону гонки. А два одновременных «опубликовать» доходят до постановки писем оба, и
            // проигравший упирается в уникальный ключ идемпотентности очереди — это последний
            // рубеж против второго письма владельцу, и он обязан выглядеть как понятный отказ, а не
            // как сырой 500.
            dbContext.ChangeTracker.Clear();
            return AnnouncementResult.Fail(AnnouncementErrorCodes.Conflict);
        }
    }
}
