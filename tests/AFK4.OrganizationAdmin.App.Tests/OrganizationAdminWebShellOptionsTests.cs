using AFK4.OrganizationAdmin.Web;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminWebShellOptionsTests
{
    [Fact]
    public void LoadFromEnvironment_UsesEmptyDefault()
    {
        var options = OrganizationAdminWebShellOptions.LoadFromEnvironment(_ => null);

        Assert.Null(options.DevServerUrl);
    }

    [Fact]
    public void LoadFromEnvironment_UsesDevServerEnvironmentVariable()
    {
        var options = OrganizationAdminWebShellOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminWebShellOptions.DevServerUrlEnvironmentVariable
                ? "http://127.0.0.1:5174"
                : null);

        Assert.Equal(new Uri("http://127.0.0.1:5174"), options.DevServerUrl);
    }

    [Theory]
    [InlineData("http://localhost:5174")]
    [InlineData("http://[::1]:5174")]
    public void LoadFromEnvironment_AllowsLoopbackDevServerUrls(string value)
    {
        var options = OrganizationAdminWebShellOptions.LoadFromEnvironment(name =>
            name == OrganizationAdminWebShellOptions.DevServerUrlEnvironmentVariable
                ? value
                : null);

        Assert.Equal(new Uri(value), options.DevServerUrl);
    }

    [Theory]
    [InlineData("file:///tmp/afk4")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/operator")]
    public void LoadFromEnvironment_RejectsInvalidDevServerUrl(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrganizationAdminWebShellOptions.LoadFromEnvironment(name =>
                name == OrganizationAdminWebShellOptions.DevServerUrlEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(OrganizationAdminWebShellOptions.DevServerUrlEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }
}
