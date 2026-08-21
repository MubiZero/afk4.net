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
    string Locale = "ru",
    string WarningKind = PlayerShellWarningKinds.None,
    ShellBrandingDto? Branding = null,
    // Код с этого монитора: человек набирает его в приложении и садится именно за эту машину.
    // Пусто, когда за ПК уже играют или связи с сервером нет — показать старый код значит
    // позвать человека к машине, которую сервер ему не отдаст.
    string? SeatingCode = null);
