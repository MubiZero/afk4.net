using System.Text.RegularExpressions;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Games;

/// <summary>
/// Каталог игр платформы и библиотеки филиалов (спека оболочки, §6.6): проверка, выдача агенту,
/// версия библиотеки. Одно место на Platform Control, Панель и агента.
/// </summary>
public static partial class GameLibrary
{
    /// <summary>
    /// Что подставится в командную строку лаунчера. Только буквы, цифры и немногие знаки: из
    /// Панели не должен приехать аргумент, который сломает строку запуска на всём зале.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,200}$")]
    private static partial Regex LauncherTargetPattern();

    [GeneratedRegex("^[0-9]{1,10}$")]
    private static partial Regex SteamAppIdPattern();

    [GeneratedRegex(@"^[A-Za-z]:\\[^""<>|?*\r\n]+\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex WindowsExePattern();

    public static string? Validate(UpsertCatalogGameRequest request) =>
        ValidateCommon(request.Name, request.Genre, request.MinAge)
        ?? ValidateText(request.Description, GameLibraryLimits.MaxDescriptionLength, "Description")
        ?? ValidateCover(request.CoverUrl)
        ?? ValidateLaunch(request.LaunchKind, request.LaunchTarget, executablePath: null, requireExecutable: false)
        ?? (request.LaunchKind == GameLaunchKindNames.Executable && !string.IsNullOrWhiteSpace(request.LaunchTarget)
            ? ValidateExecutable(request.LaunchTarget)
            : null);

    /// <param name="catalogLaunchTarget">Цель запуска из каталога: своя игра клуба может её не повторять.</param>
    public static string? Validate(UpsertBranchGameRequest request, string? catalogLaunchTarget)
    {
        var target = string.IsNullOrWhiteSpace(request.LaunchTarget) ? catalogLaunchTarget : request.LaunchTarget;
        return ValidateCommon(request.Name, request.Genre, request.MinAge)
            ?? ValidateLaunch(request.LaunchKind, target, request.ExecutablePath, requireExecutable: request.LaunchKind == GameLaunchKindNames.Executable)
            ?? (string.IsNullOrWhiteSpace(request.ExecutablePath) ? null : ValidateExecutable(request.ExecutablePath))
            ?? ValidateText(request.Arguments, GameLibraryLimits.MaxArgumentsLength, "Arguments")
            ?? (request.Arguments?.IndexOfAny(['\r', '\n']) >= 0 ? "Arguments must be one line." : null);
    }

    private static string? ValidateCommon(string name, string? genre, int? minAge)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > GameLibraryLimits.MaxNameLength)
        {
            return $"Name is required and at most {GameLibraryLimits.MaxNameLength} characters.";
        }

        if (minAge is < 0 or > GameLibraryLimits.MaxAge)
        {
            return $"MinAge must be between 0 and {GameLibraryLimits.MaxAge}.";
        }

        return ValidateText(genre, GameLibraryLimits.MaxGenreLength, "Genre");
    }

    private static string? ValidateText(string? value, int maxLength, string field) =>
        value is not null && value.Trim().Length > maxLength ? $"{field} is at most {maxLength} characters." : null;

    private static string? ValidateCover(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return null;
        }

        return coverUrl.Length <= GameLibraryLimits.MaxCoverUrlLength
               && Uri.TryCreate(coverUrl.Trim(), UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
            ? null
            : "CoverUrl must be an https address.";
    }

    private static string? ValidateLaunch(string launchKind, string? launchTarget, string? executablePath, bool requireExecutable)
    {
        if (!GameLaunchKindNames.All.Contains(launchKind))
        {
            return $"LaunchKind takes only: {string.Join(", ", GameLaunchKindNames.All)}.";
        }

        var target = launchTarget?.Trim();
        return launchKind switch
        {
            GameLaunchKindNames.Executable => requireExecutable && string.IsNullOrWhiteSpace(executablePath) && string.IsNullOrWhiteSpace(target)
                ? "An exe game needs the path to its exe."
                : null,
            // Свой путь к exe заменяет лаунчер — цель лаунчера тогда не нужна.
            _ when !string.IsNullOrWhiteSpace(executablePath) => null,
            GameLaunchKindNames.Steam => target is not null && SteamAppIdPattern().IsMatch(target) ? null : "A Steam game needs its AppID (digits).",
            _ => target is not null && LauncherTargetPattern().IsMatch(target) ? null : "The launcher needs the game's id: letters, digits, _ . : -."
        };
    }

    private static string? ValidateExecutable(string path) =>
        path.Trim().Length <= GameLibraryLimits.MaxPathLength && WindowsExePattern().IsMatch(path.Trim())
            ? null
            : @"The path must be a full Windows path to an .exe, like C:\Games\Game\game.exe.";

    public static CatalogGameDto ToDto(CatalogGameEntity entity) => new(
        entity.CatalogGameId,
        entity.Name,
        entity.Description,
        entity.Genre,
        entity.MinAge,
        entity.LaunchKind,
        entity.LaunchTarget,
        entity.CoverUrl,
        entity.IsPublished,
        entity.UpdatedAtUtc);

    /// <summary>У игры из каталога обложка и возраст — каталожные: платформа их обновит, клубу не нужно.</summary>
    public static BranchGameDto ToDto(BranchGameEntity entity, CatalogGameEntity? catalog) => new(
        entity.BranchGameId,
        entity.CatalogGameId,
        entity.Name,
        catalog?.Genre ?? entity.Genre,
        catalog?.MinAge ?? entity.MinAge,
        catalog?.CoverUrl ?? entity.CoverUrl,
        entity.LaunchKind,
        entity.LaunchTarget ?? catalog?.LaunchTarget,
        entity.ExecutablePath,
        entity.Arguments,
        entity.AvailableWithoutSession,
        entity.IsEnabled,
        entity.SortOrder,
        entity.LaunchOnSessionStart);

    public static async Task<IReadOnlyList<BranchGameDto>> ListAsync(
        PlatformDbContext dbContext, Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.BranchGames.AsNoTracking()
            .Where(game => game.OrganizationId == organizationId && game.BranchId == branchId)
            .OrderBy(game => game.SortOrder)
            .ThenBy(game => game.Name)
            .Select(game => new
            {
                Game = game,
                Catalog = dbContext.CatalogGames.FirstOrDefault(catalog => catalog.CatalogGameId == game.CatalogGameId)
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => ToDto(row.Game, row.Catalog)).ToList();
    }

    public static async Task<DeviceGameLibraryDto> ForDeviceAsync(
        PlatformDbContext dbContext, Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var version = await dbContext.BranchGameLibraries.AsNoTracking()
            .Where(library => library.BranchId == branchId)
            .Select(library => library.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var games = (await ListAsync(dbContext, organizationId, branchId, cancellationToken))
            .Where(game => game.IsEnabled)
            .Select(game => new DeviceGameDto(
                game.BranchGameId.ToString("N"),
                game.Name,
                game.Genre,
                game.MinAge,
                game.CoverUrl,
                game.LaunchKind,
                game.LaunchTarget,
                game.ExecutablePath,
                game.Arguments,
                game.AvailableWithoutSession,
                game.LaunchOnSessionStart))
            .ToList();
        return new DeviceGameLibraryDto(version, games);
    }

    /// <summary>Правка библиотеки: версия растёт тем же сохранением, что и сама правка.</summary>
    public static async Task BumpVersionAsync(
        PlatformDbContext dbContext, Guid organizationId, Guid branchId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var library = await dbContext.BranchGameLibraries.SingleOrDefaultAsync(row => row.BranchId == branchId, cancellationToken);
        if (library is null)
        {
            dbContext.BranchGameLibraries.Add(new BranchGameLibraryEntity
            {
                BranchId = branchId,
                OrganizationId = organizationId,
                Version = 1,
                UpdatedAtUtc = now
            });
            return;
        }

        library.Version++;
        library.UpdatedAtUtc = now;
    }

    /// <summary>Каталог поменял игру — ПК всех клубов с ней перечитают библиотеку и обложку.</summary>
    public static async Task BumpVersionsUsingAsync(
        PlatformDbContext dbContext, Guid catalogGameId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var branchIds = await dbContext.BranchGames
            .Where(game => game.CatalogGameId == catalogGameId)
            .Select(game => new { game.OrganizationId, game.BranchId })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var branch in branchIds)
        {
            await BumpVersionAsync(dbContext, branch.OrganizationId, branch.BranchId, now, cancellationToken);
        }
    }

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
