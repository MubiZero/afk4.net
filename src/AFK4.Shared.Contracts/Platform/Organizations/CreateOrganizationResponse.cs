using AFK4.Shared.Contracts.Identity.AccountActivation;

namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record CreateOrganizationResponse(
    OrganizationDetailDto Organization,
    OrganizationOwnerInviteDto OrganizationOwnerInvite);
