using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AFK4.OrganizationAdmin.App.Offline;

/// <summary>
/// Ключ этого ПК администратора для офлайн-аренд (docs/roadmap/offline-front-desk.md). Создаётся при первом
/// обращении и лежит под DPAPI текущего пользователя Windows: другой пользователь того же ПК и копия
/// файла на другой машине подписать им не смогут. Наружу уходит только публичная половина.
/// </summary>
public sealed class OfflineDeskKey : IDisposable
{
    private OfflineDeskKey(string keyId, ECDsa key)
    {
        KeyId = keyId;
        Key = key;
    }

    public string KeyId { get; }

    public ECDsa Key { get; }

    public string PublicKeyPem => Key.ExportSubjectPublicKeyInfoPem();

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AFK4", "OrganizationAdmin", "offline-lease-key.bin");

    public static OfflineDeskKey LoadOrCreate(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<StoredKey>(
                    ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, DataProtectionScope.CurrentUser));
                if (stored is not null)
                {
                    var key = ECDsa.Create();
                    key.ImportPkcs8PrivateKey(stored.Pkcs8, out _);
                    return new OfflineDeskKey(stored.KeyId, key);
                }
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                // Файл не расшифровать (другой пользователь, переустановка) — создаём новый ключ.
                // Старый сервер отзовёт управляющий; агенты верят только зарегистрированным.
            }
        }

        var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "desk-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = ProtectedData.Protect(
            JsonSerializer.SerializeToUtf8Bytes(new StoredKey(keyId, created.ExportPkcs8PrivateKey())),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
        return new OfflineDeskKey(keyId, created);
    }

    public void Dispose() => Key.Dispose();

    private sealed record StoredKey(string KeyId, byte[] Pkcs8);
}
