using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Search;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformSearchEndpoints
{
    private const int MaxResults = 20;

    public static void MapPlatformSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/search", async (
            string? query,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizations);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty,
                    authorization.PlatformAdminContext!.PlatformAdminUserId, AuditActionNames.ViewOrganization,
                    "PlatformSearch", null, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalized = query?.Trim();
            if (normalized is null || normalized.Length < 2) return Results.Ok(Array.Empty<PlatformSearchResultDto>());
            var normalizedLower = normalized.ToLowerInvariant();
            var exactId = Guid.TryParse(normalized, out var parsedId) ? parsedId : (Guid?)null;

            var organizations = await dbContext.Organizations.AsNoTracking()
                .Where(row => (exactId.HasValue && row.OrganizationId == exactId.Value)
                    || row.Name.ToLower().Contains(normalizedLower)
                    || row.Slug.ToLower().Contains(normalizedLower))
                .OrderBy(row => row.Name).Take(MaxResults)
                .Select(row => new PlatformSearchResultDto("organization", row.OrganizationId, row.Name, row.Slug,
                    $"/admin/organizations/{row.OrganizationId}"))
                .ToListAsync(cancellationToken);

            var remaining = MaxResults - organizations.Count;
            if (remaining <= 0) return await AuditedResultAsync(organizations);

            var clubs = await (from branch in dbContext.Branches.AsNoTracking()
                join organization in dbContext.Organizations.AsNoTracking() on branch.OrganizationId equals organization.OrganizationId
                where (exactId.HasValue && branch.BranchId == exactId.Value)
                    || branch.Name.ToLower().Contains(normalizedLower)
                    || branch.Slug.ToLower().Contains(normalizedLower)
                    || branch.City.ToLower().Contains(normalizedLower)
                orderby branch.Name
                select new PlatformSearchResultDto("club", branch.BranchId, branch.Name,
                    organization.Name + " · " + branch.City,
                    "/admin/organizations/" + organization.OrganizationId + "?tab=clubs"))
                .Take(remaining).ToListAsync(cancellationToken);

            remaining -= clubs.Count;
            if (remaining <= 0) return await AuditedResultAsync(organizations.Concat(clubs));

            var owners = await (from staff in dbContext.StaffUsers.AsNoTracking()
                join role in dbContext.StaffRoleAssignments.AsNoTracking() on staff.StaffUserId equals role.StaffUserId
                join organization in dbContext.Organizations.AsNoTracking() on staff.OrganizationId equals organization.OrganizationId
                where role.RoleName == OrganizationRoleNames.OrganizationOwner
                    && ((exactId.HasValue && staff.StaffUserId == exactId.Value)
                        || staff.DisplayName.ToLower().Contains(normalizedLower)
                        || staff.UserName.ToLower().Contains(normalizedLower))
                orderby staff.DisplayName
                select new PlatformSearchResultDto("owner", staff.StaffUserId, staff.DisplayName,
                    organization.Name + " · " + staff.UserName,
                    "/admin/organizations/" + organization.OrganizationId + "?tab=access"))
                .Distinct().Take(remaining).ToListAsync(cancellationToken);

            return await AuditedResultAsync(organizations.Concat(clubs).Concat(owners));

            async Task<IResult> AuditedResultAsync(IEnumerable<PlatformSearchResultDto> source)
            {
                var results = source.ToArray();
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty,
                    authorization.PlatformAdminContext!.PlatformAdminUserId, AuditActionNames.ViewOrganization,
                    "PlatformSearch", null, AuditOutcome.Succeeded,
                    new { QueryLength = normalized.Length, Count = results.Length, Kinds = results.Select(item => item.Kind).Distinct() },
                    cancellationToken);
                return Results.Ok(results);
            }
        });
    }
}
