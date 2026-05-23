using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AFK4.Platform.Api.Platform.Idempotency;

public static class IdempotencyKeyHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string HashRequest<T>(T request)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        var hash = SHA256.HashData(json);
        return Convert.ToHexString(hash);
    }
}
