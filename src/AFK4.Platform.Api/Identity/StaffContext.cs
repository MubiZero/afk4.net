namespace AFK4.Platform.Api.Identity;

public sealed record StaffContext(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<string> Permissions);
