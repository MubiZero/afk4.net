using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class SetupWizardDefaultsTests
{
    [Fact]
    public void ResolvePlatformBaseUrl_WithNull_FallsBackToStaging()
    {
        Assert.Equal("https://api.afk4.net", SetupWizardDefaults.ResolvePlatformBaseUrl(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvePlatformBaseUrl_WithBlank_FallsBackToStaging(string injected)
    {
        Assert.Equal("https://api.afk4.net", SetupWizardDefaults.ResolvePlatformBaseUrl(injected));
    }

    [Fact]
    public void ResolvePlatformBaseUrl_WithInjectedValue_UsesItTrimmed()
    {
        Assert.Equal("https://app.afk4.net", SetupWizardDefaults.ResolvePlatformBaseUrl("  https://app.afk4.net  "));
    }

    [Fact]
    public void PlatformBaseUrl_DefaultBuild_IsStaging()
    {
        // The test assembly is built without the AFK4PlatformBaseUrl property, so the resolver
        // must fall back to staging — this guards the contract every other SetupWizard test relies on.
        Assert.Equal(new Uri("https://api.afk4.net"), SetupWizardDefaults.PlatformBaseUrl);
    }

    [Theory]
    [InlineData("https//app.afk4.net")]
    [InlineData("not a url")]
    [InlineData("ftp://app.afk4.net")]
    public void ResolvePlatformBaseUrl_WithInvalidInjectedValue_Throws(string injected)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SetupWizardDefaults.ResolvePlatformBaseUrl(injected));
        Assert.Contains("AFK4.PlatformBaseUrl", exception.Message, StringComparison.Ordinal);
    }
}
