using System.Security.Cryptography;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Закрытый ключ этой машины: им она подписывает заявку на регистрацию.
///
/// Файл запирается так же, как bootstrap.json. Раньше он писался обычным файлом в
/// <c>%ProgramData%</c> — то есть был доступен на чтение всем локальным учётным записям, включая
/// ту, под которой за игровым ПК сидит гость.
/// </summary>
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
        RestrictedFile.RestrictToSystemAndAdministrators(
            keyPath,
            exception => SetupWizardStartupLog.Write(
                $"Could not restrict access to the device key file '{keyPath}'. It stays readable by local users.",
                exception));

        return key.ExportSubjectPublicKeyInfoPem();
    }
}
