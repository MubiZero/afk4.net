using System.Text.Json;
using AFK4.Operator.App.Configuration;
using AFK4.Operator.App.Web;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorWebBootstrapScriptTests
{
    [Fact]
    public void Create_InjectsCamelCaseHostConfig()
    {
        var appOptions = new OperatorAppOptions
        {
            PlatformBaseUrl = new Uri("https://afk4.staging.mubi.dev/"),
            CurrencyCode = "USD",
            OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            BranchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        };
        var launchTarget = new OperatorWebShellLaunchTarget(
            new Uri("https://operator.afk4.local/index.html"),
            "vite-dist",
            @"C:\afk4\dist");

        var script = OperatorWebBootstrapScript.Create(appOptions, launchTarget, "2.45.1");

        Assert.StartsWith("window.__AFK4_OPERATOR_CONFIG__ = ", script, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(script["window.__AFK4_OPERATOR_CONFIG__ = ".Length..].TrimEnd(';'));
        var root = document.RootElement;
        Assert.Equal("webview2", root.GetProperty("runtime").GetString());
        Assert.Equal("vite-dist", root.GetProperty("shellMode").GetString());
        Assert.Equal("https://afk4.staging.mubi.dev/", root.GetProperty("platformBaseUrl").GetString());
        Assert.Equal("USD", root.GetProperty("currencyCode").GetString());
        Assert.Equal("2.45.1", root.GetProperty("appVersion").GetString());
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", root.GetProperty("organizationId").GetString());
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", root.GetProperty("branchId").GetString());
    }
}
