namespace AFK4.Shared.Contracts.Install;

public sealed record InstallEnrollRequest(
    string OwnerCode,
    Guid BranchId,
    Guid? SeatId,
    string Role,
    string DisplayName,
    string MachineName,
    string DevicePublicKey);
