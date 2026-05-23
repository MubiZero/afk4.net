namespace AFK4.Platform.Api.Platform.Idempotency;

public sealed record StoredIdempotencyResult(int StatusCode, string ResponseBody, string RequestHash);

public sealed record TryReadIdempotencyResult(
    StoredIdempotencyResult? Stored,
    bool RequestHashMismatch);

public interface IPlatformIdempotencyStore
{
    Task<TryReadIdempotencyResult> TryReadAsync(
        string scope,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken);

    Task WriteAsync(
        string scope,
        string idempotencyKey,
        string requestHash,
        Guid platformAdminUserId,
        int statusCode,
        string responseBody,
        TimeSpan retention,
        CancellationToken cancellationToken);
}
