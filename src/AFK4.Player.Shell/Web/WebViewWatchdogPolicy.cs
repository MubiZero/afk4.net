namespace AFK4.Player.Shell.Web;

public readonly record struct WebViewHealthSignal(bool ProcessFailed, bool Unresponsive);

public readonly record struct WebViewWatchdogAction(bool ShowFallback, bool RestartWebView);

public static class WebViewWatchdogPolicy
{
    public static WebViewWatchdogAction Decide(WebViewHealthSignal signal)
    {
        if (signal.ProcessFailed)
        {
            return new WebViewWatchdogAction(ShowFallback: true, RestartWebView: true);
        }

        if (signal.Unresponsive)
        {
            return new WebViewWatchdogAction(ShowFallback: true, RestartWebView: false);
        }

        return new WebViewWatchdogAction(ShowFallback: false, RestartWebView: false);
    }
}
