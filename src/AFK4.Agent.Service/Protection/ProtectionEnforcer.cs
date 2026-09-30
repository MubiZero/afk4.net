using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Protection;

/// <summary>
/// Профиль защиты на ПК (спека оболочки, §6.3): держит последний полученный профиль, применяет его
/// вне обслуживания и снимает в обслуживании, докладывает серверу, что вышло по каждому пункту.
/// </summary>
public interface IProtectionEnforcer
{
    /// <summary>Применить сохранённый профиль: при старте службы и по возвращении из обслуживания.</summary>
    Task ApplyAsync(CancellationToken cancellationToken);

    /// <summary>Снять всё на время обслуживания: технику нужна обычная Windows (§6.5).</summary>
    Task ReleaseAsync(CancellationToken cancellationToken);

    /// <summary>Сердцебиение назвало версию: другая — перечитать профиль и применить.</summary>
    Task SyncAsync(int serverVersion, CancellationToken cancellationToken);

    /// <summary>Команда «перечитать профиль» из Панели.</summary>
    Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Правила закрытия окон из текущего профиля — их исполняет хост в сессии игрока.</summary>
    IReadOnlyList<BlockedWindowRuleDto> BlockedWindows { get; }

    /// <summary>Что стереть после сессии (SessionTraceNames) — по последнему полученному профилю.</summary>
    IReadOnlyList<string> ClearAfterSession { get; }

    /// <summary>Последний полученный профиль целиком: простой, правила клуба и остальное.</summary>
    ProtectionProfileDto Profile { get; }
}

public interface IProtectionPlatformClient
{
    Task<ProtectionProfileDto> GetProfileAsync(CancellationToken cancellationToken);

    Task ReportAsync(DeviceProtectionReportRequest report, CancellationToken cancellationToken);
}

public interface IProtectionProfileStore
{
    ProtectionProfileDto? Load();

    void Save(ProtectionProfileDto profile);
}

public sealed class ProtectionEnforcer(
    IMachineRegistry registry,
    IProtectionProfileStore store,
    IProtectionPlatformClient platform,
    IAgentRuntimeStateStore runtimeState,
    IOptions<AgentOptions> options,
    TimeProvider timeProvider,
    ILogger<ProtectionEnforcer> logger) : IProtectionEnforcer
{
    /// <summary>Сервер не отдал профиль — не долбить его каждые десять секунд.</summary>
    private static readonly TimeSpan FetchRetryDelay = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private ProtectionProfileDto? current;
    private bool applied;
    private DeviceProtectionReportRequest? unsentReport;
    private DateTimeOffset retryFetchAfter = DateTimeOffset.MinValue;

    // Профиля ещё нет — умолчания и стирание всего: следующий игрок не должен войти в чужой Steam
    // потому, что агент не успел спросить сервер. Сохранённая версия 0 — тоже «нет»: прежние
    // агенты писали в неё пустой набор, а умолчания с тех пор другие.
    private ProtectionProfileDto Current => current ??= store.Load() is { Version: > 0 } stored ? stored : ProtectionProfileDefaults.Initial;

    private bool InMaintenance => runtimeState.Current.State == PlayerShellStateNames.Maintenance;

    public IReadOnlyList<BlockedWindowRuleDto> BlockedWindows => Current.BlockedWindows;

    // Профиль, сохранённый прежней версией агента, списка не знает: для него — всё.
    public IReadOnlyList<string> ClearAfterSession => Current.ClearAfterSession ?? SessionTraceNames.All;

    public ProtectionProfileDto Profile => Current;

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (InMaintenance)
            {
                return;
            }

            await EnforceAsync(Current, release: false, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnforceAsync(Current, release: true, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SyncAsync(int serverVersion, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (unsentReport is not null)
            {
                await TryReportAsync(unsentReport, cancellationToken);
            }

            if (serverVersion == Current.Version && applied)
            {
                return;
            }

            if (serverVersion != Current.Version)
            {
                if (timeProvider.GetUtcNow() < retryFetchAfter || await TryFetchAsync(cancellationToken) is null)
                {
                    return;
                }
            }

            if (!InMaintenance)
            {
                await EnforceAsync(Current, release: false, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (await TryFetchAsync(cancellationToken) is null)
            {
                return SessionEnforcementResult.Rejected(
                    "The protection profile could not be fetched; the PC keeps the previous one.",
                    DeviceCommandOutcomeNames.ProtectionUnavailable);
            }

            if (InMaintenance)
            {
                return SessionEnforcementResult.Accepted(
                    $"Protection profile v{Current.Version} saved; it applies when the PC is back on the floor.",
                    DeviceCommandOutcomeNames.ProtectionApplied);
            }

            var items = await EnforceAsync(Current, release: false, cancellationToken);
            return SessionEnforcementResult.Accepted(
                $"Protection profile v{Current.Version}: {Describe(items)}.",
                DeviceCommandOutcomeNames.ProtectionApplied);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ProtectionProfileDto?> TryFetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await platform.GetProfileAsync(cancellationToken);
            store.Save(profile);
            current = profile;
            applied = false;
            return profile;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or IOException
                                          && !cancellationToken.IsCancellationRequested)
        {
            retryFetchAfter = timeProvider.GetUtcNow() + FetchRetryDelay;
            logger.LogWarning(exception, "The protection profile could not be fetched; keeping v{Version}.", Current.Version);
            return null;
        }
    }

    private async Task<IReadOnlyList<ProtectionItemReportDto>> EnforceAsync(
        ProtectionProfileDto profile, bool release, CancellationToken cancellationToken)
    {
        var items = new List<ProtectionItemReportDto>();
        var deferred = false;
        var plan = ProtectionPolicy.Plan(profile);
        if (registry.IsSupported)
        {
            RemoveLegacyMachineWrites(plan);
        }

        foreach (var item in plan)
        {
            var playerBound = item.Writes.Any(write => write.Scope == PolicyScope.Player);
            ProtectionItemReportDto? report;
            if (!registry.IsSupported)
            {
                report = item.Enabled ? new ProtectionItemReportDto(item.Name, ProtectionItemStatusNames.Unsupported, "Machine policies need Windows.") : null;
            }
            else if (playerBound && !registry.HasPlayerAccount)
            {
                // Киоска нет (не ставили или сняли): запрещать некому, а машинное от прежнего применения
                // не должно остаться на учётке администратора.
                Remove(item with { Writes = MachineWrites(item) }, release: false);
                report = item.Enabled
                    ? new ProtectionItemReportDto(item.Name, ProtectionItemStatusNames.Unsupported, "This PC has no player account: the kiosk is not installed.")
                    : null;
            }
            else
            {
                // Игрок ещё не вошёл (ПК только включился): в его куст не пишем, машинное — сразу;
                // остальное доедет со следующим сердцебиением.
                var waiting = playerBound && !registry.PlayerSignedIn;
                deferred |= waiting;
                var part = waiting ? item with { Writes = MachineWrites(item) } : item;
                report = release || !item.Enabled
                    ? Remove(part, release)
                    : Write(part);
            }

            if (report is not null)
            {
                items.Add(report);
            }
        }

        applied = !release && !deferred;
        // Отчёт о применении ждёт входа игрока: неполный отчёт был бы враньём. Снятие на обслуживание
        // ждать нельзя — технику нужна Windows сейчас.
        if (deferred && !release)
        {
            logger.LogInformation("Protection profile v{Version}: the player has not signed in yet; their policies wait.", profile.Version);
            return items;
        }

        var agentOptions = options.Value;
        await TryReportAsync(
            new DeviceProtectionReportRequest(
                agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId,
                profile.Version, timeProvider.GetUtcNow(), items),
            cancellationToken);
        logger.LogInformation("Protection profile v{Version} {Action}: {Items}.", profile.Version, release ? "released" : "applied", Describe(items));
        return items;
    }

    // Прежние версии агента писали игроцкие запреты в HKLM, то есть на всех: после обновления они
    // остались бы и на учётке администратора. Снимаем каждый раз — это одно чтение ключа на значение.
    private void RemoveLegacyMachineWrites(IReadOnlyList<ProtectionItem> plan)
    {
        foreach (var write in plan.SelectMany(item => item.Writes).Where(write => write.Scope == PolicyScope.Player))
        {
            try
            {
                registry.Remove(write with { Scope = PolicyScope.Machine });
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or PlatformNotSupportedException)
            {
                logger.LogWarning(exception, "The machine-wide copy of {Policy} could not be removed.", write.Name);
            }
        }
    }

    private static IReadOnlyList<RegistryWrite> MachineWrites(ProtectionItem item) =>
        item.Writes.Where(write => write.Scope == PolicyScope.Machine).ToList();

    private ProtectionItemReportDto Write(ProtectionItem item)
    {
        try
        {
            foreach (var write in item.Writes)
            {
                registry.Write(write);
            }

            return new ProtectionItemReportDto(item.Name, item.AppliedStatus, null);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or PlatformNotSupportedException)
        {
            logger.LogWarning(exception, "Protection item {Item} could not be applied.", item.Name);
            return new ProtectionItemReportDto(item.Name, ProtectionItemStatusNames.Failed, exception.Message);
        }
    }

    // Выключенный пункт убирает свои значения молча; в отчёт попадает только то, что клуб включил.
    private ProtectionItemReportDto? Remove(ProtectionItem item, bool release)
    {
        try
        {
            foreach (var write in item.Writes)
            {
                registry.Remove(write);
            }

            return release && item.Enabled ? new ProtectionItemReportDto(item.Name, ProtectionItemStatusNames.Released, null) : null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or PlatformNotSupportedException)
        {
            logger.LogWarning(exception, "Protection item {Item} could not be removed.", item.Name);
            return item.Enabled || release ? new ProtectionItemReportDto(item.Name, ProtectionItemStatusNames.Failed, exception.Message) : null;
        }
    }

    private async Task TryReportAsync(DeviceProtectionReportRequest report, CancellationToken cancellationToken)
    {
        try
        {
            await platform.ReportAsync(report, cancellationToken);
            unsentReport = null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            // Отчёт не дошёл — уйдёт со следующим сердцебиением: Панель должна знать правду, а не старую.
            unsentReport = report;
            logger.LogWarning(exception, "The protection report did not reach the platform; it will be retried.");
        }
    }

    private static string Describe(IReadOnlyList<ProtectionItemReportDto> items) =>
        items.Count == 0 ? "nothing enabled" : string.Join(", ", items.Select(item => $"{item.Item}={item.Status}"));
}

public sealed class FileProtectionProfileStore(IOptions<AgentOptions> options) : IProtectionProfileStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string FilePath => Path.Combine(options.Value.StateDirectory, "protection-profile.json");

    public ProtectionProfileDto? Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<ProtectionProfileDto>(File.ReadAllText(FilePath), Json)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Битый файл — не повод остаться без защиты: сервер отдаст профиль по сердцебиению.
            return null;
        }
    }

    public void Save(ProtectionProfileDto profile)
    {
        Directory.CreateDirectory(options.Value.StateDirectory);
        var temporary = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, Json));
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

public sealed class HttpProtectionPlatformClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IProtectionPlatformClient
{
    public async Task<ProtectionProfileDto> GetProfileAsync(CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            DeviceProtectionRoutes.Profile(agentOptions.DeviceId, agentOptions.OrganizationId, agentOptions.BranchId));
        using var response = await SendAsync(message, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProtectionProfileDto>(cancellationToken)
            ?? throw new JsonException("The platform returned an empty protection profile.");
    }

    public async Task ReportAsync(DeviceProtectionReportRequest report, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceProtectionRoutes.Report(options.Value.DeviceId))
        {
            Content = JsonContent.Create(report)
        };
        using var response = await SendAsync(message, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = options.Value.PlatformBaseUrl;
        // Ключ берётся из хранилища, а не из конфига: после смены в конфиге лежит старый.
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        var response = await client.SendAsync(message, cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
