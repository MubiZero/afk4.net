namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Хранилище ключа этой машины: файл рядом с остальным состоянием агента.
///
/// Ключ должен переживать перезапуск службы и не теряться от битого файла — иначе смена ключа
/// превращается в ту же поездку к ПК, от которой она и лечит. Как ключ меняется по просьбе
/// клуба, проверяет <see cref="WorkerTests"/>.
/// </summary>
public sealed class DeviceCredentialStoreTests
{
    // Ключ переживает перезапуск службы: иначе после ребута ПК предъявлял бы старый.
    [Fact]
    public void StoredSecret_SurvivesARestart()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "afk4-credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            var store = new FileDeviceCredentialStore(stateDirectory, "provisioned");
            Assert.Equal("provisioned", store.Current);

            store.Update("rotated");

            var afterRestart = new FileDeviceCredentialStore(stateDirectory, "provisioned");
            Assert.Equal("rotated", afterRestart.Current);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    // Битый файл — не повод остаться без входа: откатываемся на ключ из конфига.
    [Fact]
    public void BrokenCredentialFile_FallsBackToTheProvisionedSecret()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "afk4-credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(stateDirectory, FileDeviceCredentialStore.CredentialFileName),
                "{ not json");

            var store = new FileDeviceCredentialStore(stateDirectory, "provisioned");

            Assert.Equal("provisioned", store.Current);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    // Ключ устройства — право говорить от имени этого ПК. За игровым ПК сидит гость под обычной
    // учётной записью: файл с унаследованными правами он читает вместе со всей папкой.
    [WindowsOnlyFact]
    public void RotatedSecret_IsNotReadableThroughInheritedPermissions()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "afk4-credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            new FileDeviceCredentialStore(stateDirectory, "provisioned").Update("rotated");

            var permissions = ReadPermissions(Path.Combine(stateDirectory, FileDeviceCredentialStore.CredentialFileName));

            // «(I)» отмечает унаследованное право. Имена учётных записей на разных языках Windows
            // пишутся по-разному, а эта пометка — нет.
            Assert.DoesNotContain("(I)", permissions, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static string ReadPermissions(string path)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            ArgumentList = { path },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        return output;
    }

    [Fact]
    public void EmptySecret_IsRefusedInsteadOfWipingTheWorkingOne()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "afk4-credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            var store = new FileDeviceCredentialStore(stateDirectory, "provisioned");

            Assert.Throws<ArgumentException>(() => store.Update("  "));
            Assert.Equal("provisioned", store.Current);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
