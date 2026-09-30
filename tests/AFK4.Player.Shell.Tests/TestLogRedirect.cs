using System.Runtime.CompilerServices;

namespace AFK4.Player.Shell.Tests;

// Журнал оболочки — во временный файл до первого теста. Без этого прогон Windows-проверки на
// настоящем ПК (verify.sh windows) дописывал трассировки подставных объектов в боевой журнал
// %ProgramData%\AFK4\logs\player\player-shell.log, и в нём потом не отличить сбой ПК от теста.
internal static class TestLogRedirect
{
    [ModuleInitializer]
    internal static void Redirect() =>
        Environment.SetEnvironmentVariable(
            "AFK4_PLAYER_SHELL_LOG",
            Path.Combine(Path.GetTempPath(), $"afk4-player-shell-test-{Environment.ProcessId}.log"));
}
