using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using AFK4.Player.Shell.Configuration;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Realtime;

/// <summary>
/// Хостовая сторона канала агент ↔ оболочка, версия 2 (спека оболочки, §4.2).
///
/// Держит одно соединение, пока живо окно: представляется, принимает состояние, которое агент
/// шлёт сам, и носит запросы с ответами. Соединение пропало — переподключается с нарастающей
/// паузой; агент молчит дольше трёх пульсов — считает связь мёртвой и переподключается тоже.
/// Заменил опрос раз в 500 мс и отдельное соединение на каждый запуск игры.
/// </summary>
/// <param name="verifyAgentSession">
/// Проверять, что сервер канала — служба из сессии 0. Выключают только тесты: их поддельный агент
/// живёт в том же процессе, что и проверка.
/// </param>
public sealed class ShellPipeClient(PlayerShellOptions options, bool verifyAgentSession = true)
    : IPlayerShellStateClient, IShellAgentRequests
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    // Агент попрощался — версия протокола не та или сессия чужая. Долбиться раз в секунду в
    // канал, который ответит то же самое, незачем.
    private static readonly TimeSpan AfterByeDelay = TimeSpan.FromSeconds(30);

    // Последнее состояние важнее всей очереди: экран рисует то, что есть сейчас.
    private readonly Channel<PlayerShellStateDto> states = Channel.CreateBounded<PlayerShellStateDto>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    // Вход игрока и команды клуба теряться не должны: каждая — отдельное событие, а не снимок.
    private readonly Channel<ShellPipeMessage> pushes = Channel.CreateUnbounded<ShellPipeMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ShellPipeReplyDto>> pending = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private volatile Stream? connection;

    public bool IsConnected => connection is not null;

    /// <summary>Держать соединение до отмены. Запускается один раз на жизнь окна.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                var outcome = await ConnectAndServeAsync(cancellationToken);
                failures = outcome == SessionOutcome.Served ? 0 : failures + 1;
                delay = outcome == SessionOutcome.Refused
                    ? AfterByeDelay
                    : ReconnectDelays[Math.Min(failures, ReconnectDelays.Length - 1)];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or InvalidDataException
                                                  or JsonException or UnauthorizedAccessException)
            {
                if (failures == 0)
                {
                    // В журнал — первый сбой серии, а не каждая попытка раз в пять секунд.
                    PlayerShellStartupLog.Write("Shell pipe to the agent is down; reconnecting.", exception);
                }

                failures++;
                delay = ReconnectDelays[Math.Min(failures, ReconnectDelays.Length - 1)];
            }
            finally
            {
                connection = null;
                FailPending();
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public IAsyncEnumerable<PlayerShellStateDto> ReadStatesAsync(CancellationToken cancellationToken) =>
        states.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Кадры, которые агент шлёт без запроса: вход игрока (auth) и команды клуба (command).</summary>
    public IAsyncEnumerable<ShellPipeMessage> ReadPushesAsync(CancellationToken cancellationToken) =>
        pushes.Reader.ReadAllAsync(cancellationToken);

    public async Task<ShellPipeReplyDto> RequestAsync(
        string type,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        var request = new ShellPipeRequestDto(Guid.NewGuid(), type, payload);
        var stream = connection;
        if (stream is null)
        {
            return Unavailable(request.RequestId);
        }

        var reply = new TaskCompletionSource<ShellPipeReplyDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[request.RequestId] = reply;
        try
        {
            await SendAsync(stream, new ShellPipeMessage(ShellPipeMessageTypeNames.Request, Request: request), cancellationToken);
            return await reply.Task.WaitAsync(TimeSpan.FromMilliseconds(options.RequestTimeoutMilliseconds), cancellationToken);
        }
        // Канал закрылся между «есть соединение» и записью — это тот же «агента нет», а не сбой окна.
        catch (Exception exception) when (exception is IOException or ObjectDisposedException
                                              or InvalidOperationException or TimeoutException)
        {
            return Unavailable(request.RequestId);
        }
        finally
        {
            pending.TryRemove(request.RequestId, out _);
        }
    }

    private async Task<SessionOutcome> ConnectAndServeAsync(CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(".", options.ShellPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(options.ConnectTimeoutMilliseconds, cancellationToken);
        if (verifyAgentSession)
        {
            EnsureTheAgentIsOnTheOtherEnd(pipe);
        }

        using var self = Process.GetCurrentProcess();
        await SendAsync(pipe, new ShellPipeMessage(
            ShellPipeMessageTypeNames.Hello,
            Hello: new ShellPipeHelloDto(ShellPipeProtocol.Version, HostVersion(), self.SessionId)),
            cancellationToken);

        var served = false;
        while (true)
        {
            var message = await ReadWithinSilenceAsync(pipe, cancellationToken);
            if (message is null)
            {
                return served ? SessionOutcome.Served : SessionOutcome.Dropped;
            }

            switch (message.Type)
            {
                case ShellPipeMessageTypeNames.State when message.State is not null:
                    if (!served)
                    {
                        // Запросы можно слать, только когда агент нас принял: до первого состояния
                        // он ещё может попрощаться.
                        served = true;
                        connection = pipe;
                    }

                    states.Writer.TryWrite(message.State);
                    break;
                case ShellPipeMessageTypeNames.Reply when message.Reply is not null:
                    if (pending.TryRemove(message.Reply.RequestId, out var waiter))
                    {
                        waiter.TrySetResult(message.Reply);
                    }

                    break;
                case ShellPipeMessageTypeNames.Auth when message.Auth is not null:
                case ShellPipeMessageTypeNames.Command when message.Command is not null:
                    pushes.Writer.TryWrite(message);
                    break;
                case ShellPipeMessageTypeNames.Bye:
                    PlayerShellStartupLog.Write($"Agent closed the shell pipe: {message.Reason ?? "no reason given"}.");
                    return SessionOutcome.Refused;
            }
        }
    }

    /// <summary>
    /// Агент шлёт пульс каждые пять секунд. Три пропущенных — связи нет, даже если Windows ещё
    /// считает канал открытым (агент завис, а не упал).
    /// </summary>
    private async Task<ShellPipeMessage?> ReadWithinSilenceAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var silence = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        silence.CancelAfter(TimeSpan.FromMilliseconds(options.SilenceTimeoutMilliseconds));
        try
        {
            return await ShellPipeCodec.ReadAsync(pipe, silence.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The agent went silent on the shell pipe.");
        }
    }

    private async Task SendAsync(Stream stream, ShellPipeMessage message, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await ShellPipeCodec.WriteAsync(stream, message, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private void FailPending()
    {
        foreach (var (requestId, waiter) in pending)
        {
            if (pending.TryRemove(requestId, out _))
            {
                waiter.TrySetResult(Unavailable(requestId));
            }
        }
    }

    /// <summary>
    /// Служба живёт в сессии 0, а обычный процесс туда не попадает. Если сервер канала не в
    /// сессии 0 — канал занял кто-то чужой, и слушать его нельзя: он нарисует игроку что угодно.
    /// </summary>
    private static void EnsureTheAgentIsOnTheOtherEnd(NamedPipeClientStream pipe)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!GetNamedPipeServerSessionId(pipe.SafePipeHandle, out var serverSessionId))
        {
            throw new IOException(
                "Could not read the session of the shell pipe server.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
        }

        if (serverSessionId != 0)
        {
            throw new UnauthorizedAccessException(
                $"The shell pipe is served from session {serverSessionId}, not by the agent service.");
        }
    }

    private static string HostVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private static ShellPipeReplyDto Unavailable(Guid requestId) =>
        new(requestId, Ok: false, ShellPipeErrorCodeNames.AgentUnavailable, "The PC service is not reachable right now.");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerSessionId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint serverSessionId);

    private enum SessionOutcome
    {
        /// <summary>Агент принял и отдавал состояние, потом соединение закрылось.</summary>
        Served,

        /// <summary>Соединение закрылось, не успев начаться.</summary>
        Dropped,

        /// <summary>Агент попрощался.</summary>
        Refused
    }
}
