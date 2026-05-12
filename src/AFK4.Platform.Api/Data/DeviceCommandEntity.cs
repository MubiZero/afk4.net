namespace AFK4.Platform.Api.Data;

public sealed class DeviceCommandEntity
{
    public Guid CommandId { get; set; }

    public Guid DeviceId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
