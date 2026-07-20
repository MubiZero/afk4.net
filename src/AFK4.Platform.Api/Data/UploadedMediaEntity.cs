namespace AFK4.Platform.Api.Data;

public sealed class UploadedMediaEntity
{
    public Guid MediaId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public Guid CreatedByStaffUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
