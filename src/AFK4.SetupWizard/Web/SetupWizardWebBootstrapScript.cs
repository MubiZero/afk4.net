using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Web;

public static class SetupWizardWebBootstrapScript
{
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
            Runtime: "miniblink",
            ShellMode: launchTarget.Mode,
            MachineName: machineInfo.MachineName,
            IsPreview: isPreview,
            PlatformBaseUrl: platformBaseUrl.ToString());

        var json = JsonSerializer.Serialize(payload, SetupWizardWebJson.Default.SetupWizardWebBootstrapPayload);
        return $"window.__AFK4_SETUP_WIZARD_CONFIG__ = {json};";
    }
}

internal sealed record SetupWizardWebBootstrapPayload(
    string Runtime,
    string ShellMode,
    string MachineName,
    bool IsPreview,
    string PlatformBaseUrl);

/// <summary>Сообщения окна мастера: экран → программа (команды окна) и программа → экран (состояние окна).</summary>
internal sealed record SetupWizardWindowMessage(string? Type, string? Theme = null, bool? Maximized = null);

/// <summary>JSON окна мастера — через генератор: в Native AOT нет JSON на отражении (docs/roadmap/windows7.md).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SetupWizardWebBootstrapPayload))]
[JsonSerializable(typeof(SetupWizardWindowMessage))]
internal sealed partial class SetupWizardWebJson : JsonSerializerContext;
