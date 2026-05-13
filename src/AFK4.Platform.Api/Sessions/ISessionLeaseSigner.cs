using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public interface ISessionLeaseSigner
{
    SessionLeaseDto Sign(
        Guid SessionId,
        Guid OrganizationId,
        Guid BranchId,
        Guid SeatId,
        Guid DeviceId,
        string State,
        int Sequence,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}
