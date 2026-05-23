namespace AFK4.Platform.Api.Data;

public sealed class TenantSupportNoteEntity
{
    public Guid TenantSupportNoteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid AuthorPlatformAdminUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
