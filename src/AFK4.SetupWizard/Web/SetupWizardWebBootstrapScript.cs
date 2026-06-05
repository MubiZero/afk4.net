using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Web;

public static class SetupWizardWebBootstrapScript
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(
        SetupWizardWebShellLaunchTarget launchTarget,
        SetupWizardMachineInfo machineInfo,
        Uri platformBaseUrl,
        bool isPreview)
    {
        ArgumentNullException.ThrowIfNull(launchTarget);
        ArgumentNullException.ThrowIfNull(machineInfo);
        ArgumentNullException.ThrowIfNull(platformBaseUrl);

        var payload = new SetupWizardWebBootstrapPayload(
            Runtime: "webview2",
            ShellMode: launchTarget.Mode,
            MachineName: machineInfo.MachineName,
            IsPreview: isPreview,
            PlatformBaseUrl: platformBaseUrl.ToString());

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"window.__AFK4_SETUP_WIZARD_CONFIG__ = {json};";
    }

    private sealed record SetupWizardWebBootstrapPayload(
        string Runtime,
        string ShellMode,
        string MachineName,
        bool IsPreview,
        string PlatformBaseUrl);
}
