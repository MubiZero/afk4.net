namespace AFK4.Operator.App.Connection;

public sealed record OperatorConnectionSnapshot(
    Guid OrganizationId,
    string OrganizationSlug,
    string OrganizationName,
    Guid BranchId,
    string BranchSlug,
    string BranchName,
    string BranchCity,
    DateTimeOffset StoredAtUtc);
