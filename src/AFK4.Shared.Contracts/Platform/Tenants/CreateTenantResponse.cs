using AFK4.Shared.Contracts.Identity.AccountActivation;

namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record CreateTenantResponse(
    TenantDetailDto Tenant,
    OrganizationOwnerInviteDto OrganizationOwnerInvite);
