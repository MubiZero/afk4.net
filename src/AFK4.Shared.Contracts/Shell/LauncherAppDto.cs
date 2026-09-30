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

/// <summary>Запущенная из библиотеки игра, которая ещё работает на этом ПК.</summary>
public sealed record LaunchedAppDto(
    /// Номер запуска: по нему «Закрыть» и «Вернуться» находят именно эту копию игры.
    Guid LaunchId,
    string AppId,
    string DisplayName,
    /// Процессы игры и её дочерние. Окна ищет по ним оболочка: служба в сессии 0 окон игрока не видит.
    IReadOnlyList<int> ProcessIds);
