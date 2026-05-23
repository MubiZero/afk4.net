namespace AFK4.Shared.Contracts.Platform.Operator;

public sealed record ResolveOperatorConnectionResponse(
    Guid OrganizationId,
    string OrganizationSlug,
    string OrganizationName,
    string OrganizationStatus,
    string? OrganizationStatusReason,
    Guid BranchId,
    string BranchSlug,
    string BranchName,
    string BranchCity,
    string Source);

public static class OperatorConnectionResolutionSources
{
    public const string Slug = "slug";

    public const string SetupCode = "setup_code";
}
