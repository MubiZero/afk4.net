namespace AFK4.Shared.Contracts.Diagnostics;

public sealed record BranchDiagnosticsDto(
    Guid OrganizationId,
    Guid BranchId,
    DateTimeOffset GeneratedAtUtc,
    DeviceDiagnosticsSummaryDto DeviceSummary,
    CommandDiagnosticsSummaryDto CommandSummary,
    UpdateDiagnosticsSummaryDto UpdateSummary,
    IReadOnlyList<StaleDeviceDiagnosticsDto> StaleDevices);

public sealed record DeviceDiagnosticsSummaryDto(
    int TotalDevices,
    int OnlineDevices,
    int LockedDevices,
    int StaleDevices,
    int StaleThresholdSeconds,
    DateTimeOffset? NewestHeartbeatAtUtc);

public sealed record CommandDiagnosticsSummaryDto(
    int PendingCommands,
    int FailedCommands,
    IReadOnlyList<FailedCommandDiagnosticsDto> RecentFailures);

public sealed record UpdateDiagnosticsSummaryDto(
    int ActiveRollouts,
    int InstallingDevices,
    int FailedDevices,
    int RollbackDevices,
    IReadOnlyList<FailedUpdateDiagnosticsDto> RecentFailures);

public sealed record StaleDeviceDiagnosticsDto(
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    bool IsOnline,
    bool IsLocked,
    DateTimeOffset? LastHeartbeatAtUtc,
    long? LastHeartbeatAgeSeconds);

public sealed record FailedCommandDiagnosticsDto(
    Guid DeviceId,
    string MachineName,
    Guid CommandId,
    string Type,
    string Status,
    string? Message,
    DateTimeOffset UpdatedAtUtc);

public sealed record FailedUpdateDiagnosticsDto(
    Guid DeviceId,
    string MachineName,
    Guid UpdateRolloutId,
    string Component,
    string TargetVersion,
    string Status,
    string Message,
    DateTimeOffset UpdatedAtUtc);
