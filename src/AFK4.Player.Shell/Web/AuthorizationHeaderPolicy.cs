namespace AFK4.Player.Shell.Web;

public readonly record struct AuthorizationHeaderDecision(bool ShouldInject, string? HeaderValue);

/// Pure decision so the WebResourceRequested glue stays untestable-thin: only
/// requests to the configured API origin get the bearer header, and only when a
/// token is held. Foreign origins (and the local asset virtual host) never do.
public static class AuthorizationHeaderPolicy
{
    public static AuthorizationHeaderDecision Decide(string? requestUri, string? apiBaseUrl, string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)
            || !Uri.TryCreate(requestUri, UriKind.Absolute, out var request)
            || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBase))
        {
            return new AuthorizationHeaderDecision(false, null);
        }

        var sameOrigin = string.Equals(request.Scheme, apiBase.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Host, apiBase.Host, StringComparison.OrdinalIgnoreCase)
            && request.Port == apiBase.Port;

        return sameOrigin
            ? new AuthorizationHeaderDecision(true, $"Bearer {accessToken}")
            : new AuthorizationHeaderDecision(false, null);
    }
}
