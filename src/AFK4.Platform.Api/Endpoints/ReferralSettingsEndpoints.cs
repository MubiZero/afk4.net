using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Настройки «приведи друга». Живут рядом с кешбэком и под тем же правом: обе программы — деньги
/// клуба, раздаваемые по его собственным правилам.
/// </summary>
internal static class ReferralSettingsEndpoints
{
    private static ReferralSettingsDto ToDto(OrganizationReferralSettingsEntity? row) =>
        row is null
            ? new ReferralSettingsDto(false, 0, 0, 0, 30, 0)
            : new ReferralSettingsDto(
                row.Enabled,
                row.ReferrerBonusMinorUnits,
                row.InviteeBonusMinorUnits,
                row.MinimumTopUpMinorUnits,
                row.ClaimWindowDays,
                row.MaxRewardedPerReferrer);

    public static void MapReferralSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("referral-settings", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.OrganizationReferralSettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);

            return Results.Ok(ToDto(row));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageLoyaltySettings);

        app.MapPost("referral-settings", async (
            UpdateReferralSettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.ReferrerBonusMinorUnits < 0
                || request.InviteeBonusMinorUnits < 0
                || request.MinimumTopUpMinorUnits < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amounts"] = ["Bonus amounts and the minimum top-up must be zero or positive."]
                });
            }

            if (request.ClaimWindowDays < 0 || request.MaxRewardedPerReferrer < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limits"] = ["Claim window and per-referrer cap must be zero or positive."]
                });
            }

            // Включённая программа, которая никому ничего не платит, — это кнопка, которая
            // ничего не делает: игрок увидит экран приглашения и не получит ни дирама.
            if (request.Enabled
                && request.ReferrerBonusMinorUnits == 0
                && request.InviteeBonusMinorUnits == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amounts"] = ["An enabled referral programme must pay at least one side."]
                });
            }

            var staff = authorization.StaffContext!;
            var orgId = staff.OrganizationId;

            var row = await db.OrganizationReferralSettings.SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);
            if (row is null)
            {
                row = new OrganizationReferralSettingsEntity { OrganizationId = orgId };
                db.OrganizationReferralSettings.Add(row);
            }

            row.Enabled = request.Enabled;
            row.ReferrerBonusMinorUnits = request.ReferrerBonusMinorUnits;
            row.InviteeBonusMinorUnits = request.InviteeBonusMinorUnits;
            row.MinimumTopUpMinorUnits = request.MinimumTopUpMinorUnits;
            row.ClaimWindowDays = request.ClaimWindowDays;
            row.MaxRewardedPerReferrer = request.MaxRewardedPerReferrer;
            row.UpdatedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateLoyaltySettings,
                TargetType: "OrganizationReferralSettings",
                TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(request)), ct);

            return Results.Ok(ToDto(row));
        });
    }
}
