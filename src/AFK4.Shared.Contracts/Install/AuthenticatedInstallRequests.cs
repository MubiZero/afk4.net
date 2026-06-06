namespace AFK4.Shared.Contracts.Install;

/// <summary>Create-seat request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.</summary>
public sealed record AuthenticatedInstallCreateSeatRequest(
    Guid BranchId,
    Guid ZoneId,
    string Name);

/// <summary>Device-enroll request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.</summary>
public sealed record AuthenticatedInstallEnrollRequest(
    Guid BranchId,
    Guid? SeatId,
    string Role,
    string DisplayName,
    string MachineName,
    string DevicePublicKey);
