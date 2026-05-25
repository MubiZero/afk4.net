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
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.OwnerCodes
            .AsNoTracking()
            .Where(code => code.StaffUserId == staffUserId && code.RevokedAtUtc == null && code.ExpiresAtUtc > now)
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

    public async Task<OwnerCodeLookupResult> LookupActiveAsync(
        string rawCode,
        CancellationToken cancellationToken)
    {
        var normalized = hasher.Normalize(rawCode);
        if (normalized.Length != RandomOwnerCodeGenerator.Digits)
        {
            return OwnerCodeLookupResult.NotFound("Owner code was not found.");
        }

        var codeHash = hasher.Hash(normalized);
        var code = await dbContext.OwnerCodes
            .Where(candidate => candidate.CodeHash == codeHash)
            .OrderByDescending(candidate => candidate.RevokedAtUtc == null)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (code is null)
        {
            return OwnerCodeLookupResult.NotFound("Owner code was not found.");
        }

        if (code.RevokedAtUtc is not null)
        {
            return OwnerCodeLookupResult.Revoked(code.OwnerCodeId, "Owner code was revoked.");
        }

        var now = timeProvider.GetUtcNow();
        if (code.ExpiresAtUtc <= now)
        {
            code.RevokedAtUtc = now;
            code.RevokedReason = "expired";
            await dbContext.SaveChangesAsync(cancellationToken);
            return OwnerCodeLookupResult.Expired(code.OwnerCodeId, "Owner code has expired.");
        }

        var staffUser = await dbContext.StaffUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.StaffUserId == code.StaffUserId && candidate.IsActive,
                cancellationToken);
        if (staffUser is null)
        {
            return OwnerCodeLookupResult.NotFound("Owner code staff user was not found.");
        }

        code.LastUsedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return OwnerCodeLookupResult.Success(staffUser.StaffUserId, staffUser.OrganizationId, code.OwnerCodeId);
    }

    private async Task<OwnerCodeIssued> InsertNewCodeAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lifetime = options.Value.Lifetime;
        var expiresAt = now + lifetime;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var plaintext = generator.Generate();
            var normalized = hasher.Normalize(plaintext);
            var codeHash = hasher.Hash(normalized);
            var activeCollisionExists = await dbContext.OwnerCodes
                .AsNoTracking()
                .AnyAsync(
                    code => code.CodeHash == codeHash && code.RevokedAtUtc == null,
                    cancellationToken);
            if (activeCollisionExists)
            {
                continue;
            }

            var suffix = hasher.Suffix(normalized);

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

        throw new InvalidOperationException("Unable to generate a unique active owner code.");
    }
}
