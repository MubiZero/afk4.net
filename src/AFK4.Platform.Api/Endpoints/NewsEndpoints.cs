using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.News;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class NewsEndpoints
{
    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("news", async (
            StaffAuthorizationService authorizationService,
            EfNewsService news,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var scope = await NewsScope.ForAsync(staff, db, ct);
            var items = await news.ListForOwnerAsync(staff.OrganizationId, ct);
            return Results.Ok(items.Where(item => scope.Allows(item.BranchId)).ToList());
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageNews);

        // Выбор «где показывать» в форме новости: свои филиалы и «на всю сеть», если можно. Общий
        // список филиалов (`GET branches`) требует права владельца — управляющему он не откроется.
        app.MapGet("news/scope", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var scope = await NewsScope.ForAsync(authorization.StaffContext!, db, ct);
            return Results.Ok(new NewsScopeDto(
                scope.Branches.Select(branch => new OwnerBranchSummaryDto(branch.Key, branch.Value)).OrderBy(branch => branch.Name).ToList(),
                scope.OrganizationWide));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageNews);

        app.MapGet("branches", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewBranches);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var branches = await db.Branches.AsNoTracking()
                .Where(branch => branch.OrganizationId == authorization.StaffContext!.OrganizationId)
                .OrderBy(branch => branch.Name)
                .Select(branch => new OwnerBranchSummaryDto(branch.BranchId, branch.Name))
                .ToListAsync(ct);
            return Results.Ok(branches);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewBranches);

        app.MapPost("news", async (
            CreateNewsItemRequest request,
            StaffAuthorizationService authorizationService,
            EfNewsService news,
            PlatformDbContext db,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var scope = await NewsScope.ForAsync(staff, db, ct);
            if (!scope.Allows(request.BranchId)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await news.CreateAsync(staff.OrganizationId, request, ct);
            if (result.Outcome == NewsMutationOutcome.ValidationFailed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["news"] = [result.Error!] });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.CreateNews,
                TargetType: "NewsItem",
                TargetId: result.Item!.Id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Item);
        });

        app.MapPatch("news/{id:guid}", async (
            Guid id,
            UpdateNewsItemRequest request,
            StaffAuthorizationService authorizationService,
            EfNewsService news,
            PlatformDbContext db,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            // Нельзя ни править чужую новость, ни перенести свою туда, где писать нельзя.
            var scope = await NewsScope.ForAsync(staff, db, ct);
            var existing = await ExistingBranchAsync(db, staff.OrganizationId, id, ct);
            if (existing is null) return Results.NotFound();
            if (!scope.Allows(existing.BranchId) || !scope.Allows(request.BranchId)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await news.UpdateAsync(staff.OrganizationId, id, request, ct);
            if (result.Outcome == NewsMutationOutcome.NotFound) return Results.NotFound();
            if (result.Outcome == NewsMutationOutcome.ValidationFailed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["news"] = [result.Error!] });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateNews,
                TargetType: "NewsItem",
                TargetId: id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Item);
        });

        app.MapDelete("news/{id:guid}", async (
            Guid id,
            StaffAuthorizationService authorizationService,
            EfNewsService news,
            PlatformDbContext db,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var scope = await NewsScope.ForAsync(staff, db, ct);
            var existing = await ExistingBranchAsync(db, staff.OrganizationId, id, ct);
            if (existing is null) return Results.NotFound();
            if (!scope.Allows(existing.BranchId)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var outcome = await news.DeleteAsync(staff.OrganizationId, id, ct);
            if (outcome == NewsMutationOutcome.NotFound) return Results.NotFound();

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.DeleteNews,
                TargetType: "NewsItem",
                TargetId: id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: "{}"), ct);

            return Results.NoContent();
        });
    }

    private sealed record ExistingNews(Guid? BranchId);

    /// <summary>Филиал новости до правки; null — такой новости у организации нет.</summary>
    private static Task<ExistingNews?> ExistingBranchAsync(PlatformDbContext db, Guid organizationId, Guid id, CancellationToken ct) =>
        db.NewsItems.AsNoTracking()
            .Where(news => news.Id == id && news.OrganizationId == organizationId)
            .Select(news => new ExistingNews(news.BranchId))
            .SingleOrDefaultAsync(ct);
}
