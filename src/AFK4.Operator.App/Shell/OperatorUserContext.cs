namespace AFK4.Operator.App.Shell;

public sealed record OperatorUserContext(
    Guid StaffUserId,
    Guid OrganizationId,
    Guid BranchId,
    string DisplayName,
    IReadOnlySet<string> Permissions);
