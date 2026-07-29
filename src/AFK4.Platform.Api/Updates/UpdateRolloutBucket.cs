using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AFK4.Platform.Api.Updates;

public static class UpdateRolloutBucket
{
    public static int GetBucket(Guid rolloutId, Guid deviceId)
    {
        Span<byte> input = stackalloc byte[32];
        rolloutId.TryWriteBytes(input[..16], bigEndian: true, out _);
        deviceId.TryWriteBytes(input[16..], bigEndian: true, out _);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % 100);
    }

    public static bool IsEligible(Guid rolloutId, Guid deviceId, int batchPercent) =>
        batchPercent >= 100 || batchPercent > 0 && GetBucket(rolloutId, deviceId) < batchPercent;
}
