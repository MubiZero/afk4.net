using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class FloorMapEndpoints
{
    public static void MapFloorMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/floor-map", async (
            Guid branchId,
            HttpContext httpContext,
            IFloorMapReadService floorMapReadService,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewFloorMap,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await floorMapReadService.GetFloorMapAsync(branchId, cancellationToken);

            if (result is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers.ETag = result.ETag;
            return Results.Ok(result.FloorMap);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewFloorMap);

        app.MapPut("branches/{branchId:guid}/floor-map", async (
            Guid branchId,
            FloorMapBulkUpdateRequest request,
            HttpContext httpContext,
            IFloorMapEditService floorMapEditService,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageLayout,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateFloorMap,
                    "FloorMap",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
            var result = await floorMapEditService.BulkUpdateAsync(
                request.OrganizationId,
                branchId,
                ifMatch,
                request,
                cancellationToken);

            switch (result.Status)
            {
                case FloorMapBulkUpdateStatus.PreconditionRequired:
                    return Results.Json(new { Error = result.Error }, statusCode: StatusCodes.Status428PreconditionRequired);
                case FloorMapBulkUpdateStatus.PreconditionFailed:
                    if (!string.IsNullOrEmpty(result.CurrentETag))
                    {
                        httpContext.Response.Headers.ETag = result.CurrentETag;
                    }
                    return Results.Json(new { Error = result.Error }, statusCode: StatusCodes.Status412PreconditionFailed);
                case FloorMapBulkUpdateStatus.BadRequest:
                    return Results.BadRequest(new { Error = result.Error });
                case FloorMapBulkUpdateStatus.Conflict:
                    return Results.Conflict(new { Error = result.Error });
                case FloorMapBulkUpdateStatus.NotFound:
                    return Results.NotFound();
                case FloorMapBulkUpdateStatus.Success:
                    httpContext.Response.Headers.ETag = result.Response!.ETag;
                    await WriteAuditAsync(
                        auditRecordWriter,
                        authorization.StaffContext.OrganizationId,
                        branchId,
                        authorization.StaffContext.StaffUserId,
                        AuditActionNames.UpdateFloorMap,
                        "FloorMap",
                        branchId.ToString("D"),
                        AuditOutcome.Succeeded,
                        new { ZoneCount = result.Response.Zones.Count, SeatCount = result.Response.Seats.Count },
                        cancellationToken);
                    return Results.Ok(result.Response);
                default:
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageLayout);

    }
}
