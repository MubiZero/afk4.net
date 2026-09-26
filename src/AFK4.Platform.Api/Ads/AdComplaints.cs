using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Ads;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Ads;

/// <summary>
/// Жалобы клубов на рекламу на их ПК (спека рекламы, §8.4). Клуб — распространитель, но снять
/// рекламу сам не может: он сообщает, платформа решает. На один креатив у клуба одна открытая жалоба.
/// </summary>
public static class AdComplaints
{
    public static string? Validate(ReportClubAdRequest request)
    {
        if (!AdComplaintReasonNames.All.Contains(request.Reason)) return "Unknown complaint reason.";
        return request.Comment?.Trim().Length > AdComplaintLimits.CommentMax
            ? $"The comment must be at most {AdComplaintLimits.CommentMax} characters."
            : null;
    }

    /// <summary>Открытая жалоба клуба на креатив — новая или уже поданная раньше.</summary>
    public static async Task<AdComplaintEntity> ReportAsync(
        PlatformDbContext db, Guid organizationId, Guid creativeId, Guid staffUserId, ReportClubAdRequest request, DateTimeOffset now,
        CancellationToken ct)
    {
        var open = await db.AdComplaints.SingleOrDefaultAsync(
            complaint => complaint.OrganizationId == organizationId && complaint.CreativeId == creativeId && complaint.ResolvedAtUtc == null, ct);
        if (open is not null) return open;

        var complaint = new AdComplaintEntity
        {
            ComplaintId = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreativeId = creativeId,
            Reason = request.Reason,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ReportedByStaffUserId = staffUserId,
            CreatedAtUtc = now
        };
        db.AdComplaints.Add(complaint);
        try
        {
            await db.SaveChangesAsync(ct);
            return complaint;
        }
        catch (DbUpdateException)
        {
            // Две вкладки нажали одновременно — открытая жалоба уже есть, вторую не заводим.
            db.Entry(complaint).State = EntityState.Detached;
            return await db.AdComplaints.AsNoTracking().SingleAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.CreativeId == creativeId && candidate.ResolvedAtUtc == null, ct);
        }
    }

    public static async Task<IReadOnlyList<AdComplaintDto>> ListAsync(PlatformDbContext db, bool openOnly, CancellationToken ct)
    {
        var query = db.AdComplaints.AsNoTracking();
        if (openOnly) query = query.Where(complaint => complaint.ResolvedAtUtc == null);
        var complaints = await query.OrderByDescending(complaint => complaint.CreatedAtUtc).Take(500).ToListAsync(ct);
        return await DescribeAsync(db, complaints, ct);
    }

    public static async Task<AdComplaintDto?> ResolveAsync(
        PlatformDbContext db, Guid complaintId, string resolution, Guid? actorPlatformAdminUserId, DateTimeOffset now, CancellationToken ct)
    {
        var complaint = await db.AdComplaints.SingleOrDefaultAsync(candidate => candidate.ComplaintId == complaintId, ct);
        if (complaint is null) return null;
        if (complaint.ResolvedAtUtc is null)
        {
            complaint.ResolvedAtUtc = now;
            complaint.ResolvedByPlatformAdminUserId = actorPlatformAdminUserId;
            complaint.Resolution = resolution.Trim();
            await db.SaveChangesAsync(ct);
        }

        return (await DescribeAsync(db, [complaint], ct))[0];
    }

    private static async Task<IReadOnlyList<AdComplaintDto>> DescribeAsync(
        PlatformDbContext db, IReadOnlyList<AdComplaintEntity> complaints, CancellationToken ct)
    {
        var organizationIds = complaints.Select(complaint => complaint.OrganizationId).Distinct().ToList();
        var creativeIds = complaints.Select(complaint => complaint.CreativeId).Distinct().ToList();
        var staffIds = complaints.Select(complaint => complaint.ReportedByStaffUserId).Distinct().ToList();
        var organizations = await db.Organizations.AsNoTracking().Where(organization => organizationIds.Contains(organization.OrganizationId))
            .ToDictionaryAsync(organization => organization.OrganizationId, organization => organization.Name, ct);
        var creatives = await db.AdCreatives.AsNoTracking().Where(creative => creativeIds.Contains(creative.CreativeId))
            .ToDictionaryAsync(creative => creative.CreativeId, ct);
        var campaignIds = creatives.Values.Select(creative => creative.CampaignId).Distinct().ToList();
        var campaigns = await db.AdCampaigns.AsNoTracking().Where(campaign => campaignIds.Contains(campaign.CampaignId))
            .ToDictionaryAsync(campaign => campaign.CampaignId, ct);
        var advertiserIds = campaigns.Values.Select(campaign => campaign.AdvertiserId).Distinct().ToList();
        var advertisers = await db.AdAdvertisers.AsNoTracking().Where(advertiser => advertiserIds.Contains(advertiser.AdvertiserId))
            .ToDictionaryAsync(advertiser => advertiser.AdvertiserId, advertiser => advertiser.Name, ct);
        var staff = await db.StaffUsers.AsNoTracking().Where(user => staffIds.Contains(user.StaffUserId))
            .ToDictionaryAsync(user => user.StaffUserId, user => user.DisplayName, ct);

        return complaints.Select(complaint =>
        {
            creatives.TryGetValue(complaint.CreativeId, out var creative);
            AdCampaignEntity? campaign = null;
            if (creative is not null) campaigns.TryGetValue(creative.CampaignId, out campaign);
            return new AdComplaintDto(
                complaint.ComplaintId,
                complaint.OrganizationId,
                organizations.GetValueOrDefault(complaint.OrganizationId, string.Empty),
                campaign?.CampaignId ?? Guid.Empty,
                campaign?.Name ?? string.Empty,
                complaint.CreativeId,
                creative?.Title ?? string.Empty,
                campaign is null ? string.Empty : advertisers.GetValueOrDefault(campaign.AdvertiserId, string.Empty),
                complaint.Reason,
                complaint.Comment,
                staff.GetValueOrDefault(complaint.ReportedByStaffUserId, string.Empty),
                complaint.CreatedAtUtc,
                complaint.ResolvedAtUtc,
                complaint.Resolution,
                creative?.ArchivedAtUtc is not null);
        }).ToList();
    }
}
