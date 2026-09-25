using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Tips;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Tips;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>Чаевые администратору смены: игрок с экрана итога, клуб — настройка и возврат.</summary>
internal static class TipEndpoints
{
    public static void MapTipEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        MapPlayer(app);
        MapClub(organizations);
        MapPayout(organizations);
    }

    private static void MapPlayer(WebApplication app)
    {
        // Чужой визит отсюда неотличим от несуществующего: 404, как у отзыва.
        app.MapGet("/api/me/visits/{sessionId:guid}/tip", async (
            Guid sessionId, IPlayerContextAccessor playerContextAccessor, VisitTips tips, CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            var offer = await tips.OfferAsync(player.PlayerAccountId, sessionId, ct);
            return offer is null ? Results.NotFound() : Results.Ok(offer);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/visits/{sessionId:guid}/tip", async (
            Guid sessionId, PlayerTipRequest request, IPlayerContextAccessor playerContextAccessor, VisitTips tips, CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            var result = await tips.GiveAsync(player.PlayerAccountId, sessionId, request, ct);
            if (result.NotFound) return Results.NotFound();
            return result.Response is { } response
                ? Results.Ok(response)
                : Results.Conflict(new { error = result.Refusal });
        }).RequireRateLimiting("player-me");
    }

    private static void MapClub(IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("tip-settings", async (
            StaffAuthorizationService authorizationService, PlatformDbContext db, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageTips);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var enabled = await db.OrganizationTipSettings.AsNoTracking()
                .AnyAsync(settings => settings.OrganizationId == authorization.StaffContext!.OrganizationId && settings.Enabled, ct);
            return Results.Ok(new TipSettingsDto(enabled));
        });

        organizations.MapPut("tip-settings", async (
            UpdateTipSettingsRequest request, StaffAuthorizationService authorizationService, IAuditRecordWriter audit,
            PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageTips);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var settings = await db.OrganizationTipSettings.SingleOrDefaultAsync(row => row.OrganizationId == staff.OrganizationId, ct);
            if (settings is null)
            {
                settings = new OrganizationTipSettingsEntity { OrganizationId = staff.OrganizationId };
                db.OrganizationTipSettings.Add(settings);
            }

            settings.Enabled = request.Enabled;
            settings.UpdatedAtUtc = clock.GetUtcNow();
            settings.UpdatedByStaffUserId = staff.StaffUserId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId, BranchId: null, ActorStaffUserId: staff.StaffUserId, Action: AuditActionNames.UpdateTipSettings,
                TargetType: "OrganizationTipSettings", TargetId: staff.OrganizationId.ToString("N"), Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi", DetailsJson: JsonSerializer.Serialize(request)), ct);
            return Results.Ok(new TipSettingsDto(settings.Enabled));
        });

        organizations.MapGet("shifts/{shiftId:guid}/tips", async (
            Guid shiftId, StaffAuthorizationService authorizationService, PlatformDbContext db, VisitTips tips, CancellationToken ct) =>
        {
            var branchId = await ShiftBranchAsync(db, shiftId, ct);
            if (branchId is null) return Results.NotFound();
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId.Value, OrganizationPermissionNames.ViewShift, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await tips.ForShiftAsync(authorization.StaffContext!.OrganizationId, shiftId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        organizations.MapPost("shifts/{shiftId:guid}/tips/{ledgerEntryId:guid}/reverse", async (
            Guid shiftId, Guid ledgerEntryId, StaffAuthorizationService authorizationService, IAuditRecordWriter audit,
            PlatformDbContext db, VisitTips tips, CancellationToken ct) =>
        {
            var branchId = await ShiftBranchAsync(db, shiftId, ct);
            if (branchId is null) return Results.NotFound();
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId.Value, OrganizationPermissionNames.ManageTips, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var outcome = await tips.ReverseAsync(staff.OrganizationId, shiftId, ledgerEntryId, staff.StaffUserId, ct);
            if (outcome == VisitTips.ReverseOutcome.NotFound) return Results.NotFound();
            if (outcome == VisitTips.ReverseOutcome.ShiftClosed) return Results.Conflict(new { error = TipErrorCodeNames.ShiftClosed });
            if (outcome == VisitTips.ReverseOutcome.AlreadyReversed) return Results.Conflict(new { error = TipErrorCodeNames.AlreadyReversed });

            await audit.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId, BranchId: branchId, ActorStaffUserId: staff.StaffUserId, Action: AuditActionNames.ReverseTip,
                TargetType: "LedgerEntry", TargetId: ledgerEntryId.ToString("N"), Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi", DetailsJson: JsonSerializer.Serialize(new { shiftId })), ct);
            return Results.Ok(await tips.ForShiftAsync(staff.OrganizationId, shiftId, ct));
        });
    }

    private static void MapPayout(IEndpointRouteBuilder organizations)
    {
        // Выдать чаевые из кассы — это выдача наличных: право того, кто ведёт ящик.
        organizations.MapPost("shifts/{shiftId:guid}/tips/payout", async (
            Guid shiftId, PayOutShiftTipsRequest request, StaffAuthorizationService authorizationService, IAuditRecordWriter audit,
            PlatformDbContext db, VisitTips tips, AFK4.Platform.Api.Shifts.IShiftService shifts, CancellationToken ct) =>
        {
            var branchId = await ShiftBranchAsync(db, shiftId, ct);
            if (branchId is null) return Results.NotFound();
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId.Value, OrganizationPermissionNames.ManageShiftCash, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return Results.BadRequest(new { error = "idempotency_key_required" });

            var staff = authorization.StaffContext!;
            var (result, error) = await tips.PayOutAsync(shifts, staff.OrganizationId, shiftId, staff.StaffUserId, request.IdempotencyKey, ct);
            if (result is null) return Results.NotFound();
            if (error is not null) return Results.Conflict(new { error });

            await audit.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId, BranchId: branchId, ActorStaffUserId: staff.StaffUserId, Action: AuditActionNames.PayOutTips,
                TargetType: "Shift", TargetId: shiftId.ToString("N"), Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi", DetailsJson: JsonSerializer.Serialize(new { result.PaidOut })), ct);
            return Results.Ok(result);
        });
    }

    private static Task<Guid?> ShiftBranchAsync(PlatformDbContext db, Guid shiftId, CancellationToken ct) =>
        db.Shifts.AsNoTracking()
            .Where(shift => shift.ShiftId == shiftId)
            .Select(shift => (Guid?)shift.BranchId)
            .SingleOrDefaultAsync(ct);
}
