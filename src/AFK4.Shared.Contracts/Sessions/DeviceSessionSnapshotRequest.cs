namespace AFK4.Shared.Contracts.Sessions;

public sealed record DeviceSessionSnapshotRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid? ActiveSessionId,
    SessionLeaseDto? ActiveLease,
    bool IsLocked,
    int PendingLocalEventCount,
    DateTimeOffset ObservedAtUtc);
