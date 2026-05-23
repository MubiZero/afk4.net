namespace AFK4.Platform.Api.Data;

public sealed class PlatformIdempotencyRecordEntity
{
    public Guid PlatformIdempotencyRecordId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public Guid PlatformAdminUserId { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
