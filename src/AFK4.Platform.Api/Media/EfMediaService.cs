using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

public sealed class EfMediaService(
    PlatformDbContext db, IMediaStorage storage, IOptions<MediaOptions> options, TimeProvider clock)
{
    public async Task<MediaServiceResult> UploadAsync(Guid organizationId, Guid branchId, Guid staffUserId,
        string purpose, string declaredContentType, Stream content, long sizeBytes, CancellationToken ct)
    {
        if (!options.Value.S3.IsConfigured)
            return new(false, "Media storage is not configured on this environment.", null);

        if (sizeBytes <= 0 || sizeBytes > options.Value.MaxBytes)
            return new(false, "File exceeds the maximum allowed size.", null);

        // Считать голову для magic-byte и переиграть поток целиком в память (файлы мелкие ≤ MaxBytes).
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var head = new byte[12];
        var read = await buffer.ReadAsync(head.AsMemory(0, 12), ct);
        var sniffed = MediaValidation.SniffImageContentType(head.AsSpan(0, read));
        if (sniffed is null)
            return new(false, "Unsupported file type. Allowed: PNG, JPEG, WEBP.", null);
        buffer.Position = 0;

        // У «одиночных» назначений (логотип, обложка) новая загрузка вытесняет прежнюю.
        // Галерея так себя не ведёт: там второе фото стирало бы первое. Кроме файла, на который
        // ссылается сохранённая запись: новая картинка ещё не сохранена, и «Отмена» в форме оставила
        // бы клуб со ссылкой на стёртый логотип. Он уйдёт со следующей загрузкой, когда перестанет
        // быть сохранённым.
        if (MediaPurposeNames.IsSingle(purpose))
        {
            var saved = await SavedUrlAsync(organizationId, branchId, purpose, ct);
            var previous = await db.UploadedMedia
                .Where(m => m.OrganizationId == organizationId && m.BranchId == branchId && m.Purpose == purpose)
                .ToListAsync(ct);
            foreach (var old in previous.Where(old => old.PublicUrl != saved))
            {
                await storage.DeleteAsync(old.ObjectKey, ct);
                db.UploadedMedia.Remove(old);
            }
        }

        var mediaId = Guid.NewGuid();
        var objectKey = $"{organizationId}/{branchId}/{mediaId}.{MediaValidation.ExtensionFor(sniffed)}";
        var url = await storage.PutAsync(objectKey, sniffed, buffer, ct);

        var entity = new UploadedMediaEntity
        {
            MediaId = mediaId, OrganizationId = organizationId, BranchId = branchId,
            Purpose = purpose, ObjectKey = objectKey, ContentType = sniffed,
            SizeBytes = sizeBytes, PublicUrl = url, CreatedByStaffUserId = staffUserId,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.UploadedMedia.Add(entity);
        await db.SaveChangesAsync(ct);
        return new(true, null, new UploadedMediaDto(mediaId, url, sniffed, sizeBytes));
    }

    private Task<string?> SavedUrlAsync(Guid organizationId, Guid branchId, string purpose, CancellationToken ct) => purpose switch
    {
        MediaPurposeNames.BranchLogo => db.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == branchId && branch.OrganizationId == organizationId)
            .Select(branch => branch.LogoUrl).SingleOrDefaultAsync(ct),
        MediaPurposeNames.BranchCover => db.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == branchId && branch.OrganizationId == organizationId)
            .Select(branch => branch.CoverImageUrl).SingleOrDefaultAsync(ct),
        MediaPurposeNames.OrganizationLogo => db.Organizations.AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.LogoUrl).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null)
    };

    public async Task<bool> DeleteAsync(Guid organizationId, Guid branchId, Guid mediaId, CancellationToken ct)
    {
        var entity = await db.UploadedMedia.SingleOrDefaultAsync(
            m => m.MediaId == mediaId && m.OrganizationId == organizationId && m.BranchId == branchId, ct);
        if (entity is null) return false;
        await storage.DeleteAsync(entity.ObjectKey, ct);
        db.UploadedMedia.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
