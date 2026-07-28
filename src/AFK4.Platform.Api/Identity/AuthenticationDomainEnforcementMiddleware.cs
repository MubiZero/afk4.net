using AFK4.Platform.Api.Platform.Identity;

namespace AFK4.Platform.Api.Identity;

public sealed class AuthenticationDomainEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IStaffContextAccessor staffContextAccessor,
        IPlatformAdminContextAccessor platformContextAccessor)
    {
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<AuthenticationDomainMetadata>();
        if (metadata is null)
        {
            await next(httpContext);
            return;
        }

        var hasOppositeDomain = metadata.Domain switch
        {
            AuthenticationDomain.Platform => staffContextAccessor.Current is not null,
            AuthenticationDomain.Organization => platformContextAccessor.Current is not null
                && httpContext.GetEndpoint()?.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is null,
            _ => true
        };

        if (hasOppositeDomain)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (metadata.Domain == AuthenticationDomain.Organization
            && staffContextAccessor.Current is { } staffContext
            && httpContext.Request.RouteValues.TryGetValue("organizationId", out var routeValue)
            && (!Guid.TryParse(Convert.ToString(routeValue), out var routeOrganizationId)
                || routeOrganizationId != staffContext.OrganizationId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }
}
