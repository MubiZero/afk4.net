using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Showcase;

/// <summary>
/// Показы рекламы платформы (спека рекламы, §4): суммы по карточке за день, без игрока. Пачка
/// отправляется раз в час; ушедшая, но не подтверждённая пачка уходит повторно с тем же ключом —
/// сервер её не удвоит, а новые показы копятся уже в следующую.
/// </summary>
public interface IShowcaseImpressions
{
    void Record(string cardId, long shownMs);

    Task FlushIfDueAsync(CancellationToken cancellationToken);
}

public sealed record ShowcaseImpressionBatch(string BatchId, IReadOnlyList<ShowcaseImpressionDto> Items);

public sealed record ShowcaseImpressionLog(IReadOnlyList<ShowcaseImpressionDto> Open, ShowcaseImpressionBatch? Pending);

public interface IShowcaseImpressionStore
{
    ShowcaseImpressionLog? Load();

    void Save(ShowcaseImpressionLog log);
}

public interface IShowcaseImpressionClient
{
    Task SendAsync(ShowcaseImpressionBatch batch, CancellationToken cancellationToken);
}

public sealed class ShowcaseImpressions(
    IShowcaseImpressionStore store,
    IShowcaseImpressionClient client,
    TimeProvider timeProvider,
    ILogger<ShowcaseImpressions> logger) : IShowcaseImpressions
{
    public static readonly TimeSpan FlushEvery = TimeSpan.FromHours(1);

    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(5);

    // Карточка, мелькнувшая на долю секунды, — не показ: так бывает, когда человек подошёл к ПК.
    public const long MinimumShownMs = 1000;

    private const string AdPrefix = "ad:";

    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<(string CardId, string Day), (int Impressions, long ShownMs)>? open;
    private ShowcaseImpressionBatch? pending;
    private DateTimeOffset nextFlushAt = DateTimeOffset.MinValue;

    public void Record(string cardId, long shownMs)
    {
        if (!cardId.StartsWith(AdPrefix, StringComparison.Ordinal) || shownMs < MinimumShownMs)
        {
            return;
        }

        var day = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        lock (sync)
        {
            var counts = Open();
            var key = (cardId, day);
            var current = counts.GetValueOrDefault(key);
            counts[key] = (current.Impressions + 1, current.ShownMs + shownMs);
            Persist();
        }
    }

    public async Task FlushIfDueAsync(CancellationToken cancellationToken)
    {
        ShowcaseImpressionBatch? batch;
        lock (sync)
        {
            var counts = Open();
            if (timeProvider.GetUtcNow() < nextFlushAt || (pending is null && counts.Count == 0))
            {
                return;
            }

            if (pending is null)
            {
                var items = counts
                    .OrderBy(entry => entry.Key.Day, StringComparer.Ordinal)
                    .Take(AdLimits.MaxBatchItems)
                    .Select(entry => new ShowcaseImpressionDto(entry.Key.CardId, entry.Key.Day, entry.Value.Impressions, entry.Value.ShownMs))
                    .ToList();
                foreach (var item in items)
                {
                    counts.Remove((item.CardId, item.Day));
                }

                pending = new ShowcaseImpressionBatch(Guid.NewGuid().ToString("N"), items);
                Persist();
            }

            batch = pending;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await client.SendAsync(batch, cancellationToken);
            lock (sync)
            {
                pending = null;
                nextFlushAt = timeProvider.GetUtcNow() + FlushEvery;
                Persist();
            }
        }
        catch (InvalidDataException exception)
        {
            // Сервер пачку отверг: повтор той же пачки упрётся в тот же отказ и запрёт все следующие.
            lock (sync)
            {
                pending = null;
                nextFlushAt = timeProvider.GetUtcNow() + FlushEvery;
                Persist();
            }

            logger.LogWarning(exception, "Showcase impressions batch {BatchId} was refused and dropped.", batch.BatchId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException
                                          && !cancellationToken.IsCancellationRequested)
        {
            lock (sync)
            {
                nextFlushAt = timeProvider.GetUtcNow() + RetryAfterFailure;
            }

            logger.LogWarning(exception, "Showcase impressions batch {BatchId} was not delivered; retrying later.", batch.BatchId);
        }
        finally
        {
            gate.Release();
        }
    }

    private Dictionary<(string CardId, string Day), (int Impressions, long ShownMs)> Open()
    {
        if (open is not null)
        {
            return open;
        }

        var saved = store.Load();
        pending = saved?.Pending;
        open = (saved?.Open ?? [])
            .GroupBy(item => (item.CardId, item.Day))
            .ToDictionary(group => group.Key, group => (group.Sum(item => item.Impressions), group.Sum(item => item.ShownMs)));
        return open;
    }

    private void Persist()
    {
        try
        {
            store.Save(new ShowcaseImpressionLog(
                open!.Select(entry => new ShowcaseImpressionDto(entry.Key.CardId, entry.Key.Day, entry.Value.Impressions, entry.Value.ShownMs)).ToList(),
                pending));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не записалось — счёт живёт в памяти до следующей записи; витрина от этого не страдает.
            logger.LogWarning(exception, "Showcase impressions could not be saved.");
        }
    }
}

public sealed class FileShowcaseImpressionStore(IOptions<AgentOptions> options) : IShowcaseImpressionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string FilePath => Path.Combine(options.Value.StateDirectory, "showcase-impressions.json");

    public ShowcaseImpressionLog? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<ShowcaseImpressionLog>(File.ReadAllText(FilePath), Json) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(ShowcaseImpressionLog log)
    {
        Directory.CreateDirectory(options.Value.StateDirectory);
        var temporary = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(log, Json));
            File.Copy(temporary, FilePath, overwrite: true);
        }
        finally
        {
            AFK4.Agent.Service.Shell.CachedImages.TryDelete(temporary);
        }
    }
}

public sealed class HttpShowcaseImpressionClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IShowcaseImpressionClient
{
    public async Task SendAsync(ShowcaseImpressionBatch batch, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;
        using var message = new HttpRequestMessage(HttpMethod.Post, AdRoutes.DeviceImpressions(agentOptions.DeviceId))
        {
            Content = JsonContent.Create(new DeviceShowcaseImpressionsRequest(
                agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId, batch.BatchId, batch.Items))
        };
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throw new InvalidDataException("The platform refused the impressions batch.");
        }

        response.EnsureSuccessStatusCode();
    }
}
