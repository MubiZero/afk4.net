using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Showcase;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Endpoints;

internal static class ShowcaseEndpoints
{
    public static void MapShowcaseEndpoints(this WebApplication app)
    {
        // Агент спрашивает раз в 10 минут с ETag прошлого ответа: не изменилось — 304 без тела.
        app.MapGet("/api/devices/{deviceId:guid}/showcase", async (
            Guid deviceId,
            Guid organizationId,
            Guid branchId,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            DeviceShowcase showcase,
            CancellationToken cancellationToken) =>
        {
            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(organizationId, branchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspended = await organizationStatusGuard.RequireActiveAsync(organizationId, cancellationToken);
            if (suspended is not null)
            {
                return suspended;
            }

            var snapshot = await showcase.GetAsync(organizationId, branchId, cancellationToken);
            httpContext.Response.Headers.ETag = snapshot.ETag;
            var known = httpContext.Request.GetTypedHeaders().IfNoneMatch;
            return known.Any(tag => tag.Tag.Equals(snapshot.ETag, StringComparison.Ordinal))
                ? Results.StatusCode(StatusCodes.Status304NotModified)
                : Results.Ok(snapshot.Showcase);
        });
    }
}
