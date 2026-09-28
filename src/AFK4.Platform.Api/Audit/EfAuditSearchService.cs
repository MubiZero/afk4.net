using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Audit;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Audit;

public sealed class EfAuditSearchService(PlatformDbContext dbContext)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public Task<AuditSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId && record.BranchId == branchId);
        return ExecuteAsync(records, dbContext.Organizations.AsNoTracking(), query, cancellationToken);
    }

    public Task<AuditSearchResultDto> SearchOrganizationAsync(
        Guid organizationId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId);
        return ExecuteAsync(records, dbContext.Organizations.AsNoTracking(), query, cancellationToken);
    }

    public Task<AuditSearchResultDto> SearchPlatformAsync(
        Guid? organizationId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords.AsNoTracking();
        if (organizationId.HasValue)
        {
            records = records.Where(record => record.OrganizationId == organizationId.Value);
        }
        return ExecuteAsync(records, dbContext.Organizations.AsNoTracking(), query, cancellationToken);
    }

    private async Task<AuditSearchResultDto> ExecuteAsync(
        IQueryable<AuditRecordEntity> records,
        IQueryable<OrganizationEntity> organizations,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var action = Normalize(query.Action);
        var outcome = Normalize(query.Outcome);
        var targetType = Normalize(query.TargetType);

        if (action is not null)
        {
            records = records.Where(record => record.Action == action);
        }

        if (outcome is not null)
        {
            records = records.Where(record => record.Outcome == outcome);
        }

        if (targetType is not null)
        {
            records = records.Where(record => record.TargetType == targetType);
        }

        if (query.FromUtc.HasValue)
        {
            records = records.Where(record => record.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            records = records.Where(record => record.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (query.ActorStaffUserId.HasValue)
        {
            records = records.Where(record => record.ActorStaffUserId == query.ActorStaffUserId.Value);
        }

        // Amount filters only match money-relevant records (those carrying an amount); records without
        // an amount are excluded when an amount bound is set.
        if (query.MinAmountMinorUnits.HasValue)
        {
            records = records.Where(record =>
                record.AmountMinorUnits != null && record.AmountMinorUnits >= query.MinAmountMinorUnits.Value);
        }

        if (query.MaxAmountMinorUnits.HasValue)
        {
            records = records.Where(record =>
                record.AmountMinorUnits != null && record.AmountMinorUnits <= query.MaxAmountMinorUnits.Value);
        }

        // Имя клуба приезжает вместе с записью: без него журнал платформы читается как столбец
        // идентификаторов, и опознать клуб можно, только сходив за ним в другой раздел.
        var result = await records
            .OrderByDescending(record => record.CreatedAtUtc)
            .ThenByDescending(record => record.AuditRecordId)
            .Take(limit)
            .Select(record => new AuditRecordDto(
                record.AuditRecordId,
                record.OrganizationId,
                record.BranchId,
                record.ActorStaffUserId,
                record.Action,
                record.TargetType,
                record.TargetId,
                record.Outcome,
                record.SourceApp,
                record.DetailsJson,
                record.CreatedAtUtc)
            {
                ActorPlatformAdminUserId = record.ActorPlatformAdminUserId,
                AmountMinorUnits = record.AmountMinorUnits,
                OrganizationName = organizations
                    .Where(organization => organization.OrganizationId == record.OrganizationId)
                    .Select(organization => organization.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new AuditSearchResultDto(await WithActorNamesAsync(result, cancellationToken), limit);
    }

    private async Task<IReadOnlyList<AuditRecordDto>> WithActorNamesAsync(
        List<AuditRecordDto> records,
        CancellationToken cancellationToken)
    {
        var staffIds = records.Where(record => record.ActorStaffUserId is not null)
            .Select(record => record.ActorStaffUserId!.Value).Distinct().ToList();
        var adminIds = records.Where(record => record.ActorPlatformAdminUserId is not null)
            .Select(record => record.ActorPlatformAdminUserId!.Value).Distinct().ToList();
        var staffNames = await dbContext.StaffUsers.AsNoTracking()
            .Where(staff => staffIds.Contains(staff.StaffUserId))
            .ToDictionaryAsync(staff => staff.StaffUserId, staff => staff.DisplayName, cancellationToken);
        var adminNames = await dbContext.PlatformAdminUsers.AsNoTracking()
            .Where(admin => adminIds.Contains(admin.PlatformAdminUserId))
            .ToDictionaryAsync(admin => admin.PlatformAdminUserId, admin => admin.DisplayName, cancellationToken);

        return records.Select(record => record with
        {
            ActorDisplayName = record.ActorPlatformAdminUserId is { } adminId && adminNames.TryGetValue(adminId, out var adminName)
                ? adminName
                : record.ActorStaffUserId is null ? null : SystemActorIds.ResolveDisplayName(record.ActorStaffUserId, staffNames)
        }).ToList();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
