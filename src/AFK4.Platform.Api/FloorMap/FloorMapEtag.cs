using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.FloorMap;

internal static class FloorMapEtag
{
    public static string Compute(IEnumerable<ZoneEntity> zones, IEnumerable<SeatEntity> seats)
    {
        var builder = new StringBuilder();
        foreach (var zone in zones.OrderBy(zone => zone.ZoneId))
        {
            builder.Append("z|")
                .Append(zone.ZoneId.ToString("D"))
                .Append('|')
                .Append(zone.SortOrder.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(zone.Name)
                .Append('\n');
        }

        foreach (var seat in seats.OrderBy(seat => seat.SeatId))
        {
            builder.Append("s|")
                .Append(seat.SeatId.ToString("D"))
                .Append('|')
                .Append(seat.ZoneId.ToString("D"))
                .Append('|')
                .Append(seat.SortOrder.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(seat.Name)
                .Append('\n');
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "\"" + Convert.ToHexString(hashBytes).ToLowerInvariant() + "\"";
    }
}
