namespace AFK4.Platform.Api.Identity;

public sealed class PlayerAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IPlayerTokenService tokenService,
        IPlayerContextAccessor playerContextAccessor)
    {
        // Only resolve a player principal for the player edge; never on staff/admin routes.
        if (httpContext.Request.Path.StartsWithSegments("/api/me", StringComparison.OrdinalIgnoreCase))
        {
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            const string bearerPrefix = "Bearer ";

            if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = authorization[bearerPrefix.Length..].Trim();
                playerContextAccessor.Current =
                    await tokenService.ValidateAsync(token, httpContext.RequestAborted);
            }
        }

        await next(httpContext);
    }
}
