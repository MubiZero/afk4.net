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
            fileName => System.IO.Path.Combine(@"C:\Program Files\AFK4\Setup Wizard", "payload", fileName));

        var result = resolver.ResolvePlayerShellMsiPath();

        Assert.EndsWith(System.IO.Path.Combine("payload", "AFK4.Player.Shell.msi"), result);
        Assert.Equal(result, probed);
    }

    [Fact]
    public void ResolveOperatorAppMsiPath_UsesPayloadSubfolderNextToBaseDirectory()
    {
        var resolver = new SetupWizardPayloadResolver(
            _ => true,
            fileName => System.IO.Path.Combine(@"C:\Program Files\AFK4\Setup Wizard", "payload", fileName));

        var result = resolver.ResolveOperatorAppMsiPath();

        Assert.EndsWith(System.IO.Path.Combine("payload", "AFK4.OrganizationAdmin.App.msi"), result);
    }

    [Fact]
    public void ResolvePlayerShellMsiPath_WhenMissing_ReturnsNull()
    {
        var resolver = new SetupWizardPayloadResolver(_ => false, fileName => @"C:\nope\payload\" + fileName);

        Assert.Null(resolver.ResolvePlayerShellMsiPath());
    }
}
