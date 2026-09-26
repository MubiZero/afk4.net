using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Loyalty;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Подарок на день рождения. Рядом с кешбэком и «приведи друга» и под тем же правом: все три —
/// деньги клуба, раздаваемые по его правилам.
/// </summary>
internal static class BirthdayGiftSettingsEndpoints
{
    private static BirthdayGiftSettingsDto ToDto(OrganizationBirthdayGiftSettingsEntity? row) =>
        row is null
            ? new BirthdayGiftSettingsDto(false, 0, BirthdayGifts.DefaultRecentVisitDays)
            : new BirthdayGiftSettingsDto(row.Enabled, row.AmountMinorUnits, row.RecentVisitDays);

    public static void MapBirthdayGiftSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("birthday-gift-settings", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.OrganizationBirthdayGiftSettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);
            return Results.Ok(ToDto(row));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageLoyaltySettings);

        app.MapPost("birthday-gift-settings", async (
            UpdateBirthdayGiftSettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.AmountMinorUnits < 0 || request.AmountMinorUnits > BirthdayGifts.MaxAmountMinorUnits)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amount"] = [$"The gift is between 0 and {BirthdayGifts.MaxAmountMinorUnits} minor units."]
                });
            }

            if (request.RecentVisitDays is < 0 or > BirthdayGifts.MaxRecentVisitDays)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["recentVisitDays"] = [$"Recent visit window is 0..{BirthdayGifts.MaxRecentVisitDays} days."]
                });
            }

            // Включённый подарок в ноль — кнопка, которая ничего не делает: игрок прочтёт
            // «с днём рождения» и не получит ни дирама.
            if (request.Enabled && request.AmountMinorUnits == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amount"] = ["An enabled birthday gift must give something."]
                });
            }

            var staff = authorization.StaffContext!;
            var orgId = staff.OrganizationId;
            var row = await db.OrganizationBirthdayGiftSettings.SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);
            if (row is null)
            {
                row = new OrganizationBirthdayGiftSettingsEntity { OrganizationId = orgId };
                db.OrganizationBirthdayGiftSettings.Add(row);
            }

            row.Enabled = request.Enabled;
            row.AmountMinorUnits = request.AmountMinorUnits;
            row.RecentVisitDays = request.RecentVisitDays;
            row.UpdatedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateLoyaltySettings,
                TargetType: "OrganizationBirthdayGiftSettings",
                TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(request)), ct);

            return Results.Ok(ToDto(row));
        });
    }
}
