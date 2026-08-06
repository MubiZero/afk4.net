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
            INewsService news,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var items = await news.ListForOwnerAsync(authorization.StaffContext!.OrganizationId, ct);
            return Results.Ok(items);
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
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
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
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
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
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
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
}
