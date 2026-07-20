using System.Collections.Concurrent;
using AFK4.Platform.Api.Media;

namespace AFK4.Platform.Api.Tests.Fakes;

public sealed class FakeMediaStorage : IMediaStorage
{
    public readonly ConcurrentDictionary<string, byte[]> Objects = new();

    public async Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        Objects[objectKey] = ms.ToArray();
        return PublicUrlFor(objectKey);
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    { Objects.TryRemove(objectKey, out _); return Task.CompletedTask; }

    public string PublicUrlFor(string objectKey) => $"https://media.test/{objectKey}";
}
