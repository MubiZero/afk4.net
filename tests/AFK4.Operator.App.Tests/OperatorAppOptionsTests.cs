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
}
