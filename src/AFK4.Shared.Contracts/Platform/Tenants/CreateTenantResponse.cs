using AFK4.Shared.Contracts.Platform.Invites;

namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record CreateTenantResponse(
    TenantDetailDto Tenant,
    OwnerInviteDto OwnerInvite);
