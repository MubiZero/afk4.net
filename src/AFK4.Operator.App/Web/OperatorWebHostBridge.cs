using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Operator.App.Connection;

namespace AFK4.Operator.App.Web;

// The web UI now signs itself in over plain HTTP (staffAuthApi.ts + sessionStorage) — see
// docs/superpowers/plans/2026-07-22-operator-unified-admin-foundation.md. This bridge is left with
// only device-identity concerns (machine/seat pinning) that must stay native-side.
public sealed class OperatorWebHostBridge(IOperatorConnectionStore connectionStore)
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
            !request.Type.StartsWith("connection:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var payload = request.Type switch
            {
                "connection:loadConnection" => await LoadConnectionAsync(cancellationToken),
                "connection:saveConnection" => await SaveConnectionAsync(request.Payload, cancellationToken),
                "connection:clearConnection" => await ClearConnectionAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported host bridge request: {request.Type}.")
            };

            return CreateResponse(request.RequestId, ok: true, payload, error: null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new OperatorWebBridgeError("connection_failed", exception.Message));
        }
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

    private sealed record OperatorWebBridgeError(
        string Code,
        string Message);

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
