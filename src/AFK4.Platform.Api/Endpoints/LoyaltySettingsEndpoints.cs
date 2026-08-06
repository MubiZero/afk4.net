using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class LoyaltySettingsEndpoints
{
    public static void MapLoyaltySettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("loyalty-settings", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.OrganizationLoyaltySettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);

            return Results.Ok(row is null
                ? new LoyaltySettingsDto(false, 0, false, 0, false, 0, 0, 0)
                : new LoyaltySettingsDto(
                    row.TopUpEnabled, row.TopUpPercentBasisPoints,
                    row.ShopEnabled, row.ShopPercentBasisPoints,
                    row.SessionEnabled, row.SessionPercentBasisPoints,
                    row.CashbackCapMinorUnits, row.MinimumSourceMinorUnits));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageLoyaltySettings);

        app.MapPost("loyalty-settings", async (
            UpdateLoyaltySettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.TopUpPercentBasisPoints is < 0 or > 10000
                || request.ShopPercentBasisPoints is < 0 or > 10000
                || request.SessionPercentBasisPoints is < 0 or > 10000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["percentBasisPoints"] = ["Percent must be between 0 and 10000 basis points (0–100%)."]
                });
            }

            if (request.CashbackCapMinorUnits < 0 || request.MinimumSourceMinorUnits < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limits"] = ["Cashback cap and minimum source must be zero or positive."]
                });
            }

            var staff = authorization.StaffContext!;
            var orgId = staff.OrganizationId;
            var now = timeProvider.GetUtcNow();

            var row = await db.OrganizationLoyaltySettings.SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);
            if (row is null)
            {
                row = new OrganizationLoyaltySettingsEntity { OrganizationId = orgId };
                db.OrganizationLoyaltySettings.Add(row);
            }
            row.TopUpEnabled = request.TopUpEnabled;
            row.TopUpPercentBasisPoints = request.TopUpPercentBasisPoints;
            row.ShopEnabled = request.ShopEnabled;
            row.ShopPercentBasisPoints = request.ShopPercentBasisPoints;
            row.SessionEnabled = request.SessionEnabled;
            row.SessionPercentBasisPoints = request.SessionPercentBasisPoints;
            row.CashbackCapMinorUnits = request.CashbackCapMinorUnits;
            row.MinimumSourceMinorUnits = request.MinimumSourceMinorUnits;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateLoyaltySettings,
                TargetType: "OrganizationLoyaltySettings",
                TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(request)), ct);

            return Results.Ok(new LoyaltySettingsDto(
                row.TopUpEnabled, row.TopUpPercentBasisPoints,
                row.ShopEnabled, row.ShopPercentBasisPoints,
                row.SessionEnabled, row.SessionPercentBasisPoints,
                row.CashbackCapMinorUnits, row.MinimumSourceMinorUnits));
        });
    }
}
