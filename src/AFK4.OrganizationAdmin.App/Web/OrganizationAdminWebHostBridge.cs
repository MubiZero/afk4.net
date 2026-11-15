using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AFK4.OrganizationAdmin.App;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Offline;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.OrganizationAdmin.Web;

// Auth runs over plain HTTP (staffAuthApi.ts + sessionStorage). This bridge stays narrow: it owns
// machine-local connection identity and update-safety activity that cannot live in the browser.
public sealed class OrganizationAdminWebHostBridge(
    IOrganizationAdminConnectionStore connectionStore,
    OrganizationAdminActivityState? updateActivityState = null,
    IOrganizationAdminShutdownAcknowledgementStore? shutdownAcknowledgementStore = null,
    Action? requestShutdown = null,
    OfflineDesk? offlineDesk = null,
    OfflineJournal? offlineJournal = null)
{
    public async Task<string?> HandleAsync(string webMessageJson, CancellationToken cancellationToken)
    {
        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(webMessageJson, OrganizationAdminBridgeJson.Default.BridgeRequest);
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

        if (request.Type.StartsWith("offline:", StringComparison.Ordinal))
        {
            return await HandleOfflineAsync(request, cancellationToken);
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
        BridgeRequest request,
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

            var payload = DeserializePayload(request.Payload, OrganizationAdminBridgeJson.Default.RestartAndInstallPayload);
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
            return CreateResponse(request.RequestId!, true, new AcceptedPayload(true), null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            return CreateResponse(request.RequestId!, false, null,
                new OrganizationAdminWebBridgeError("update_failed", "The update restart request could not be saved."));
        }
    }

    // Офлайн у стойки (docs/roadmap/offline-front-desk.md): ключ этого ПК, передача подписанной аренды ПК
    // в зале и журнал кассы на диске. Без хоста (в браузере) Панель офлайн-действий не предлагает.
    private async Task<string> HandleOfflineAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        if (offlineDesk is null || offlineJournal is null)
        {
            return CreateResponse(request.RequestId!, false, null,
                new OrganizationAdminWebBridgeError("offline_unavailable", "Offline desk is not available in this host."));
        }

        try
        {
            object payload;
            switch (request.Type)
            {
                case "offline:key":
                    var (keyId, publicKeyPem) = offlineDesk.PublicKey();
                    payload = new OfflineKeyPayload(keyId, publicKeyPem);
                    break;
                case "offline:sendLease":
                    var send = DeserializePayload(request.Payload, OrganizationAdminBridgeJson.Default.SendLeasePayload);
                    payload = await offlineDesk.SendLeaseAsync(send.Address, send.Lease, cancellationToken);
                    break;
                case "offline:journalAppend":
                    offlineJournal.Append(DeserializePayload(request.Payload, OrganizationAdminBridgeJson.Default.JournalAppendPayload).Record);
                    payload = new AppendedPayload(true);
                    break;
                case "offline:journalRead":
                    payload = new RecordsPayload(offlineJournal.Read());
                    break;
                case "offline:journalAck":
                    var ack = DeserializePayload(request.Payload, OrganizationAdminBridgeJson.Default.JournalAckPayload);
                    payload = new RemainingPayload(offlineJournal.Acknowledge(ack.Ids.ToHashSet(StringComparer.Ordinal)));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported host bridge request: {request.Type}.");
            }

            return CreateResponse(request.RequestId!, true, payload, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return CreateResponse(request.RequestId!, false, null,
                new OrganizationAdminWebBridgeError("offline_pc_unreachable", "The PC did not answer over the club network."));
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            return CreateResponse(request.RequestId!, false, null,
                new OrganizationAdminWebBridgeError("offline_failed", exception.Message));
        }
    }

    private async Task<StoredConnection?> LoadConnectionAsync(CancellationToken cancellationToken)
    {
        var snapshot = await connectionStore.LoadAsync(cancellationToken);
        return snapshot is null ? null : CreateStoredConnection(snapshot);
    }

    private async Task<StoredConnection> SaveConnectionAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload(payload, OrganizationAdminBridgeJson.Default.StoredConnectionPayload);
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
        return new ClearedPayload(true);
    }

    private static StoredConnection CreateStoredConnection(OrganizationAdminConnectionSnapshot snapshot)
    {
        return new StoredConnection(
            snapshot.OrganizationId,
            snapshot.OrganizationSlug,
            snapshot.OrganizationName,
            snapshot.BranchId,
            snapshot.BranchSlug,
            snapshot.BranchName,
            snapshot.BranchCity,
            snapshot.StoredAtUtc);
    }

    private static T DeserializePayload<T>(JsonElement payload, JsonTypeInfo<T> typeInfo)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException("Host bridge payload is required.");
        }

        return payload.Deserialize(typeInfo)
            ?? throw new InvalidOperationException("Host bridge payload is invalid.");
    }

    /// <summary>Ответ об ошибке, которую поймало окно: тот же конверт, что у остальных ответов моста.</summary>
    public static string CreateErrorResponse(string requestId, string code, string message) =>
        CreateResponse(requestId, ok: false, payload: null, new OrganizationAdminWebBridgeError(code, message));

    // Без отражения: тела — только записи из OrganizationAdminBridgeJson. «payload» пишется всегда (null, если тела нет),
    // как и раньше: страница ждёт это поле у каждого ответа.
    private static string CreateResponse(
        string requestId,
        bool ok,
        object? payload,
        OrganizationAdminWebBridgeError? error)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "host:response");
            writer.WriteString("requestId", requestId);
            writer.WriteBoolean("ok", ok);
            writer.WritePropertyName("payload");
            WritePayload(writer, payload);
            if (error is not null)
            {
                writer.WriteStartObject("error");
                writer.WriteString("code", error.Code);
                writer.WriteString("message", error.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WritePayload(Utf8JsonWriter writer, object? payload)
    {
        var context = OrganizationAdminBridgeJson.Default;
        switch (payload)
        {
            case null: writer.WriteNullValue(); break;
            case StoredConnection value: JsonSerializer.Serialize(writer, value, context.StoredConnection); break;
            case ClearedPayload value: JsonSerializer.Serialize(writer, value, context.ClearedPayload); break;
            case AcceptedPayload value: JsonSerializer.Serialize(writer, value, context.AcceptedPayload); break;
            case OfflineKeyPayload value: JsonSerializer.Serialize(writer, value, context.OfflineKeyPayload); break;
            case AppendedPayload value: JsonSerializer.Serialize(writer, value, context.AppendedPayload); break;
            case RecordsPayload value: JsonSerializer.Serialize(writer, value, context.RecordsPayload); break;
            case RemainingPayload value: JsonSerializer.Serialize(writer, value, context.RemainingPayload); break;
            case OfflineSeatResponse value: JsonSerializer.Serialize(writer, value, context.OfflineSeatResponse); break;
            default: throw new InvalidOperationException($"{payload.GetType().Name} has no JSON contract for the host bridge.");
        }
    }

    internal sealed record BridgeRequest(
        string? Type,
        string? RequestId,
        string? OperationId,
        JsonElement Payload);

    private sealed record OrganizationAdminWebBridgeError(
        string Code,
        string Message);

    internal sealed record StoredConnectionPayload(
        string? OrganizationId,
        string? OrganizationSlug,
        string? OrganizationName,
        string? BranchId,
        string? BranchSlug,
        string? BranchName,
        string? BranchCity,
        DateTimeOffset? StoredAtUtc);

    internal sealed record RestartAndInstallPayload(
        string? UpdateRolloutId,
        string? UpdatePackageId);

    internal sealed record SendLeasePayload(string Address, AFK4.Shared.Contracts.Sessions.SessionLeaseDto Lease);

    internal sealed record JournalAppendPayload(JsonElement Record);

    internal sealed record JournalAckPayload(IReadOnlyList<string> Ids);

    internal sealed record StoredConnection(
        Guid OrganizationId,
        string OrganizationSlug,
        string OrganizationName,
        Guid BranchId,
        string BranchSlug,
        string BranchName,
        string BranchCity,
        DateTimeOffset StoredAtUtc);

    internal sealed record ClearedPayload(bool Cleared);

    internal sealed record AcceptedPayload(bool Accepted);

    internal sealed record OfflineKeyPayload(string KeyId, string PublicKeyPem);

    internal sealed record AppendedPayload(bool Appended);

    internal sealed record RecordsPayload(IReadOnlyList<JsonElement> Records);

    internal sealed record RemainingPayload(int Remaining);
}
