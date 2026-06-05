using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.SetupWizard.Web;

public sealed class SetupWizardWebHostBridge(ISetupWizardApiClient apiClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string?> HandleAsync(string webMessageJson, CancellationToken cancellationToken)
    {
        SetupWizardWebBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SetupWizardWebBridgeRequest>(webMessageJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Type) ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            !request.Type.StartsWith("wizard:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var payload = request.Type switch
            {
                "wizard:discover" => await DiscoverAsync(request.Payload, cancellationToken),
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
                new SetupWizardWebBridgeError(ErrorCodeFor(request.Type), exception.Message));
        }
    }

    private async Task<WizardDiscoverResult> DiscoverAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardDiscoverPayload>(payload);
        if (string.IsNullOrWhiteSpace(request.OwnerCode))
        {
            throw new InvalidOperationException("Owner code is required.");
        }

        var normalized = NormalizeOwnerCode(request.OwnerCode);
        if (normalized.Length != 8)
        {
            throw new InvalidOperationException("Owner code must be exactly 8 digits.");
        }

        var response = await apiClient.DiscoverAsync(normalized, cancellationToken);
        var branches = response.Branches
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapBranch)
            .ToArray();

        return new WizardDiscoverResult(response.OwnerDisplayName, branches);
    }

    private static WizardBranch MapBranch(Shared.Contracts.Install.InstallBranchDto branch)
    {
        var zoneLookup = branch.FloorMap.Zones
            .OrderBy(zone => zone.SortOrder)
            .ToDictionary(zone => zone.ZoneId, zone => zone);
        var freeSeatIds = branch.FreeSeatIds.ToHashSet();

        var zones = branch.FloorMap.Zones
            .OrderBy(zone => zone.SortOrder)
            .Select(zone => new WizardZone(zone.ZoneId, zone.Name, zone.SortOrder))
            .ToArray();

        var seats = branch.FloorMap.Seats
            .OrderBy(seat => ZoneSortOrder(zoneLookup, seat.ZoneId))
            .ThenBy(seat => seat.SortOrder)
            .ThenBy(seat => seat.SeatName, StringComparer.OrdinalIgnoreCase)
            .Select(seat => new WizardSeat(
                seat.SeatId,
                seat.SeatName,
                seat.ZoneId,
                seat.ZoneName,
                seat.SortOrder,
                seat.State,
                seat.DeviceId,
                seat.DeviceName,
                seat.IsDeviceOnline))
            .ToArray();

        return new WizardBranch(
            branch.BranchId,
            branch.Slug,
            branch.Name,
            zones,
            seats,
            branch.FreeSeatIds.Where(id => freeSeatIds.Contains(id)).ToArray());
    }

    private static int ZoneSortOrder(IDictionary<Guid, FloorMapZoneDto> lookup, Guid zoneId) =>
        lookup.TryGetValue(zoneId, out var zone) ? zone.SortOrder : int.MaxValue;

    private static string NormalizeOwnerCode(string value)
    {
        var digits = new char[value.Length];
        var digitCount = 0;
        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                digits[digitCount] = character;
                digitCount++;
                continue;
            }

            if (char.IsWhiteSpace(character) || character == '-')
            {
                continue;
            }

            throw new InvalidOperationException("Owner code must contain only digits, spaces, or dashes.");
        }

        return new string(digits, 0, digitCount);
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

    private static string ErrorCodeFor(string? requestType) => requestType switch
    {
        "wizard:discover" => "wizard_discover_failed",
        _ => "wizard_request_failed"
    };

    private static string CreateResponse(
        string requestId,
        bool ok,
        object? payload,
        SetupWizardWebBridgeError? error)
    {
        return JsonSerializer.Serialize(
            new SetupWizardWebBridgeResponse("host:response", requestId, ok, payload, error),
            JsonOptions);
    }

    private sealed record SetupWizardWebBridgeRequest(
        string? Type,
        string? RequestId,
        JsonElement Payload);

    private sealed record SetupWizardWebBridgeResponse(
        string Type,
        string RequestId,
        bool Ok,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        object? Payload,
        SetupWizardWebBridgeError? Error);

    private sealed record SetupWizardWebBridgeError(string Code, string Message);

    private sealed record WizardDiscoverPayload(string? OwnerCode);

    private sealed record WizardDiscoverResult(string OwnerName, IReadOnlyList<WizardBranch> Branches);

    private sealed record WizardBranch(
        Guid BranchId,
        string BranchSlug,
        string BranchName,
        IReadOnlyList<WizardZone> Zones,
        IReadOnlyList<WizardSeat> Seats,
        IReadOnlyList<Guid> FreeSeatIds);

    private sealed record WizardZone(Guid ZoneId, string Name, int SortOrder);

    private sealed record WizardSeat(
        Guid SeatId,
        string PcName,
        Guid ZoneId,
        string ZoneName,
        int SortOrder,
        string Status,
        Guid? EnrolledDeviceId,
        string? EnrolledDeviceName,
        bool? IsOnline);
}
