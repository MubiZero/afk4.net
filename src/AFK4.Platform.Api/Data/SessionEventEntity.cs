namespace AFK4.Platform.Api.Data;

public sealed class SessionEventEntity
{
    public Guid SessionEventId { get; set; }

    public Guid SessionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? ActorStaffUserId { get; set; }

    public Guid? DeviceId { get; set; }

    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
