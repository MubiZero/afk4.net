namespace AFK4.Agent.Service.Tests;

/// <summary>Каталог на время одной проверки. Сам каталог создаёт тот, кто в него пишет.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryDirectory Create()
    {
        return new TemporaryDirectory(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"afk4-agent-{Guid.NewGuid():N}"));
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
