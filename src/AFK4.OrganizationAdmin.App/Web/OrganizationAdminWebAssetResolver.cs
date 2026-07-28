using System.IO;

namespace AFK4.OrganizationAdmin.Web;

public sealed class OrganizationAdminWebAssetResolver
{
    public const string LocalVirtualHost = "operator.afk4.local";

    private readonly DirectoryInfo baseDirectory;
    private readonly Func<string, bool> fileExists;

    public OrganizationAdminWebAssetResolver(string baseDirectory)
        : this(baseDirectory, File.Exists)
    {
    }

    public OrganizationAdminWebAssetResolver(string baseDirectory, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(fileExists);

        this.baseDirectory = new DirectoryInfo(baseDirectory);
        this.fileExists = fileExists;
    }

    public OrganizationAdminWebShellLaunchTarget Resolve(OrganizationAdminWebShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DevServerUrl is not null)
        {
            return new OrganizationAdminWebShellLaunchTarget(options.DevServerUrl, "dev-server", LocalFolderPath: null);
        }

        var repositoryRoot = FindRepositoryRoot(baseDirectory);
        if (repositoryRoot is not null)
        {
            var builtIndexPath = Path.Combine(
                repositoryRoot.FullName,
                "src",
                "AFK4.OrganizationAdmin.Web",
                "dist",
                "index.html");

            if (fileExists(builtIndexPath))
            {
                return CreateLocalTarget(builtIndexPath, "vite-dist");
            }
        }

        var outputFallbackIndexPath = Path.Combine(baseDirectory.FullName, "WebAssets", "index.html");
        if (fileExists(outputFallbackIndexPath))
        {
            return CreateLocalTarget(outputFallbackIndexPath, "fallback-assets");
        }

        if (repositoryRoot is not null)
        {
            var sourceFallbackIndexPath = Path.Combine(
                repositoryRoot.FullName,
                "src",
                "AFK4.OrganizationAdmin.App",
                "WebAssets",
                "index.html");

            if (fileExists(sourceFallbackIndexPath))
            {
                return CreateLocalTarget(sourceFallbackIndexPath, "fallback-assets");
            }
        }

        throw new InvalidOperationException(
            "Organization Admin frontend assets were not found. Build src/AFK4.OrganizationAdmin.Web or include WebAssets/index.html.");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo startDirectory)
    {
        var current = startDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AFK4.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static OrganizationAdminWebShellLaunchTarget CreateLocalTarget(string indexPath, string mode)
    {
        var localFolderPath = Path.GetDirectoryName(Path.GetFullPath(indexPath))
            ?? throw new InvalidOperationException("Operator frontend asset path has no parent directory.");

        return new OrganizationAdminWebShellLaunchTarget(
            new Uri($"https://{LocalVirtualHost}/index.html"),
            mode,
            localFolderPath);
    }
}
