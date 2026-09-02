using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class AuthorizationHeaderPolicyTests
{
    private const string ApiBase = "https://api.afk4.net";

    [Fact]
    public void Injects_WhenRequestMatchesApiOriginAndTokenPresent()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            requestUri: "https://api.afk4.net/api/me/dashboard",
            apiBaseUrl: ApiBase,
            accessToken: "tok123");

        Assert.True(decision.ShouldInject);
        Assert.Equal("Bearer tok123", decision.HeaderValue);
    }

    [Fact]
    public void DoesNotInject_WhenTokenMissing()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://api.afk4.net/api/me/dashboard", ApiBase, accessToken: null);

        Assert.False(decision.ShouldInject);
    }

    [Fact]
    public void DoesNotInject_ForForeignOrigin()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://evil.example.com/api/me/dashboard", ApiBase, accessToken: "tok123");

        Assert.False(decision.ShouldInject);
    }

    [Fact]
    public void DoesNotInject_ForLocalVirtualHostAssets()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://player.afk4.local/index.html", ApiBase, accessToken: "tok123");

        Assert.False(decision.ShouldInject);
    }
}
