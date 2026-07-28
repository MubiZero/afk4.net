using AFK4.OrganizationAdmin.App.Configuration;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminOptionsTests
{
    [Fact]
    public void LoadFromEnvironment_UsesLocalhostDefault()
    {
        var options = OrganizationAdminOptions.LoadFromEnvironment(_ => null);

        Assert.Equal(new Uri("http://localhost:5074"), options.PlatformBaseUrl);
        Assert.Equal("TJS", options.CurrencyCode);
        Assert.Equal("ru", options.PreferredLocale);
    }

    [Fact]
    public void LoadFromEnvironment_UsesPreferredLocaleEnvironmentVariable()
    {
        var options = OrganizationAdminOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminOptions.PreferredLocaleEnvironmentVariable ? "tg" : null);

        Assert.Equal("tg", options.PreferredLocale);
    }

    [Fact]
    public void LoadFromEnvironment_ClampsUnknownPreferredLocaleToRu()
    {
        var options = OrganizationAdminOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminOptions.PreferredLocaleEnvironmentVariable ? "fr" : null);

        Assert.Equal("ru", options.PreferredLocale);
    }

    [Fact]
    public void LoadFromEnvironment_UsesPlatformBaseUrlEnvironmentVariable()
    {
        var options = OrganizationAdminOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminOptions.PlatformBaseUrlEnvironmentVariable
                ? "https://afk4.staging.mubi.dev/"
                : null);

        Assert.Equal(new Uri("https://afk4.staging.mubi.dev/"), options.PlatformBaseUrl);
    }

    [Theory]
    [InlineData("file:///tmp/afk4")]
    [InlineData("not-a-url")]
    public void LoadFromEnvironment_RejectsInvalidPlatformBaseUrl(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrganizationAdminOptions.LoadFromEnvironment(name =>
                name == OrganizationAdminOptions.PlatformBaseUrlEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(OrganizationAdminOptions.PlatformBaseUrlEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironment_UsesCurrencyCodeEnvironmentVariable()
    {
        var options = OrganizationAdminOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminOptions.CurrencyCodeEnvironmentVariable
                ? "usd"
                : null);

        Assert.Equal("USD", options.CurrencyCode);
    }

    [Fact]
    public void LoadFromEnvironment_UsesOrganizationAndBranchEnvironmentVariables()
    {
        var organizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var branchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var options = OrganizationAdminOptions.LoadFromEnvironment(name =>
            name switch
            {
                OrganizationAdminOptions.OrganizationIdEnvironmentVariable => organizationId.ToString(),
                OrganizationAdminOptions.BranchIdEnvironmentVariable => branchId.ToString(),
                _ => null
            });

        Assert.Equal(organizationId, options.OrganizationId);
        Assert.Equal(branchId, options.BranchId);
    }

    [Theory]
    [InlineData("TJ")]
    [InlineData("USDD")]
    [InlineData("12$")]
    public void LoadFromEnvironment_RejectsInvalidCurrencyCode(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrganizationAdminOptions.LoadFromEnvironment(name =>
                name == OrganizationAdminOptions.CurrencyCodeEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(OrganizationAdminOptions.CurrencyCodeEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OrganizationAdminOptions.OrganizationIdEnvironmentVariable)]
    [InlineData(OrganizationAdminOptions.BranchIdEnvironmentVariable)]
    public void LoadFromEnvironment_RejectsInvalidGuidEnvironmentVariables(string environmentVariableName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrganizationAdminOptions.LoadFromEnvironment(name =>
                name == environmentVariableName
                    ? "not-a-guid"
                    : null));

        Assert.Contains(environmentVariableName, exception.Message, StringComparison.Ordinal);
    }
}
