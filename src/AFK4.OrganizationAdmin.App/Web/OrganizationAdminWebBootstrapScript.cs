using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.OrganizationAdmin.App.Configuration;

namespace AFK4.OrganizationAdmin.Web;

public static partial class OrganizationAdminWebBootstrapScript
{
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
            Product: OrganizationAdminOptions.Product,
            CompatibilityEpoch: OrganizationAdminOptions.CompatibilityEpoch,
            Runtime: "miniblink",
            ShellMode: launchTarget.Mode,
            PlatformBaseUrl: appOptions.PlatformBaseUrl.ToString(),
            CurrencyCode: appOptions.CurrencyCode,
            AppVersion: normalizedVersion,
            OrganizationId: appOptions.OrganizationId,
            BranchId: appOptions.BranchId);

        var json = JsonSerializer.Serialize(payload, OrganizationAdminBootstrapJson.Default.OrganizationAdminWebBootstrapPayload);
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

    internal sealed record OrganizationAdminWebBootstrapPayload(
        string Product,
        int CompatibilityEpoch,
        string Runtime,
        string ShellMode,
        string PlatformBaseUrl,
        string CurrencyCode,
        string AppVersion,
        Guid? OrganizationId,
        Guid? BranchId);

    /// <summary>JSON настроек экрана без отражения: Native AOT (docs/roadmap/windows7.md).</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(OrganizationAdminWebBootstrapPayload))]
    internal sealed partial class OrganizationAdminBootstrapJson : JsonSerializerContext;
}
