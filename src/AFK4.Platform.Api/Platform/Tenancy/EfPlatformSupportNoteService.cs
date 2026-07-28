using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfPlatformSupportNoteService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformSupportNoteService
{
    private const int MaxBodyLength = 4000;

    public async Task<PlatformOrganizationOperationResult<IReadOnlyList<OrganizationSupportNoteDto>>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationSupportNoteDto>>.BadRequest(
                "OrganizationId is required.");
        }

        var organizationExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationSupportNoteDto>>.NotFound(
                "Organization was not found.");
        }

        var notes = await dbContext.OrganizationSupportNotes
            .AsNoTracking()
            .Where(note => note.OrganizationId == organizationId)
            .OrderByDescending(note => note.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            notes.Select(note => note.AuthorPlatformAdminUserId),
            cancellationToken);

        IReadOnlyList<OrganizationSupportNoteDto> result = notes
            .Select(note => ToDto(note, displayNames))
            .ToList();
        return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationSupportNoteDto>>.Success(result);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationSupportNoteDto>> CreateAsync(
        Guid organizationId,
        CreateOrganizationSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.BadRequest(
                "OrganizationId is required.");
        }

        var bodyError = ValidateBody(request.Body);
        if (bodyError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.BadRequest(bodyError);
        }

        var organizationExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.NotFound("Organization was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var entity = new OrganizationSupportNoteEntity
        {
            OrganizationSupportNoteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            AuthorPlatformAdminUserId = platformAdminUserId,
            Body = request.Body.Trim(),
            CreatedAtUtc = now
        };
        dbContext.OrganizationSupportNotes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            [platformAdminUserId],
            cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.Success(ToDto(entity, displayNames));
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationSupportNoteDto>> UpdateAsync(
        Guid organizationId,
        Guid organizationSupportNoteId,
        UpdateOrganizationSupportNoteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.BadRequest(
                "OrganizationId is required.");
        }

        if (organizationSupportNoteId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.BadRequest(
                "OrganizationSupportNoteId is required.");
        }

        var bodyError = ValidateBody(request.Body);
        if (bodyError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.BadRequest(bodyError);
        }

        var note = await dbContext.OrganizationSupportNotes
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationSupportNoteId == organizationSupportNoteId &&
                    candidate.OrganizationId == organizationId,
                cancellationToken);
        if (note is null)
        {
            return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.NotFound(
                "Support note was not found in this organization.");
        }

        note.Body = request.Body.Trim();
        // Audit at the endpoint layer captures both the editing admin and the original author.
        _ = platformAdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        var displayNames = await LoadAuthorDisplayNamesAsync(
            [note.AuthorPlatformAdminUserId],
            cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationSupportNoteDto>.Success(ToDto(note, displayNames));
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

    private static OrganizationSupportNoteDto ToDto(
        OrganizationSupportNoteEntity note,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        return new OrganizationSupportNoteDto(
            OrganizationSupportNoteId: note.OrganizationSupportNoteId,
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
