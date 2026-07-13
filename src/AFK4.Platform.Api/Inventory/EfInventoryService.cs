using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Inventory;

public sealed class EfInventoryService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    ILowStockNotifier? lowStockNotifier = null,
    IInventoryCostService? inventoryCostService = null) : IInventoryService
{
    private const string CategoryCreateOperation = "pos-category-create";
    private const string ProductCreateOperation = "pos-product-create";
    private const string StockMovementCreateOperation = "stock-movement-create";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<PosProductCategoryDto>> CreateCategoryAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<PosProductCategoryDto, CreateProductCategoryRequest>(
            request.OrganizationId,
            branchId,
            CategoryCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<PosProductCategoryDto>.Invalid("Organization id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BillingCommandServiceResult<PosProductCategoryDto>.Invalid("Category name is required.");
        }

        var normalizedName = NormalizeName(request.Name);
        var exists = await dbContext.PosProductCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.OrganizationId == request.OrganizationId &&
                    category.BranchId == branchId &&
                    category.Name == normalizedName,
                cancellationToken);

        if (exists)
        {
            return BillingCommandServiceResult<PosProductCategoryDto>.Invalid("Product category name already exists.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var category = new PosProductCategoryEntity
            {
                CategoryId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                Name = normalizedName,
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.PosProductCategories.Add(category);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(category);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                CategoryCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosProductCategoryDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosProductCategoryDto, CreateProductCategoryRequest>(
            request.OrganizationId,
            branchId,
            CategoryCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosProductDto>> CreateProductAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<PosProductDto, CreateProductRequest>(
            request.OrganizationId,
            branchId,
            ProductCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var validation = ValidateCreateProductRequest(request);
        if (validation is not null)
        {
            return BillingCommandServiceResult<PosProductDto>.Invalid(validation);
        }

        var categoryExists = await dbContext.PosProductCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.OrganizationId == request.OrganizationId &&
                    category.BranchId == branchId &&
                    category.CategoryId == request.CategoryId &&
                    category.IsActive,
                cancellationToken);

        if (!categoryExists)
        {
            return BillingCommandServiceResult<PosProductDto>.Missing("Product category was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        var skuExists = await dbContext.PosProducts
            .AsNoTracking()
            .AnyAsync(
                product =>
                    product.OrganizationId == request.OrganizationId &&
                    product.BranchId == branchId &&
                    product.Sku == normalizedSku,
                cancellationToken);

        if (skuExists)
        {
            return BillingCommandServiceResult<PosProductDto>.Invalid("Product SKU already exists.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var product = new PosProductEntity
            {
                ProductId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                CategoryId = request.CategoryId,
                Name = request.Name.Trim(),
                Sku = normalizedSku,
                CurrencyCode = request.Price.CurrencyCode.Trim().ToUpperInvariant(),
                PriceMinorUnits = request.Price.MinorUnits,
                TrackStock = request.TrackStock,
                AllowNegativeStock = request.AllowNegativeStock,
                ReorderThreshold = request.ReorderThreshold,
                AvailableInShell = request.AvailableInShell,
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.PosProducts.Add(product);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(product, stockOnHand: 0);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                ProductCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PosProductDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PosProductDto, CreateProductRequest>(
            request.OrganizationId,
            branchId,
            ProductCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PosProductDto>> UpdateProductAsync(
        Guid branchId,
        Guid productId,
        Guid actorStaffUserId,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            return BillingCommandServiceResult<PosProductDto>.Invalid("Product id is required.");
        }

        var validation = ValidateUpdateProductRequest(request);
        if (validation is not null)
        {
            return BillingCommandServiceResult<PosProductDto>.Invalid(validation);
        }

        var product = await dbContext.PosProducts
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ProductId == productId,
                cancellationToken);

        if (product is null)
        {
            return BillingCommandServiceResult<PosProductDto>.Missing("Product was not found.");
        }

        var categoryExists = await dbContext.PosProductCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.OrganizationId == request.OrganizationId &&
                    category.BranchId == branchId &&
                    category.CategoryId == request.CategoryId &&
                    category.IsActive,
                cancellationToken);

        if (!categoryExists)
        {
            return BillingCommandServiceResult<PosProductDto>.Missing("Product category was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        var skuExists = await dbContext.PosProducts
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ProductId != productId &&
                    candidate.Sku == normalizedSku,
                cancellationToken);

        if (skuExists)
        {
            return BillingCommandServiceResult<PosProductDto>.Invalid("Product SKU already exists.");
        }

        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.Sku = normalizedSku;
        product.CurrencyCode = request.Price.CurrencyCode.Trim().ToUpperInvariant();
        product.PriceMinorUnits = request.Price.MinorUnits;
        product.TrackStock = request.TrackStock;
        product.AllowNegativeStock = request.AllowNegativeStock;
        product.ReorderThreshold = request.ReorderThreshold;
        product.AvailableInShell = request.AvailableInShell;
        product.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        var stockOnHand = await dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.OrganizationId == request.OrganizationId &&
                movement.BranchId == branchId &&
                movement.ProductId == productId)
            .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;

        return BillingCommandServiceResult<PosProductDto>.Ok(ToDto(product, stockOnHand));
    }

    public async Task<BillingCommandServiceResult<StockMovementDto>> CreateStockMovementAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateStockMovementRequest request,
        CancellationToken cancellationToken)
    {
        var requestHashInput = new
        {
            BranchId = branchId,
            Request = request
        };
        var idempotency = await GetExistingIdempotencyAsync<StockMovementDto, object>(
            request.OrganizationId,
            branchId,
            StockMovementCreateOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var validation = ValidateCreateStockMovementRequest(request);
        if (validation is not null)
        {
            return BillingCommandServiceResult<StockMovementDto>.Invalid(validation);
        }

        var product = await dbContext.PosProducts
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ProductId == request.ProductId &&
                    candidate.IsActive,
                cancellationToken);

        if (product is null)
        {
            return BillingCommandServiceResult<StockMovementDto>.Missing("Product was not found.");
        }

        if (!product.TrackStock)
        {
            return BillingCommandServiceResult<StockMovementDto>.Invalid("Stock movements require a tracked product.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            if (request.MovementType.Trim() == StockMovementTypeNames.Purchase)
            {
                await (inventoryCostService ?? new EfInventoryCostService(dbContext)).ReconcileInboundAsync(
                    product.OrganizationId,
                    product.BranchId,
                    product.ProductId,
                    request.QuantityDelta,
                    request.UnitCost.MinorUnits,
                    cancellationToken);
            }

            var movement = new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = product.OrganizationId,
                BranchId = product.BranchId,
                ProductId = product.ProductId,
                MovementType = request.MovementType.Trim(),
                QuantityDelta = request.QuantityDelta,
                CurrencyCode = request.UnitCost.CurrencyCode.Trim().ToUpperInvariant(),
                UnitCostMinorUnits = request.UnitCost.MinorUnits,
                Reason = request.Reason.Trim(),
                CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            };

            dbContext.StockMovements.Add(movement);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (lowStockNotifier is not null)
            {
                await lowStockNotifier.EvaluateProductsAsync(
                    product.OrganizationId, product.BranchId, [product.ProductId], cancellationToken);
            }

            var response = ToDto(movement);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                StockMovementCreateOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<StockMovementDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<StockMovementDto, object>(
            request.OrganizationId,
            branchId,
            StockMovementCreateOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<IReadOnlyList<PosProductDto>>> GetCatalogAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.PosProducts
            .AsNoTracking()
            .Where(product =>
                product.OrganizationId == organizationId &&
                product.BranchId == branchId &&
                product.IsActive)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.ProductId).ToHashSet();
        var stockMovements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId &&
                productIds.Contains(movement.ProductId))
            .ToListAsync(cancellationToken);
        var stockByProductId = stockMovements
            .GroupBy(movement => movement.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.QuantityDelta));
        var productIdList = products.Select(p => p.ProductId).ToList();
        var barcodeRows = await dbContext.ProductBarcodes.AsNoTracking()
            .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId && productIdList.Contains(b.ProductId))
            .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var barcodesByProduct = barcodeRows
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(b => b.Code).ToList());
        IReadOnlyList<PosProductDto> response = products
            .Select(product => ToDto(product, stockByProductId.GetValueOrDefault(product.ProductId),
                barcodesByProduct.GetValueOrDefault(product.ProductId)))
            .ToList();

        return BillingCommandServiceResult<IReadOnlyList<PosProductDto>>.Ok(response);
    }

    public async Task<BillingCommandServiceResult<InventoryStockDto>> GetStockAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.PosProducts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ProductId == productId &&
                    candidate.IsActive,
                cancellationToken);

        if (product is null)
        {
            return BillingCommandServiceResult<InventoryStockDto>.Missing("Product was not found.");
        }

        var stockOnHand = await dbContext.StockMovements
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId &&
                movement.ProductId == productId)
            .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;

        return BillingCommandServiceResult<InventoryStockDto>.Ok(new InventoryStockDto(
            product.ProductId,
            product.Name,
            product.Sku,
            product.TrackStock,
            stockOnHand));
    }

    public async Task<BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>> GetStockMovementsAsync(
        Guid organizationId,
        Guid branchId,
        Guid? productId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>.Invalid("Organization id is required.");
        }

        if (limit <= 0)
        {
            return BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>.Invalid("Limit must be positive.");
        }

        var query = dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId);

        if (productId is { } requestedProductId && requestedProductId != Guid.Empty)
        {
            query = query.Where(movement => movement.ProductId == requestedProductId);
        }

        var movements = await query
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenByDescending(movement => movement.StockMovementId)
            .Take(Math.Min(limit, 200))
            .ToListAsync(cancellationToken);

        var staffIds = movements.Select(movement => movement.CreatedByStaffUserId).Distinct().ToList();
        var staffNames = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(staff => staff.OrganizationId == organizationId && staffIds.Contains(staff.StaffUserId))
            .ToDictionaryAsync(staff => staff.StaffUserId, staff => staff.DisplayName, cancellationToken);

        return BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>.Ok(
            movements.Select(movement => ToDto(movement, staffNames.GetValueOrDefault(movement.CreatedByStaffUserId))).ToList());
    }

    public async Task<BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>> GetProductBarcodesAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>.Invalid("Organization id is required.");
        }

        var rows = await dbContext.ProductBarcodes.AsNoTracking()
            .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId && b.ProductId == productId)
            .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ProductBarcodeDto> dtos = rows
            .Select(b => new ProductBarcodeDto(b.BarcodeId, b.ProductId, b.Code, b.IsPrimary))
            .ToList();

        return BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>.Ok(dtos);
    }

    public async Task<BillingCommandServiceResult<ProductBarcodeDto>> AddProductBarcodeAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid productId,
        AddProductBarcodeRequest request,
        CancellationToken cancellationToken)
    {
        var code = (request.Code ?? "").Trim();
        if (code.Length == 0)
        {
            return BillingCommandServiceResult<ProductBarcodeDto>.Invalid("Barcode code is required.");
        }

        if (code.Length > 64)
        {
            return BillingCommandServiceResult<ProductBarcodeDto>.Invalid("Barcode code is too long.");
        }

        var product = await dbContext.PosProducts.AsNoTracking().FirstOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId && p.BranchId == branchId
              && p.ProductId == productId && p.IsActive,
            cancellationToken);

        if (product is null)
        {
            return BillingCommandServiceResult<ProductBarcodeDto>.Missing("Product not found.");
        }

        var clash = await dbContext.ProductBarcodes.AsNoTracking().AnyAsync(
            b => b.OrganizationId == request.OrganizationId && b.BranchId == branchId && b.Code == code,
            cancellationToken);

        if (clash)
        {
            return BillingCommandServiceResult<ProductBarcodeDto>.Invalid("Barcode is already bound to a product.");
        }

        var existing = await dbContext.ProductBarcodes
            .Where(b => b.OrganizationId == request.OrganizationId && b.BranchId == branchId && b.ProductId == productId)
            .ToListAsync(cancellationToken);

        var makePrimary = request.IsPrimary || existing.Count == 0;
        if (makePrimary)
        {
            foreach (var row in existing)
            {
                row.IsPrimary = false;
            }
        }

        var entity = new ProductBarcodeEntity
        {
            BarcodeId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            ProductId = productId,
            Code = code,
            IsPrimary = makePrimary,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        dbContext.ProductBarcodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BillingCommandServiceResult<ProductBarcodeDto>.Ok(
            new ProductBarcodeDto(entity.BarcodeId, entity.ProductId, entity.Code, entity.IsPrimary));
    }

    public async Task<BillingCommandServiceResult<ProductBarcodeDto>> DeleteProductBarcodeAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        Guid barcodeId,
        CancellationToken cancellationToken)
    {
        var target = await dbContext.ProductBarcodes.FirstOrDefaultAsync(
            b => b.OrganizationId == organizationId && b.BranchId == branchId
              && b.ProductId == productId && b.BarcodeId == barcodeId,
            cancellationToken);

        if (target is null)
        {
            return BillingCommandServiceResult<ProductBarcodeDto>.Missing("Barcode not found.");
        }

        dbContext.ProductBarcodes.Remove(target);

        if (target.IsPrimary)
        {
            var promote = await dbContext.ProductBarcodes
                .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId
                         && b.ProductId == productId && b.BarcodeId != barcodeId)
                .OrderBy(b => b.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (promote is not null)
            {
                promote.IsPrimary = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return BillingCommandServiceResult<ProductBarcodeDto>.Ok(
            new ProductBarcodeDto(target.BarcodeId, target.ProductId, target.Code, target.IsPrimary));
    }

    private static string? ValidateCreateProductRequest(CreateProductRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return "Product SKU is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Price.CurrencyCode))
        {
            return "Currency code is required.";
        }

        if (request.Price.MinorUnits < 0)
        {
            return "Product price cannot be negative.";
        }

        if (request.ReorderThreshold < 0)
        {
            return "Reorder threshold cannot be negative.";
        }

        return null;
    }

    private static string? ValidateUpdateProductRequest(UpdateProductRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return "Product SKU is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Price.CurrencyCode))
        {
            return "Currency code is required.";
        }

        if (request.Price.MinorUnits < 0)
        {
            return "Product price cannot be negative.";
        }

        if (request.ReorderThreshold < 0)
        {
            return "Reorder threshold cannot be negative.";
        }

        return null;
    }

    private static string? ValidateCreateStockMovementRequest(CreateStockMovementRequest request)
    {
        if (request.QuantityDelta == 0)
        {
            return "Stock movement quantity delta cannot be zero.";
        }

        if (request.MovementType == StockMovementTypeNames.Purchase && request.QuantityDelta < 0)
        {
            return "Purchase quantity must be positive.";
        }

        if (request.MovementType is not
            StockMovementTypeNames.Purchase and not
            StockMovementTypeNames.Sale and not
            StockMovementTypeNames.Refund and not
            StockMovementTypeNames.Adjustment)
        {
            return "Unsupported stock movement type.";
        }

        if (string.IsNullOrWhiteSpace(request.UnitCost.CurrencyCode))
        {
            return "Currency code is required.";
        }

        if (request.UnitCost.MinorUnits < 0)
        {
            return "Unit cost cannot be negative.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "Reason is required.";
        }

        return null;
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

    private static string NormalizeName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }

    private static string NormalizeSku(string sku)
    {
        return sku.Trim().ToUpperInvariant();
    }

    private static PosProductCategoryDto ToDto(PosProductCategoryEntity category)
    {
        return new PosProductCategoryDto(
            category.CategoryId,
            category.OrganizationId,
            category.BranchId,
            category.Name,
            category.IsActive,
            category.CreatedAtUtc);
    }

    private static PosProductDto ToDto(PosProductEntity product, int stockOnHand, IReadOnlyList<string>? barcodes = null)
    {
        return new PosProductDto(
            product.ProductId,
            product.OrganizationId,
            product.BranchId,
            product.CategoryId,
            product.Name,
            product.Sku,
            new MoneyDto(product.CurrencyCode, product.PriceMinorUnits),
            product.TrackStock,
            product.AllowNegativeStock,
            product.IsActive,
            stockOnHand,
            product.CreatedAtUtc,
            product.ReorderThreshold,
            product.AvailableInShell,
            product.AvgCostMinorUnits,
            barcodes);
    }

    private static StockMovementDto ToDto(StockMovementEntity movement, string? createdByDisplayName = null)
    {
        return new StockMovementDto(
            movement.StockMovementId,
            movement.OrganizationId,
            movement.BranchId,
            movement.ProductId,
            movement.MovementType,
            movement.QuantityDelta,
            new MoneyDto(movement.CurrencyCode, movement.UnitCostMinorUnits),
            movement.Reason,
            movement.CreatedByStaffUserId,
            movement.CreatedAtUtc,
            createdByDisplayName);
    }
}
