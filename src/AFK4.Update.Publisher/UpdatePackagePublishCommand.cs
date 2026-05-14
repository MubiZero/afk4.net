using System.Text.Json;
using System.Security.Cryptography;

namespace AFK4.Update.Publisher;

public static class UpdatePackagePublishCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var options = Parse(args);
            var result = await new FileSystemUpdatePackagePublisher().PublishAsync(options, cancellationToken);
            await output.WriteLineAsync(result.RequestJson);
            await error.WriteLineAsync($"Published artifact: {result.PublishedArtifactPath}");
            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            UnauthorizedAccessException or
            CryptographicException or
            HttpRequestException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    public static UpdatePackagePublishOptions Parse(string[] args)
    {
        var values = ParsePairs(args);
        var organizationId = RequiredGuid(values, "--organization-id");
        var artifactStoreKind = values.TryGetValue("--artifact-store", out var configuredArtifactStoreKind) &&
            !string.IsNullOrWhiteSpace(configuredArtifactStoreKind)
            ? configuredArtifactStoreKind
            : UpdateArtifactStoreKindNames.FileSystem;

        values.TryGetValue("--hosting-root", out var hostingRoot);
        values.TryGetValue("--published-file-name", out var publishedFileName);
        values.TryGetValue("--output", out var outputPath);
        values.TryGetValue("--signing-key", out var signingKeyPath);
        values.TryGetValue("--signing-key-env-var", out var signingKeyEnvironmentVariable);

        return new UpdatePackagePublishOptions(
            organizationId,
            Required(values, "--component"),
            Required(values, "--version"),
            Required(values, "--channel"),
            Required(values, "--artifact"),
            string.IsNullOrWhiteSpace(hostingRoot) ? null : hostingRoot,
            OptionalUri(values, "--public-base-uri"),
            string.IsNullOrWhiteSpace(signingKeyPath) ? null : signingKeyPath,
            Required(values, "--release-notes"),
            string.IsNullOrWhiteSpace(publishedFileName) ? null : publishedFileName,
            string.IsNullOrWhiteSpace(outputPath) ? null : outputPath,
            artifactStoreKind,
            OptionalUri(values, "--artifact-upload-uri"),
            OptionalUri(values, "--artifact-public-uri"),
            string.IsNullOrWhiteSpace(signingKeyEnvironmentVariable) ? null : signingKeyEnvironmentVariable);
    }

    private static Dictionary<string, string> ParsePairs(string[] args)
    {
        if (args.Length % 2 != 0)
        {
            throw new ArgumentException("Arguments must be provided as --name value pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Argument '{name}' must start with --.");
            }

            values[name] = args[index + 1];
        }

        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required argument {name} is missing.");
    }

    private static Guid RequiredGuid(IReadOnlyDictionary<string, string> values, string name)
    {
        var value = Required(values, name);
        return Guid.TryParse(value, out var guid) && guid != Guid.Empty
            ? guid
            : throw new ArgumentException($"{name} must be a non-empty GUID.");
    }

    private static Uri RequiredUri(IReadOnlyDictionary<string, string> values, string name)
    {
        var value = Required(values, name);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new ArgumentException($"{name} must be an absolute URI.");
    }

    private static Uri? OptionalUri(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new ArgumentException($"{name} must be an absolute URI.");
    }

    private const string Usage = """
        AFK4.Update.Publisher

        Required:
          --organization-id <guid>
          --component <operator-app|agent-service|player-shell>
          --version <version>
          --channel <internal|beta|stable>
          --artifact <path>
          --release-notes <text>

        File-system artifact publishing:
          --hosting-root <directory>
          --public-base-uri <uri>

        HTTP PUT artifact publishing:
          --artifact-store http-put
          --artifact-upload-uri <presigned-upload-uri>
          --artifact-public-uri <cdn-public-artifact-uri>

        Signing key source, choose exactly one:
          --signing-key <ecdsa-private-key-pem-path>
          --signing-key-env-var <environment-variable-containing-ecdsa-private-key-pem>

        Optional:
          --artifact-store <file-system|http-put>
          --published-file-name <file-name>
          --output <create-update-package-request-json-path>
        """;
}
