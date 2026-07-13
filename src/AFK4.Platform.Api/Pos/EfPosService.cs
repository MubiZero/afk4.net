using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Receipts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Pos;

public sealed class EfPosService(
    PlatformDbContext dbContext,
    IPaymentProvider paymentProvider,
    IReceiptNumberGenerator receiptNumberGenerator,
    TimeProvider timeProvider,
    IInventoryCostService inventoryCostService,
    ILowStockNotifier? lowStockNotifier = null) : IPosService
{
    private const string CreateSaleOperation = "pos-sale-create";
    private const string PaySaleOperation = "pos-sale-pay";
    private const string RefundSaleOperation = "pos-sale-refund";
    private const string VoidSaleOperation = "pos-sale-void";
    private const string SaleReceiptType = "sale";
    private const string RefundReceiptType = "refund";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<PosSaleDto>> CreateSaleAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePosSaleRequest request,
        CancellationToken cancellationToken)
    {
        var requestHashInput = new
        {
            BranchId = branchId,
            Request = request
        };
        var idempotency = await GetExistingIdempotencyAsync<PosSaleDto, object>(
            request.OrganizationId,
            branchId,
            CreateSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var validation = ValidateCreateSaleRequest(request);
        if (validation is not null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid(validation);
        }

        var shift = await dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ShiftId == request.ShiftId &&
                    candidate.State == ShiftStateNames.Open,
                cancellationToken);

        if (shift is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("An open shift is required to create a POS sale.");
        }

        if (request.PlayerAccountId is Guid playerAccountId)
        {
            var playerExists = await dbContext.PlayerAccounts
                .AsNoTracking()
                .AnyAsync(
                    player =>
                        player.OrganizationId == request.OrganizationId &&
                        player.HomeBranchId == branchId &&
                        player.PlayerAccountId == playerAccountId &&
                        player.IsActive,
                    cancellationToken);

            if (!playerExists)
            {
                return BillingCommandServiceResult<PosSaleDto>.Missing("Player account was not found.");
            }
        }

        var requestedProductIds = request.Lines
            .Select(line => line.ProductId)
            .Distinct()
            .ToList();
        var products = await dbContext.PosProducts
            .AsNoTracking()
            .Where(product =>
                product.OrganizationId == request.OrganizationId &&
                product.BranchId == branchId &&
                product.IsActive &&
                requestedProductIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        if (products.Count != requestedProductIds.Count)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("Product was not found.");
        }

        var currencyCode = products.Values.First().CurrencyCode;
        if (products.Values.Any(product => !string.Equals(product.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("POS sale lines must use one currency.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var sale = new PosSaleEntity
            {
                PosSaleId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                ShiftId = shift.ShiftId,
                CreatedByStaffUserId = actorStaffUserId,
                PlayerAccountId = request.PlayerAccountId,
                SessionId = request.SessionId,
                State = PosSaleStateNames.Draft,
                CurrencyCode = currencyCode.ToUpperInvariant(),
                TotalMinorUnits = 0,
                CreatedAtUtc = now
            };

            var lines = request.Lines
                .Select(line =>
                {
                    var product = products[line.ProductId];
                    var lineTotal = product.PriceMinorUnits * line.Quantity;

                    return new PosSaleLineEntity
                    {
                        PosSaleLineId = Guid.NewGuid(),
                        PosSaleId = sale.PosSaleId,
                        ProductId = product.ProductId,
                        ProductName = product.Name,
                        Quantity = line.Quantity,
                        CurrencyCode = product.CurrencyCode.ToUpperInvariant(),
                        UnitPriceMinorUnits = product.PriceMinorUnits,
                        UnitCostMinorUnits = product.TrackStock ? product.AvgCostMinorUnits : 0,
                        LineTotalMinorUnits = lineTotal,
                        TrackStock = product.TrackStock,
                        AllowNegativeStock = product.AllowNegativeStock
                    };
                })
                .ToList();
            sale.TotalMinorUnits = lines.Sum(line => line.LineTotalMinorUnits);

            dbContext.PosSales.Add(sale);
            dbContext.PosSaleLines.AddRange(lines);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(sale, lines);
            AddIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                CreateSaleOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosSaleDto, object>(
            request.OrganizationId,
            branchId,
            CreateSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> PaySaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var saleScope = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);

        if (saleScope is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        var requestHashInput = new
        {
            PosSaleId = posSaleId,
            Request = request
        };
        var idempotency = await GetExistingIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            PaySaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId != saleScope.OrganizationId)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Organization id does not match the POS sale.");
        }

        var expectedAmount = new MoneyDto(saleScope.CurrencyCode, saleScope.TotalMinorUnits);
        var paymentAuthorization = await paymentProvider.AcceptManualPaymentAsync(request, expectedAmount, cancellationToken);
        if (!paymentAuthorization.Succeeded || paymentAuthorization.Response is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid(paymentAuthorization.Error ?? "Payment was rejected.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await dbContext.PosSales
                .SingleAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
            var lines = await LoadSaleLinesAsync(posSaleId, cancellationToken);

            if (sale.State is not PosSaleStateNames.Draft and not PosSaleStateNames.PendingPayment)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("Only draft POS sales can be paid.");
            }

            var stockValidation = await ValidateStockForPaymentAsync(sale, lines, cancellationToken);
            if (stockValidation is not null)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(stockValidation);
            }

            var now = timeProvider.GetUtcNow();
            foreach (var line in lines.Where(line => line.TrackStock))
            {
                dbContext.StockMovements.Add(new StockMovementEntity
                {
                    StockMovementId = Guid.NewGuid(),
                    OrganizationId = sale.OrganizationId,
                    BranchId = sale.BranchId,
                    ProductId = line.ProductId,
                    MovementType = StockMovementTypeNames.Sale,
                    QuantityDelta = -line.Quantity,
                    CurrencyCode = line.CurrencyCode,
                    UnitCostMinorUnits = line.UnitCostMinorUnits,
                    Reason = $"POS sale {sale.PosSaleId}",
                    CreatedByStaffUserId = actorStaffUserId,
                    CreatedAtUtc = now
                });
            }

            dbContext.Payments.Add(new PaymentEntity
            {
                PaymentId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                PosSaleId = sale.PosSaleId,
                ShiftId = sale.ShiftId,
                CreatedByStaffUserId = actorStaffUserId,
                PaymentKind = "payment",
                Provider = paymentAuthorization.Response.Provider,
                PaymentMethod = paymentAuthorization.Response.PaymentMethod,
                CurrencyCode = paymentAuthorization.Response.Amount.CurrencyCode,
                AmountMinorUnits = paymentAuthorization.Response.Amount.MinorUnits,
                Note = paymentAuthorization.Response.Note,
                CreatedAtUtc = now
            });

            var receiptNumber = await receiptNumberGenerator.GenerateAsync(
                sale.OrganizationId,
                sale.BranchId,
                SaleReceiptType,
                now,
                cancellationToken);
            var branchLocale = await dbContext.Branches
                .Where(branch => branch.BranchId == sale.BranchId)
                .Select(branch => branch.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken) ?? "ru";
            var receipt = new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                PosSaleId = sale.PosSaleId,
                ReceiptNumber = receiptNumber,
                ReceiptType = SaleReceiptType,
                CurrencyCode = sale.CurrencyCode,
                TotalMinorUnits = sale.TotalMinorUnits,
                Locale = branchLocale,
                CreatedAtUtc = now
            };
            dbContext.Receipts.Add(receipt);

            sale.State = PosSaleStateNames.Paid;
            sale.PaidAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (lowStockNotifier is not null)
            {
                var soldProductIds = lines.Where(line => line.TrackStock).Select(line => line.ProductId).Distinct().ToList();
                if (soldProductIds.Count > 0)
                {
                    await lowStockNotifier.EvaluateProductsAsync(sale.OrganizationId, sale.BranchId, soldProductIds, cancellationToken);
                }
            }

            var response = ToDto(sale, lines, receipt);
            AddIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                PaySaleOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            PaySaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> RefundSaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        var saleScope = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);

        if (saleScope is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        var requestHashInput = new
        {
            PosSaleId = posSaleId,
            Request = request
        };
        var idempotency = await GetExistingIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            RefundSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId != saleScope.OrganizationId)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Organization id does not match the POS sale.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Refund reason is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await dbContext.PosSales
                .SingleAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
            var lines = await LoadSaleLinesAsync(posSaleId, cancellationToken);

            if (sale.State != PosSaleStateNames.Paid)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("Only paid POS sales can be refunded.");
            }

            var trackedProductLines = lines
                .Where(line => line.TrackStock)
                .GroupBy(line => line.ProductId)
                .ToList();
            if (trackedProductLines.Any(productLines =>
                    productLines.Select(line => line.UnitCostMinorUnits).Distinct().Skip(1).Any()))
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(
                    "Refunded lines for the same product must have the same cost snapshot.");
            }

            var now = timeProvider.GetUtcNow();
            foreach (var productLines in trackedProductLines)
            {
                await inventoryCostService.ReconcileInboundAsync(
                    sale.OrganizationId,
                    sale.BranchId,
                    productLines.Key,
                    productLines.Sum(line => line.Quantity),
                    productLines.First().UnitCostMinorUnits,
                    cancellationToken);

                foreach (var line in productLines)
                {
                    dbContext.StockMovements.Add(new StockMovementEntity
                    {
                        StockMovementId = Guid.NewGuid(),
                        OrganizationId = sale.OrganizationId,
                        BranchId = sale.BranchId,
                        ProductId = line.ProductId,
                        MovementType = StockMovementTypeNames.Refund,
                        QuantityDelta = line.Quantity,
                        CurrencyCode = line.CurrencyCode,
                        UnitCostMinorUnits = line.UnitCostMinorUnits,
                        Reason = request.Reason.Trim(),
                        CreatedByStaffUserId = actorStaffUserId,
                        CreatedAtUtc = now
                    });
                }
            }

            var originalPaymentMethod = await dbContext.Payments
                .AsNoTracking()
                .Where(payment =>
                    payment.PosSaleId == sale.PosSaleId &&
                    payment.PaymentKind == "payment")
                .OrderBy(payment => payment.CreatedAtUtc)
                .Select(payment => payment.PaymentMethod)
                .FirstOrDefaultAsync(cancellationToken) ?? PaymentMethodNames.Cash;

            dbContext.Payments.Add(new PaymentEntity
            {
                PaymentId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                PosSaleId = sale.PosSaleId,
                ShiftId = sale.ShiftId,
                CreatedByStaffUserId = actorStaffUserId,
                PaymentKind = "refund",
                Provider = "manual",
                PaymentMethod = originalPaymentMethod,
                CurrencyCode = sale.CurrencyCode,
                AmountMinorUnits = -sale.TotalMinorUnits,
                Note = request.Reason.Trim(),
                CreatedAtUtc = now
            });

            var receiptNumber = await receiptNumberGenerator.GenerateAsync(
                sale.OrganizationId,
                sale.BranchId,
                RefundReceiptType,
                now,
                cancellationToken);
            var branchLocale = await dbContext.Branches
                .Where(branch => branch.BranchId == sale.BranchId)
                .Select(branch => branch.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken) ?? "ru";
            var receipt = new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                PosSaleId = sale.PosSaleId,
                ReceiptNumber = receiptNumber,
                ReceiptType = RefundReceiptType,
                CurrencyCode = sale.CurrencyCode,
                TotalMinorUnits = sale.TotalMinorUnits,
                Locale = branchLocale,
                CreatedAtUtc = now
            };
            dbContext.Receipts.Add(receipt);

            sale.State = PosSaleStateNames.Refunded;
            sale.RefundReason = request.Reason.Trim();
            sale.RefundedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(sale, lines, receipt);
            AddIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                RefundSaleOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            RefundSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> VoidSaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        VoidPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        var saleScope = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);

        if (saleScope is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        var requestHashInput = new
        {
            PosSaleId = posSaleId,
            Request = request
        };
        var idempotency = await GetExistingIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            VoidSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId != saleScope.OrganizationId)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Organization id does not match the POS sale.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Void reason is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await dbContext.PosSales
                .SingleAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
            var lines = await LoadSaleLinesAsync(posSaleId, cancellationToken);

            if (sale.State is not PosSaleStateNames.Draft and not PosSaleStateNames.PendingPayment)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("Only draft POS sales can be voided.");
            }

            var now = timeProvider.GetUtcNow();
            sale.State = PosSaleStateNames.Voided;
            sale.VoidReason = request.Reason.Trim();
            sale.VoidedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(sale, lines);
            AddIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                VoidSaleOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosSaleDto, object>(
            saleScope.OrganizationId,
            saleScope.BranchId,
            VoidSaleOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> GetSaleAsync(
        Guid organizationId,
        Guid posSaleId,
        CancellationToken cancellationToken)
    {
        var sale = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.PosSaleId == posSaleId,
                cancellationToken);

        if (sale is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        var lines = await LoadSaleLinesAsync(posSaleId, cancellationToken);
        var latestReceipt = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.OrganizationId == organizationId &&
                receipt.PosSaleId == posSaleId)
            .OrderByDescending(receipt => receipt.CreatedAtUtc)
            .ThenByDescending(receipt => receipt.ReceiptId)
            .FirstOrDefaultAsync(cancellationToken);
        var shopOrderId = await dbContext.ShopOrders
            .AsNoTracking()
            .Where(order =>
                order.OrganizationId == organizationId &&
                order.PosSaleId == posSaleId)
            .Select(order => (Guid?)order.ShopOrderId)
            .SingleOrDefaultAsync(cancellationToken);

        return BillingCommandServiceResult<PosSaleDto>.Ok(ToDto(sale, lines, latestReceipt, shopOrderId));
    }

    private static string? ValidateCreateSaleRequest(CreatePosSaleRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (request.ShiftId == Guid.Empty)
        {
            return "Shift id is required.";
        }

        if (request.Lines.Count == 0)
        {
            return "POS sale requires at least one line.";
        }

        if (request.PlayerAccountId == Guid.Empty)
        {
            return "Player account id must be omitted or non-empty.";
        }

        if (request.Lines.Any(line => line.ProductId == Guid.Empty))
        {
            return "Product id is required.";
        }

        if (request.Lines.Any(line => line.Quantity <= 0))
        {
            return "POS sale line quantity must be greater than zero.";
        }

        return null;
    }

    private async Task<string?> ValidateStockForPaymentAsync(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines.Where(line => line.TrackStock && !line.AllowNegativeStock))
        {
            var stockOnHand = await dbContext.StockMovements
                .Where(movement =>
                    movement.OrganizationId == sale.OrganizationId &&
                    movement.BranchId == sale.BranchId &&
                    movement.ProductId == line.ProductId)
                .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;

            if (stockOnHand - line.Quantity < 0)
            {
                return "Insufficient stock for tracked product.";
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<PosSaleLineEntity>> LoadSaleLinesAsync(
        Guid posSaleId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PosSaleLines
            .Where(line => line.PosSaleId == posSaleId)
            .OrderBy(line => line.ProductName)
            .ThenBy(line => line.ProductId)
            .ToListAsync(cancellationToken);
    }

    private async Task<BillingCommandServiceResult<TResponse>?> GetExistingIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BillingCommandServiceResult<TResponse>.Invalid("Idempotency key is required.");
        }

        var requestHash = HashRequest(request);
        var idempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Operation == operation &&
                    candidate.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return BillingCommandServiceResult<TResponse>.RequestConflict("Idempotency key was already used for a different request.");
        }

        var response = JsonSerializer.Deserialize<TResponse>(existing.ResponseJson, JsonOptions);

        return response is null
            ? BillingCommandServiceResult<TResponse>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<TResponse>.Ok(response);
    }

    private void AddIdempotencyRecord<TRequest, TResponse>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        TResponse response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = operation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private async Task<BillingCommandServiceResult<TResponse>?> ReplayIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await GetExistingIdempotencyAsync<TResponse, TRequest>(
            organizationId,
            branchId,
            operation,
            idempotencyKey,
            request,
            cancellationToken);
    }

    private async Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                return await action();
            }
            catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
            {
                dbContext.ChangeTracker.Clear();
                var recovered = await recoverIdempotencyRaceAsync();

                if (recovered is not null)
                {
                    return recovered;
                }

                throw;
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var recovered = await recoverIdempotencyRaceAsync();

            if (recovered is not null)
            {
                return recovered;
            }

            throw;
        }
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }

    private static PosSaleDto ToDto(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        ReceiptEntity? latestReceipt = null,
        Guid? shopOrderId = null)
    {
        return new PosSaleDto(
            sale.PosSaleId,
            sale.OrganizationId,
            sale.BranchId,
            sale.ShiftId,
            sale.State,
            lines.Select(ToDto).ToList(),
            new MoneyDto(sale.CurrencyCode, sale.TotalMinorUnits),
            sale.CreatedByStaffUserId,
            sale.CreatedAtUtc,
            sale.PaidAtUtc,
            sale.RefundedAtUtc,
            sale.VoidedAtUtc,
            latestReceipt is null ? null : ToDto(latestReceipt, shopOrderId),
            sale.PlayerAccountId,
            shopOrderId);
    }

    private static ReceiptDto ToDto(ReceiptEntity receipt, Guid? shopOrderId = null)
    {
        return new ReceiptDto(
            receipt.ReceiptId,
            receipt.OrganizationId,
            receipt.BranchId,
            receipt.PosSaleId,
            receipt.ReceiptNumber,
            receipt.ReceiptType,
            new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
            receipt.CreatedAtUtc,
            receipt.SessionId,
            shopOrderId);
    }

    private static PosSaleLineDto ToDto(PosSaleLineEntity line)
    {
        return new PosSaleLineDto(
            line.ProductId,
            line.ProductName,
            line.Quantity,
            new MoneyDto(line.CurrencyCode, line.UnitPriceMinorUnits),
            new MoneyDto(line.CurrencyCode, line.LineTotalMinorUnits));
    }
}
