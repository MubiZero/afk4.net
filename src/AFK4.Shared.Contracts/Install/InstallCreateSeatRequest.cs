namespace AFK4.Shared.Contracts.Install;

public sealed record InstallCreateSeatRequest(
    string OwnerCode,
    Guid BranchId,
    Guid ZoneId,
    string Name);
