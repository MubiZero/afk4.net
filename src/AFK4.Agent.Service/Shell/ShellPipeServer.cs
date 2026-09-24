using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

/// <summary>Сроки канала. Отдельной записью — чтобы тесты не ждали по пять секунд на кадр.</summary>
public sealed record ShellPipeTimings(TimeSpan HelloTimeout, TimeSpan RebuildInterval, TimeSpan KeepAliveInterval)
{
    public static ShellPipeTimings Default { get; } = new(
        HelloTimeout: TimeSpan.FromSeconds(5),
        // Раз в секунду пересобираем состояние: так «офлайн» и «последняя минута» наступают
        // вовремя и без сигнала — их никто не присылает, они просто случаются по часам.
        RebuildInterval: TimeSpan.FromSeconds(1),
        // Даже без изменений хост слышит агента каждые пять секунд — по тишине он понимает,
        // что агента нет.
        KeepAliveInterval: TimeSpan.FromSeconds(5));
}

/// <summary>
/// Канал агент ↔ оболочка, версия 2 (спека оболочки, §4.2): одно постоянное соединение, агент сам
/// присылает состояние, когда оно изменилось, хост присылает запросы и получает ответы.
///
/// Заменил два старых канала: состояние хост спрашивал раз в 500 мс новым соединением, за
/// запуском игры ходил вторым, и оба были открыты любому процессу на машине.
/// </summary>
public sealed class ShellPipeServer(
    IOptions<AgentOptions> options,
    IPlayerShellStateBuilder stateBuilder,
    IShellStateSignal stateSignal,
    IPlayerShellRequestHandler requestHandler,
    IPlayerShellLaunchContext launchContext,
    TimeProvider timeProvider,
    ILogger<ShellPipeServer> logger,
    ShellPipeTimings? timings = null) : BackgroundService
{
    private static readonly JsonSerializerOptions SignatureJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ShellPipeTimings timings = timings ?? ShellPipeTimings.Default;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe(options.Value);
                await pipe.WaitForConnectionAsync(stoppingToken);
                await ServeAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                // Хост закрылся или оборвал кадр — ждём следующего подключения.
                logger.LogDebug(exception, "Shell pipe connection ended with an I/O error.");
            }
            catch (UnauthorizedAccessException exception)
            {
                // Канал с таким именем уже кем-то создан. Молчать нельзя: если это чужой процесс,
                // оболочка разговаривает не с агентом.
                logger.LogWarning(exception, "Shell pipe {PipeName} could not be created.", options.Value.ShellPipeName);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // Сбой одного соединения не должен остановить канал навсегда: экран игрока застынет.
                logger.LogWarning(exception, "Shell pipe connection failed unexpectedly. Waiting for the next one.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        var writeLock = new SemaphoreSlim(1, 1);

        var hello = await ReadHelloAsync(pipe, stoppingToken);
        if (hello is null)
        {
            logger.LogWarning("Shell host connected but did not introduce itself. Closing the connection.");
            return;
        }

        if (hello.Protocol != ShellPipeProtocol.Version)
        {
            logger.LogWarning(
                "Shell host {HostVersion} speaks protocol {Protocol}, agent speaks {AgentProtocol}.",
                hello.HostVersion,
                hello.Protocol,
                ShellPipeProtocol.Version);
            await SendAsync(pipe, writeLock, Bye(ShellPipeErrorCodeNames.ProtocolMismatch), stoppingToken);
            return;
        }

        var clientSessionId = ClientSessionId(pipe) ?? hello.SessionId;
        var consoleSessionId = launchContext.GetActiveUserSession()?.SessionId;
        if (consoleSessionId != clientSessionId)
        {
            logger.LogWarning(
                "Shell host from session {ClientSessionId} refused: the console session is {ConsoleSessionId}.",
                clientSessionId,
                consoleSessionId);
            await SendAsync(pipe, writeLock, Bye(ShellPipeErrorCodeNames.WrongSession), stoppingToken);
            return;
        }

        logger.LogInformation("Shell host {HostVersion} connected from session {SessionId}.", hello.HostVersion, clientSessionId);

        using var connection = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var pushing = PushStatesAsync(pipe, writeLock, connection.Token);
        var reading = ReadRequestsAsync(pipe, writeLock, connection.Token);

        // Кто первым закончил — хост закрылся или запись упала, — тот и закрывает соединение.
        var finished = await Task.WhenAny(pushing, reading);
        await connection.CancelAsync();
        try
        {
            await Task.WhenAll(pushing, reading);
        }
        catch (OperationCanceledException) when (connection.IsCancellationRequested)
        {
        }

        await finished;
        logger.LogInformation("Shell host disconnected.");
    }

    private async Task<ShellPipeHelloDto?> ReadHelloAsync(Stream pipe, CancellationToken stoppingToken)
    {
        using var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        helloTimeout.CancelAfter(timings.HelloTimeout);
        try
        {
            var message = await ShellPipeCodec.ReadAsync(pipe, helloTimeout.Token);
            return message is { Type: ShellPipeMessageTypeNames.Hello, Hello: { } hello } ? hello : null;
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            logger.LogWarning(exception, "Shell host sent an unreadable first frame.");
            return null;
        }
    }

    private async Task PushStatesAsync(Stream pipe, SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        string? lastSignature = null;
        var lastSentAt = 0L;

        while (true)
        {
            var state = stateBuilder.Build();
            var signature = Signature(state);
            if (!string.Equals(signature, lastSignature, StringComparison.Ordinal)
                || timeProvider.GetElapsedTime(lastSentAt) >= timings.KeepAliveInterval)
            {
                await SendAsync(pipe, writeLock, new ShellPipeMessage(ShellPipeMessageTypeNames.State, State: state), cancellationToken);
                lastSignature = signature;
                lastSentAt = timeProvider.GetTimestamp();
            }

            await stateSignal.WaitAsync(timings.RebuildInterval, cancellationToken);
        }
    }

    private async Task ReadRequestsAsync(Stream pipe, SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        var inFlight = new List<Task>();
        try
        {
            while (true)
            {
                var message = await ShellPipeCodec.ReadAsync(pipe, cancellationToken);
                if (message is null)
                {
                    return;
                }

                if (message is { Type: ShellPipeMessageTypeNames.Request, Request: { } request })
                {
                    // Каждый запрос отвечается сам по себе: вызов администратора ждёт сервер, и
                    // запуск игры за ним стоять в очереди не должен.
                    inFlight.RemoveAll(task => task.IsCompleted);
                    inFlight.Add(AnswerAsync(pipe, writeLock, request, cancellationToken));
                    continue;
                }

                logger.LogDebug("Shell host sent an unexpected {MessageType} frame; ignored.", message.Type);
            }
        }
        finally
        {
            try
            {
                await Task.WhenAll(inFlight);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException)
            {
                // Хост ушёл, отвечать некому.
            }
        }
    }

    private async Task AnswerAsync(Stream pipe, SemaphoreSlim writeLock, ShellPipeRequestDto request, CancellationToken cancellationToken)
    {
        ShellPipeReplyDto reply;
        try
        {
            reply = await requestHandler.HandleAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Shell request {RequestType} failed unexpectedly.", request.Type);
            reply = new ShellPipeReplyDto(request.RequestId, Ok: false, ShellPipeErrorCodeNames.LaunchFailed, "The agent could not handle the request.");
        }

        await SendAsync(pipe, writeLock, new ShellPipeMessage(ShellPipeMessageTypeNames.Reply, Reply: reply), cancellationToken);
        // Запуск или вызов мог поменять то, что видно на экране, — не ждём следующего круга.
        stateSignal.Notify();
    }

    private static async Task SendAsync(Stream pipe, SemaphoreSlim writeLock, ShellPipeMessage message, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await ShellPipeCodec.WriteAsync(pipe, message, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static ShellPipeMessage Bye(string reason) => new(ShellPipeMessageTypeNames.Bye, Reason: reason);

    /// <summary>
    /// Отпечаток состояния без того, что меняется само по себе каждую секунду: остатка и времени
    /// сборки. Хост досчитывает их сам по сроку аренды — слать кадр ради тиканья незачем.
    /// </summary>
    private static string Signature(PlayerShellStateDto state) =>
        JsonSerializer.Serialize(state with { RemainingSeconds = null, ObservedAtUtc = null }, SignatureJsonOptions);

    private static NamedPipeServerStream CreatePipe(AgentOptions agentOptions)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(
                agentOptions.ShellPipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        return NamedPipeServerStreamAcl.Create(
            agentOptions.ShellPipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            CreatePipeSecurity(agentOptions.ShellPipeClientSid));
    }

    /// <summary>
    /// Кто может открыть канал: система, администраторы и игрок. Раньше каналы создавались без
    /// списка доступа — и любой процесс на машине мог попросить агента запустить что-то от имени
    /// системы или прочитать чужое состояние.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static PipeSecurity CreatePipeSecurity(string? clientSid)
    {
        var client = string.IsNullOrWhiteSpace(clientSid)
            ? new SecurityIdentifier(WellKnownSidType.InteractiveSid, null)
            : new SecurityIdentifier(clientSid);

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(client, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return security;
    }

    /// <summary>
    /// Сессия процесса на том конце — по словам Windows, а не хоста. <c>null</c> там, где спросить
    /// не у кого (не Windows): тогда верим приветствию.
    /// </summary>
    private static int? ClientSessionId(NamedPipeServerStream pipe)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return NativeMethods.GetNamedPipeClientSessionId(pipe.SafePipeHandle, out var sessionId)
            ? checked((int)sessionId)
            : throw new IOException(
                "Could not read the shell host's session.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
    }
}

internal static partial class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeClientSessionId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientSessionId);
}
