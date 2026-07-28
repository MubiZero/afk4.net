using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.OrganizationAdmin.App.Configuration;

namespace AFK4.OrganizationAdmin.Web;

public static class OrganizationAdminWebBootstrapScript
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(OrganizationAdminOptions appOptions, OrganizationAdminWebShellLaunchTarget launchTarget) =>
        Create(appOptions, launchTarget, ResolveInstalledVersion());

    public static string Create(
        OrganizationAdminOptions appOptions,
        OrganizationAdminWebShellLaunchTarget launchTarget,
        string appVersion)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(launchTarget);
        var normalizedVersion = string.IsNullOrWhiteSpace(appVersion) ? "—" : appVersion.Trim();

        var payload = new OrganizationAdminWebBootstrapPayload(
            Runtime: "webview2",
            ShellMode: launchTarget.Mode,
            PlatformBaseUrl: appOptions.PlatformBaseUrl.ToString(),
            CurrencyCode: appOptions.CurrencyCode,
            AppVersion: normalizedVersion,
            OrganizationId: appOptions.OrganizationId,
            BranchId: appOptions.BranchId);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"window.__AFK4_ORGANIZATION_ADMIN_CONFIG__ = {json};";
    }

    private static string ResolveInstalledVersion()
    {
        var assembly = typeof(OrganizationAdminWebBootstrapScript).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', 2)[0];

        return !string.IsNullOrWhiteSpace(informational)
            ? informational
            : assembly.GetName().Version?.ToString() ?? "—";
    }

    private sealed record OrganizationAdminWebBootstrapPayload(
        string Runtime,
        string ShellMode,
        string PlatformBaseUrl,
        string CurrencyCode,
        string AppVersion,
        Guid? OrganizationId,
        Guid? BranchId);
}
