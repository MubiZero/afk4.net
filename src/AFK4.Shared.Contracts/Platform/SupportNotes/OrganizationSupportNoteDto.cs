namespace AFK4.Shared.Contracts.Platform.SupportNotes;

public sealed record OrganizationSupportNoteDto(
    Guid OrganizationSupportNoteId,
    Guid OrganizationId,
    Guid AuthorPlatformAdminId,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAtUtc);
