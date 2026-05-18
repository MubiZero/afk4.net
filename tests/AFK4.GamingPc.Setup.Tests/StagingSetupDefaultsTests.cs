using AFK4.GamingPc.Setup.Core;

namespace AFK4.GamingPc.Setup.Tests;

public sealed class StagingSetupDefaultsTests
{
    [Fact]
    public void Defaults_TargetCurrentStagingSmokeEnvironment()
    {
        Assert.Equal(new Uri("https://afk4.staging.mubi.dev"), StagingSetupDefaults.PlatformBaseUrl);
        Assert.Equal(Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"), StagingSetupDefaults.OrganizationId);
        Assert.Equal(Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"), StagingSetupDefaults.BranchId);
        Assert.Equal(Guid.Parse("9f3adbd3-957e-4dc8-8d34-a6bfa56b9275"), StagingSetupDefaults.SmokeSeatId);
        Assert.Equal("AFK4.Agent.Service", StagingSetupDefaults.AgentServiceName);
        Assert.Equal("internal", StagingSetupDefaults.UpdateChannel);
        Assert.EndsWith(@"AFK4\Player Shell\AFK4.Player.Shell.exe", StagingSetupDefaults.PlayerShellExecutablePath);
    }
}
