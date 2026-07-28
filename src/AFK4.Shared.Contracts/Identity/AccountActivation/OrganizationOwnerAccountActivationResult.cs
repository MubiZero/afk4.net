namespace AFK4.Shared.Contracts.Identity.AccountActivation;

public sealed record OrganizationOwnerAccountActivationResult(
    Guid OrganizationId,
    Guid BranchId,
    string NextStep);
