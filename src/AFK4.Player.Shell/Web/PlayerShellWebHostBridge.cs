using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Launcher;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

public sealed class PlayerShellWebHostBridge(
    ILauncherCommandClient launcher,
    Func<PlayerShellStateDto?> getLatestState,
    IPlayerApiAuthClient auth)
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
        "shell:pause",
        "auth:signIn",
        "auth:signOut",
        "auth:loadState"
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
            "auth:signIn" => await HandleSignInAsync(requestId, payload, cancellationToken),
            "auth:signOut" => HandleSignOut(requestId),
            "auth:loadState" => Ok(requestId, Snapshot()),
            _ => Error(requestId, "unknown_request", "Unsupported request type.")
        };
    }

    public static string CreateStatePush(PlayerShellStateDto state) =>
        JsonSerializer.Serialize(new { type = "shell:stateChanged", payload = state }, JsonOptions);

    public static string CreateAuthPush(AuthSnapshot s) =>
        JsonSerializer.Serialize(
            new { type = "shell:authChanged",
                  payload = new { authenticated = s.Authenticated, displayName = s.DisplayName, phoneVerified = s.PhoneVerified } },
            JsonOptions);

    private async Task<string> HandleSignInAsync(string requestId, JsonElement payload, CancellationToken ct)
    {
        var state = getLatestState();
        if (state is null)
        {
            return Error(requestId, "no_state", "Shell state not yet available; cannot determine organization.");
        }

        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("phoneNumber", out var phoneEl) || phoneEl.ValueKind != JsonValueKind.String
            || !payload.TryGetProperty("password", out var pwEl) || pwEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(phoneEl.GetString()) || string.IsNullOrEmpty(pwEl.GetString()))
        {
            return Error(requestId, "invalid_payload", "auth:signIn requires phoneNumber and password.");
        }

        await auth.SignInAsync(state.OrganizationId, phoneEl.GetString()!, pwEl.GetString()!, ct);
        return Ok(requestId, Snapshot());
    }

    private string HandleSignOut(string requestId)
    {
        auth.SignOut();
        return Ok(requestId, Snapshot());
    }

    private object Snapshot()
    {
        var s = auth.Current;
        return new { authenticated = s.Authenticated, displayName = s.DisplayName, phoneVerified = s.PhoneVerified };
    }

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
