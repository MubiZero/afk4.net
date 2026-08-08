using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformOrganizationService
{
    Task<PlatformOrganizationOperationResult<CreateOrganizationResponse>> CreateAsync(
        CreateOrganizationRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationSummaryDto>> ListAsync(CancellationToken cancellationToken);

    Task<OrganizationDetailDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> CreateOrRotateOrganizationOwnerInviteAsync(
        Guid organizationId,
        CreateOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>> ListOrganizationOwnerInvitesAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>> AcceptOrganizationOwnerInviteAsync(
        AcceptOrganizationOwnerInviteRequest request,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateStatusAsync(
        Guid organizationId,
        UpdateOrganizationStatusRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateLimitsAsync(
        Guid organizationId,
        UpdateOrganizationLimitsRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateProfileAsync(
        Guid organizationId,
        UpdateOrganizationProfileRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> RevokeOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        RevokeOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> ResendOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateUpdateChannelAsync(
        Guid organizationId,
        UpdateOrganizationUpdateChannelRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> TransferOrganizationOwnerAsync(
        Guid organizationId,
        TransferOrganizationOwnerRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationBranchDto>> CreateBranchAsync(
        Guid organizationId,
        CreateBranchRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);
}
