namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformAdminAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IPlatformAdminTokenService tokenService,
        IPlatformAdminContextAccessor contextAccessor)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";

        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[bearerPrefix.Length..].Trim();
            contextAccessor.Current = await tokenService.ValidateAsync(token, httpContext.RequestAborted);
        }

        await next(httpContext);
    }
}
