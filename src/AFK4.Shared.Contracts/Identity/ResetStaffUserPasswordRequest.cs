namespace AFK4.Shared.Contracts.Identity;

public sealed record ResetStaffUserPasswordRequest(
    Guid OrganizationId,
    string NewPassword);
