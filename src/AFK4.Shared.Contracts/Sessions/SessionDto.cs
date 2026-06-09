namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionDto(
    Guid SessionId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    string State,
    string TariffRuleVersionId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset? EndedAtUtc,
    int? RemainingSeconds,
    SessionLeaseDto? CurrentLease,
    // Optimistic-concurrency version the client echoes back as ExpectedVersion on the next mutation.
    int Version = 0);
