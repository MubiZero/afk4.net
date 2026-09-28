using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

/// <summary>
/// Убирает из хранилища картинки клуба, на которые больше ничто не ссылается: фото новости или
/// товара заменили, новость удалили, картинку загрузили, а форму закрыли без сохранения. Раньше
/// такие файлы оставались в хранилище навсегда.
///
/// Смотрим не на событие «заменили», а на итог: файл никому не нужен. Так одним правилом
/// покрыты и замена, и удаление, и брошенная форма — и новое место с картинкой не придётся
/// учить удалять за собой, достаточно добавить его в <see cref="ReferencedMediaIdsAsync"/>.
/// </summary>
public sealed class OrphanMediaSweeper(
    PlatformDbContext db,
    IMediaStorage storage,
    IOptions<MediaOptions> options,
    TimeProvider clock)
{
    /// <summary>
    /// Столько живёт свежая загрузка без ссылки: форма, в которую её положили, может быть ещё
    /// открыта. Сутки с запасом покрывают и перерыв на обед посреди редактирования.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!options.Value.S3.IsConfigured)
        {
            return 0;
        }

        var cutoff = clock.GetUtcNow() - Grace;
        var organizationIds = await db.UploadedMedia.AsNoTracking()
            .Where(media => media.CreatedAtUtc < cutoff)
            .Select(media => media.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

        var removed = 0;
        foreach (var organizationId in organizationIds)
        {
            var referenced = await ReferencedMediaIdsAsync(organizationId, ct);
            var orphans = (await db.UploadedMedia
                    .Where(media => media.OrganizationId == organizationId && media.CreatedAtUtc < cutoff)
                    .ToListAsync(ct))
                .Where(media => !referenced.Contains(media.MediaId))
                .ToList();

            foreach (var orphan in orphans)
            {
                // Сначала файл, потом запись: упади сохранение — следующий прогон удалит уже
                // несуществующий файл ещё раз, и хранилище это спокойно примет.
                await storage.DeleteAsync(orphan.ObjectKey, ct);
                db.UploadedMedia.Remove(orphan);
            }

            // Клуб за клубом: сбой на одном не откатывает уже сделанное у других.
            await db.SaveChangesAsync(ct);
            removed += orphans.Count;
        }

        return removed;
    }

    /// <summary>
    /// Всё, что показывает картинки клуба. Сравниваем по номеру загрузки в имени файла, а не по
    /// адресу целиком: смени хранилище публичный домен — старые ссылки всё ещё указывают на те же
    /// файлы, и стереть их из-за другого хоста было бы бедой.
    /// </summary>
    private async Task<HashSet<Guid>> ReferencedMediaIdsAsync(Guid organizationId, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        var urls = new List<string?>();
        urls.AddRange(await db.Organizations.AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.LogoUrl)
            .ToListAsync(ct));

        var branches = await db.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId)
            .Select(branch => new { branch.LogoUrl, branch.CoverImageUrl, branch.PhotosJson })
            .ToListAsync(ct);
        foreach (var branch in branches)
        {
            urls.Add(branch.LogoUrl);
            urls.Add(branch.CoverImageUrl);
            foreach (var photo in BranchPhotos.Deserialize(branch.PhotosJson))
            {
                if (photo.MediaId is { } photoMediaId)
                {
                    ids.Add(photoMediaId);
                }

                urls.Add(photo.Url);
            }
        }

        urls.AddRange(await db.NewsItems.AsNoTracking()
            .Where(news => news.OrganizationId == organizationId && news.ImageUrl != null)
            .Select(news => news.ImageUrl)
            .ToListAsync(ct));
        urls.AddRange(await db.PosProducts.AsNoTracking()
            .Where(product => product.OrganizationId == organizationId && product.ImageUrl != null)
            .Select(product => product.ImageUrl)
            .ToListAsync(ct));

        foreach (var url in urls)
        {
            if (MediaIdOf(url) is { } mediaId)
            {
                ids.Add(mediaId);
            }
        }

        return ids;
    }

    /// <summary>Загрузка лежит как `{клуб}/{зал}/{номер}.{расширение}` — номер и есть MediaId.</summary>
    public static Guid? MediaIdOf(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        return Guid.TryParse(fileName, out var mediaId) ? mediaId : null;
    }
}

/// <summary>Раз в шесть часов: спешить некуда, а лишний проход по всем загрузкам ни к чему.</summary>
public sealed class OrphanMediaSweepHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<OrphanMediaSweepHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(6);

    protected override string JobName => PlatformJobNames.OrphanMediaSweep;

    protected override TimeSpan Interval => TickInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var removed = await scopedServices.GetRequiredService<OrphanMediaSweeper>().RunOnceAsync(cancellationToken);
        if (removed > 0)
        {
            Log.LogInformation("Orphan media sweep removed {Count} file(s).", removed);
        }

        return removed;
    }
}
