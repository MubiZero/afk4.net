namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceEnrollmentRequest(
    Guid OrganizationId,
    Guid BranchId,
    string EnrollmentCode,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset RequestedAtUtc);
