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
///
/// Состав выгрузки обязан покрывать то, что стирает <see cref="OrganizationPurgeService"/>: это
/// единственный шанс клуба забрать своё. Раньше выгружались пять таблиц из примерно сорока
/// стираемых — клуб безвозвратно терял финансовую историю игроков, смены и кассовые операции,
/// чеки, платежи, тарифы и собственный персонал, не имея возможности их даже увидеть.
/// Соответствие держит OrganizationExportParityTests: новая стираемая таблица заставит решить,
/// выгружаем её или почему нет.
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
            await WriteEntryAsync(archive, "sale_lines.csv", await BuildSaleLinesAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "staff.csv", await BuildStaffAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "ledger.csv", await BuildLedgerAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "shifts.csv", await BuildShiftsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "cash_movements.csv", await BuildCashMovementsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "receipts.csv", await BuildReceiptsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "payments.csv", await BuildPaymentsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "tariffs.csv", await BuildTariffsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "player_packages.csv", await BuildPlayerPackagesAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "shop_orders.csv", await BuildShopOrdersAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "stock_movements.csv", await BuildStockMovementsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "branches.csv", await BuildBranchesAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "seats.csv", await BuildSeatsAsync(organizationId, cancellationToken), cancellationToken);
            await WriteEntryAsync(archive, "devices.csv", await BuildDevicesAsync(organizationId, cancellationToken), cancellationToken);
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

    /// <summary>
    /// Состав чеков. Без него sales.csv говорит только «на сколько», а клуб уносит историю продаж
    /// без ответа на «что продали».
    /// </summary>
    private async Task<string> BuildSaleLinesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var saleIds = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.OrganizationId == organizationId)
            .Select(sale => sale.PosSaleId)
            .ToArrayAsync(cancellationToken);

        var lines = await dbContext.PosSaleLines
            .AsNoTracking()
            .Where(line => saleIds.Contains(line.PosSaleId))
            .Select(line => new
            {
                line.PosSaleLineId,
                line.PosSaleId,
                line.ProductId,
                line.ProductName,
                line.Quantity,
                line.UnitPriceMinorUnits,
                line.LineTotalMinorUnits
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["sale_line_id", "sale_id", "product_id", "product_name", "quantity", "unit_price", "line_total"],
            lines.Select(line => new[]
            {
                line.PosSaleLineId.ToString(),
                line.PosSaleId.ToString(),
                line.ProductId.ToString(),
                line.ProductName,
                line.Quantity.ToString(CultureInfo.InvariantCulture),
                Money(line.UnitPriceMinorUnits),
                Money(line.LineTotalMinorUnits)
            }));
    }

    /// <summary>
    /// Персонал клуба. Пароли и токены сюда не попадают — они и клубу не нужны, и отдавать их
    /// файлом нельзя; уходит то, по чему человека узнают: имя, логин, контакты, роли.
    /// </summary>
    private async Task<string> BuildStaffAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var staff = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(user => user.OrganizationId == organizationId)
            .OrderBy(user => user.CreatedAtUtc)
            .Select(user => new { user.StaffUserId, user.UserName, user.DisplayName, user.Email, user.Phone, user.IsActive, user.CreatedAtUtc })
            .ToArrayAsync(cancellationToken);

        var assignments = await dbContext.StaffRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganizationId == organizationId)
            .Select(assignment => new { assignment.StaffUserId, assignment.RoleName })
            .ToArrayAsync(cancellationToken);
        var rolesByStaff = assignments
            .GroupBy(assignment => assignment.StaffUserId)
            .ToDictionary(group => group.Key, group => string.Join(" ", group.Select(role => role.RoleName).Order()));

        return Csv(
            ["staff_id", "login", "display_name", "email", "phone", "roles", "is_active", "created_at"],
            staff.Select(user => new[]
            {
                user.StaffUserId.ToString(),
                user.UserName,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Phone ?? string.Empty,
                rolesByStaff.GetValueOrDefault(user.StaffUserId, string.Empty),
                user.IsActive ? "true" : "false",
                Timestamp(user.CreatedAtUtc)
            }));
    }

    /// <summary>
    /// Движения по счетам игроков — то, из чего складывается их баланс и долг. Без этой выгрузки
    /// клуб не может ни вернуть людям деньги, ни объяснить, откуда взялась задолженность.
    /// </summary>
    private async Task<string> BuildLedgerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var entries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OrganizationId == organizationId)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new
            {
                entry.LedgerEntryId,
                entry.BranchId,
                entry.PlayerAccountId,
                entry.SessionId,
                entry.EntryType,
                entry.AccountType,
                entry.AmountMinorUnits,
                entry.QuantitySeconds,
                entry.CurrencyCode,
                entry.Description,
                entry.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["entry_id", "branch_id", "player_id", "session_id", "entry_type", "account_type", "amount", "seconds", "currency", "description", "created_at"],
            entries.Select(entry => new[]
            {
                entry.LedgerEntryId.ToString(),
                entry.BranchId.ToString(),
                entry.PlayerAccountId.ToString(),
                entry.SessionId?.ToString() ?? string.Empty,
                entry.EntryType,
                entry.AccountType,
                Money(entry.AmountMinorUnits),
                entry.QuantitySeconds.ToString(CultureInfo.InvariantCulture),
                entry.CurrencyCode,
                entry.Description,
                Timestamp(entry.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildShiftsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var shifts = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OrganizationId == organizationId)
            .OrderBy(shift => shift.OpenedAtUtc)
            .Select(shift => new
            {
                shift.ShiftId,
                shift.BranchId,
                shift.OpenedByStaffUserId,
                shift.ClosedByStaffUserId,
                shift.State,
                shift.CurrencyCode,
                shift.StartingCashMinorUnits,
                shift.CountedCashMinorUnits,
                shift.ExpectedCashMinorUnits,
                shift.DifferenceMinorUnits,
                shift.OpenedAtUtc,
                shift.ClosedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["shift_id", "branch_id", "opened_by", "closed_by", "state", "currency", "starting_cash", "counted_cash", "expected_cash", "difference", "opened_at", "closed_at"],
            shifts.Select(shift => new[]
            {
                shift.ShiftId.ToString(),
                shift.BranchId.ToString(),
                shift.OpenedByStaffUserId.ToString(),
                shift.ClosedByStaffUserId?.ToString() ?? string.Empty,
                shift.State,
                shift.CurrencyCode,
                Money(shift.StartingCashMinorUnits),
                Money(shift.CountedCashMinorUnits),
                Money(shift.ExpectedCashMinorUnits),
                Money(shift.DifferenceMinorUnits),
                Timestamp(shift.OpenedAtUtc),
                shift.ClosedAtUtc is null ? string.Empty : Timestamp(shift.ClosedAtUtc.Value)
            }));
    }

    private async Task<string> BuildCashMovementsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var movements = await dbContext.CashMovements
            .AsNoTracking()
            .Where(movement => movement.OrganizationId == organizationId)
            .OrderBy(movement => movement.CreatedAtUtc)
            .Select(movement => new
            {
                movement.CashMovementId,
                movement.BranchId,
                movement.ShiftId,
                movement.MovementType,
                movement.CurrencyCode,
                movement.AmountMinorUnits,
                movement.Reason,
                movement.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["movement_id", "branch_id", "shift_id", "movement_type", "currency", "amount", "reason", "created_at"],
            movements.Select(movement => new[]
            {
                movement.CashMovementId.ToString(),
                movement.BranchId.ToString(),
                movement.ShiftId.ToString(),
                movement.MovementType,
                movement.CurrencyCode,
                Money(movement.AmountMinorUnits),
                movement.Reason,
                Timestamp(movement.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildReceiptsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var receipts = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.OrganizationId == organizationId)
            .OrderBy(receipt => receipt.CreatedAtUtc)
            .Select(receipt => new
            {
                receipt.ReceiptId,
                receipt.BranchId,
                receipt.PosSaleId,
                receipt.SessionId,
                receipt.ReceiptNumber,
                receipt.ReceiptType,
                receipt.CurrencyCode,
                receipt.TotalMinorUnits,
                receipt.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["receipt_id", "branch_id", "sale_id", "session_id", "number", "type", "currency", "total", "created_at"],
            receipts.Select(receipt => new[]
            {
                receipt.ReceiptId.ToString(),
                receipt.BranchId.ToString(),
                receipt.PosSaleId?.ToString() ?? string.Empty,
                receipt.SessionId?.ToString() ?? string.Empty,
                receipt.ReceiptNumber,
                receipt.ReceiptType,
                receipt.CurrencyCode,
                Money(receipt.TotalMinorUnits),
                Timestamp(receipt.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildPaymentsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.OrganizationId == organizationId)
            .OrderBy(payment => payment.CreatedAtUtc)
            .Select(payment => new
            {
                payment.PaymentId,
                payment.BranchId,
                payment.ShiftId,
                payment.PosSaleId,
                payment.SessionId,
                payment.PaymentKind,
                payment.PaymentMethod,
                payment.Provider,
                payment.CurrencyCode,
                payment.AmountMinorUnits,
                payment.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["payment_id", "branch_id", "shift_id", "sale_id", "session_id", "kind", "method", "provider", "currency", "amount", "created_at"],
            payments.Select(payment => new[]
            {
                payment.PaymentId.ToString(),
                payment.BranchId.ToString(),
                payment.ShiftId.ToString(),
                payment.PosSaleId?.ToString() ?? string.Empty,
                payment.SessionId?.ToString() ?? string.Empty,
                payment.PaymentKind,
                payment.PaymentMethod,
                payment.Provider,
                payment.CurrencyCode,
                Money(payment.AmountMinorUnits),
                Timestamp(payment.CreatedAtUtc)
            }));
    }

    /// <summary>
    /// Тарифы вместе с версиями: цена без даты, с которой она действовала, не объясняет ни одного
    /// старого чека.
    /// </summary>
    private async Task<string> BuildTariffsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var tariffs = await dbContext.Tariffs
            .AsNoTracking()
            .Where(tariff => tariff.OrganizationId == organizationId)
            .Select(tariff => new { tariff.TariffId, tariff.BranchId, tariff.Name, tariff.IsActive })
            .ToArrayAsync(cancellationToken);
        var tariffById = tariffs.ToDictionary(tariff => tariff.TariffId);

        var versions = await dbContext.TariffVersions
            .AsNoTracking()
            .Where(version => version.OrganizationId == organizationId)
            .OrderBy(version => version.EffectiveFromUtc)
            .Select(version => new
            {
                version.TariffId,
                version.VersionNumber,
                version.CurrencyCode,
                version.PricePerMinuteMinorUnits,
                version.MinimumBillableMinutes,
                version.EffectiveFromUtc,
                version.RetiredAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["tariff_id", "branch_id", "name", "is_active", "version", "currency", "price_per_minute", "minimum_minutes", "effective_from", "retired_at"],
            versions.Select(version =>
            {
                var tariff = tariffById.GetValueOrDefault(version.TariffId);
                return new[]
                {
                    version.TariffId.ToString(),
                    tariff?.BranchId.ToString() ?? string.Empty,
                    tariff?.Name ?? string.Empty,
                    tariff is null ? string.Empty : tariff.IsActive ? "true" : "false",
                    version.VersionNumber.ToString(CultureInfo.InvariantCulture),
                    version.CurrencyCode,
                    Money(version.PricePerMinuteMinorUnits),
                    version.MinimumBillableMinutes.ToString(CultureInfo.InvariantCulture),
                    Timestamp(version.EffectiveFromUtc),
                    version.RetiredAtUtc is null ? string.Empty : Timestamp(version.RetiredAtUtc.Value)
                };
            }));
    }

    private async Task<string> BuildPlayerPackagesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var packages = await dbContext.PlayerPackages
            .AsNoTracking()
            .Where(package => package.OrganizationId == organizationId)
            .OrderBy(package => package.PurchasedAtUtc)
            .Select(package => new
            {
                package.PlayerPackageId,
                package.BranchId,
                package.PlayerAccountId,
                package.Name,
                package.CurrencyCode,
                package.PurchasedPriceMinorUnits,
                package.IncludedSeconds,
                package.BonusSeconds,
                package.PurchasedAtUtc,
                package.ExpiresAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["package_id", "branch_id", "player_id", "name", "currency", "price", "included_seconds", "bonus_seconds", "purchased_at", "expires_at"],
            packages.Select(package => new[]
            {
                package.PlayerPackageId.ToString(),
                package.BranchId.ToString(),
                package.PlayerAccountId.ToString(),
                package.Name,
                package.CurrencyCode,
                Money(package.PurchasedPriceMinorUnits),
                package.IncludedSeconds.ToString(CultureInfo.InvariantCulture),
                package.BonusSeconds.ToString(CultureInfo.InvariantCulture),
                Timestamp(package.PurchasedAtUtc),
                package.ExpiresAtUtc is null ? string.Empty : Timestamp(package.ExpiresAtUtc.Value)
            }));
    }

    private async Task<string> BuildShopOrdersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var orders = await dbContext.ShopOrders
            .AsNoTracking()
            .Where(order => order.OrganizationId == organizationId)
            .OrderBy(order => order.PlacedAtUtc)
            .Select(order => new
            {
                order.ShopOrderId,
                order.BranchId,
                order.PlayerAccountId,
                order.SeatId,
                order.Status,
                order.TotalMinorUnits,
                order.CurrencyCode,
                order.PlacedAtUtc,
                order.DeliveredAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["order_id", "branch_id", "player_id", "seat_id", "status", "currency", "total", "placed_at", "delivered_at"],
            orders.Select(order => new[]
            {
                order.ShopOrderId.ToString(),
                order.BranchId.ToString(),
                order.PlayerAccountId.ToString(),
                order.SeatId.ToString(),
                order.Status,
                order.CurrencyCode,
                Money(order.TotalMinorUnits),
                Timestamp(order.PlacedAtUtc),
                order.DeliveredAtUtc is null ? string.Empty : Timestamp(order.DeliveredAtUtc.Value)
            }));
    }

    private async Task<string> BuildStockMovementsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(movement => movement.OrganizationId == organizationId)
            .OrderBy(movement => movement.CreatedAtUtc)
            .Select(movement => new
            {
                movement.StockMovementId,
                movement.BranchId,
                movement.ProductId,
                movement.MovementType,
                movement.QuantityDelta,
                movement.CurrencyCode,
                movement.UnitCostMinorUnits,
                movement.Reason,
                movement.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["movement_id", "branch_id", "product_id", "movement_type", "quantity_delta", "currency", "unit_cost", "reason", "created_at"],
            movements.Select(movement => new[]
            {
                movement.StockMovementId.ToString(),
                movement.BranchId.ToString(),
                movement.ProductId.ToString(),
                movement.MovementType,
                movement.QuantityDelta.ToString(CultureInfo.InvariantCulture),
                movement.CurrencyCode,
                Money(movement.UnitCostMinorUnits),
                movement.Reason,
                Timestamp(movement.CreatedAtUtc)
            }));
    }

    private async Task<string> BuildBranchesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId)
            .OrderBy(branch => branch.Name)
            .Select(branch => new { branch.BranchId, branch.Slug, branch.Name, branch.City, branch.Address, branch.Phone })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["branch_id", "slug", "name", "city", "address", "phone"],
            branches.Select(branch => new[]
            {
                branch.BranchId.ToString(),
                branch.Slug,
                branch.Name,
                branch.City,
                branch.Address ?? string.Empty,
                branch.Phone ?? string.Empty
            }));
    }

    /// <summary>Карта зала: места вместе с залами, в которых они стоят.</summary>
    private async Task<string> BuildSeatsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var zoneNames = await dbContext.Zones
            .AsNoTracking()
            .Where(zone => zone.OrganizationId == organizationId)
            .ToDictionaryAsync(zone => zone.ZoneId, zone => zone.Name, cancellationToken);

        var seats = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.OrganizationId == organizationId)
            .OrderBy(seat => seat.SortOrder)
            .Select(seat => new { seat.SeatId, seat.BranchId, seat.ZoneId, seat.Name, seat.SortOrder })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["seat_id", "branch_id", "zone", "name", "sort_order"],
            seats.Select(seat => new[]
            {
                seat.SeatId.ToString(),
                seat.BranchId.ToString(),
                zoneNames.GetValueOrDefault(seat.ZoneId, string.Empty),
                seat.Name,
                seat.SortOrder.ToString(CultureInfo.InvariantCulture)
            }));
    }

    /// <summary>
    /// Машины клуба. Ключи устройств и их секреты не выгружаются: на новой платформе они
    /// бесполезны, а в файле — опасны.
    /// </summary>
    private async Task<string> BuildDevicesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.OrganizationId == organizationId)
            .OrderBy(device => device.DisplayName)
            .Select(device => new
            {
                device.DeviceId,
                device.BranchId,
                device.MachineName,
                device.DisplayName,
                device.Role,
                device.EnrollmentState,
                device.EnrolledAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Csv(
            ["device_id", "branch_id", "machine_name", "display_name", "role", "enrollment_state", "enrolled_at"],
            devices.Select(device => new[]
            {
                device.DeviceId.ToString(),
                device.BranchId.ToString(),
                device.MachineName,
                device.DisplayName,
                device.Role,
                device.EnrollmentState,
                Timestamp(device.EnrolledAtUtc)
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
