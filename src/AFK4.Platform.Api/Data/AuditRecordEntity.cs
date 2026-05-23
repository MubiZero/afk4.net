namespace AFK4.Platform.Api.Data;

public sealed class AuditRecordEntity
{
    public Guid AuditRecordId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? BranchId { get; set; }

    public Guid? ActorStaffUserId { get; set; }

    public Guid? ActorPlatformAdminUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string SourceApp { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
