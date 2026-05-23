using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Operator.App.Configuration;

namespace AFK4.Operator.App.Web;

public static class OperatorWebBootstrapScript
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(OperatorAppOptions appOptions, OperatorWebShellLaunchTarget launchTarget)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(launchTarget);

        var payload = new OperatorWebBootstrapPayload(
            Runtime: "webview2",
            ShellMode: launchTarget.Mode,
            PlatformBaseUrl: appOptions.PlatformBaseUrl.ToString(),
            CurrencyCode: appOptions.CurrencyCode,
            OrganizationId: appOptions.OrganizationId,
            BranchId: appOptions.BranchId);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"window.__AFK4_OPERATOR_CONFIG__ = {json};";
    }

    private sealed record OperatorWebBootstrapPayload(
        string Runtime,
        string ShellMode,
        string PlatformBaseUrl,
        string CurrencyCode,
        Guid? OrganizationId,
        Guid? BranchId);
}
