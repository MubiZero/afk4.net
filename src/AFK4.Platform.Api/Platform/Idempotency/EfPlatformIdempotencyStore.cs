using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Idempotency;

public sealed class EfPlatformIdempotencyStore(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformIdempotencyStore
{
    public async Task<TryReadIdempotencyResult> TryReadAsync(
        string scope,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var record = await dbContext.PlatformIdempotencyRecords
            .AsNoTracking()
            .Where(candidate => candidate.Scope == scope && candidate.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null || record.ExpiresAtUtc <= now)
        {
            return new TryReadIdempotencyResult(null, RequestHashMismatch: false);
        }

        var stored = new StoredIdempotencyResult(record.ResponseStatusCode, record.ResponseBody, record.RequestHash);
        return new TryReadIdempotencyResult(stored, RequestHashMismatch: !string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal));
    }

    public async Task WriteAsync(
        string scope,
        string idempotencyKey,
        string requestHash,
        Guid platformAdminUserId,
        int statusCode,
        string responseBody,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        dbContext.PlatformIdempotencyRecords.Add(new PlatformIdempotencyRecordEntity
        {
            PlatformIdempotencyRecordId = Guid.NewGuid(),
            Scope = scope,
            IdempotencyKey = idempotencyKey,
            PlatformAdminUserId = platformAdminUserId,
            RequestHash = requestHash,
            ResponseStatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(retention)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
