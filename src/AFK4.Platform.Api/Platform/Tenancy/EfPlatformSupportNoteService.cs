using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfPlatformSupportNoteService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformSupportNoteService
{
    private const int MaxBodyLength = 4000;

    public async Task<PlatformTenantOperationResult<IReadOnlyList<TenantSupportNoteDto>>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<IReadOnlyList<TenantSupportNoteDto>>.BadRequest(
                "OrganizationId is required.");
        }

        var organizationExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformTenantOperationResult<IReadOnlyList<TenantSupportNoteDto>>.NotFound(
                "Tenant was not found.");
        }

        var notes = await dbContext.TenantSupportNotes
            .AsNoTracking()
            .Where(note => note.OrganizationId == organizationId)
            .OrderByDescending(note => note.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            notes.Select(note => note.AuthorPlatformAdminUserId),
            cancellationToken);

        IReadOnlyList<TenantSupportNoteDto> result = notes
            .Select(note => ToDto(note, displayNames))
            .ToList();
        return PlatformTenantOperationResult<IReadOnlyList<TenantSupportNoteDto>>.Success(result);
    }

    public async Task<PlatformTenantOperationResult<TenantSupportNoteDto>> CreateAsync(
        Guid organizationId,
        CreateTenantSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.BadRequest(
                "OrganizationId is required.");
        }

        var bodyError = ValidateBody(request.Body);
        if (bodyError is not null)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.BadRequest(bodyError);
        }

        var organizationExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.NotFound("Tenant was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var entity = new TenantSupportNoteEntity
        {
            TenantSupportNoteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            AuthorPlatformAdminUserId = platformAdminUserId,
            Body = request.Body.Trim(),
            CreatedAtUtc = now
        };
        dbContext.TenantSupportNotes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            [platformAdminUserId],
            cancellationToken);
        return PlatformTenantOperationResult<TenantSupportNoteDto>.Success(ToDto(entity, displayNames));
    }

    public async Task<PlatformTenantOperationResult<TenantSupportNoteDto>> UpdateAsync(
        Guid organizationId,
        Guid tenantSupportNoteId,
        UpdateTenantSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.BadRequest(
                "OrganizationId is required.");
        }

        if (tenantSupportNoteId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.BadRequest(
                "TenantSupportNoteId is required.");
        }

        var bodyError = ValidateBody(request.Body);
        if (bodyError is not null)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.BadRequest(bodyError);
        }

        var note = await dbContext.TenantSupportNotes
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.TenantSupportNoteId == tenantSupportNoteId &&
                    candidate.OrganizationId == organizationId,
                cancellationToken);
        if (note is null)
        {
            return PlatformTenantOperationResult<TenantSupportNoteDto>.NotFound(
                "Support note was not found in this tenant.");
        }

        note.Body = request.Body.Trim();
        // Audit at the endpoint layer captures both the editing admin and the original author.
        _ = platformAdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            [note.AuthorPlatformAdminUserId],
            cancellationToken);
        return PlatformTenantOperationResult<TenantSupportNoteDto>.Success(ToDto(note, displayNames));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadAuthorDisplayNamesAsync(
        IEnumerable<Guid> authorIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = authorIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .Where(admin => distinctIds.Contains(admin.PlatformAdminUserId))
            .ToDictionaryAsync(admin => admin.PlatformAdminUserId, admin => admin.DisplayName, cancellationToken);
    }

    private static TenantSupportNoteDto ToDto(
        TenantSupportNoteEntity note,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        return new TenantSupportNoteDto(
            TenantSupportNoteId: note.TenantSupportNoteId,
            OrganizationId: note.OrganizationId,
            AuthorPlatformAdminId: note.AuthorPlatformAdminUserId,
            AuthorDisplayName: displayNames.GetValueOrDefault(note.AuthorPlatformAdminUserId, string.Empty),
            Body: note.Body,
            CreatedAtUtc: note.CreatedAtUtc);
    }

    private static string? ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Body is required.";
        }

        return body.Trim().Length > MaxBodyLength
            ? $"Body must contain {MaxBodyLength} characters or fewer."
            : null;
    }
}
