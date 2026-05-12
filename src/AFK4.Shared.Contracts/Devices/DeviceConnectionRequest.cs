namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceConnectionRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset ConnectedAtUtc);
