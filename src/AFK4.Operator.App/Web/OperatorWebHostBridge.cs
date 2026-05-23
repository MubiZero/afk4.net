using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Connection;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Web;

public sealed class OperatorWebHostBridge(
    IOperatorAuthApiClient authApiClient,
    IOperatorTokenStore tokenStore,
    IOperatorConnectionStore connectionStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string?> HandleAsync(string webMessageJson, CancellationToken cancellationToken)
    {
        OperatorWebBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<OperatorWebBridgeRequest>(webMessageJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Type) ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            !(request.Type.StartsWith("auth:", StringComparison.Ordinal)
              || request.Type.StartsWith("connection:", StringComparison.Ordinal)))
        {
            return null;
        }

        var errorCode = request.Type.StartsWith("connection:", StringComparison.Ordinal)
            ? "connection_failed"
            : "auth_failed";

        try
        {
            var payload = request.Type switch
            {
                "auth:loadToken" => await LoadTokenAsync(cancellationToken),
                "auth:signIn" => await SignInAsync(request.Payload, cancellationToken),
                "auth:refresh" => await RefreshAsync(cancellationToken),
                "auth:signOut" => await SignOutAsync(cancellationToken),
                "connection:loadConnection" => await LoadConnectionAsync(cancellationToken),
                "connection:saveConnection" => await SaveConnectionAsync(request.Payload, cancellationToken),
                "connection:clearConnection" => await ClearConnectionAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported host bridge request: {request.Type}.")
            };

            return CreateResponse(request.RequestId, ok: true, payload, error: null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException)
        {
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new OperatorWebBridgeError(errorCode, exception.Message));
        }
    }

    private async Task<OperatorWebAuthSession?> LoadTokenAsync(CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        return snapshot is null ? null : CreateSession(snapshot);
    }

    private async Task<OperatorWebAuthSession> SignInAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var signIn = DeserializePayload<OperatorWebSignInPayload>(payload);
        if (!Guid.TryParse(signIn.OrganizationId, out var organizationId))
        {
            throw new InvalidOperationException("OrganizationId must be a valid GUID.");
        }

        if (string.IsNullOrWhiteSpace(signIn.UserName))
        {
            throw new InvalidOperationException("User name is required.");
        }

        if (string.IsNullOrWhiteSpace(signIn.Password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        var response = await authApiClient.SignInAsync(
            organizationId,
            signIn.UserName.Trim(),
            signIn.Password,
            cancellationToken);

        return CreateSession(response);
    }

    private async Task<OperatorWebAuthSession> RefreshAsync(CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Operator refresh token is missing.");
        }

        var response = await authApiClient.RefreshAsync(snapshot.RefreshToken, cancellationToken);
        return CreateSession(response);
    }

    private async Task<object> SignOutAsync(CancellationToken cancellationToken)
    {
        await tokenStore.ClearAsync(cancellationToken);
        return new { signedOut = true };
    }

    private async Task<OperatorWebStoredConnection?> LoadConnectionAsync(CancellationToken cancellationToken)
    {
        var snapshot = await connectionStore.LoadAsync(cancellationToken);
        return snapshot is null ? null : CreateStoredConnection(snapshot);
    }

    private async Task<OperatorWebStoredConnection> SaveConnectionAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OperatorWebStoredConnectionPayload>(payload);
        if (!Guid.TryParse(request.OrganizationId, out var organizationId) || organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("OrganizationId must be a valid GUID.");
        }

        if (!Guid.TryParse(request.BranchId, out var branchId) || branchId == Guid.Empty)
        {
            throw new InvalidOperationException("BranchId must be a valid GUID.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizationSlug)
            || string.IsNullOrWhiteSpace(request.OrganizationName)
            || string.IsNullOrWhiteSpace(request.BranchSlug)
            || string.IsNullOrWhiteSpace(request.BranchName))
        {
            throw new InvalidOperationException("OrganizationSlug, OrganizationName, BranchSlug, and BranchName are required.");
        }

        var storedAtUtc = request.StoredAtUtc ?? DateTimeOffset.UtcNow;

        var snapshot = new OperatorConnectionSnapshot(
            organizationId,
            request.OrganizationSlug.Trim(),
            request.OrganizationName.Trim(),
            branchId,
            request.BranchSlug.Trim(),
            request.BranchName.Trim(),
            (request.BranchCity ?? string.Empty).Trim(),
            storedAtUtc);

        await connectionStore.SaveAsync(snapshot, cancellationToken);
        return CreateStoredConnection(snapshot);
    }

    private async Task<object> ClearConnectionAsync(CancellationToken cancellationToken)
    {
        await connectionStore.ClearAsync(cancellationToken);
        return new { cleared = true };
    }

    private static OperatorWebStoredConnection CreateStoredConnection(OperatorConnectionSnapshot snapshot)
    {
        return new OperatorWebStoredConnection(
            snapshot.OrganizationId,
            snapshot.OrganizationSlug,
            snapshot.OrganizationName,
            snapshot.BranchId,
            snapshot.BranchSlug,
            snapshot.BranchName,
            snapshot.BranchCity,
            snapshot.StoredAtUtc);
    }

    private static T DeserializePayload<T>(JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException("Host bridge payload is required.");
        }

        return payload.Deserialize<T>(JsonOptions)
            ?? throw new InvalidOperationException("Host bridge payload is invalid.");
    }

    private static OperatorWebAuthSession CreateSession(StaffSignInResponse response)
    {
        return new OperatorWebAuthSession(
            response.StaffUserId,
            response.OrganizationId,
            response.DisplayName,
            response.AccessToken,
            response.AccessTokenExpiresAtUtc,
            response.RefreshTokenExpiresAtUtc,
            response.BranchIds,
            response.BranchIds.FirstOrDefault() == Guid.Empty ? null : response.BranchIds.FirstOrDefault(),
            response.Permissions);
    }

    private static OperatorWebAuthSession CreateSession(OperatorTokenSnapshot snapshot)
    {
        return new OperatorWebAuthSession(
            snapshot.StaffUserId,
            snapshot.OrganizationId,
            snapshot.DisplayName,
            snapshot.AccessToken,
            snapshot.AccessTokenExpiresAtUtc,
            snapshot.RefreshTokenExpiresAtUtc,
            snapshot.BranchIds,
            snapshot.BranchIds.FirstOrDefault() == Guid.Empty ? null : snapshot.BranchIds.FirstOrDefault(),
            snapshot.Permissions);
    }

    private static string CreateResponse(
        string requestId,
        bool ok,
        object? payload,
        OperatorWebBridgeError? error)
    {
        return JsonSerializer.Serialize(
            new OperatorWebBridgeResponse("host:response", requestId, ok, payload, error),
            JsonOptions);
    }

    private sealed record OperatorWebBridgeRequest(
        string? Type,
        string? RequestId,
        JsonElement Payload);

    private sealed record OperatorWebBridgeResponse(
        string Type,
        string RequestId,
        bool Ok,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        object? Payload,
        OperatorWebBridgeError? Error);

    private sealed record OperatorWebSignInPayload(
        string? OrganizationId,
        string? UserName,
        string? Password);

    private sealed record OperatorWebBridgeError(
        string Code,
        string Message);

    private sealed record OperatorWebAuthSession(
        Guid StaffUserId,
        Guid OrganizationId,
        string DisplayName,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        DateTimeOffset RefreshTokenExpiresAtUtc,
        IReadOnlyList<Guid> BranchIds,
        Guid? ActiveBranchId,
        IReadOnlyList<string> Permissions);

    private sealed record OperatorWebStoredConnectionPayload(
        string? OrganizationId,
        string? OrganizationSlug,
        string? OrganizationName,
        string? BranchId,
        string? BranchSlug,
        string? BranchName,
        string? BranchCity,
        DateTimeOffset? StoredAtUtc);

    private sealed record OperatorWebStoredConnection(
        Guid OrganizationId,
        string OrganizationSlug,
        string OrganizationName,
        Guid BranchId,
        string BranchSlug,
        string BranchName,
        string BranchCity,
        DateTimeOffset StoredAtUtc);
}
