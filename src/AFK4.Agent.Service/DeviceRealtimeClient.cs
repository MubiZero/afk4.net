using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public interface IDeviceRealtimeClient : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Канал отпал насовсем — поднять заново. Зовётся из цикла сердцебиения и не держит его:
    /// возвращённая задача — сама попытка, ждать её циклу не нужно.
    /// </summary>
    Task EnsureConnectedAsync(CancellationToken cancellationToken);
}

public interface IDeviceHubConnection : IAsyncDisposable
{
    event Func<string?, Task>? Reconnected;

    /// <summary>Соединение закрыто и само подниматься не будет.</summary>
    bool IsDisconnected { get; }

    IDisposable On<T>(string methodName, Func<T, Task> handler);

    Task StartAsync(CancellationToken cancellationToken);

    Task InvokeAsync(string methodName, object? argument, CancellationToken cancellationToken = default);
}

public sealed class DeviceRealtimeClient : IDeviceRealtimeClient
{
    private readonly AgentOptions options;
    private readonly IDeviceCommandHandler commandHandler;
    private readonly ILogger<DeviceRealtimeClient> logger;
    private readonly IDeviceHubConnection connection;
    private readonly ISessionLeaseStore? leaseStore;
    private readonly ICommandResultOutbox? commandResultOutbox;

    /// null у тех, кто про смену ключа не знает (тесты хаба): тогда берётся ключ из конфига.
    private readonly IDeviceCredentialStore? credentialStore;

    private int restarting;
    private bool restartFailed;

    /// ПК зарегистрирован на открытом соединении. Открытое, но незарегистрированное соединение
    /// команд не получает: сервер не знает, чьё оно.
    private volatile bool registered;

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        ISessionLeaseStore leaseStore,
        ICommandResultOutbox commandResultOutbox,
        IDeviceCredentialStore credentialStore,
        PlayerSignIn playerSignIn)
        : this(
            options,
            commandHandler,
            logger,
            leaseStore,
            commandResultOutbox,
            new SignalRDeviceHubConnection(
                new HubConnectionBuilder()
                    .WithUrl(new Uri(options.Value.PlatformBaseUrl, "/hubs/devices"))
                    .WithAutomaticReconnect()
                    .Build()),
            credentialStore,
            playerSignIn)
    {
    }

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        IDeviceHubConnection connection)
        : this(options, commandHandler, logger, leaseStore: null, commandResultOutbox: null, connection)
    {
    }

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        ISessionLeaseStore? leaseStore,
        ICommandResultOutbox? commandResultOutbox,
        IDeviceHubConnection connection,
        IDeviceCredentialStore? credentialStore = null,
        PlayerSignIn? playerSignIn = null)
    {
        this.credentialStore = credentialStore;
        this.options = options.Value;
        this.commandHandler = commandHandler;
        this.logger = logger;
        this.leaseStore = leaseStore;
        this.commandResultOutbox = commandResultOutbox;
        this.connection = connection;

        this.connection.On<DeviceCommandDto>(DeviceRealtimeEvents.DeviceCommand, HandleCommandAsync);
        if (playerSignIn is not null)
        {
            // Человек поднёс телефон к QR на мониторе: забрать заявку сразу, не дожидаясь сердцебиения.
            this.connection.On<PlayerSignInClaimedDto>(
                DeviceRealtimeEvents.PlayerSignInClaimed,
                claim => playerSignIn.RedeemClaimAsync(claim.ClaimId, CancellationToken.None));
        }
        this.connection.Reconnected += HandleReconnectedAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await connection.StartAsync(cancellationToken);
        await RegisterDeviceAsync(cancellationToken);

        logger.LogInformation("Realtime device channel connected for {DeviceId}.", options.DeviceId);
    }

    /// <summary>
    /// SignalR переподключается сам лишь четыре раза (0, 2, 10 и 30 секунд) и сдаётся, а старт без
    /// сети не повторяет вовсе. На приёмке 30.09.2026 ПК 18 минут был без связи — канал закрылся
    /// и не поднялся ни после возврата связи, ни после перезапуска сервера: команды Панели шли
    /// только с сердцебиением, до 10 секунд вместо мгновения. Теперь цикл сердцебиения
    /// поднимает закрытый канал сам. Одна попытка за раз, в журнал — только первая неудача подряд.
    /// </summary>
    public Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if ((!connection.IsDisconnected && registered) || Interlocked.Exchange(ref restarting, 1) == 1)
        {
            return Task.CompletedTask;
        }

        return RestartAsync(cancellationToken);
    }

    private async Task RestartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Соединение открыто, а регистрация сорвалась (приёмка 30.09.2026: сервер ответил «Invalid
            // device credential» в момент старта) — канал висел «подключённым», и поднимать его было
            // незачем, а команды по нему не шли. Такому нужна только регистрация.
            if (connection.IsDisconnected)
            {
                await StartAsync(cancellationToken);
            }
            else
            {
                await RegisterDeviceAsync(cancellationToken);
                logger.LogInformation("Realtime device channel registered again for {DeviceId}.", options.DeviceId);
            }

            restartFailed = false;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (!restartFailed)
            {
                logger.LogWarning(exception, "Realtime device channel is down and could not be restarted. Commands arrive with the heartbeat until it is back.");
            }

            restartFailed = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Volatile.Write(ref restarting, 0);
        }
    }

    private Task HandleReconnectedAsync(string? connectionId)
    {
        return RegisterDeviceAsync(CancellationToken.None);
    }

    private async Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        registered = false;
        var request = DeviceConnectionRequestFactory.Create(
            options, DateTimeOffset.UtcNow, leaseStore, credentialStore?.Current);
        await connection.InvokeAsync(DeviceRealtimeMethods.RegisterDeviceAsync, request, cancellationToken);
        registered = true;
    }

    private async Task HandleCommandAsync(DeviceCommandDto command)
    {
        // Persist the result before delivery so SignalR shares the same durable return path as the HTTP
        // heartbeat (spec §6.4). If the realtime invoke fails, the result stays queued and the heartbeat
        // drain re-POSTs it; the backend dedupes on CommandId.
        var result = await commandHandler.HandleAsync(command, CancellationToken.None);
        commandResultOutbox?.Enqueue(result);

        try
        {
            await connection.InvokeAsync(DeviceRealtimeMethods.ReportCommandResultAsync, result);
        }
        catch (Exception exception)
        {
            // Ответ уже в очереди — его до-отправит сердцебиение. А вот исключение отсюда уходило
            // внутрь SignalR и не попадало никуда: в журнале машины про эту команду не было ни
            // строчки, и разбираться было не с чем.
            logger.LogWarning(
                exception,
                "Command {CommandId} result could not be reported over the realtime channel. It stays queued for the heartbeat.",
                command.CommandId);
            return;
        }

        commandResultOutbox?.Acknowledge(result.CommandId);

        logger.LogInformation(
            "Command {CommandId} acknowledged as {Status}.",
            command.CommandId,
            result.Status);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

internal sealed class SignalRDeviceHubConnection(HubConnection connection) : IDeviceHubConnection
{
    public event Func<string?, Task>? Reconnected
    {
        add => connection.Reconnected += value;
        remove => connection.Reconnected -= value;
    }

    public bool IsDisconnected => connection.State == HubConnectionState.Disconnected;

    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        return connection.On(methodName, handler);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    public Task InvokeAsync(string methodName, object? argument, CancellationToken cancellationToken = default)
    {
        return connection.InvokeAsync(methodName, argument, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return connection.DisposeAsync();
    }
}
