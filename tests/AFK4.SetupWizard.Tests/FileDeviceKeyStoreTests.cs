using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class FileDeviceKeyStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "afk4-setup-wizard-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetOrCreatePublicKeyPemAsync_ReusesExistingPrivateKey()
    {
        var keyPath = Path.Combine(directory, "device-key.pem");
        var store = new FileDeviceKeyStore(keyPath);

        var first = await store.GetOrCreatePublicKeyPemAsync(CancellationToken.None);
        var second = await new FileDeviceKeyStore(keyPath).GetOrCreatePublicKeyPemAsync(CancellationToken.None);

        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
        Assert.True(File.Exists(keyPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
