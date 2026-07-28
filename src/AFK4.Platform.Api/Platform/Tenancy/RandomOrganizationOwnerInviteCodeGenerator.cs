using System.Security.Cryptography;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class RandomOrganizationOwnerInviteCodeGenerator : IOrganizationOwnerInviteCodeGenerator
{
    // 16 bytes -> 32 lowercase hex chars -> 128 bits of entropy.
    private const int CodeByteLength = 16;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeByteLength);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
