using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Realtime;
using AFK4.Player.Shell.Workstation;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

/// <summary>
/// Мост хост ↔ страница оболочки, версия 2 (спека оболочки, §4.4). Конверт — общий из
/// @afk4/host-bridge: запрос {type, requestId, payload}, ответ host:response, событие {type,
/// payload}. Имена и тела — из ShellBridgeContracts, те же, что у страницы.
/// </summary>
public sealed class ShellBridgeHost(
    IShellAgentRequests agent,
    DevicePlayerSession session,
    Func<PlayerShellStateDto?> latestState,
    ISystemControls? system = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Язык, выбранный на экране; живёт до выхода игрока.</summary>
    public string? Locale { get; private set; }

    /// <summary>Вход или выход поменял, кто за ПК: окну надо разослать auth.changed.</summary>
    public event Action<ShellAuthStateDto>? AuthChanged;

    public async Task<string> HandleAsync(string requestJson, CancellationToken cancellationToken)
    {
        string requestId;
        string type;
        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            requestId = root.TryGetProperty("requestId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString()! : "";
            type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
            payload = root.TryGetProperty("payload", out var p) ? p.Clone() : default;
        }
        catch (JsonException)
        {
            return Error("", ShellPipeErrorCodeNames.InvalidPayload, "The request is not valid JSON.");
        }

        return type switch
        {
            ShellBridgeRequestTypeNames.ShellReady => Ok(requestId, new ShellSnapshotDto(latestState(), session.Current, system?.Read())),
            ShellBridgeRequestTypeNames.AuthSignIn => await SignInAsync(requestId, payload, cancellationToken),
            ShellBridgeRequestTypeNames.AuthSignOut => await SignOutAsync(requestId, cancellationToken),
            ShellBridgeRequestTypeNames.AppLaunch => await LaunchAsync(requestId, payload, cancellationToken),
            ShellBridgeRequestTypeNames.AssistCall => await AskAgentAsync(
                requestId, ShellPipeRequestTypeNames.Assist, new Dictionary<string, string>(), cancellationToken),
            ShellBridgeRequestTypeNames.MaintenanceReturn => await AskAgentAsync(
                requestId, ShellPipeRequestTypeNames.MaintenanceReturn, new Dictionary<string, string>(), cancellationToken),
            ShellBridgeRequestTypeNames.UiSetLocale => SetLocale(requestId, payload),
            ShellBridgeRequestTypeNames.ShowcaseImpression => await ImpressionAsync(requestId, payload, cancellationToken),
            ShellBridgeRequestTypeNames.SystemSetVolume or ShellBridgeRequestTypeNames.SystemSetMicMuted
                or ShellBridgeRequestTypeNames.SystemSetLayout when system is not null => ChangeSystem(requestId, type, payload),
            _ => Error(requestId, ShellBridgeErrorCodeNames.NotSupported, $"The shell host does not handle '{type}' yet.")
        };
    }

    /// <summary>Событие для страницы: {type, payload}.</summary>
    public static string Event(string type, object? payload) =>
        JsonSerializer.Serialize(new { type, payload }, JsonOptions);

    private async Task<string> SignInAsync(string requestId, JsonElement payload, CancellationToken cancellationToken)
    {
        var phone = ReadString(payload, "phone");
        var pin = ReadString(payload, "pin");
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(pin))
        {
            return Error(requestId, ShellPipeErrorCodeNames.InvalidPayload, "auth.signIn needs a phone and a PIN.");
        }

        // Удачный ответ пуст: сам вход приходит от агента кадром auth и доезжает до страницы
        // событием auth.changed — тем же путём, что и вход по QR.
        return await AskAgentAsync(
            requestId,
            ShellPipeRequestTypeNames.SignInPin,
            new Dictionary<string, string> { ["phone"] = phone, ["pin"] = pin },
            cancellationToken);
    }

    private async Task<string> SignOutAsync(string requestId, CancellationToken cancellationToken)
    {
        await session.SignOutAsync(cancellationToken);
        Locale = null;
        var signedOut = session.Current;
        AuthChanged?.Invoke(signedOut);
        return Ok(requestId, signedOut);
    }

    private Task<string> LaunchAsync(string requestId, JsonElement payload, CancellationToken cancellationToken)
    {
        var appId = ReadString(payload, "appId");
        return string.IsNullOrWhiteSpace(appId)
            ? Task.FromResult(Error(requestId, ShellPipeErrorCodeNames.InvalidPayload, "app.launch needs an appId."))
            : AskAgentAsync(requestId, ShellPipeRequestTypeNames.Launch, new Dictionary<string, string> { ["appId"] = appId }, cancellationToken);
    }

    /// <summary>Показ карточки витрины — агенту: он решает, что считать, и копит суммы.</summary>
    private Task<string> ImpressionAsync(string requestId, JsonElement payload, CancellationToken cancellationToken)
    {
        var cardId = ReadString(payload, "cardId");
        var shownMs = ReadInt(payload, "shownMs");
        return string.IsNullOrWhiteSpace(cardId) || shownMs is null or < 0
            ? Task.FromResult(Error(requestId, ShellPipeErrorCodeNames.InvalidPayload, "showcase.impression needs a cardId and shownMs."))
            : AskAgentAsync(
                requestId,
                ShellPipeRequestTypeNames.ShowcaseImpression,
                new Dictionary<string, string>
                {
                    ["cardId"] = cardId,
                    ["shownMs"] = shownMs.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                },
                cancellationToken);
    }

    private string SetLocale(string requestId, JsonElement payload)
    {
        var locale = ReadString(payload, "locale");
        if (string.IsNullOrWhiteSpace(locale))
        {
            return Error(requestId, ShellPipeErrorCodeNames.InvalidPayload, "ui.setLocale needs a locale.");
        }

        Locale = locale;
        return Ok(requestId, null);
    }

    /// <summary>Ответ — что стало на ПК после изменения: страница сверяет с тем, что показала заранее.</summary>
    private string ChangeSystem(string requestId, string type, JsonElement payload)
    {
        var controls = system!;
        try
        {
            switch (type)
            {
                case ShellBridgeRequestTypeNames.SystemSetVolume when ReadInt(payload, "volume") is { } volume:
                    controls.SetVolume(volume);
                    break;
                case ShellBridgeRequestTypeNames.SystemSetMicMuted when ReadBool(payload, "micMuted") is { } muted:
                    controls.SetMicMuted(muted);
                    break;
                case ShellBridgeRequestTypeNames.SystemSetLayout when ReadString(payload, "layout") is { } layout
                                                                    && KeyboardLayouts.KlidFor(layout) is not null:
                    controls.SetLayout(layout);
                    // Windows меняет раскладку сообщением окну — чтение сразу вернуло бы старую. Отвечаем
                    // запрошенной, а настоящую подтвердит опрос через секунду.
                    return Ok(requestId, controls.Read() with { Layout = layout });
                default:
                    return Error(requestId, ShellPipeErrorCodeNames.InvalidPayload, $"{type} has an invalid payload.");
            }
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidOperationException
                                              or InvalidCastException)
        {
            PlayerShellStartupLog.Write($"{type} failed.", exception);
            return Error(requestId, ShellBridgeErrorCodeNames.SystemUnavailable, "Windows did not allow the change.");
        }

        return Ok(requestId, controls.Read());
    }

    /// <summary>Коды отказов агента идут на страницу как есть: у канала и моста они общие.</summary>
    private async Task<string> AskAgentAsync(
        string requestId,
        string pipeType,
        IReadOnlyDictionary<string, string> pipePayload,
        CancellationToken cancellationToken)
    {
        var reply = await agent.RequestAsync(pipeType, pipePayload, cancellationToken);
        return reply.Ok
            ? Ok(requestId, null)
            : Error(requestId, reply.ErrorCode ?? ShellBridgeErrorCodeNames.AgentUnavailable, reply.Message ?? "The PC service refused.");
    }

    private static int? ReadInt(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool? ReadBool(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static string? ReadString(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Ok(string requestId, object? payload) =>
        JsonSerializer.Serialize(new { type = "host:response", requestId, ok = true, payload }, JsonOptions);

    private static string Error(string requestId, string code, string message) =>
        JsonSerializer.Serialize(new { type = "host:response", requestId, ok = false, error = new { code, message } }, JsonOptions);
}
