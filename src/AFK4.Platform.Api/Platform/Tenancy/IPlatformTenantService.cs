using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Identity.AccountActivation;
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

    Task<PlatformTenantOperationResult<OrganizationOwnerInviteDto>> CreateOrRotateOrganizationOwnerInviteAsync(
        Guid organizationId,
        CreateOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>> ListOrganizationOwnerInvitesAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<OrganizationOwnerAccountActivationResult>> AcceptOrganizationOwnerInviteAsync(
        AcceptOrganizationOwnerInviteRequest request,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateStatusAsync(
        Guid organizationId,
        UpdateTenantStatusRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateLimitsAsync(
        Guid organizationId,
        UpdateTenantLimitsRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<OrganizationOwnerInviteDto>> RevokeOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        RevokeOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<OrganizationOwnerInviteDto>> ResendOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        CancellationToken cancellationToken);
}
