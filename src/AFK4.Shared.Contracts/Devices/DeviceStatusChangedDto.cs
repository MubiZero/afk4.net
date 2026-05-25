using AFK4.Shared.Contracts.Install;

namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceStatusChangedDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    bool IsOnline,
    bool IsLocked,
    DateTimeOffset ObservedAtUtc,
    string DisplayName = "",
    string Role = DeviceRoleNames.GamingPc,
    string EnrollmentState = DeviceEnrollmentStateNames.Approved,
    Guid? SeatId = null);
