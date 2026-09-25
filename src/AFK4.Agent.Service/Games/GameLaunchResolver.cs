using System.Text.Json;
using AFK4.Shared.Contracts.Games;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Games;

/// <summary>Где на этом ПК лаунчеры. null — лаунчера нет, игра через него недоступна.</summary>
public interface IGameLauncherLocator
{
    string? SteamExecutable();

    string? EpicLauncherExecutable();

    string? RiotClientExecutable();

    string? BattleNetExecutable();

    /// <summary>Чем открыть ссылку лаунчера без проводника: в киоске его нет.</summary>
    string UrlOpener();
}

/// <summary>Команда запуска игры на этом ПК.</summary>
public sealed record ResolvedLaunch(string ExecutablePath, string Arguments);

/// <summary>
/// Способ запуска из библиотеки клуба — в команду на этом ПК (спека оболочки, §6.6). Путь к
/// лаунчеру у каждого ПК свой, поэтому его находит агент, а сервер хранит только что запускать.
/// Свой путь к exe у игры заменяет лаунчер целиком.
/// </summary>
public static class GameLaunchResolver
{
    public static ResolvedLaunch? Resolve(DeviceGameDto game, IGameLauncherLocator locator)
    {
        var arguments = game.Arguments?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(game.ExecutablePath))
        {
            return new ResolvedLaunch(game.ExecutablePath.Trim(), arguments);
        }

        var target = game.LaunchTarget?.Trim();
        return game.LaunchKind switch
        {
            GameLaunchKindNames.Executable when !string.IsNullOrWhiteSpace(target) => new ResolvedLaunch(target, arguments),
            _ when string.IsNullOrWhiteSpace(target) => null,
            GameLaunchKindNames.Steam => Launcher(locator.SteamExecutable(), Join($"-applaunch {target}", arguments)),
            GameLaunchKindNames.Riot => Launcher(locator.RiotClientExecutable(), Join($"--launch-product={target} --launch-patchline=live", arguments)),
            GameLaunchKindNames.BattleNet => Launcher(locator.BattleNetExecutable(), Join($"--exec=\"launch {target}\"", arguments)),
            // Epic запускает игру по своей ссылке; открыть её без проводника умеет url.dll. Параметры
            // запуска у такой ссылки не передаются — их задают в самом лаунчере.
            GameLaunchKindNames.Epic => locator.EpicLauncherExecutable() is null
                ? null
                : new ResolvedLaunch(
                    locator.UrlOpener(),
                    $"url.dll,FileProtocolHandler com.epicgames.launcher://apps/{Uri.EscapeDataString(target!)}?action=launch&silent=true"),
            _ => null
        };
    }

    private static ResolvedLaunch? Launcher(string? executable, string arguments) =>
        executable is null ? null : new ResolvedLaunch(executable, arguments);

    private static string Join(string launcherArguments, string gameArguments) =>
        gameArguments.Length == 0 ? launcherArguments : $"{launcherArguments} {gameArguments}";
}

/// <summary>Лаунчеры на Windows: реестр, если он знает путь, иначе место установки по умолчанию.</summary>
public sealed class WindowsGameLauncherLocator : IGameLauncherLocator
{
    private static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public string? SteamExecutable()
    {
        var installPath = OperatingSystem.IsWindows()
            ? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string
            : null;
        return Existing(
            installPath is null ? null : Path.Combine(installPath, "steam.exe"),
            Path.Combine(ProgramFilesX86, "Steam", "steam.exe"));
    }

    public string? EpicLauncherExecutable() => Existing(
        Path.Combine(ProgramFilesX86, "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
        Path.Combine(ProgramFilesX86, "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe"));

    public string? RiotClientExecutable()
    {
        // Riot сам пишет, где его клиент: RiotClientInstalls.json → rc_default.
        string? fromInstalls = null;
        var installs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
        try
        {
            if (File.Exists(installs))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(installs));
                fromInstalls = document.RootElement.TryGetProperty("rc_default", out var path) ? path.GetString() : null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return Existing(fromInstalls, @"C:\Riot Games\Riot Client\RiotClientServices.exe");
    }

    public string? BattleNetExecutable() => Existing(Path.Combine(ProgramFilesX86, "Battle.net", "Battle.net.exe"));

    public string UrlOpener() => Path.Combine(Environment.SystemDirectory, "rundll32.exe");

    private static string? Existing(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate));
}
