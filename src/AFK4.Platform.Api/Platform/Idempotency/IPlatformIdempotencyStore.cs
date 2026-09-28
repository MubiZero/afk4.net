namespace AFK4.Platform.Api.Platform.Idempotency;

public sealed record StoredIdempotencyResult(int StatusCode, string ResponseBody, string RequestHash);

public sealed record TryReadIdempotencyResult(
    StoredIdempotencyResult? Stored,
    bool RequestHashMismatch);
