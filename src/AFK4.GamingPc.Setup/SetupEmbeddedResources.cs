using System.Reflection;
using System.IO;

namespace AFK4.GamingPc.Setup;

public sealed class SetupEmbeddedResources(Assembly assembly)
{
    private const string MsiResourceName = "AFK4.GamingPc.Setup.Resources.gaming-pc.msi";
    private const string LeasePublicKeyResourceName = "AFK4.GamingPc.Setup.Resources.staging-session-signing-public.pem";

    public string ReadLeasePublicKeyPem()
    {
        using var stream = OpenRequired(LeasePublicKeyResourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<string> ExtractMsiAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AFK4", "GamingPcSetup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "afk4-gaming-pc.msi");
        await using var input = OpenRequired(MsiResourceName);
        await using var output = File.Create(path);
        await input.CopyToAsync(output, cancellationToken);
        return path;
    }

    private Stream OpenRequired(string resourceName)
    {
        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Required setup resource '{resourceName}' was not embedded.");
    }
}
