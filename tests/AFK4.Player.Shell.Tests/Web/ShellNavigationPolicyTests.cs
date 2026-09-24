using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>Страница оболочки уходит только в саму себя: из браузера поверх киоска до рабочего стола один шаг.</summary>
public sealed class ShellNavigationPolicyTests
{
    private const string App = "https://afk4-player.local/index.html";

    [Theory]
    [InlineData("https://afk4-player.local/index.html#/session", true)]
    [InlineData("https://afk4-player.local/assets/app.js", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("http://afk4-player.local/index.html", false)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void OnlyTheShellItself_IsAllowed(string? target, bool allowed)
    {
        Assert.Equal(allowed, ShellNavigationPolicy.IsAllowed(target, App));
    }

    [Fact]
    public void BeforeThePageIsKnown_NothingIsAllowed()
    {
        Assert.False(ShellNavigationPolicy.IsAllowed("https://afk4-player.local/index.html", null));
    }
}
