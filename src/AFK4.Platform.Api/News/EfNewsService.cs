using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.News;

public sealed class EfNewsService(PlatformDbContext db, TimeProvider timeProvider) : INewsService
{
    private const int TitleMax = 200;
    private const int BodyMax = 4000;
    private const int ImageUrlMax = 2048;

    public async Task<IReadOnlyList<NewsItemDto>> ListForOwnerAsync(Guid orgId, CancellationToken ct)
    {
        var rows = await db.NewsItems.AsNoTracking()
            .Where(news => news.OrganizationId == orgId)
            .OrderByDescending(news => news.PublishAtUtc ?? news.CreatedAtUtc)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<NewsMutationResult> CreateAsync(Guid orgId, CreateNewsItemRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(orgId, request.Title, request.Body, request.ImageUrl,
            request.BranchId, request.PublishAtUtc, request.ExpiresAtUtc, ct);
        if (error is not null) return NewsMutationResult.Invalid(error);

        var now = timeProvider.GetUtcNow();
        var entity = new NewsItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = request.BranchId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            ImageUrl = NormalizeImageUrl(request.ImageUrl),
            IsPublished = request.IsPublished,
            PublishAtUtc = request.PublishAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.NewsItems.Add(entity);
        await db.SaveChangesAsync(ct);
        return NewsMutationResult.Ok(ToDto(entity));
    }

    public async Task<NewsMutationResult> UpdateAsync(Guid orgId, Guid id, UpdateNewsItemRequest request, CancellationToken ct)
    {
        var entity = await db.NewsItems.SingleOrDefaultAsync(news => news.Id == id && news.OrganizationId == orgId, ct);
        if (entity is null) return NewsMutationResult.Missing;

        var error = await ValidateAsync(orgId, request.Title, request.Body, request.ImageUrl,
            request.BranchId, request.PublishAtUtc, request.ExpiresAtUtc, ct);
        if (error is not null) return NewsMutationResult.Invalid(error);

        entity.BranchId = request.BranchId;
        entity.Title = request.Title.Trim();
        entity.Body = request.Body.Trim();
        entity.ImageUrl = NormalizeImageUrl(request.ImageUrl);
        entity.IsPublished = request.IsPublished;
        entity.PublishAtUtc = request.PublishAtUtc;
        entity.ExpiresAtUtc = request.ExpiresAtUtc;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return NewsMutationResult.Ok(ToDto(entity));
    }

    public async Task<NewsMutationOutcome> DeleteAsync(Guid orgId, Guid id, CancellationToken ct)
    {
        var entity = await db.NewsItems.SingleOrDefaultAsync(news => news.Id == id && news.OrganizationId == orgId, ct);
        if (entity is null) return NewsMutationOutcome.NotFound;
        db.NewsItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NewsMutationOutcome.Success;
    }

    public async Task<IReadOnlyList<PlayerNewsItemDto>> ListForPlayerAsync(Guid orgId, Guid? homeBranchId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await db.NewsItems.AsNoTracking()
            .Where(news => news.OrganizationId == orgId
                && news.IsPublished
                && (news.BranchId == null || news.BranchId == homeBranchId)
                && (news.PublishAtUtc == null || news.PublishAtUtc <= now)
                && (news.ExpiresAtUtc == null || news.ExpiresAtUtc > now))
            .OrderByDescending(news => news.PublishAtUtc ?? news.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return rows
            .Select(news => new PlayerNewsItemDto(news.Id, news.Title, news.Body, news.ImageUrl,
                news.PublishAtUtc ?? news.CreatedAtUtc))
            .ToList();
    }

    private async Task<string?> ValidateAsync(Guid orgId, string title, string body, string? imageUrl,
        Guid? branchId, DateTimeOffset? publishAt, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Title is required.";
        if (title.Trim().Length > TitleMax) return $"Title must be at most {TitleMax} characters.";
        if (string.IsNullOrWhiteSpace(body)) return "Body is required.";
        if (body.Trim().Length > BodyMax) return $"Body must be at most {BodyMax} characters.";
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (imageUrl.Length > ImageUrlMax) return $"Image URL must be at most {ImageUrlMax} characters.";
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "Image URL must be an absolute http or https address.";
            }
        }
        if (publishAt is not null && expiresAt is not null && publishAt >= expiresAt)
        {
            return "PublishAtUtc must be earlier than ExpiresAtUtc.";
        }
        if (branchId is not null)
        {
            var exists = await db.Branches.AsNoTracking()
                .AnyAsync(branch => branch.BranchId == branchId && branch.OrganizationId == orgId, ct);
            if (!exists) return "BranchId does not belong to this organization.";
        }
        return null;
    }

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

    private static NewsItemDto ToDto(NewsItemEntity news) =>
        new(news.Id, news.BranchId, news.Title, news.Body, news.ImageUrl, news.IsPublished,
            news.PublishAtUtc, news.ExpiresAtUtc, news.CreatedAtUtc, news.UpdatedAtUtc);
}
