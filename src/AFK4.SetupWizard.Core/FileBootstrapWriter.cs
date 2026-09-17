using System.Text.Json;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Writes the Agent bootstrap configuration to a JSON file the Agent service reads at startup
/// (<c>%ProgramData%\AFK4\Agent\bootstrap.json</c>). This is the authoritative channel: unlike
/// machine environment variables, a file is read fresh on every process start, so the Agent
/// picks up enrollment immediately without waiting for the next reboot to refresh the SCM's
/// cached environment. The file holds the device credential, so it is locked down to SYSTEM and
/// Administrators.
/// </summary>
public sealed class FileBootstrapWriter(
    string machineName,
    string? bootstrapFilePath = null,
    bool restrictAccess = true) : ISetupWizardBootstrapWriter
{
    public static string DefaultBootstrapFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent",
        "bootstrap.json");

    private readonly string filePath = string.IsNullOrWhiteSpace(bootstrapFilePath)
        ? DefaultBootstrapFilePath
        : bootstrapFilePath;

    public void Write(SetupWizardBootstrapConfig config)
    {
        var agentSection = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in AgentBootstrapValues.Build(config, machineName))
        {
            // Bind PlayerShellAutoStartEnabled as a real JSON boolean so AgentOptions binds cleanly.
            agentSection[key] = bool.TryParse(value, out var flag) ? flag : value;
        }

        var document = new Dictionary<string, object> { ["Agent"] = agentSection };
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json);

        if (restrictAccess)
        {
            RestrictedFile.RestrictToSystemAndAdministrators(
                filePath,
                exception => SetupWizardStartupLog.Write(
                    $"Could not restrict access to '{filePath}'. The device credential stays readable by local users.",
                    exception));
        }
    }
}
