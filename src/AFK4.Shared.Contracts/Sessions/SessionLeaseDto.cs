namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionLeaseDto(
    Guid SessionId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    string State,
    int Sequence,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string SignatureAlgorithm,
    string Signature);
