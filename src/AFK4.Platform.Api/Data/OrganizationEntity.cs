namespace AFK4.Platform.Api.Data;

public sealed class OrganizationEntity
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
