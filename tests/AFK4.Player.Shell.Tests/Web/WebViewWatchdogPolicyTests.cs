using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class WebViewWatchdogPolicyTests
{
    [Fact]
    public void Healthy_NoFailure_KeepsWebVisible()
    {
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: false, Unresponsive: false));

        Assert.False(action.ShowFallback);
        Assert.False(action.RestartWebView);
    }

    [Fact]
    public void RenderProcessFailed_ShowsFallbackAndRestarts()
    {
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: true, Unresponsive: false));

        Assert.True(action.ShowFallback);
        Assert.True(action.RestartWebView);
    }

    [Fact]
    public void Unresponsive_ShowsFallbackButDoesNotRestartYet()
    {
        // An unresponsive page may recover; cover the desktop but give it a chance
        // before killing the process.
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: false, Unresponsive: true));

        Assert.True(action.ShowFallback);
        Assert.False(action.RestartWebView);
    }
}
