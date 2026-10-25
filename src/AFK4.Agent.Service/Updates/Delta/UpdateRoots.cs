using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates.Delta;

/// <summary>Имена корней манифеста: какая папка установки имеется в виду. Совпадают с --manifest-roots публикатора.</summary>
public static class UpdateRootNames
{
    public const string AgentService = "agent-service";
    public const string SetupWizard = "setup-wizard";
    public const string PlayerShell = "player-shell";
    public const string OrganizationAdmin = "organization-admin";

    public static bool IsKnown(string root) =>
        root is AgentService or SetupWizard or PlayerShell or OrganizationAdmin;
}

/// <summary>Где на этой машине лежит папка установки корня. null — корень здесь не установлен, дельта невозможна.</summary>
public interface IUpdateRootResolver
{
    string? Resolve(string root);
}

public sealed class OptionsUpdateRootResolver(IOptions<AgentOptions> options) : IUpdateRootResolver
{
    public string? Resolve(string root)
    {
        var agentOptions = options.Value;
        var agentServiceDirectory = TrimSeparator(AppContext.BaseDirectory);

        var directory = root switch
        {
            UpdateRootNames.AgentService => agentServiceDirectory,
            // Мастер ставится MSI агента рядом со службой: …\AFK4\Setup Wizard.
            UpdateRootNames.SetupWizard => Path.Combine(Path.GetDirectoryName(agentServiceDirectory) ?? string.Empty, "Setup Wizard"),
            UpdateRootNames.PlayerShell => DirectoryOf(agentOptions.PlayerShellExecutablePath),
            UpdateRootNames.OrganizationAdmin => DirectoryOf(agentOptions.OrganizationAdminExecutablePath),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? directory : null;
    }

    private static string? DirectoryOf(string executablePath) =>
        string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath);

    private static string TrimSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

public static class UpdateRelativePath
{
    /// <summary>
    /// Путь из манифеста в путь на диске внутри корня. Манифест подписан, но путь всё равно проверяется:
    /// подмена «..» вывела бы запись за пределы папки установки, а запись идёт от имени SYSTEM.
    /// </summary>
    public static bool TryResolve(string rootDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.StartsWith('/')
            || relativePath.Contains('\\')
            || relativePath.Contains(':')
            || relativePath.Contains('\0'))
        {
            return false;
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0
                || segment is "." or ".."
                || segment.EndsWith(' ')
                || segment.EndsWith('.')))
        {
            return false;
        }

        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, string.Join(Path.DirectorySeparatorChar, segments)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
