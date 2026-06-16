using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.FloorMap;

internal static class FloorMapEtag
{
    public static string Compute(
        IEnumerable<ZoneEntity> zones,
        IEnumerable<SeatEntity> seats,
        IEnumerable<WallEntity> walls)
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
                .Append('|')
                .Append(Coord(zone.GeoX)).Append(',').Append(Coord(zone.GeoY)).Append(',')
                .Append(Coord(zone.GeoWidth)).Append(',').Append(Coord(zone.GeoHeight))
                .Append('|')
                .Append(zone.Color ?? string.Empty)
                .Append('|')
                .Append(zone.ZoneType ?? string.Empty)
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
                .Append('|')
                .Append(Coord(seat.PosX)).Append(',').Append(Coord(seat.PosY)).Append(',')
                .Append(seat.Rotation.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(seat.SeatType)
                .Append('\n');
        }

        foreach (var wall in walls.OrderBy(wall => wall.WallId))
        {
            builder.Append("w|")
                .Append(wall.WallId.ToString("D"))
                .Append('|')
                .Append(wall.X1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.Y1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.X2.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.Y2.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "\"" + Convert.ToHexString(hashBytes).ToLowerInvariant() + "\"";
    }

    private static string Coord(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
