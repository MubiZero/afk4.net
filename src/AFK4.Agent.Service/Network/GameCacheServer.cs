using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Games;

namespace AFK4.Agent.Service.Network;

/// <summary>
/// Кеш игр клуба, встроенный в агента ПК администратора (docs/operations/game-cache-lancache.md): клубу
/// ничего не ставить. Отвечает как LANCache: Steam и Epic игровых ПК (их hosts ведёт
/// <see cref="GameCacheService"/>) приходят сюда с настоящим именем CDN в Host, кусок игры качается из
/// интернета один раз и дальше отдаётся с диска. Чужим в сети и чужим сайтам не прокси: только частные
/// адреса и только имена Steam и Epic.
/// </summary>
public sealed class GameCacheServer(
    IHttpClientFactory httpClientFactory,
    ILogger<GameCacheServer> logger,
    string? cacheRoot = null,
    string listenPrefix = "http://+:80/") : BackgroundService
{
    public const string UpstreamClientName = "game-cache-upstream";
    private const string FirewallRuleName = "AFK4.NET Agent (game cache)";

    /// <summary>Кусок Steam — около мегабайта, Epic — тоже; больше этого в кеш не кладём, просто отдаём.</summary>
    private const long MaxCachedObjectBytes = 64L * 1024 * 1024;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> fetches = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim enabledChanged = new(0, 1);
    private volatile bool enabled;
    private string? directory;
    private long storedBytes;
    private long quotaBytes;

    /// <summary>Кеш поднят и слушает сеть клуба. Уходит в сердцебиение: сервер называет этот ПК кешем филиала.</summary>
    public bool Serving { get; private set; }

    public long StoredBytes => Interlocked.Read(ref storedBytes);

    /// <summary>Из сердцебиения: этот ПК — кеш филиала (ПК администратора, кеш в Панели не выключен).</summary>
    public void Configure(bool host)
    {
        if (host == enabled) return;
        enabled = host;
        if (enabledChanged.CurrentCount == 0) enabledChanged.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (enabled) await ServeAsync(stoppingToken);
                else await enabledChanged.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ServeAsync(CancellationToken stoppingToken)
    {
        directory = cacheRoot ?? ChooseDirectory();
        Directory.CreateDirectory(directory);
        storedBytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".part", StringComparison.Ordinal))
            .Sum(path => new FileInfo(path).Length);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
        quotaBytes = GameCacheStore.QuotaBytes(drive.TotalSize, drive.AvailableFreeSpace, StoredBytes);

        if (OperatingSystem.IsWindows() && cacheRoot is null) EnsureFirewallRule();
        using var listener = new HttpListener();
        listener.Prefixes.Add(listenPrefix);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            // Порт 80 занят чужой программой: кеша не будет, игровые ПК качают как раньше.
            logger.LogWarning("Game cache did not start on {Prefix}: {Reason}. Retrying in 5 minutes.", listenPrefix, exception.Message);
            Serving = false;
            await enabledChanged.WaitAsync(TimeSpan.FromMinutes(5), stoppingToken);
            return;
        }

        Serving = true;
        logger.LogInformation("Game cache serves from {Directory}: {Stored} of {Quota} bytes.", directory, StoredBytes, quotaBytes);
        using var stopToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var stop = stopToken.Token.Register(listener.Stop);
        _ = Task.Run(async () =>
        {
            while (enabled && !stopToken.IsCancellationRequested)
            {
                await enabledChanged.WaitAsync(stopToken.Token);
            }

            await stopToken.CancelAsync();
        }, CancellationToken.None);

        while (!stopToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (stopToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException exception)
            {
                logger.LogWarning("Game cache listener failed: {Reason}", exception.Message);
                continue;
            }

            _ = HandleAsync(context, stopToken.Token);
        }

        Serving = false;
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            if (request.Url?.AbsolutePath == GameCacheNames.HeartbeatPath)
            {
                response.AddHeader(GameCacheNames.HeartbeatHeader, Environment.MachineName);
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            var host = request.UserHostName?.Split(':')[0] ?? string.Empty;
            if (request.HttpMethod != "GET"
                || !GameCacheStore.IsAllowedHost(host)
                || request.RemoteEndPoint is not { } remote
                || !(IPAddress.IsLoopback(remote.Address) || GameCacheNames.IsValidAddress(remote.Address.MapToIPv4().ToString())))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var key = GameCacheStore.Key(request.UserAgent, host, request.Url!.AbsolutePath);
            var path = GameCacheStore.PathFor(directory!, key);
            if (request.Headers["Range"] is null && await TryServeFromDiskAsync(path, response, cancellationToken))
            {
                return;
            }

            await FetchAsync(request, response, host, key, path, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or HttpListenerException or OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested) logger.LogDebug(exception, "Game cache request failed.");
            try { response.StatusCode = (int)HttpStatusCode.BadGateway; } catch (InvalidOperationException) { }
        }
        finally
        {
            try { response.Close(); } catch (Exception closing) when (closing is HttpListenerException or ObjectDisposedException) { }
        }
    }

    private static async Task<bool> TryServeFromDiskAsync(string path, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }

        await using (file)
        {
            // Отметка «брали недавно»: вытеснение уберёт то, что давно никто не качал.
            try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); } catch (IOException) { }
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/octet-stream";
            response.ContentLength64 = file.Length;
            response.AddHeader("X-Cache-Status", "HIT");
            await file.CopyToAsync(response.OutputStream, cancellationToken);
        }

        return true;
    }

    private async Task FetchAsync(HttpListenerRequest request, HttpListenerResponse response, string host, string key, string path, CancellationToken cancellationToken)
    {
        var cacheable = request.Headers["Range"] is null;
        // Один кусок качается из интернета один раз, даже если его разом попросили пять ПК.
        var gate = fetches.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (cacheable) await gate.WaitAsync(cancellationToken);
        try
        {
            if (cacheable && await TryServeFromDiskAsync(path, response, cancellationToken)) return;

            using var upstream = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{request.Url!.PathAndQuery}");
            if (request.UserAgent is { } userAgent) upstream.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            if (request.Headers["Range"] is { } range) upstream.Headers.TryAddWithoutValidation("Range", range);
            var client = httpClientFactory.CreateClient(UpstreamClientName);
            using var answer = await client.SendAsync(upstream, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.StatusCode = (int)answer.StatusCode;
            response.ContentType = answer.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var length = answer.Content.Headers.ContentLength;
            if (length is { } known) response.ContentLength64 = known;
            response.AddHeader("X-Cache-Status", "MISS");

            await using var body = await answer.Content.ReadAsStreamAsync(cancellationToken);
            if (!cacheable || answer.StatusCode != HttpStatusCode.OK || length is not ({ } size and > 0 and <= MaxCachedObjectBytes))
            {
                await body.CopyToAsync(response.OutputStream, cancellationToken);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var part = path + ".part";
            await using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            // Кладём в кеш только целый кусок: оборванный в кеше отдавался бы всем ПК битым.
            if (new FileInfo(part).Length == size)
            {
                File.Move(part, path, overwrite: true);
                if (Interlocked.Add(ref storedBytes, size) > quotaBytes) Evict();
            }
            else
            {
                File.Delete(part);
            }
        }
        finally
        {
            if (cacheable)
            {
                gate.Release();
                fetches.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
            }
        }
    }

    private readonly object evicting = new();

    /// <summary>Место кончилось — уходит то, что дольше всех никто не качал, пока кеш не опустится до 90 % квоты.</summary>
    private void Evict()
    {
        if (!Monitor.TryEnter(evicting)) return;
        try
        {
            var files = new DirectoryInfo(directory!).EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(file => file.Extension != ".part")
                .OrderBy(file => file.LastWriteTimeUtc);
            foreach (var file in files)
            {
                if (Interlocked.Read(ref storedBytes) <= quotaBytes * 9 / 10) break;
                var length = file.Length;
                try
                {
                    file.Delete();
                    Interlocked.Add(ref storedBytes, -length);
                }
                catch (IOException)
                {
                    // Файл сейчас отдаётся — уйдёт в следующий раз.
                }
            }
        }
        finally
        {
            Monitor.Exit(evicting);
        }
    }

    // Кеш — на диске, где больше всего свободного места: системный SSD ПК администратора часто мал.
    private static string ChooseDirectory()
    {
        var drive = DriveInfo.GetDrives()
            .Where(candidate => candidate.DriveType == DriveType.Fixed && candidate.IsReady)
            .OrderByDescending(candidate => candidate.AvailableFreeSpace)
            .First();
        return Path.Combine(drive.RootDirectory.FullName, "AFK4GameCache");
    }

    private void EnsureFirewallRule()
    {
        Netsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");
        if (!Netsh($"advfirewall firewall add rule name=\"{FirewallRuleName}\" dir=in action=allow protocol=TCP localport=80 profile=private,domain remoteip=localsubnet"))
        {
            logger.LogWarning("Could not open TCP 80 for the game cache; club PCs may not reach it.");
        }
    }

    private static bool Netsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", arguments) { CreateNoWindow = true, UseShellExecute = false });
            return process is not null && process.WaitForExit(10_000) && process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

/// <summary>Правила кеша без сети и диска — ключ, допустимые имена, квота.</summary>
public static class GameCacheStore
{
    private const long GiB = 1024L * 1024 * 1024;

    /// <summary>
    /// Ключ — как у LANCache (cache_key «$cacheidentifier$uri»): клиент Steam качает одни и те же куски с
    /// разных серверов CDN, поэтому у него ключ — «steam» и путь; у остальных — имя и путь. Строка запроса
    /// в ключ не входит: в ней у CDN подписи, а содержимое куска то же.
    /// </summary>
    public static string Key(string? userAgent, string host, string path) =>
        (userAgent?.StartsWith("Valve/Steam HTTP Client 1.0", StringComparison.Ordinal) == true ? "steam" : host.ToLowerInvariant()) + path;

    public static string PathFor(string directory, string key)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(directory, hash[..2], hash);
    }

    /// <summary>Только Steam и Epic: иначе ПК администратора стал бы открытым прокси для всей сети клуба.</summary>
    public static bool IsAllowedHost(string host) =>
        host.EndsWith(".steamcontent.com", StringComparison.OrdinalIgnoreCase)
        || GameCacheNames.HostsEntries.Contains(host.ToLowerInvariant());

    /// <summary>
    /// Сколько кеш может занять: не больше половины места, что у него есть (своё + свободное), и всегда
    /// оставляя запас — 30 ГБ или 15 % диска, что больше: ПК администратора должен работать и дальше.
    /// </summary>
    public static long QuotaBytes(long driveTotalBytes, long driveFreeBytes, long cacheBytes)
    {
        var reserve = Math.Max(30 * GiB, driveTotalBytes * 15 / 100);
        var available = cacheBytes + driveFreeBytes;
        return Math.Max(0, Math.Min(available - reserve, available / 2));
    }
}
