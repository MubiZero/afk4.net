namespace AFK4.Shared.Contracts.Shell;

public sealed record LauncherAppDto(
    string AppId,
    string DisplayName,
    string Category,
    string? IconUri,
    bool IsAvailable);
