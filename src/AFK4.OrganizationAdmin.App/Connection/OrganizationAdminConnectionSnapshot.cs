namespace AFK4.OrganizationAdmin.App.Connection;

public sealed record OrganizationAdminConnectionSnapshot(
    Guid OrganizationId,
    string OrganizationSlug,
    string OrganizationName,
    Guid BranchId,
    string BranchSlug,
    string BranchName,
    string BranchCity,
    DateTimeOffset StoredAtUtc);
