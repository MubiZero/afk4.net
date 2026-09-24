using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

/// <summary>Вход вышел — токены; не вышел — код отказа сервера.</summary>
public sealed record PlayerSignInOutcome(PlatformPersonSessionResponse? Session, string? ErrorCode)
{
    public static PlayerSignInOutcome Refused(string errorCode) => new(null, errorCode);
}

/// <summary>Вход игрока на сервере ключом этого ПК: токены, которые он выдаёт, привязаны к машине.</summary>
public interface IPlayerSignInClient
{
    Task<PlayerSignInOutcome> SignInWithPinAsync(string phone, string pin, CancellationToken cancellationToken);

    Task<PlayerSignInOutcome> RedeemClaimAsync(Guid claimId, CancellationToken cancellationToken);
}

public sealed class HttpPlayerSignInClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IPlayerSignInClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<PlayerSignInOutcome> SignInWithPinAsync(string phone, string pin, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        return SendAsync(
            $"/api/devices/{agentOptions.DeviceId:D}/player-sign-in",
            new DevicePlayerSignInRequest(agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId, phone, pin),
            cancellationToken);
    }

    public Task<PlayerSignInOutcome> RedeemClaimAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        return SendAsync(
            $"/api/devices/{agentOptions.DeviceId:D}/sign-in-claims/{claimId:D}/redeem",
            new DeviceRedeemSignInClaimRequest(agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId),
            cancellationToken);
    }

    private async Task<PlayerSignInOutcome> SendAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = options.Value.PlatformBaseUrl;

        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        // Ключ берётся из хранилища, а не из конфига: после смены в конфиге лежит старый.
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>(JsonOptions, cancellationToken);
            return session is null
                ? throw new HttpRequestException("The platform answered a sign-in without a session.")
                : new PlayerSignInOutcome(session, null);
        }

        // Отказ по правилу приходит с кодом; без кода — это не ответ на вход, а сбой пути к серверу
        // (например, ключ ПК не принят), и игроку его надо показать как «нет связи с клубом».
        var errorCode = await ReadErrorCodeAsync(response, cancellationToken);
        return errorCode is null
            ? throw new HttpRequestException($"Sign-in failed with {(int)response.StatusCode}.", null, response.StatusCode)
            : PlayerSignInOutcome.Refused(errorCode);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Conflict
            or HttpStatusCode.TooManyRequests or HttpStatusCode.NotFound))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
