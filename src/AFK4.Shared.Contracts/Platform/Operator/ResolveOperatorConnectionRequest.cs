namespace AFK4.Shared.Contracts.Platform.Operator;

public sealed record ResolveOperatorConnectionRequest(
    string? OrganizationSlug,
    string? BranchSlug,
    string? SetupCode);
