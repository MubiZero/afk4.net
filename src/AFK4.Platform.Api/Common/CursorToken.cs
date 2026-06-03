using System;
using System.Text;

namespace AFK4.Platform.Api.Common;

// Opaque keyset-pagination cursor for (CreatedAtUtc DESC, Id DESC) ordered lists.
// Encodes "<unixMillis>:<guid>" as URL-safe base64. Decode never throws on user
// input — bad cursors yield false so the caller falls back to the first page.
public static class CursorToken
{
    public static string Encode(DateTimeOffset timestamp, Guid id)
    {
        var payload = $"{timestamp.ToUnixTimeMilliseconds()}:{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var separator = payload.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            if (!long.TryParse(payload[..separator], out var unixMillis) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "N", out id))
            {
                return false;
            }

            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMillis);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
