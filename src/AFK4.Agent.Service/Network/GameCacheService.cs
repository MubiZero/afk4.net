using System.Diagnostics;
using System.Net;
using System.Text;
using AFK4.Shared.Contracts.Games;

namespace AFK4.Agent.Service.Network;

/// <summary>
/// Кеш игр клуба (docs/operations/game-cache-lancache.md): пока кеш-сервер отвечает, Steam и Epic на этом
/// ПК качают через него — агент держит их имена в hosts. Перестал отвечать — строки убираются: Steam
/// без кеша сам пошёл бы в интернет, а Epic упёрся бы в мёртвый адрес. Роутер клуба не трогаем.
/// </summary>
public sealed class GameCacheService(
    IHttpClientFactory httpClientFactory,
    ILogger<GameCacheService> logger,
    string? hostsPath = null) : BackgroundService
{
    public const string HttpClientName = "game-cache";

    /// <summary>Как часто проверять кеш: сервер в клубе включают и выключают руками, минуты хватает.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly string hostsFile = hostsPath ?? Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
    private readonly SemaphoreSlim changed = new(0, 1);
    private string? address;
    private bool configured;

    /// <summary>Видел ли ПК кеш при последней проверке; null — кеш не задан. Уходит в сердцебиение.</summary>
    public bool? Reachable { get; private set; }

    /// <summary>Адрес из сердцебиения: пусто — кеша нет; null — сервер прежней версии, ничего не меняем.</summary>
    public void Configure(string? cacheAddress)
    {
        if (cacheAddress is null) return;
        var next = GameCacheNames.IsValidAddress(cacheAddress) ? cacheAddress : null;
        if (configured && next == address) return;
        address = next;
        configured = true;
        if (changed.CurrentCount == 0) changed.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await changed.WaitAsync(CheckInterval, stoppingToken);
                if (configured) await CheckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Game cache: hosts file could not be updated.");
            }
        }
    }

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        var current = address;
        var reachable = current is not null && await ProbeAsync(current, cancellationToken);
        Reachable = current is null ? null : reachable;
        if (!OperatingSystem.IsWindows() && hostsPath is null) return;

        var text = File.Exists(hostsFile) ? await File.ReadAllTextAsync(hostsFile, cancellationToken) : string.Empty;
        var next = GameCacheHosts.Rewrite(text, reachable ? current : null);
        if (next == text) return;

        await File.WriteAllTextAsync(hostsFile, next, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        logger.LogInformation("Game cache {Address}: hosts {State}.", current, reachable ? "point to the cache" : "cleared");
        if (OperatingSystem.IsWindows() && hostsPath is null) FlushDns();
    }

    private async Task<bool> ProbeAsync(string cacheAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync($"http://{cacheAddress}{GameCacheNames.HeartbeatPath}", cancellationToken);
            return response.StatusCode == HttpStatusCode.NoContent || response.Headers.Contains(GameCacheNames.HeartbeatHeader);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    // Windows помнит прежний ответ DNS ещё минуты: без сброса Steam ходил бы старым путём.
    private void FlushDns()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
            process?.WaitForExit(5_000);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(exception, "Game cache: DNS cache could not be flushed.");
        }
    }
}

/// <summary>Строки кеша игр в hosts — между своими метками: чужие строки файла агент не трогает.</summary>
public static class GameCacheHosts
{
    public const string BeginMarker = "# AFK4 game cache begin (managed by AFK4 Agent)";
    public const string EndMarker = "# AFK4 game cache end";

    /// <param name="address">Куда вести имена кеша; null — убрать блок.</param>
    public static string Rewrite(string hosts, string? address)
    {
        var newline = hosts.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : Environment.NewLine;
        var lines = hosts.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var begin = lines.IndexOf(BeginMarker);
        var end = lines.IndexOf(EndMarker);
        if (begin >= 0 && end > begin)
        {
            lines.RemoveRange(begin, end - begin + 1);
        }

        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        if (address is not null)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add(BeginMarker);
            lines.AddRange(GameCacheNames.HostsEntries.Select(host => $"{address} {host}"));
            lines.Add(EndMarker);
        }

        return lines.Count == 0 ? string.Empty : string.Join(newline, lines) + newline;
    }
}
