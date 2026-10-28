using AFK4.Agent.Service.Network;
using AFK4.Agent.Service.Protection;
using AFK4.Agent.Service.Uninstall;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Уборка при удалении агента: машинные запреты снимаются все, какие агент мог поставить при любом профиле,
/// а из hosts уходит только блок кеша игр.
/// </summary>
public sealed class UninstallCleanupTests : IDisposable
{
    private readonly string hostsPath = Path.Combine(Directory.CreateTempSubdirectory("afk4-uninstall-").FullName, "hosts");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(hostsPath)!, recursive: true);

    [Fact]
    public void EveryMachinePolicyTheAgentCouldWriteIsRemoved()
    {
        var policies = UninstallCleanup.MachinePolicies();

        Assert.All(policies, write => Assert.Equal(PolicyScope.Machine, write.Scope));
        Assert.Contains(policies, write => write.Name == "HideFastUserSwitching");
        Assert.Contains(policies, write => write.Key == ProtectionPolicy.Chrome && write.Name == "URLBlocklist" && write.IsList);
        Assert.Contains(policies, write => write.Key == ProtectionPolicy.Edge && write.Name == "DownloadRestrictions");
        Assert.Contains(ProtectionPolicy.TaskManager, policies);
        // Копии игроцких запретов в HKLM от прежних версий агента.
        Assert.Contains(policies, write => write.Key == ProtectionPolicy.ExplorerPolicies && write.Name == "NoRun");
        Assert.Contains(policies, write => write.Key == ProtectionPolicy.ExplorerPolicies && write.Name == "NoDrives");
    }

    [Fact]
    public void OnlyTheGameCacheBlockLeavesHosts()
    {
        const string foreign = "127.0.0.1 localhost\r\n10.0.0.5 printer.local\r\n";
        File.WriteAllText(hostsPath, GameCacheHosts.Rewrite(foreign, "192.168.1.50"));

        UninstallCleanup.ClearHostsFile(hostsPath);

        Assert.Equal(foreign, File.ReadAllText(hostsPath));
    }

    [Fact]
    public void HostsWithoutTheBlockIsNotRewritten()
    {
        const string foreign = "127.0.0.1 localhost\n\n\n";
        File.WriteAllText(hostsPath, foreign);
        var written = File.GetLastWriteTimeUtc(hostsPath).AddMinutes(-5);
        File.SetLastWriteTimeUtc(hostsPath, written);

        UninstallCleanup.ClearHostsFile(hostsPath);

        Assert.Equal(foreign, File.ReadAllText(hostsPath));
        Assert.Equal(written, File.GetLastWriteTimeUtc(hostsPath));
    }
}
