using System.Security.Cryptography;

namespace AFK4.SetupWizard.Core;

public sealed class FileDeviceKeyStore(string keyPath) : IDeviceKeyStore
{
    public FileDeviceKeyStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AFK4",
            "SetupWizard",
            "device-key.pem"))
    {
    }

    public async Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(keyPath))
        {
            var existingPem = await File.ReadAllTextAsync(keyPath, cancellationToken);
            key.ImportFromPem(existingPem);
            return key.ExportSubjectPublicKeyInfoPem();
        }

        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var privatePem = key.ExportECPrivateKeyPem();
        await File.WriteAllTextAsync(keyPath, privatePem, cancellationToken);
        return key.ExportSubjectPublicKeyInfoPem();
    }
}
