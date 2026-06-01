namespace AFK4.Shared.Contracts.Shell;

public sealed record PlayerShellStateDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string State,
    Guid? SessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    int? RemainingSeconds,
    bool IsOnline,
    bool IsGraceMode,
    int WarningThresholdSeconds,
    string Message,
    IReadOnlyList<LauncherAppDto> LauncherApps,
    string Locale = "ru");
