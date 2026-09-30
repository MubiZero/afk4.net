using System.Runtime.CompilerServices;

namespace AFK4.SetupWizard.Tests;

// Журнал мастера — во временный файл до первого теста. Приёмка 30.09.2026: на чистой машине в
// %ProgramData%\AFK4\logs\setup-wizard.log нашлись «sc.exe exited with code 1053» из
// FakeCompletionAction и «The kiosk could not be set up» из KioskProvisionerTests.FakeMachine —
// Windows-проверка писала подставные сбои в боевой журнал, и по нему было не отличить сбой ПК от теста.
internal static class TestLogRedirect
{
    [ModuleInitializer]
    internal static void Redirect() =>
        Environment.SetEnvironmentVariable(
            "AFK4_SETUP_WIZARD_LOG",
            Path.Combine(Path.GetTempPath(), $"afk4-setup-wizard-test-{Environment.ProcessId}.log"));
}
