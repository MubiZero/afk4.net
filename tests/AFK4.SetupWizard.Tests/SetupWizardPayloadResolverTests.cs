using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class SetupWizardPayloadResolverTests
{
    [Fact]
    public void ResolvePlayerShellMsiPath_UsesPayloadSubfolderNextToBaseDirectory()
    {
        string? probed = null;
        var resolver = new SetupWizardPayloadResolver(
            path => { probed = path; return true; },
            () => System.IO.Path.Combine(@"C:\Program Files\AFK4\Setup Wizard", "payload", "AFK4.Player.Shell.msi"));

        var result = resolver.ResolvePlayerShellMsiPath();

        Assert.EndsWith(System.IO.Path.Combine("payload", "AFK4.Player.Shell.msi"), result);
        Assert.Equal(result, probed);
    }

    [Fact]
    public void ResolvePlayerShellMsiPath_WhenMissing_ReturnsNull()
    {
        var resolver = new SetupWizardPayloadResolver(_ => false, () => @"C:\nope\payload\AFK4.Player.Shell.msi");

        Assert.Null(resolver.ResolvePlayerShellMsiPath());
    }
}
