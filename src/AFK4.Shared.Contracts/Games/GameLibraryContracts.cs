using System.Globalization;

namespace AFK4.Shared.Contracts.Games;

/// <summary>
/// Чем запускается игра (спека оболочки, §6.6). Путь к лаунчеру на каждом ПК свой — его находит
/// агент; сервер хранит только что запускать.
/// </summary>
public static class GameLaunchKindNames
{
    /// <summary>Через Steam по AppID: <c>steam.exe -applaunch 730</c>.</summary>
    public const string Steam = "steam";

    /// <summary>Через Epic Games Launcher по имени приложения: <c>Fortnite</c>.</summary>
    public const string Epic = "epic";

    /// <summary>Через Riot Client по продукту: <c>league_of_legends</c>, <c>valorant</c>.</summary>
    public const string Riot = "riot";

    /// <summary>Через Battle.net по коду игры: <c>WoW</c>, <c>Pro</c>.</summary>
    public const string BattleNet = "battlenet";

    /// <summary>Своим exe по пути на ПК.</summary>
    public const string Executable = "exe";

    public static readonly IReadOnlyList<string> All = [Steam, Epic, Riot, BattleNet, Executable];
}

/// <summary>Игра в каталоге платформы — из него клубы добавляют игры себе.</summary>
public sealed record CatalogGameDto(
    Guid CatalogGameId,
    string Name,
    string? Description,
    string? Genre,
    /// Возрастная отметка: 0, 12, 16, 18. Даты рождения у игрока нет — отметка только видна.
    int? MinAge,
    /// Одно из GameLaunchKindNames.
    string LaunchKind,
    /// AppID Steam, имя приложения Epic, продукт Riot, код Battle.net; для exe — путь по умолчанию.
    string? LaunchTarget,
    string? CoverUrl,
    bool IsPublished,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertCatalogGameRequest(
    string Name,
    string? Description,
    string? Genre,
    int? MinAge,
    string LaunchKind,
    string? LaunchTarget,
    string? CoverUrl,
    bool IsPublished);

/// <summary>Игра в библиотеке филиала — то, что увидит игрок на ПК.</summary>
public sealed record BranchGameDto(
    Guid BranchGameId,
    /// Из каталога — тогда обложка и возраст берутся оттуда; null — своя игра клуба.
    Guid? CatalogGameId,
    string Name,
    string? Genre,
    int? MinAge,
    string? CoverUrl,
    /// Одно из GameLaunchKindNames.
    string LaunchKind,
    string? LaunchTarget,
    /// Свой путь к exe вместо лаунчера — когда игра стоит не там, где её ищет агент.
    string? ExecutablePath,
    string? Arguments,
    /// Запускается и без сессии: лаунчер для пополнения Steam, например.
    bool AvailableWithoutSession,
    bool IsEnabled,
    int SortOrder);

public sealed record UpsertBranchGameRequest(
    Guid OrganizationId,
    Guid? CatalogGameId,
    string Name,
    string? Genre,
    int? MinAge,
    string LaunchKind,
    string? LaunchTarget,
    string? ExecutablePath,
    string? Arguments,
    bool AvailableWithoutSession,
    bool IsEnabled);

/// <summary>Порядок игр в библиотеке: все игры филиала в новом порядке.</summary>
public sealed record ReorderBranchGamesRequest(Guid OrganizationId, IReadOnlyList<Guid> BranchGameIds);

/// <summary>Игра для агента: всё, чтобы найти лаунчер на этом ПК и показать плитку.</summary>
public sealed record DeviceGameDto(
    string AppId,
    string DisplayName,
    string? Genre,
    int? MinAge,
    string? CoverUrl,
    /// Одно из GameLaunchKindNames.
    string LaunchKind,
    string? LaunchTarget,
    string? ExecutablePath,
    string? Arguments,
    bool AvailableWithoutSession);

/// <summary>Библиотека филиала для агента. Версия едет в сердцебиении.</summary>
public sealed record DeviceGameLibraryDto(int Version, IReadOnlyList<DeviceGameDto> Games);

public static class GameLibraryErrorCodeNames
{
    public const string InvalidGame = "invalid_game";

    public const string CatalogGameNotFound = "catalog_game_not_found";

    public const string LibraryFull = "game_library_full";
}

public static class GameLibraryLimits
{
    public const int MaxGamesPerBranch = 300;
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxGenreLength = 60;
    public const int MaxLaunchTargetLength = 200;
    public const int MaxPathLength = 512;
    public const int MaxArgumentsLength = 512;
    public const int MaxCoverUrlLength = 1024;
    public const int MaxAge = 21;
}

public static class GameLibraryRoutes
{
    public static string DeviceLibrary(Guid deviceId, Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/devices/{deviceId:D}/games?organizationId={organizationId:D}&branchId={branchId:D}");
}
