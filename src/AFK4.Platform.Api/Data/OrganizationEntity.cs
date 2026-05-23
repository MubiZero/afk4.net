namespace AFK4.Platform.Api.Data;

public sealed class OrganizationEntity
{
    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "active";

    public string? StatusReason { get; set; }

    public DateTimeOffset? StatusChangedAtUtc { get; set; }

    public string PlanCode { get; set; } = "starter";

    public string SubscriptionStatus { get; set; } = "trial";

    public string LimitsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
