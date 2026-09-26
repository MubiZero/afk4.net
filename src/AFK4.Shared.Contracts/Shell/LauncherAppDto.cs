namespace AFK4.Shared.Contracts.Shell;

public sealed record LauncherAppDto(
    string AppId,
    string DisplayName,
    string Category,
    string? IconUri,
    bool IsAvailable,
    /// Возрастная отметка игры (0, 12, 16, 18).
    int? MinAge = null,
    /// Игрок моложе отметки: плитка заперта, агент игру не запустит. Возраст неизвестен (дата
    /// рождения по желанию) — не заперта.
    bool AgeLocked = false);
