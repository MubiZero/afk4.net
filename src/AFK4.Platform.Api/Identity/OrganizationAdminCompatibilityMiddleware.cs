using AFK4.Platform.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

public sealed class OrganizationAdminCompatibilityMiddleware(RequestDelegate next)
{
    public const string ProductHeader = "X-AFK4-Product";
    public const string EpochHeader = "X-AFK4-Compatibility-Epoch";
    public const string VersionHeader = "X-AFK4-Client-Version";
    public const string ProductName = "organization-admin";

    public async Task InvokeAsync(
        HttpContext httpContext,
        IOptions<OrganizationAdminCompatibilityOptions> options)
    {
        var domain = httpContext.GetEndpoint()?.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain;
        if (domain != AuthenticationDomain.Organization)
        {
            await next(httpContext);
            return;
        }

        var policy = options.Value;
        var product = httpContext.Request.Headers[ProductHeader].ToString();
        var epochValue = httpContext.Request.Headers[EpochHeader].ToString();
        var version = httpContext.Request.Headers[VersionHeader].ToString();

        if (!string.Equals(product, ProductName, StringComparison.Ordinal)
            || !int.TryParse(epochValue, out var epoch)
            || epoch != policy.RequiredEpoch
            || string.IsNullOrWhiteSpace(version))
        {
            httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                code = "organization_admin_upgrade_required",
                requiredEpoch = policy.RequiredEpoch,
                downloadUrl = policy.DownloadUrl
            }, httpContext.RequestAborted);
            return;
        }

        await next(httpContext);
    }
}
