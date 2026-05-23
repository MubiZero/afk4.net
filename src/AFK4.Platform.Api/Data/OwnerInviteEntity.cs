namespace AFK4.Platform.Api.Data;

public sealed class OwnerInviteEntity
{
    public Guid OwnerInviteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NormalizedCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? OwnerUserName { get; set; }

    public string? OwnerDisplayName { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedByStaffUserId { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Guid? RevokedByPlatformAdminUserId { get; set; }

    public string? RevokedReason { get; set; }

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
