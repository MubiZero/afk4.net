using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Support;

/// <summary>
/// Пускает платформенную поддержку в админку клиента по сессионному токену. Граница доступа —
/// метка <see cref="PlatformSupportAccessMetadata"/> на эндпоинте: без неё сессия не проходит,
/// поэтому денежные эндпоинты закрыты уже тем, что их никто не помечал.
/// </summary>
public sealed class PlatformSupportSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        PlatformSupportAccessGrantService supportAccessService,
        IStaffContextAccessor staffContextAccessor,
        IPlatformSupportContextAccessor supportContextAccessor,
        PlatformDbContext dbContext)
    {
        var header = context.Request.Headers[PlatformSupportAccessGrantService.GrantHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            await next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<PlatformSupportAccessMetadata>();

        // The same header name is also read, with different semantics (a raw grant id rather than an
        // opaque session token), by the older per-endpoint ValidateAsync/ValidateBranchAsync flow on
        // OrganizationAuditEndpoints/DiagnosticsEndpoints — that flow authenticates itself (platform-admin
        // bearer token + an explicit grant lookup) and must keep working unmodified. A header value that
        // isn't a live support session here is therefore not this middleware's concern: fall through and
        // let whatever the endpoint already does with it decide the outcome.
        var support = await supportAccessService.AuthenticateSessionAsync(
            header, metadata?.Permission ?? string.Empty, context.RequestAborted);

        if (support is null)
        {
            await next(context);
            return;
        }

        if (metadata is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var branchIds = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == support.OrganizationId)
            .Select(branch => branch.BranchId)
            .ToListAsync(context.RequestAborted);

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { metadata.Permission };

        supportContextAccessor.Current = support;
        staffContextAccessor.Current = new StaffContext(
            StaffUserId: Guid.Empty,
            OrganizationId: support.OrganizationId,
            DisplayName: "Поддержка платформы",
            BranchIds: branchIds.ToHashSet(),
            Permissions: permissions)
        {
            SupportAccess = support,
            PermissionsByBranch = branchIds.ToDictionary(
                branchId => branchId,
                _ => (IReadOnlySet<string>)permissions)
        };

        await next(context);
    }
}
