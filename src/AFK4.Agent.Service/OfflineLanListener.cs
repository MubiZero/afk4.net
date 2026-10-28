using System.Net;
using System.Text.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Network;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

/// <summary>
/// Приём офлайн-аренд от Панели по сети клуба. Подпись ключом ПК администратора и есть авторизация:
/// без него никто в сети ПК не откроет (проверяет <see cref="OfflineSeatCommands"/>). Порт открыт
/// в брандмауэре для локальной подсети при любом профиле сети (<see cref="ClubNetworkFirewall"/>).
/// </summary>
public sealed class OfflineLanListener(
    OfflineSeatCommands commands,
    IOptions<AgentOptions> options,
    ILogger<OfflineLanListener> logger) : BackgroundService
{
    private const string FirewallRuleName = "AFK4.NET Agent (club network)";
    private const int MaxBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly TaskCompletionSource<bool> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Слушатель поднят (true) или не поднимется (false). Хост запускает <see cref="ExecuteAsync"/> в
    /// отдельной задаче, и до этого момента стучаться на порт бесполезно.
    /// </summary>
    public Task<bool> Listening => listening.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured || options.Value.OfflineLanPort <= 0)
        {
            listening.TrySetResult(false);
            return;
        }

        // Брандмауэр — только когда слушаем сеть клуба, а не одну петлю.
        if (OperatingSystem.IsWindows() && options.Value.OfflineLanPrefixHost == "http://+")
        {
            EnsureFirewallRule(options.Value.OfflineLanPort);
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add($"{options.Value.OfflineLanPrefixHost}:{options.Value.OfflineLanPort}/afk4/offline/");
        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            logger.LogWarning("The club network listener did not start: {Reason}. Offline leases from the Panel will not arrive.", exception.Message);
            listening.TrySetResult(false);
            return;
        }

        listening.TrySetResult(true);

        using var stop = stoppingToken.Register(listener.Stop);
        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (HttpListenerException exception)
            {
                logger.LogWarning("Club network listener failed: {Reason}", exception.Message);
                continue;
            }

            _ = HandleAsync(context, stoppingToken);
        }
    }

    public async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var response = context.Response;
        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != OfflineLeaseSigning.AgentLeasePath)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Тело читается с потолком, а не по заголовку длины: клиент может слать его частями.
            var body = await ReadBoundedAsync(context.Request.InputStream, cancellationToken);
            SessionLeaseDto? lease;
            try
            {
                lease = body is null ? null : JsonSerializer.Deserialize<SessionLeaseDto>(body, Json);
            }
            catch (JsonException)
            {
                lease = null;
            }

            if (lease is null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var result = await commands.ApplyAsync(lease, cancellationToken);
            response.StatusCode = result.Accepted ? (int)HttpStatusCode.OK : (int)HttpStatusCode.Conflict;
            response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(response.OutputStream, result, Json, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Offline lease request failed.");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            response.Close();
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBodyBytes)
            {
                return null;
            }
        }

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    private void EnsureFirewallRule(int port)
    {
        if (!ClubNetworkFirewall.EnsureRule(FirewallRuleName, port))
        {
            logger.LogWarning("Could not open TCP {Port} for the club network; the Panel may not reach this PC offline.", port);
        }
    }
}
