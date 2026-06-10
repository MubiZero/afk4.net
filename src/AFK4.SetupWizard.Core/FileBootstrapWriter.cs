using System.Diagnostics;
using System.Runtime.Versioning;
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
            RestrictAccessToSystemAndAdministrators(filePath);
        }
    }

    // Best-effort hardening — the bootstrap file contains the device credential, so strip
    // inherited ACLs and grant only SYSTEM + Administrators. Failure here never blocks enrollment.
    private static void RestrictAccessToSystemAndAdministrators(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            RunIcacls(path);
        }
        catch
        {
            // Ignore: a slightly looser ACL must not break setup.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RunIcacls(string path)
    {
        var icacls = Path.Combine(Environment.SystemDirectory, "icacls.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(icacls) ? icacls : "icacls.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("/inheritance:r");
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-18:F");      // NT AUTHORITY\SYSTEM
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-32-544:F");  // BUILTIN\Administrators

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);
    }
}
