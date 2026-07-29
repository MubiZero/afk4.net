using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Updates;

namespace AFK4.OrganizationAdmin.Web;

// Auth runs over plain HTTP (staffAuthApi.ts + sessionStorage). This bridge stays narrow: it owns
// machine-local connection identity and update-safety activity that cannot live in the browser.
public sealed class OrganizationAdminWebHostBridge(
    IOrganizationAdminConnectionStore connectionStore,
    OrganizationAdminActivityState? updateActivityState = null,
    IOrganizationAdminShutdownAcknowledgementStore? shutdownAcknowledgementStore = null,
    Action? requestShutdown = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string?> HandleAsync(string webMessageJson, CancellationToken cancellationToken)
    {
        OrganizationAdminWebBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<OrganizationAdminWebBridgeRequest>(webMessageJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null)
        {
            return null;
        }

        if (request.Type is "update-activity:start" or "update-activity:finish")
        {
            if (updateActivityState is not null &&
                request.OperationId is { Length: > 0 and <= 128 } operationId)
            {
                if (request.Type == "update-activity:start") updateActivityState.StartWebCommand(operationId);
                else updateActivityState.FinishWebCommand(operationId);
            }
            return null;
        }

        if (
            string.IsNullOrWhiteSpace(request.Type) ||
            string.IsNullOrWhiteSpace(request.RequestId))
        {
            return null;
        }

        if (request.Type == "update:restartAndInstall")
        {
            return await RestartAndInstallAsync(request, cancellationToken);
        }

        if (!request.Type.StartsWith("connection:", StringComparison.Ordinal)) return null;

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
                new OrganizationAdminWebBridgeError("connection_failed", exception.Message));
        }
    }

    private async Task<string> RestartAndInstallAsync(
        OrganizationAdminWebBridgeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (updateActivityState?.HasCriticalCommandInFlight == true)
            {
                return CreateResponse(request.RequestId!, false, null,
                    new OrganizationAdminWebBridgeError("update_busy", "A critical operation is still running."));
            }

            if (shutdownAcknowledgementStore is null || requestShutdown is null)
            {
                return CreateResponse(request.RequestId!, false, null,
                    new OrganizationAdminWebBridgeError("update_unavailable", "Local update coordination is unavailable."));
            }

            var payload = DeserializePayload<OrganizationAdminRestartAndInstallPayload>(request.Payload);
            if (!Guid.TryParse(payload.UpdateRolloutId, out var rolloutId) || rolloutId == Guid.Empty ||
                !Guid.TryParse(payload.UpdatePackageId, out var packageId) || packageId == Guid.Empty)
            {
                return CreateResponse(request.RequestId!, false, null,
                    new OrganizationAdminWebBridgeError("update_invalid", "The selected update is invalid."));
            }

            await shutdownAcknowledgementStore.PersistAsync(
                new OrganizationAdminShutdownAcknowledgement(rolloutId, packageId, DateTimeOffset.UtcNow),
                cancellationToken);
            requestShutdown();
            return CreateResponse(request.RequestId!, true, new { accepted = true }, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            return CreateResponse(request.RequestId!, false, null,
                new OrganizationAdminWebBridgeError("update_failed", "The update restart request could not be saved."));
        }
    }

    private async Task<OrganizationAdminWebStoredConnection?> LoadConnectionAsync(CancellationToken cancellationToken)
    {
        var snapshot = await connectionStore.LoadAsync(cancellationToken);
        return snapshot is null ? null : CreateStoredConnection(snapshot);
    }

    private async Task<OrganizationAdminWebStoredConnection> SaveConnectionAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OrganizationAdminWebStoredConnectionPayload>(payload);
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

        var snapshot = new OrganizationAdminConnectionSnapshot(
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

    private static OrganizationAdminWebStoredConnection CreateStoredConnection(OrganizationAdminConnectionSnapshot snapshot)
    {
        return new OrganizationAdminWebStoredConnection(
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
        OrganizationAdminWebBridgeError? error)
    {
        return JsonSerializer.Serialize(
            new OrganizationAdminWebBridgeResponse("host:response", requestId, ok, payload, error),
            JsonOptions);
    }

    private sealed record OrganizationAdminWebBridgeRequest(
        string? Type,
        string? RequestId,
        string? OperationId,
        JsonElement Payload);

    private sealed record OrganizationAdminWebBridgeResponse(
        string Type,
        string RequestId,
        bool Ok,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        object? Payload,
        OrganizationAdminWebBridgeError? Error);

    private sealed record OrganizationAdminWebBridgeError(
        string Code,
        string Message);

    private sealed record OrganizationAdminWebStoredConnectionPayload(
        string? OrganizationId,
        string? OrganizationSlug,
        string? OrganizationName,
        string? BranchId,
        string? BranchSlug,
        string? BranchName,
        string? BranchCity,
        DateTimeOffset? StoredAtUtc);

    private sealed record OrganizationAdminRestartAndInstallPayload(
        string? UpdateRolloutId,
        string? UpdatePackageId);

    private sealed record OrganizationAdminWebStoredConnection(
        Guid OrganizationId,
        string OrganizationSlug,
        string OrganizationName,
        Guid BranchId,
        string BranchSlug,
        string BranchName,
        string BranchCity,
        DateTimeOffset StoredAtUtc);
}
