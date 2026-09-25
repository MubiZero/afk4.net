using System.Globalization;
using AFK4.Platform.Api.Ads;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>Реклама платформы: кабинет в Platform Control и приём показов с ПК.</summary>
internal static class AdEndpoints
{
    private const string AdvertiserTarget = "AdAdvertiser";
    private const string CampaignTarget = "AdCampaign";
    private const string CreativeTarget = "AdCreative";

    public static void MapAdEndpoints(this WebApplication app)
    {
        MapAdvertisers(app);
        MapCampaigns(app);
        MapCreatives(app);
        MapReport(app);
        MapDevice(app);
    }

    private static void MapAdvertisers(WebApplication app)
    {
        app.MapGet(AdRoutes.Advertisers, async (
            PlatformAdminAuthorizationService authorizationService, PlatformDbContext db, CancellationToken ct) =>
        {
            if (Deny(authorizationService) is { } denied) return denied;
            var advertisers = await db.AdAdvertisers.AsNoTracking().OrderBy(advertiser => advertiser.Name).ToListAsync(ct);
            return Results.Ok(advertisers.Select(ToDto).ToList());
        });

        app.MapPost(AdRoutes.Advertisers, (UpsertAdvertiserRequest request, PlatformAdminAuthorizationService authorizationService,
                IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
            UpsertAdvertiserAsync(null, request, authorizationService, audit, db, clock, ct));

        app.MapPut($"{AdRoutes.Advertisers}/{{advertiserId:guid}}", (Guid advertiserId, UpsertAdvertiserRequest request,
                PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
                CancellationToken ct) =>
            UpsertAdvertiserAsync(advertiserId, request, authorizationService, audit, db, clock, ct));
    }

    private static async Task<IResult> UpsertAdvertiserAsync(Guid? advertiserId, UpsertAdvertiserRequest request,
        PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
        CancellationToken ct)
    {
        if (Deny(authorizationService) is { } denied) return denied;
        if (PlatformAds.Validate(request) is { } error) return Invalid(error);

        var actor = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAds).PlatformAdminContext?.PlatformAdminUserId;
        AdAdvertiserEntity? advertiser;
        if (advertiserId is null)
        {
            advertiser = new AdAdvertiserEntity
            {
                AdvertiserId = Guid.NewGuid(), CreatedAtUtc = clock.GetUtcNow(), CreatedByPlatformAdminUserId = actor
            };
            db.AdAdvertisers.Add(advertiser);
        }
        else
        {
            advertiser = await db.AdAdvertisers.SingleOrDefaultAsync(candidate => candidate.AdvertiserId == advertiserId, ct);
            if (advertiser is null) return Results.NotFound();
        }

        advertiser.Name = request.Name.Trim();
        advertiser.Contact = request.Contact?.Trim() ?? string.Empty;
        await db.SaveChangesAsync(ct);
        await WritePlatformAuditAsync(audit, Guid.Empty, actor, AuditActionNames.UpsertAdvertiser, AdvertiserTarget,
            advertiser.AdvertiserId.ToString("N"), AuditOutcome.Succeeded, new { advertiser.Name }, ct);
        return Results.Ok(ToDto(advertiser));
    }

    private static void MapCampaigns(WebApplication app)
    {
        app.MapGet(AdRoutes.Campaigns, async (
            PlatformAdminAuthorizationService authorizationService, PlatformDbContext db, CancellationToken ct) =>
        {
            if (Deny(authorizationService) is { } denied) return denied;
            return Results.Ok(await PlatformAds.ListAsync(db, ct));
        });

        app.MapPost(AdRoutes.Campaigns, (UpsertAdCampaignRequest request, PlatformAdminAuthorizationService authorizationService,
                IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
            UpsertCampaignAsync(null, request, authorizationService, audit, db, clock, ct));

        app.MapPut($"{AdRoutes.Campaigns}/{{campaignId:guid}}", (Guid campaignId, UpsertAdCampaignRequest request,
                PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
                CancellationToken ct) =>
            UpsertCampaignAsync(campaignId, request, authorizationService, audit, db, clock, ct));

        app.MapPost($"{AdRoutes.Campaigns}/{{campaignId:guid}}/state", async (Guid campaignId, SetAdCampaignStateRequest request,
            PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
            CancellationToken ct) =>
        {
            if (Deny(authorizationService) is { } denied) return denied;
            if (!AdCampaignStateNames.All.Contains(request.State)) return Invalid("Unknown campaign state.");
            var campaign = await db.AdCampaigns.SingleOrDefaultAsync(candidate => candidate.CampaignId == campaignId, ct);
            if (campaign is null) return Results.NotFound();

            // Запустить кампанию без одобренного креатива — значит запустить пустоту: показывать нечего.
            if (request.State == AdCampaignStateNames.Active
                && !await db.AdCreatives.AnyAsync(creative => creative.CampaignId == campaignId && creative.Moderation == AdModerationNames.Approved, ct))
            {
                return Results.Conflict(new { Error = "The campaign has no approved creative.", Code = AdErrorCodeNames.NotApproved });
            }

            var actor = Actor(authorizationService);
            campaign.State = request.State;
            campaign.UpdatedAtUtc = clock.GetUtcNow();
            campaign.UpdatedByPlatformAdminUserId = actor;
            await db.SaveChangesAsync(ct);
            await WritePlatformAuditAsync(audit, Guid.Empty, actor, AuditActionNames.SetAdCampaignState, CampaignTarget,
                campaignId.ToString("N"), AuditOutcome.Succeeded, new { request.State }, ct);
            return Results.Ok(await CampaignDtoAsync(db, campaign, ct));
        });
    }

    private static async Task<IResult> UpsertCampaignAsync(Guid? campaignId, UpsertAdCampaignRequest request,
        PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
        CancellationToken ct)
    {
        if (Deny(authorizationService) is { } denied) return denied;
        if (PlatformAds.Validate(request) is { } error) return Invalid(error);
        if (!await db.AdAdvertisers.AnyAsync(advertiser => advertiser.AdvertiserId == request.AdvertiserId, ct))
            return Invalid("Advertiser was not found.");

        var now = clock.GetUtcNow();
        var actor = Actor(authorizationService);
        AdCampaignEntity? campaign;
        if (campaignId is null)
        {
            campaign = new AdCampaignEntity { CampaignId = Guid.NewGuid(), State = AdCampaignStateNames.Draft, CreatedAtUtc = now };
            db.AdCampaigns.Add(campaign);
        }
        else
        {
            campaign = await db.AdCampaigns.SingleOrDefaultAsync(candidate => candidate.CampaignId == campaignId, ct);
            if (campaign is null) return Results.NotFound();
        }

        PlatformAds.Apply(campaign, request);
        campaign.UpdatedAtUtc = now;
        campaign.UpdatedByPlatformAdminUserId = actor;
        await db.SaveChangesAsync(ct);
        await WritePlatformAuditAsync(audit, Guid.Empty, actor, AuditActionNames.UpsertAdCampaign, CampaignTarget,
            campaign.CampaignId.ToString("N"), AuditOutcome.Succeeded,
            new { campaign.Name, campaign.Category, campaign.StartsAtUtc, campaign.EndsAtUtc }, ct);
        return Results.Ok(await CampaignDtoAsync(db, campaign, ct));
    }

    private static void MapCreatives(WebApplication app)
    {
        app.MapPost($"{AdRoutes.Campaigns}/{{campaignId:guid}}/creatives", (Guid campaignId, UpsertAdCreativeRequest request,
                PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
                CancellationToken ct) =>
            UpsertCreativeAsync(campaignId, null, request, authorizationService, audit, db, clock, ct));

        app.MapPut($"{AdRoutes.Campaigns}/{{campaignId:guid}}/creatives/{{creativeId:guid}}", (Guid campaignId, Guid creativeId,
                UpsertAdCreativeRequest request, PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit,
                PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
            UpsertCreativeAsync(campaignId, creativeId, request, authorizationService, audit, db, clock, ct));

        app.MapPost($"{AdRoutes.Campaigns}/{{campaignId:guid}}/creatives/{{creativeId:guid}}/moderation", async (
            Guid campaignId, Guid creativeId, ModerateAdCreativeRequest request, PlatformAdminAuthorizationService authorizationService,
            IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (Deny(authorizationService) is { } denied) return denied;
            if (request.Approve && !request.ConfirmedAllowed)
            {
                return Results.BadRequest(new
                {
                    Error = "Confirm the creative advertises no club, alcohol, tobacco or betting.",
                    Code = AdErrorCodeNames.ConfirmationRequired
                });
            }

            if (!request.Approve && (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > AdLimits.ReasonMax))
                return Invalid($"A rejection needs a reason of at most {AdLimits.ReasonMax} characters.");

            var creative = await db.AdCreatives.SingleOrDefaultAsync(
                candidate => candidate.CreativeId == creativeId && candidate.CampaignId == campaignId, ct);
            if (creative is null) return Results.NotFound();

            var actor = Actor(authorizationService);
            creative.Moderation = request.Approve ? AdModerationNames.Approved : AdModerationNames.Rejected;
            creative.RejectedReason = request.Approve ? null : request.Reason!.Trim();
            creative.ModeratedAtUtc = clock.GetUtcNow();
            creative.ModeratedByPlatformAdminUserId = actor;
            await db.SaveChangesAsync(ct);
            await WritePlatformAuditAsync(audit, Guid.Empty, actor, AuditActionNames.ModerateAdCreative, CreativeTarget,
                creativeId.ToString("N"), AuditOutcome.Succeeded, new { request.Approve, request.Reason, request.ConfirmedAllowed }, ct);
            return Results.Ok(PlatformAds.ToDto(creative));
        });
    }

    private static async Task<IResult> UpsertCreativeAsync(Guid campaignId, Guid? creativeId, UpsertAdCreativeRequest request,
        PlatformAdminAuthorizationService authorizationService, IAuditRecordWriter audit, PlatformDbContext db, TimeProvider clock,
        CancellationToken ct)
    {
        if (Deny(authorizationService) is { } denied) return denied;
        if (PlatformAds.Validate(request) is { } error) return Invalid(error);
        if (!await db.AdCampaigns.AnyAsync(campaign => campaign.CampaignId == campaignId, ct)) return Results.NotFound();

        AdCreativeEntity? creative;
        if (creativeId is null)
        {
            creative = new AdCreativeEntity { CreativeId = Guid.NewGuid(), CampaignId = campaignId, CreatedAtUtc = clock.GetUtcNow() };
            db.AdCreatives.Add(creative);
        }
        else
        {
            creative = await db.AdCreatives.SingleOrDefaultAsync(candidate => candidate.CreativeId == creativeId && candidate.CampaignId == campaignId, ct);
            if (creative is null) return Results.NotFound();
        }

        creative.Title = request.Title.Trim();
        creative.Body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim();
        creative.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        // Правка — новый креатив для модератора: одобряли не этот текст и не эту картинку.
        creative.Moderation = AdModerationNames.Pending;
        creative.RejectedReason = null;
        creative.ModeratedAtUtc = null;
        creative.ModeratedByPlatformAdminUserId = null;
        await db.SaveChangesAsync(ct);
        await WritePlatformAuditAsync(audit, Guid.Empty, Actor(authorizationService), AuditActionNames.UpsertAdCreative, CreativeTarget,
            creative.CreativeId.ToString("N"), AuditOutcome.Succeeded, new { creative.Title, creative.ImageUrl }, ct);
        return Results.Ok(PlatformAds.ToDto(creative));
    }

    private static void MapReport(WebApplication app)
    {
        app.MapGet(AdRoutes.Report, async (string from, string to, Guid? campaignId,
            PlatformAdminAuthorizationService authorizationService, PlatformDbContext db, CancellationToken ct) =>
        {
            if (Deny(authorizationService) is { } denied) return denied;
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDay)
                || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDay)
                || toDay < fromDay || toDay.DayNumber - fromDay.DayNumber > AdLimits.ReportMaxDays)
            {
                return Invalid($"Give from and to as yyyy-MM-dd, at most {AdLimits.ReportMaxDays} days apart.");
            }

            return Results.Ok(await PlatformAds.ReportAsync(db, fromDay, toDay, campaignId, ct));
        });
    }

    private static void MapDevice(WebApplication app)
    {
        app.MapPost("/api/devices/{deviceId:guid}/showcase/impressions", async (
            Guid deviceId, DeviceShowcaseImpressionsRequest request, HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator, PlatformDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (request.DeviceId != deviceId
                || !credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            // Приостановленный клуб показы тоже шлёт: реклама у него уже была на экране, и счёт
            // рекламодателю должен быть честным.
            return await PlatformAds.IngestAsync(db, request, clock.GetUtcNow(), ct) switch
            {
                PlatformAds.IngestOutcome.Invalid => Invalid("The batch is malformed."),
                _ => Results.NoContent()
            };
        });
    }

    private static IResult? Deny(PlatformAdminAuthorizationService authorizationService)
    {
        var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAds);
        if (!authorization.IsAuthenticated) return Results.Unauthorized();
        return authorization.IsAllowed ? null : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Guid? Actor(PlatformAdminAuthorizationService authorizationService) =>
        authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAds).PlatformAdminContext?.PlatformAdminUserId;

    private static async Task<AdCampaignDto> CampaignDtoAsync(PlatformDbContext db, AdCampaignEntity campaign, CancellationToken ct)
    {
        var advertiser = await db.AdAdvertisers.AsNoTracking()
            .Where(candidate => candidate.AdvertiserId == campaign.AdvertiserId)
            .Select(candidate => candidate.Name)
            .SingleOrDefaultAsync(ct) ?? string.Empty;
        var creatives = await db.AdCreatives.AsNoTracking().Where(creative => creative.CampaignId == campaign.CampaignId).ToListAsync(ct);
        return PlatformAds.ToDto(campaign, advertiser, creatives);
    }

    private static AdvertiserDto ToDto(AdAdvertiserEntity advertiser) =>
        new(advertiser.AdvertiserId, advertiser.Name, advertiser.Contact, advertiser.CreatedAtUtc);

    private static IResult Invalid(string error) => Results.BadRequest(new { Error = error, Code = AdErrorCodeNames.Invalid });
}
