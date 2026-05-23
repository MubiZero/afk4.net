namespace AFK4.Shared.Contracts.Platform.SupportNotes;

public sealed record TenantSupportNoteDto(
    Guid TenantSupportNoteId,
    Guid OrganizationId,
    Guid AuthorPlatformAdminId,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAtUtc);
