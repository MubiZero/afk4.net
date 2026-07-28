using AFK4.Shared.Contracts.Platform.SupportNotes;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformSupportNoteService
{
    Task<PlatformOrganizationOperationResult<IReadOnlyList<OrganizationSupportNoteDto>>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationSupportNoteDto>> CreateAsync(
        Guid organizationId,
        CreateOrganizationSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationOperationResult<OrganizationSupportNoteDto>> UpdateAsync(
        Guid organizationId,
        Guid organizationSupportNoteId,
        UpdateOrganizationSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken);
}
