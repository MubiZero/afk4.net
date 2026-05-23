using AFK4.Operator.App.Configuration;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorAppOptionsTests
{
    [Fact]
    public void LoadFromEnvironment_UsesLocalhostDefault()
    {
        var options = OperatorAppOptions.LoadFromEnvironment(_ => null);

        Assert.Equal(new Uri("http://localhost:5074"), options.PlatformBaseUrl);
        Assert.Equal("TJS", options.CurrencyCode);
    }

    [Fact]
    public void LoadFromEnvironment_UsesPlatformBaseUrlEnvironmentVariable()
    {
        var options = OperatorAppOptions.LoadFromEnvironment(name =>
            name == OperatorAppOptions.PlatformBaseUrlEnvironmentVariable
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
            OperatorAppOptions.LoadFromEnvironment(name =>
                name == OperatorAppOptions.PlatformBaseUrlEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(OperatorAppOptions.PlatformBaseUrlEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironment_UsesCurrencyCodeEnvironmentVariable()
    {
        var options = OperatorAppOptions.LoadFromEnvironment(name =>
            name == OperatorAppOptions.CurrencyCodeEnvironmentVariable
                ? "usd"
                : null);

        Assert.Equal("USD", options.CurrencyCode);
    }

    [Fact]
    public void LoadFromEnvironment_UsesOrganizationAndBranchEnvironmentVariables()
    {
        var organizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var branchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var options = OperatorAppOptions.LoadFromEnvironment(name =>
            name switch
            {
                OperatorAppOptions.OrganizationIdEnvironmentVariable => organizationId.ToString(),
                OperatorAppOptions.BranchIdEnvironmentVariable => branchId.ToString(),
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
            OperatorAppOptions.LoadFromEnvironment(name =>
                name == OperatorAppOptions.CurrencyCodeEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(OperatorAppOptions.CurrencyCodeEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OperatorAppOptions.OrganizationIdEnvironmentVariable)]
    [InlineData(OperatorAppOptions.BranchIdEnvironmentVariable)]
    public void LoadFromEnvironment_RejectsInvalidGuidEnvironmentVariables(string environmentVariableName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OperatorAppOptions.LoadFromEnvironment(name =>
                name == environmentVariableName
                    ? "not-a-guid"
                    : null));

        Assert.Contains(environmentVariableName, exception.Message, StringComparison.Ordinal);
    }
}
