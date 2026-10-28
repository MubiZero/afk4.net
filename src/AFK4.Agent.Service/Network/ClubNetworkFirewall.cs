using System.Diagnostics;

namespace AFK4.Agent.Service.Network;

/// <summary>
/// Входящие порты агента в сети клуба (офлайн-аренда от Панели, кеш игр). Правило действует при любом
/// профиле сети Windows: сеть клуба нередко числится «Общедоступной», и ждать, пока кто-то сменит профиль,
/// — значит остаться без кеша и без кассы при обрыве связи. Профиль сети не трогаем — от него зависят
/// общий доступ к файлам и обнаружение, это чужая настройка. Пускаем только локальную подсеть, а дальше
/// защищают сами службы: аренда — подписью ключа клуба, кеш — только Steam и Epic и только из сети клуба.
/// </summary>
public static class ClubNetworkFirewall
{
    public static string AddRuleArguments(string ruleName, int port) =>
        $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP " +
        $"localport={port} profile=any remoteip=localsubnet";

    /// <summary>Пересоздаёт правило при каждом старте: порт мог смениться, а правило — быть правлено руками.</summary>
    public static bool EnsureRule(string ruleName, int port)
    {
        RemoveRule(ruleName);
        return Netsh(AddRuleArguments(ruleName, port));
    }

    /// <summary>Правила нет — netsh отвечает ошибкой; для удаления это тот же итог.</summary>
    public static void RemoveRule(string ruleName) => Netsh($"advfirewall firewall delete rule name=\"{ruleName}\"");

    private static bool Netsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
