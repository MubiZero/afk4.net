using AFK4.Shared.Contracts.Platform.SupportNotes;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformSupportNoteService
{
    Task<PlatformTenantOperationResult<IReadOnlyList<TenantSupportNoteDto>>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantSupportNoteDto>> CreateAsync(
        Guid organizationId,
        CreateTenantSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformTenantOperationResult<TenantSupportNoteDto>> UpdateAsync(
        Guid organizationId,
        Guid tenantSupportNoteId,
        UpdateTenantSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);
}
