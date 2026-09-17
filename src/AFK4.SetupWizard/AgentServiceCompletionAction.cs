using System.Diagnostics;
using System.IO;
using System.Threading;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

public sealed class AgentServiceCompletionAction(string serviceName = "AFK4.Agent.Service") : ISetupWizardCompletionAction
{
    private const int ServiceDoesNotExist = 1060;
    private const int ServiceAlreadyRunning = 1056;

    /// <summary>Сколько раз пробовать поднять службу, пока она останавливается. Полминуты в сумме.</summary>
    private const int StartAttempts = 60;

    private static readonly TimeSpan StartRetryDelay = TimeSpan.FromMilliseconds(500);

    public void Complete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var queryResult = RunScCommand(["query", serviceName], throwOnFailure: false);
        if (queryResult == ServiceDoesNotExist)
        {
            return;
        }

        RunScCommand(["config", serviceName, "start=", "auto"], throwOnFailure: true);
        var startResult = RunScCommand(["start", serviceName], throwOnFailure: false);
        if (startResult == ServiceAlreadyRunning)
        {
            startResult = RestartWithTheNewEnrollment();
        }

        if (startResult != 0)
        {
            throw new InvalidOperationException($"AFK4.NET Agent Service could not be started. sc.exe exited with code {startResult}.");
        }

        SetupWizardFirstRunRegistration.Clear();
    }

    /// <summary>
    /// Настройку агент читает один раз, при запуске.
    ///
    /// Служба, которая уже крутилась, продолжала работать со старой: перезапись машины на другой
    /// клуб и починка bootstrap.json после сбоя не действовали до перезагрузки, а агент, поднятый
    /// без настройки, так и спал в простое. Со стороны клуба это выглядело как устройство,
    /// которое после успешной регистрации почему-то не выходит на связь.
    /// </summary>
    private int RestartWithTheNewEnrollment()
    {
        RunScCommand(["stop", serviceName], throwOnFailure: false);

        // sc stop возвращается сразу, а служба останавливается ещё какое-то время. Успехом
        // считаем только нулевой код от start: пока служба останавливается, sc отвечает «уже
        // запущена», и принять этот ответ за успех значило бы оставить машину с остановленным
        // агентом.
        var result = ServiceAlreadyRunning;
        for (var attempt = 0; attempt < StartAttempts && result != 0; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(StartRetryDelay);
            }

            result = RunScCommand(["start", serviceName], throwOnFailure: false);
        }

        return result;
    }

    private static int RunScCommand(IReadOnlyList<string> arguments, bool throwOnFailure)
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var scPath = string.IsNullOrWhiteSpace(windowsDirectory)
            ? "sc.exe"
            : Path.Combine(windowsDirectory, "System32", "sc.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = scPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sc.exe could not be started.");
        process.WaitForExit();

        if (throwOnFailure && process.ExitCode != 0)
        {
            var output = (process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd()).Trim();
            throw new InvalidOperationException($"sc.exe {arguments[0]} failed with exit code {process.ExitCode}. {output}");
        }

        return process.ExitCode;
    }
}
