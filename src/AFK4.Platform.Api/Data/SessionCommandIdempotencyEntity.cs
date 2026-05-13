namespace AFK4.Platform.Api.Data;

public sealed class SessionCommandIdempotencyEntity
{
    public Guid SessionCommandIdempotencyId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string IdempotencyKeyHash { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
