using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Tenants;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformTenantService
{
    Task<PlatformTenantOperationResult<CreateTenantResponse>> CreateAsync(
        CreateTenantRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantSummaryDto>> ListAsync(CancellationToken cancellationToken);

    Task<TenantDetailDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<OwnerInviteDto>> CreateOrRotateOwnerInviteAsync(
        Guid organizationId,
        CreateOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<StaffSignInResponse>> AcceptOwnerInviteAsync(
        AcceptOwnerInviteRequest request,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateStatusAsync(
        Guid organizationId,
        UpdateTenantStatusRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantDetailDto>> UpdatePlanAsync(
        Guid organizationId,
        UpdateTenantPlanRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateLimitsAsync(
        Guid organizationId,
        UpdateTenantLimitsRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<OwnerInviteDto>> RevokeOwnerInviteAsync(
        Guid ownerInviteId,
        RevokeOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);
}
