using System.Globalization;
using System.IO.Compression;
using System.Text;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Offboarding;

/// <summary>
/// Выгрузка данных клуба перед уходом: ZIP из CSV, собранный в памяти и отданный ответом. Файла на
/// диске не остаётся и фоновой сборки нет — при масштабе клуба это лишняя машинерия, которая ещё и
/// оставляет персональные данные лежать где-то до следующей уборки.
/// </summary>
public sealed class OrganizationExportService(PlatformDbContext dbContext)
{
    public async Task<byte[]> BuildArchiveAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(archive, "players.csv", await BuildPlayersAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "sessions.csv", await BuildSessionsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "sales.csv", await BuildSalesAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "products.csv", await BuildProductsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "reservations.csv", await BuildReservationsAsync(organizationId, cancellationToken), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, string content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        // BOM обязателен: без него Excel в русской локали открывает кириллицу кракозябрами, и
        // выгрузка, формально верная, оказывается нечитаемой для того, кому она и нужна.
        await stream.WriteAsync(Encoding.UTF8.GetPreamble(), cancellationToken);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(content), cancellationToken);
    }

    private async Task<string> BuildPlayersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var players = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.OrganizationId == organizationId)
            .OrderBy(player => player.CreatedAtUtc)
            .Select(player => new
            {
                player.PlayerAccountId,
                player.DisplayName,
                player.PhoneNumber,
                player.Email,
                player.IsActive,
                player.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            // Баланс игрока — величина вычисляемая (сумма движений по счёту), собственного поля у
            // него нет; выдумывать его в выгрузке значило бы отдать клубу цифру, которой в системе
            // не существует.
            ["player_id", "display_name", "phone", "email", "is_active", "created_at"],
            players.Select(player => new[]
            {
                player.PlayerAccountId.ToString(),
                player.DisplayName,
                player.PhoneNumber ?? string.Empty,
                player.Email ?? string.Empty,
                player.IsActive ? "true" : "false",
                Timestamp(player.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildSessionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.OrganizationId == organizationId)
            .OrderBy(session => session.StartedAtUtc)
            .Select(session => new
            {
                session.SessionId,
                session.BranchId,
                session.SeatId,
                session.PlayerAccountId,
                session.State,
                session.StartedAtUtc,
                session.EndedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["session_id", "branch_id", "seat_id", "player_id", "state", "started_at", "ended_at"],
            sessions.Select(session => new[]
            {
                session.SessionId.ToString(),
                session.BranchId.ToString(),
                session.SeatId.ToString(),
                session.PlayerAccountId?.ToString() ?? string.Empty,
                session.State,
                session.StartedAtUtc is null ? string.Empty : Timestamp(session.StartedAtUtc.Value),
                session.EndedAtUtc is null ? string.Empty : Timestamp(session.EndedAtUtc.Value)
            }));
    }

    private async Task<string> BuildSalesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var sales = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.OrganizationId == organizationId)
            .OrderBy(sale => sale.CreatedAtUtc)
            .Select(sale => new
            {
                sale.PosSaleId,
                sale.BranchId,
                sale.ShiftId,
                sale.State,
                sale.TotalMinorUnits,
                sale.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["sale_id", "branch_id", "shift_id", "state", "total", "created_at"],
            sales.Select(sale => new[]
            {
                sale.PosSaleId.ToString(),
                sale.BranchId.ToString(),
                sale.ShiftId.ToString(),
                sale.State,
                Money(sale.TotalMinorUnits),
                Timestamp(sale.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildProductsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var products = await dbContext.PosProducts
            .AsNoTracking()
            .Where(product => product.OrganizationId == organizationId)
            .OrderBy(product => product.Name)
            .Select(product => new { product.ProductId, product.Name, product.Sku, product.PriceMinorUnits, product.IsActive })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["product_id", "name", "sku", "price", "is_active"],
            products.Select(product => new[]
            {
                product.ProductId.ToString(),
                product.Name,
                product.Sku,
                Money(product.PriceMinorUnits),
                product.IsActive ? "true" : "false"
            }));
    }

    private async Task<string> BuildReservationsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.OrganizationId == organizationId)
            .OrderBy(reservation => reservation.StartsAtUtc)
            .Select(reservation => new
            {
                reservation.ReservationId,
                reservation.BranchId,
                reservation.SeatId,
                reservation.CustomerName,
                reservation.PhoneNumber,
                reservation.State,
                reservation.StartsAtUtc,
                reservation.EndsAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["reservation_id", "branch_id", "seat_id", "customer_name", "phone", "state", "starts_at", "ends_at"],
            reservations.Select(reservation => new[]
            {
                reservation.ReservationId.ToString(),
                reservation.BranchId.ToString(),
                reservation.SeatId?.ToString() ?? string.Empty,
                reservation.CustomerName,
                reservation.PhoneNumber ?? string.Empty,
                reservation.State,
                Timestamp(reservation.StartsAtUtc),
                Timestamp(reservation.EndsAtUtc)
            }));
    }

    // Разделитель — точка с запятой: с запятой Excel в русской локали кладёт всю строку в одну
    // ячейку, и выгрузка выглядит испорченной.
    private static string Csv(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', header.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(';', row.Select(Escape)));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Поле с разделителем, кавычкой или переводом строки берётся в кавычки, внутренние кавычки
    /// удваиваются. Без этого имя «Иванов; Пётр» разъезжается на две колонки и сдвигает всю строку.
    /// </summary>
    private static string Escape(string value)
    {
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Money(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
