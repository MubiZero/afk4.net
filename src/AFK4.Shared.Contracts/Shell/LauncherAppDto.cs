namespace AFK4.Shared.Contracts.Shell;

public sealed record LauncherAppDto(
    string AppId,
    string DisplayName,
    string Category,
    string? IconUri,
    bool IsAvailable,
    /// Возрастная отметка игры (0, 12, 16, 18). Проверить её не на чем — у игрока нет даты рождения.
    int? MinAge = null);
