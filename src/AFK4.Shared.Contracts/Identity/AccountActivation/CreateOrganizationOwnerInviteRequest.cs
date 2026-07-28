namespace AFK4.Shared.Contracts.Identity.AccountActivation;

public sealed record CreateOrganizationOwnerInviteRequest(
    Guid BranchId,
    string? OwnerUserName,
    string? OwnerDisplayName,
    TimeSpan? Lifetime,
    string? OwnerEmail = null);
