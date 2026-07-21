using AFK4.Shared.Contracts.Media;

namespace AFK4.Platform.Api.Media;

public sealed record MediaServiceResult(bool Succeeded, string? Error, UploadedMediaDto? Media);

public interface IMediaService
{
    // Валидирует, заливает, пишет запись; при purpose с "одиночным" смыслом (branch-logo)
    // удаляет прежний объект того же (branchId, purpose).
    Task<MediaServiceResult> UploadAsync(Guid organizationId, Guid branchId, Guid staffUserId,
        string purpose, string declaredContentType, Stream content, long sizeBytes, CancellationToken ct);
    Task<bool> DeleteAsync(Guid organizationId, Guid branchId, Guid mediaId, CancellationToken ct);
}
