namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset ObservedAtUtc,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? ActiveSessionLeaseExpiresAtUtc,
    int? ActiveSessionLeaseSequence);
