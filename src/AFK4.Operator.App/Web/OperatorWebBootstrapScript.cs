using System.Reflection;
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

    public static string Create(OperatorAppOptions appOptions, OperatorWebShellLaunchTarget launchTarget) =>
        Create(appOptions, launchTarget, ResolveInstalledVersion());

    public static string Create(
        OperatorAppOptions appOptions,
        OperatorWebShellLaunchTarget launchTarget,
        string appVersion)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(launchTarget);
        var normalizedVersion = string.IsNullOrWhiteSpace(appVersion) ? "—" : appVersion.Trim();

        var payload = new OperatorWebBootstrapPayload(
            Runtime: "webview2",
            ShellMode: launchTarget.Mode,
            PlatformBaseUrl: appOptions.PlatformBaseUrl.ToString(),
            CurrencyCode: appOptions.CurrencyCode,
            AppVersion: normalizedVersion,
            OrganizationId: appOptions.OrganizationId,
            BranchId: appOptions.BranchId);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"window.__AFK4_OPERATOR_CONFIG__ = {json};";
    }

    private static string ResolveInstalledVersion()
    {
        var assembly = typeof(OperatorWebBootstrapScript).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', 2)[0];

        return !string.IsNullOrWhiteSpace(informational)
            ? informational
            : assembly.GetName().Version?.ToString() ?? "—";
    }

    private sealed record OperatorWebBootstrapPayload(
        string Runtime,
        string ShellMode,
        string PlatformBaseUrl,
        string CurrencyCode,
        string AppVersion,
        Guid? OrganizationId,
        Guid? BranchId);
}
