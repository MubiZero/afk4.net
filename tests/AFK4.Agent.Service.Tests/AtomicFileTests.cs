namespace AFK4.Agent.Service.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"afk4-atomic-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ReplacesTheWholeFileAndLeavesNoTemporaryFiles()
    {
        var path = Path.Combine(root, "nested", "lease.json");
        AtomicFile.WriteAllText(path, "{\"v\":1}");
        AtomicFile.WriteAllText(path, "{\"v\":2}");

        Assert.Equal("{\"v\":2}", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(path)!));
    }

    // Запись, оборвавшаяся посреди (здесь — исключением в сериализации), не трогает действующий файл:
    // при отключении света агент найдёт прежнюю аренду целиком, а не половину JSON.
    [Fact]
    public void Write_ThatFailsMidway_KeepsThePreviousFileIntact()
    {
        var path = Path.Combine(root, "lease.json");
        AtomicFile.WriteAllText(path, "{\"paidUntil\":\"18:00\"}");

        Assert.Throws<InvalidOperationException>(() => AtomicFile.Write(path, stream =>
        {
            stream.Write("{\"paidUn"u8);
            throw new InvalidOperationException("power cut");
        }));

        Assert.Equal("{\"paidUntil\":\"18:00\"}", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(root));
    }

    [Fact]
    public void Write_RunsTheHookOnTheTemporaryFileBeforeItBecomesLive()
    {
        var path = Path.Combine(root, "secret.json");
        string? hookedPath = null;

        AtomicFile.Write(path, stream => stream.Write("{}"u8), beforeReplace: temporary =>
        {
            hookedPath = temporary;
            Assert.False(File.Exists(path));
        });

        Assert.NotNull(hookedPath);
        Assert.NotEqual(path, hookedPath);
        Assert.True(File.Exists(path));
    }
}
