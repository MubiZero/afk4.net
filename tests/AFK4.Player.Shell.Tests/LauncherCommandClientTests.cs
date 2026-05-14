using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Launcher;

namespace AFK4.Player.Shell.Tests;

public sealed class LauncherCommandClientTests
{
    [Fact]
    public async Task LaunchAsync_WhenAgentPipeUnavailable_ReturnsRejectedResult()
    {
        var client = new LauncherCommandClient(new PlayerShellOptions
        {
            CommandPipeName = $"afk4-missing-{Guid.NewGuid():N}",
            PipeConnectionTimeoutMilliseconds = 1
        });

        var result = await client.LaunchAsync("counter-strike-2", CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("not available", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
