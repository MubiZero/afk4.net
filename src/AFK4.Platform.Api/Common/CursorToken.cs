using System;
using System.Text;

namespace AFK4.Platform.Api.Common;

// Opaque keyset-pagination cursor for (CreatedAtUtc DESC, Id DESC) ordered lists.
// Encodes "t<utcTicks>:<guid>" as base64. Decode never throws on user input — bad cursors
// yield false so the caller falls back to the first page.
//
// The moment is kept whole, not in milliseconds: Postgres stores microseconds, and a cursor cut
// to the millisecond sat after rows of its own millisecond — the next page skipped them. The
// old "<unixMillis>:<guid>" form is still read, so a page already open in an app keeps paging.
public static class CursorToken
{
    public static string Encode(DateTimeOffset timestamp, Guid id)
    {
        var payload = $"t{timestamp.UtcTicks}:{id:N}";
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

            var moment = payload[..separator];
            var inTicks = moment.StartsWith('t');
            if (!long.TryParse(inTicks ? moment[1..] : moment, out var value) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "N", out id))
            {
                return false;
            }

            if (inTicks)
            {
                if (value < DateTimeOffset.MinValue.UtcTicks || value > DateTimeOffset.MaxValue.UtcTicks)
                {
                    return false;
                }

                timestamp = new DateTimeOffset(value, TimeSpan.Zero);
                return true;
            }

            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
