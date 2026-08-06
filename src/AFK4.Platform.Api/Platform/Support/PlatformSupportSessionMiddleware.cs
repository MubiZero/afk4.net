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

        var support = await supportAccessService.AuthenticateSessionAsync(
            header, metadata?.Permission ?? string.Empty, context.RequestAborted);

        if (support is null)
        {
            // Заголовок, который не резолвится в живую сессию, — это отклонённый credential, а не
            // отсутствующий: теперь это единственный механизм, читающий данный заголовок, так что
            // тихий пропуск дальше позволил бы протухшему/поддельному/просроченному токену дойти до
            // эндпоинта как до неаутентифицированного запроса вместо явного отказа.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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
