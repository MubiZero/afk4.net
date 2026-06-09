using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Player.Shell.Launcher;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

public sealed class PlayerShellWebHostBridge(
    ILauncherCommandClient launcher,
    Func<PlayerShellStateDto?> getLatestState)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> AllowedTypes =
    [
        "shell:loadState",
        "launcher:launch",
        "shell:requestOperator",
        "shell:pause"
    ];

    public async Task<string?> HandleAsync(string requestJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;
        var requestId = root.TryGetProperty("requestId", out var id) ? id.GetString() ?? "" : "";
        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

        if (!AllowedTypes.Contains(type))
        {
            return Error(requestId, "unknown_request", $"Unsupported request type '{type}'.");
        }

        var payload = root.TryGetProperty("payload", out var p) ? p : default;

        return type switch
        {
            "shell:loadState" => Ok(requestId, getLatestState()),
            "launcher:launch" => await HandleLaunchAsync(requestId, payload, cancellationToken),
            "shell:requestOperator" => Ok(requestId, new { requested = true }),
            "shell:pause" => Ok(requestId, new { paused = true }),
            _ => Error(requestId, "unknown_request", "Unsupported request type.")
        };
    }

    public static string CreateStatePush(PlayerShellStateDto state) =>
        JsonSerializer.Serialize(new { type = "shell:stateChanged", payload = state }, JsonOptions);

    private async Task<string> HandleLaunchAsync(string requestId, JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("appId", out var appIdEl)
            || appIdEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(appIdEl.GetString()))
        {
            return Error(requestId, "invalid_payload", "launcher:launch requires a non-empty appId.");
        }

        var result = await launcher.LaunchAsync(appIdEl.GetString()!, ct);
        return Ok(requestId, result);
    }

    private static string Ok(string requestId, object? payload) =>
        JsonSerializer.Serialize(
            new { type = "host:response", requestId, ok = true, payload },
            JsonOptions);

    private static string Error(string requestId, string code, string message) =>
        JsonSerializer.Serialize(
            new { type = "host:response", requestId, ok = false, error = new { code, message } },
            JsonOptions);
}
