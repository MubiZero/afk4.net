using System.Collections.Concurrent;
using AFK4.Platform.Api.Media;

namespace AFK4.Platform.Api.Tests.Fakes;

/// <summary>Обложки «из Steam» без сети: какие номера приложений знает — те и отдаёт.</summary>
public sealed class FakeSteamCoverSource : ISteamCoverSource
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46];

    public readonly ConcurrentDictionary<string, byte[]> Known = new();

    public Task<(byte[] Bytes, string ContentType)?> FetchAsync(string steamAppId, CancellationToken ct) =>
        Task.FromResult<(byte[] Bytes, string ContentType)?>(Known.TryGetValue(steamAppId, out var bytes) ? (bytes, "image/jpeg") : null);
}
