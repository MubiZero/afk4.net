namespace AFK4.Platform.Api.Media;

public interface IMediaStorage
{
    // Кладёт объект, возвращает публичный URL. objectKey формирует вызывающий.
    Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct);
    Task DeleteAsync(string objectKey, CancellationToken ct);
    string PublicUrlFor(string objectKey);
}
