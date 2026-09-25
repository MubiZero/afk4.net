using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Games;

/// <summary>Игра, которую видит и запускает игрок, — уже с командой запуска на этом ПК.</summary>
/// <param name="ExecutablePath">null — лаунчера игры на ПК нет: плитка видна, но недоступна.</param>
public sealed record LauncherEntry(
    string AppId,
    string DisplayName,
    string Category,
    string? ExecutablePath,
    string Arguments,
    bool AllowWithoutSession,
    string? IconUri,
    int? MinAge);

/// <summary>
/// Один список на «что показать игроку» и «что ему разрешено запустить»: два разных списка
/// разошлись бы в первый же день.
/// </summary>
public interface ILauncherCatalog
{
    IReadOnlyList<LauncherEntry> Entries();

    LauncherEntry? Find(string appId);
}

public interface IGameLibrarySync
{
    /// <summary>Сердцебиение назвало версию библиотеки: другая — перечитать и докачать обложки.</summary>
    Task SyncAsync(int serverVersion, CancellationToken cancellationToken);
}

public interface IGameLibraryStore
{
    DeviceGameLibraryDto? Load();

    void Save(DeviceGameLibraryDto library);
}

public interface IGameLibraryClient
{
    Task<DeviceGameLibraryDto> GetAsync(CancellationToken cancellationToken);
}

public interface ICoverCache
{
    /// <summary>Адрес обложки для страницы или null — её нет в кэше.</summary>
    string? CoverUri(string appId);

    Task RefreshAsync(IReadOnlyList<DeviceGameDto> games, CancellationToken cancellationToken);
}

/// <summary>
/// Библиотека игр филиала на ПК (спека оболочки, §6.6). Пока у клуба в Панели пусто, игрок видит
/// прежний список из <c>bootstrap.json</c>: ПК, настроенные вручную до библиотеки, не должны
/// остаться без игр в день обновления.
/// </summary>
public sealed class GameLibraryService(
    IOptions<AgentOptions> options,
    IGameLibraryStore store,
    IGameLibraryClient client,
    ICoverCache covers,
    IGameLauncherLocator locator,
    IShellStateSignal shellStateSignal,
    TimeProvider timeProvider,
    ILogger<GameLibraryService> logger) : ILauncherCatalog, IGameLibrarySync
{
    private static readonly TimeSpan FetchRetryDelay = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private DeviceGameLibraryDto? library;
    private bool loaded;
    private DateTimeOffset retryFetchAfter = DateTimeOffset.MinValue;

    private DeviceGameLibraryDto? Current
    {
        get
        {
            if (!loaded)
            {
                library = store.Load();
                loaded = true;
            }

            return library;
        }
    }

    public IReadOnlyList<LauncherEntry> Entries()
    {
        var server = Current;
        if (server is { Games.Count: > 0 })
        {
            return server.Games.Select(game =>
            {
                var launch = GameLaunchResolver.Resolve(game, locator);
                return new LauncherEntry(
                    game.AppId,
                    game.DisplayName,
                    string.IsNullOrWhiteSpace(game.Genre) ? "Games" : game.Genre,
                    launch?.ExecutablePath,
                    launch?.Arguments ?? string.Empty,
                    game.AvailableWithoutSession,
                    covers.CoverUri(game.AppId),
                    game.MinAge);
            }).ToList();
        }

        return options.Value.LauncherApps
            .Where(app => app.IsEnabled && !string.IsNullOrWhiteSpace(app.AppId) && !string.IsNullOrWhiteSpace(app.ExecutablePath))
            .Select(app => new LauncherEntry(
                app.AppId,
                string.IsNullOrWhiteSpace(app.DisplayName) ? app.AppId : app.DisplayName,
                string.IsNullOrWhiteSpace(app.Category) ? "Games" : app.Category,
                app.ExecutablePath,
                app.Arguments,
                app.AllowWithoutSession,
                IconUri: null,
                MinAge: null))
            .ToList();
    }

    public LauncherEntry? Find(string appId) =>
        Entries().FirstOrDefault(entry => string.Equals(entry.AppId, appId, StringComparison.OrdinalIgnoreCase));

    public async Task SyncAsync(int serverVersion, CancellationToken cancellationToken)
    {
        if (serverVersion == (Current?.Version ?? 0) || timeProvider.GetUtcNow() < retryFetchAfter)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var fetched = await client.GetAsync(cancellationToken);
            store.Save(fetched);
            library = fetched;
            loaded = true;
            // Обложки — после списка: без сети плитки есть сразу, картинки догоняют.
            await covers.RefreshAsync(fetched.Games, cancellationToken);
            shellStateSignal.Notify();
            logger.LogInformation("Game library v{Version}: {Count} game(s).", fetched.Version, fetched.Games.Count);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or IOException
                                          && !cancellationToken.IsCancellationRequested)
        {
            retryFetchAfter = timeProvider.GetUtcNow() + FetchRetryDelay;
            logger.LogWarning(exception, "The game library could not be fetched; keeping v{Version}.", Current?.Version ?? 0);
        }
        finally
        {
            gate.Release();
        }
    }
}

public sealed class FileGameLibraryStore(IOptions<AgentOptions> options) : IGameLibraryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string FilePath => Path.Combine(options.Value.StateDirectory, "game-library.json");

    public DeviceGameLibraryDto? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<DeviceGameLibraryDto>(File.ReadAllText(FilePath), Json) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Битый файл — не беда: сервер отдаст библиотеку по сердцебиению.
            return null;
        }
    }

    public void Save(DeviceGameLibraryDto library)
    {
        Directory.CreateDirectory(options.Value.StateDirectory);
        var temporary = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(library, Json));
            File.Copy(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

public sealed class HttpGameLibraryClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IGameLibraryClient
{
    public async Task<DeviceGameLibraryDto> GetAsync(CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            GameLibraryRoutes.DeviceLibrary(agentOptions.DeviceId, agentOptions.OrganizationId, agentOptions.BranchId));
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceGameLibraryDto>(cancellationToken)
            ?? throw new JsonException("The platform returned an empty game library.");
    }
}

/// <summary>
/// Обложки на диске ПК. Имя файла — игра и отпечаток адреса: сменилась обложка в каталоге — новый
/// файл, и WebView2 не покажет старую из своего кэша.
/// </summary>
public sealed class FileCoverCache(IHttpClientFactory httpClientFactory, ILogger<FileCoverCache> logger, string? directory = null) : ICoverCache
{
    /// <summary>Больше обложке не нужно: это картинка на плитку, а не фон на весь экран.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] Extensions = [".webp", ".png", ".jpg", ".jpeg"];

    private readonly string folder = Path.Combine(directory ?? ShellShowcaseAssets.Directory(), ShellShowcaseAssets.CoversFolder);

    public string? CoverUri(string appId)
    {
        var file = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, $"{appId}.*").FirstOrDefault(candidate => !candidate.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            : null;
        return file is null ? null : $"https://{ShellShowcaseAssets.VirtualHost}/{ShellShowcaseAssets.CoversFolder}/{Path.GetFileName(file)}";
    }

    public async Task RefreshAsync(IReadOnlyList<DeviceGameDto> games, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            if (!Uri.TryCreate(game.CoverUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            var extension = Extensions.FirstOrDefault(candidate => uri.AbsolutePath.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)) ?? ".jpg";
            var name = $"{game.AppId}.{Fingerprint(uri)}{extension}";
            wanted.Add(name);
            var path = Path.Combine(folder, name);
            if (File.Exists(path))
            {
                continue;
            }

            try
            {
                await DownloadAsync(uri, path, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
            {
                // Нет обложки — плитка рисуется по названию; игра от этого не пропадает.
                logger.LogWarning(exception, "Cover for {AppId} could not be downloaded.", game.AppId);
            }
        }

        foreach (var stale in Directory.EnumerateFiles(folder).Where(file => !wanted.Contains(Path.GetFileName(file))).ToList())
        {
            TryDelete(stale);
        }
    }

    private async Task DownloadAsync(Uri uri, string path, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("covers");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidDataException($"{uri} is not an image.");
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new InvalidDataException($"{uri} is larger than {MaxBytes} bytes.");
        }

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(temporary))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > MaxBytes)
                    {
                        throw new InvalidDataException($"{uri} is larger than {MaxBytes} bytes.");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static string Fingerprint(Uri uri) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)))[..12];

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
