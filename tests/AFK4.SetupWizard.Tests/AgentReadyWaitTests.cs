using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class AgentReadyWaitTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"afk4-ready-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Запущенная служба без отметки — агент с негодной настройкой: мастер должен это сказать,
    // а не рапортовать «подключён».
    [Fact]
    public void WithoutTheMarker_ThrowsAfterTheTimeout()
    {
        Assert.Throws<AgentDidNotAcceptSettingsException>(() =>
            AgentReadyWait.ForMarker(AgentReadyMarker.PathIn(directory), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20)));
    }

    // Отметку пишет агент (тот же AgentReadyMarker), ждёт мастер: общий путь — единственная нить.
    [Fact]
    public async Task MarkerWrittenByTheAgentSide_EndsTheWait()
    {
        var writer = Task.Run(async () =>
        {
            await Task.Delay(150);
            AgentReadyMarker.Write(directory);
        });

        AgentReadyWait.ForMarker(AgentReadyMarker.PathIn(directory), TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(20));

        await writer;
    }
}
