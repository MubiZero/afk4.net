using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity.OwnerCodes;

public sealed class OwnerCodeService(
    PlatformDbContext dbContext,
    IOwnerCodeGenerator generator,
    IOwnerCodeHasher hasher,
    IOptions<OwnerCodeOptions> options,
    TimeProvider timeProvider) : IOwnerCodeService
{
    public async Task<OwnerCodeOperationResult<OwnerCodeIssued>> GenerateAsync(
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var staffUserExists = await dbContext.StaffUsers
            .AsNoTracking()
            .AnyAsync(staffUser => staffUser.StaffUserId == staffUserId, cancellationToken);
        if (!staffUserExists)
        {
            return OwnerCodeOperationResult<OwnerCodeIssued>.NotFound("Staff user was not found.");
        }

        var activeExists = await dbContext.OwnerCodes
            .AsNoTracking()
            .AnyAsync(
                code => code.StaffUserId == staffUserId && code.RevokedAtUtc == null,
                cancellationToken);
        if (activeExists)
        {
            return OwnerCodeOperationResult<OwnerCodeIssued>.Conflict(
                "An active owner code already exists for this staff user; use rotate.");
        }

        var issued = await InsertNewCodeAsync(staffUserId, cancellationToken);
        return OwnerCodeOperationResult<OwnerCodeIssued>.Success(issued);
    }

    public async Task<OwnerCodeOperationResult<OwnerCodeIssued>> RotateAsync(
        Guid staffUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var staffUserExists = await dbContext.StaffUsers
            .AsNoTracking()
            .AnyAsync(staffUser => staffUser.StaffUserId == staffUserId, cancellationToken);
        if (!staffUserExists)
        {
            return OwnerCodeOperationResult<OwnerCodeIssued>.NotFound("Staff user was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var revokedReason = string.IsNullOrWhiteSpace(reason) ? "rotated" : reason;

        var activeCodes = await dbContext.OwnerCodes
            .Where(code => code.StaffUserId == staffUserId && code.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var existing in activeCodes)
        {
            existing.RevokedAtUtc = now;
            existing.RevokedReason = revokedReason;
        }

        var issued = await InsertNewCodeAsync(staffUserId, cancellationToken);
        return OwnerCodeOperationResult<OwnerCodeIssued>.Success(issued);
    }

    public async Task<OwnerCodeSummary?> GetActiveSummaryAsync(
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.OwnerCodes
            .AsNoTracking()
            .Where(code => code.StaffUserId == staffUserId && code.RevokedAtUtc == null)
            .OrderByDescending(code => code.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is null)
        {
            return null;
        }

        return new OwnerCodeSummary(
            active.CodeSuffix,
            active.ExpiresAtUtc,
            active.LastUsedAtUtc,
            active.FailedAttemptCount);
    }

    private async Task<OwnerCodeIssued> InsertNewCodeAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lifetime = options.Value.Lifetime;
        var plaintext = generator.Generate();
        var normalized = hasher.Normalize(plaintext);
        var codeHash = hasher.Hash(normalized);
        var suffix = hasher.Suffix(normalized);
        var expiresAt = now + lifetime;

        dbContext.OwnerCodes.Add(new OwnerCodeEntity
        {
            OwnerCodeId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            CodeHash = codeHash,
            CodeSuffix = suffix,
            ExpiresAtUtc = expiresAt,
            LastUsedAtUtc = null,
            FailedAttemptCount = 0,
            RevokedAtUtc = null,
            RevokedReason = null,
            CreatedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OwnerCodeIssued(plaintext, suffix, expiresAt);
    }
}
