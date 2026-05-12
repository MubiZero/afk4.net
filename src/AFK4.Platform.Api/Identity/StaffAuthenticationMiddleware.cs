namespace AFK4.Platform.Api.Identity;

public sealed class StaffAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IStaffTokenService tokenService,
        IStaffContextAccessor staffContextAccessor)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";

        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[bearerPrefix.Length..].Trim();
            staffContextAccessor.Current = await tokenService.ValidateAsync(token, httpContext.RequestAborted);
        }

        await next(httpContext);
    }
}
