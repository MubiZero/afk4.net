using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;
using AFK4.Shared.Contracts.Showcase;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Showcase;

/// <summary>Карточки витрины для экрана — с адресами картинок в кэше ПК.</summary>
public interface IShowcaseSource
{
    IReadOnlyList<ShowcaseCardDto> Cards();
}

public interface IShowcaseSync
{
    /// <summary>Спросить сервер, если подошёл срок; иначе ничего не делать.</summary>
    Task SyncIfDueAsync(CancellationToken cancellationToken);
}

/// <summary>Ответ сервера: <c>Showcase</c> пуст — с прошлого раза ничего не изменилось (304).</summary>
public sealed record ShowcaseFetch(DeviceShowcaseDto? Showcase, string? ETag);

public interface IShowcaseClient
{
    Task<ShowcaseFetch> GetAsync(string? etag, CancellationToken cancellationToken);
}

public sealed record StoredShowcase(string? ETag, IReadOnlyList<ShowcaseCardDto> Cards);

public interface IShowcaseStore
{
    StoredShowcase? Load();

    void Save(StoredShowcase showcase);
}

public interface IShowcaseImageCache
{
    /// <summary>Адрес картинки в кэше ПК или null — её нет (не скачалась или адрес не https).</summary>
    string? PageUri(string? remoteUrl);

    Task RefreshAsync(IReadOnlyList<ShowcaseCardDto> cards, CancellationToken cancellationToken);
}

/// <summary>
/// Витрина свободного ПК (спека оболочки, §5.7). Сервер спрашивается раз в 10 минут с ETag
/// прошлого ответа: автокарточки (турнир, хит бара) меняются без правок в Панели, и счётчик
/// версий в сердцебиении их бы не увидел. Без сети экран крутит то, что уже лежит на диске.
/// </summary>
public sealed class ShowcaseService(
    IShowcaseStore store,
    IShowcaseClient client,
    IShowcaseImageCache images,
    IShellStateSignal shellStateSignal,
    TimeProvider timeProvider,
    ILogger<ShowcaseService> logger) : IShowcaseSource, IShowcaseSync
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private StoredShowcase? stored;
    private bool loaded;
    private IReadOnlyList<ShowcaseCardDto>? forPage;
    private DateTimeOffset nextFetchAt = DateTimeOffset.MinValue;

    private StoredShowcase? Stored
    {
        get
        {
            lock (sync)
            {
                if (!loaded)
                {
                    stored = store.Load();
                    loaded = true;
                }

                return stored;
            }
        }
    }

    public IReadOnlyList<ShowcaseCardDto> Cards()
    {
        // Состояние экрану уходит каждые 5 секунд; искать файлы на диске — только после обновления.
        lock (sync)
        {
            if (forPage is not null)
            {
                return forPage;
            }
        }

        var cards = (Stored?.Cards ?? [])
            .Select(card => card with { ImageUrl = images.PageUri(card.ImageUrl) })
            .ToList();
        lock (sync)
        {
            forPage = cards;
        }

        return cards;
    }

    public async Task SyncIfDueAsync(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() < nextFetchAt)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var fetch = await client.GetAsync(Stored?.ETag, cancellationToken);
            if (fetch.Showcase is { } showcase)
            {
                var next = new StoredShowcase(fetch.ETag, showcase.Cards);
                store.Save(next);
                lock (sync)
                {
                    stored = next;
                    loaded = true;
                }

                logger.LogInformation("Showcase updated: {Count} card(s).", showcase.Cards.Count);
            }

            // Картинки — и после 304: не скачавшаяся в прошлый раз докачивается при следующем
            // круге, а уже лежащие на диске повторно не качаются.
            await images.RefreshAsync(Stored?.Cards ?? [], cancellationToken);
            lock (sync)
            {
                forPage = null;
            }

            shellStateSignal.Notify();
            nextFetchAt = timeProvider.GetUtcNow() + RefreshEvery;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or IOException
                                          && !cancellationToken.IsCancellationRequested)
        {
            nextFetchAt = timeProvider.GetUtcNow() + RetryAfterFailure;
            logger.LogWarning(exception, "The showcase could not be fetched; keeping {Count} cached card(s).", Stored?.Cards.Count ?? 0);
        }
        finally
        {
            gate.Release();
        }
    }
}

public sealed class FileShowcaseStore(IOptions<AgentOptions> options) : IShowcaseStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // В папке агента, а не в папке витрины: ту хост отдаёт странице, а список карточек странице
    // приходит состоянием.
    private string FilePath => Path.Combine(options.Value.StateDirectory, "showcase.json");

    public StoredShowcase? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<StoredShowcase>(File.ReadAllText(FilePath), Json) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Битый файл — не беда: без ETag сервер отдаст витрину целиком.
            return null;
        }
    }

    public void Save(StoredShowcase showcase)
    {
        Directory.CreateDirectory(options.Value.StateDirectory);
        var temporary = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(showcase, Json));
            File.Copy(temporary, FilePath, overwrite: true);
        }
        finally
        {
            CachedImages.TryDelete(temporary);
        }
    }
}

public sealed class HttpShowcaseClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IShowcaseClient
{
    public async Task<ShowcaseFetch> GetAsync(string? etag, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            ShowcaseRoutes.Device(agentOptions.DeviceId, agentOptions.OrganizationId, agentOptions.BranchId));
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        if (!string.IsNullOrWhiteSpace(etag))
        {
            message.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new ShowcaseFetch(null, etag);
        }

        response.EnsureSuccessStatusCode();
        var showcase = await response.Content.ReadFromJsonAsync<DeviceShowcaseDto>(cancellationToken)
            ?? throw new JsonException("The platform returned an empty showcase.");
        return new ShowcaseFetch(showcase, response.Headers.ETag?.ToString());
    }
}

/// <summary>Картинки витрины в папке ПК. Экран без сети показывает то, что уже здесь.</summary>
public sealed class FileShowcaseImageCache(
    IHttpClientFactory httpClientFactory,
    ILogger<FileShowcaseImageCache> logger,
    string? directory = null) : IShowcaseImageCache
{
    /// <summary>Картинка на весь экран: больше, чем обложке, но не фильм.</summary>
    public const long MaxBytes = 8 * 1024 * 1024;

    private readonly string folder = Path.Combine(directory ?? ShellShowcaseAssets.Directory(), ShellShowcaseAssets.CardsFolder);

    public string? PageUri(string? remoteUrl)
    {
        if (CachedImages.Parse(remoteUrl) is not { } uri)
        {
            return null;
        }

        var name = FileName(uri);
        return File.Exists(Path.Combine(folder, name)) ? CachedImages.PageUri(ShellShowcaseAssets.CardsFolder, name) : null;
    }

    public async Task RefreshAsync(IReadOnlyList<ShowcaseCardDto> cards, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            if (CachedImages.Parse(card.ImageUrl) is not { } uri)
            {
                continue;
            }

            var name = FileName(uri);
            wanted.Add(name);
            var path = Path.Combine(folder, name);
            if (File.Exists(path))
            {
                continue;
            }

            try
            {
                await CachedImages.DownloadAsync(httpClientFactory.CreateClient("covers"), uri, path, MaxBytes, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
            {
                // Нет картинки — карточка показывается текстом на цвете клуба.
                logger.LogWarning(exception, "Showcase image for {CardId} could not be downloaded.", card.CardId);
            }
        }

        CachedImages.Prune(folder, wanted);
    }

    private static string FileName(Uri uri) => $"{CachedImages.Fingerprint(uri)}{CachedImages.Extension(uri)}";
}
