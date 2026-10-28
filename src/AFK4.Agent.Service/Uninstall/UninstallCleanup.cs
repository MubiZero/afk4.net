using System.Runtime.Versioning;
using System.Text;
using AFK4.Agent.Service.Logging;
using AFK4.Agent.Service.Network;
using AFK4.Agent.Service.Power;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Uninstall;

/// <summary>
/// Уборка при удалении агента: установщик зовёт <c>AFK4.Agent.Service.exe --uninstall-cleanup</c> после
/// остановки службы и до удаления файлов. Возвращает Windows то, что агент менял на ходу (список — на
/// странице загрузки сайта, docs/operations/public-site.md): правила брандмауэра, блок кеша игр в hosts,
/// машинные запреты защиты и «быстрый запуск». Киоск снимает мастер (<c>--remove-kiosk</c>): учётка игрока — его.
/// </summary>
public static class UninstallCleanup
{
    public const string Argument = "--uninstall-cleanup";

    /// <summary>
    /// Все машинные значения, которые агент мог записать при любом профиле, и копии игроцких запретов в HKLM
    /// от прежних версий агента. Учётку игрока с её веткой реестра удаляет мастер вместе с киоском.
    /// </summary>
    public static IReadOnlyList<RegistryWrite> MachinePolicies() =>
        ProtectionPolicy.Plan(ProtectionProfileDefaults.Initial)
            .SelectMany(item => item.Writes)
            .Select(write => write with { Scope = PolicyScope.Machine })
            .Append(ProtectionPolicy.TaskManager)
            .DistinctBy(write => (write.Key, write.Name))
            .ToList();

    public static void ClearHostsFile(string hostsPath)
    {
        if (!File.Exists(hostsPath))
        {
            return;
        }

        // Без блока файл не трогаем вовсе: Rewrite заодно убрал бы чужие пустые строки в конце.
        var hosts = File.ReadAllText(hostsPath);
        if (hosts.Contains(GameCacheHosts.BeginMarker, StringComparison.Ordinal))
        {
            File.WriteAllText(hostsPath, GameCacheHosts.Rewrite(hosts, address: null), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    /// <summary>Код выхода 0 — всё убрано; 1 — что-то не вышло, подробности в logs\uninstall-cleanup.log.</summary>
    [SupportedOSPlatform("windows")]
    public static int RunOnThisPc()
    {
        var registry = new WindowsMachineRegistry(Options.Create(new AgentOptions()));
        (string Name, Action Run)[] steps =
        [
            ("firewall rules", () =>
            {
                ClubNetworkFirewall.RemoveRule(OfflineLanListener.FirewallRuleName);
                ClubNetworkFirewall.RemoveRule(GameCacheServer.FirewallRuleName);
            }),
            ("hosts game cache block", () => ClearHostsFile(GameCacheHosts.SystemPath)),
            ("machine policies", () =>
            {
                foreach (var write in MachinePolicies())
                {
                    registry.Remove(write);
                }
            }),
            ("fast startup", WakeOnLanReadiness.RestoreFastStartup)
        ];

        var log = new StringBuilder();
        var failed = false;
        foreach (var (name, run) in steps)
        {
            try
            {
                run();
                log.AppendLine($"{DateTimeOffset.Now:O} {name}: done");
            }
            // Уборка не должна срывать удаление: шаг, который не вышел, записывается, остальные идут дальше.
            catch (Exception exception)
            {
                failed = true;
                log.AppendLine($"{DateTimeOffset.Now:O} {name}: failed: {exception.Message}");
            }
        }

        try
        {
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(FileLoggerProvider.DefaultLogPath)!, "uninstall-cleanup.log"), log.ToString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.Write(log);
        }

        return failed ? 1 : 0;
    }
}
