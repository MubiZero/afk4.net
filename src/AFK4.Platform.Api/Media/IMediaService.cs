using AFK4.Shared.Contracts.Media;

namespace AFK4.Platform.Api.Media;

public sealed record MediaServiceResult(bool Succeeded, string? Error, UploadedMediaDto? Media);
