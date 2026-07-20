namespace AFK4.Shared.Contracts.Media;

public sealed record UploadedMediaDto(
    Guid MediaId,
    string Url,
    string ContentType,
    long SizeBytes);
