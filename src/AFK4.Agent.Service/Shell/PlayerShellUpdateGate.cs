using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

/// <summary>
/// Обновление оболочки через канал. Приёмка 30.09–01.10.2026 показала две беды. Установщик менял
/// файлы, а работающая оболочка оставалась прежней до перезагрузки ПК — сервер и Панель при этом
/// уже писали новую версию. А супервизор, не знавший об установке, мог поднять оболочку посреди
/// замены файлов — процесс вставал на смеси старых и новых сборок и терял поля нового состояния.
/// Пока идёт установка, супервизор оболочку не поднимает; после неё старый процесс закрывается, и
/// супервизор запускает уже новую сборку.
/// </summary>
public interface IPlayerShellUpdateGate
{
    bool IsSuspended { get; }

    /// <summary>Не поднимать оболочку, пока возвращённое не освобождено.</summary>
    IDisposable Suspend();

    /// <summary>Закрыть работающую оболочку (с её окнами WebView2), чтобы супервизор поднял новую сборку.</summary>
    int StopRunningShells();
}

public sealed class PlayerShellUpdateGate(
    IOptions<AgentOptions> options,
    ILogger<PlayerShellUpdateGate> logger) : IPlayerShellUpdateGate
{
    private int suspended;

    public bool IsSuspended => Volatile.Read(ref suspended) > 0;

    public IDisposable Suspend()
    {
        Interlocked.Increment(ref suspended);
        return new Release(this);
    }

    public int StopRunningShells()
    {
        var executablePath = options.Value.PlayerShellExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return 0;
        }

        var stopped = 0;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath)))
        {
            using (process)
            {
                try
                {
                    if (!string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    stopped++;
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    logger.LogWarning(exception, "The old Player Shell process {ProcessId} could not be stopped after the update.", process.Id);
                }
            }
        }

        logger.LogInformation("Player Shell updated: stopped {Count} old shell process(es); the supervisor starts the new build.", stopped);
        return stopped;
    }

    private sealed class Release(PlayerShellUpdateGate gate) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                Interlocked.Decrement(ref gate.suspended);
            }
        }
    }
}
