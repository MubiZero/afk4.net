using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Audit;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Audit;

public sealed class EfAuditSearchService(PlatformDbContext dbContext) : IAuditSearchService
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
        return ExecuteAsync(records, query, cancellationToken);
    }

    public Task<AuditSearchResultDto> SearchOrganizationAsync(
        Guid organizationId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId);
        return ExecuteAsync(records, query, cancellationToken);
    }

    private static async Task<AuditSearchResultDto> ExecuteAsync(
        IQueryable<AuditRecordEntity> records,
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
                AmountMinorUnits = record.AmountMinorUnits
            })
            .ToListAsync(cancellationToken);

        return new AuditSearchResultDto(result, limit);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
