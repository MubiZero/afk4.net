namespace AFK4.Platform.Api.Data;

/// <summary>
/// An additive staff onboarding path: an admin invites a new staff member by email, and the
/// invitee sets their own password on accept (mirrors <see cref="OwnerInviteEntity"/> +
/// <see cref="PasswordResetTokenEntity"/>). Does not replace inline-password staff creation.
/// Opaque token (RNG -> SHA-256 stored, plaintext emailed once); single-use, expiring.
/// </summary>
public sealed class StaffInviteEntity
{
    public Guid StaffInviteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Comma-separated branch role names to assign on accept.</summary>
    public string RoleNamesCsv { get; set; } = string.Empty;

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedByStaffUserId { get; set; }
}
