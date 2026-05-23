using System.Text.Json;
using AFK4.Platform.Api.Platform.Tenancy;

namespace AFK4.Platform.Api.Identity;

public sealed class TenantSuspensionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(
        HttpContext httpContext,
        IStaffContextAccessor staffContextAccessor,
        ITenantStatusGuard tenantStatusGuard)
    {
        var staffContext = staffContextAccessor.Current;
        if (staffContext is not null && MutatingMethods.Contains(httpContext.Request.Method))
        {
            var snapshot = await tenantStatusGuard.GetAsync(staffContext.OrganizationId, httpContext.RequestAborted);
            if (snapshot is not null && !snapshot.IsActive)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                httpContext.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(
                    new
                    {
                        Error = "TenantSuspended",
                        Status = snapshot.Status,
                        Reason = snapshot.Reason
                    },
                    CamelCaseJson);
                await httpContext.Response.WriteAsync(payload, httpContext.RequestAborted);
                return;
            }
        }

        await next(httpContext);
    }
}
