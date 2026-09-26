using AFK4.Platform.Api.Ads;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// «Реклама на ваших ПК» (спека рекламы, §8.4): клуб — тоже распространитель рекламы, поэтому видит,
/// какая реклама платформы идёт и шла на его ПК. Смотреть — тем, кто видит подписку клуба.
/// </summary>
internal static class ClubAdEndpoints
{
    public static void MapClubAdEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("platform-ads", async (
            Guid organizationId, StaffAuthorizationService authorizationService, PlatformDbContext db,
            IOrganizationFeatureSnapshot features, IOptions<InstallOptions> install, TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var adsEnabled = (await features.GetEnabledAsync(organizationId, ct)).Contains(PlatformFeatureNames.PlatformAds);
            return Results.Ok(await ClubAds.ListAsync(db, organizationId, adsEnabled, clock.GetUtcNow(), install.Value.ApiBaseUrl, ct));
        });

        // «Пожаловаться»: снять рекламу клуб не может, но сообщает платформе, а та решает.
        organizations.MapPost("platform-ads/{creativeId:guid}/complaints", async (
            Guid organizationId, Guid creativeId, ReportClubAdRequest request, StaffAuthorizationService authorizationService,
            PlatformDbContext db, IOrganizationFeatureSnapshot features, IOptions<InstallOptions> install, IAuditRecordWriter audit,
            TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (AdComplaints.Validate(request) is { } invalid) return Results.BadRequest(new { error = invalid });

            // Жаловаться можно на то, что было на ПК клуба: идёт сейчас или показывалось за 30 дней.
            var now = clock.GetUtcNow();
            var adsEnabled = (await features.GetEnabledAsync(organizationId, ct)).Contains(PlatformFeatureNames.PlatformAds);
            var seen = await ClubAds.ListAsync(db, organizationId, adsEnabled, now, install.Value.ApiBaseUrl, ct);
            if (seen.Ads.All(ad => ad.CreativeId != creativeId))
                return Results.NotFound(new { error = AdComplaintErrorCodeNames.NotShown });

            var staffUserId = authorization.StaffContext.StaffUserId;
            var complaint = await AdComplaints.ReportAsync(db, organizationId, creativeId, staffUserId, request, now, ct);
            await audit.WriteAsync(new AuditRecordWriteRequest(
                organizationId, BranchId: null, ActorStaffUserId: staffUserId, Action: AuditActionNames.ReportPlatformAd,
                TargetType: "AdCreative", TargetId: creativeId.ToString("N"), Outcome: AuditOutcome.Succeeded, SourceApp: "OrganizationAdmin",
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { complaint.ComplaintId, complaint.Reason })), ct);
            return Results.Ok(await ClubAds.ListAsync(db, organizationId, adsEnabled, now, install.Value.ApiBaseUrl, ct));
        });
    }
}
