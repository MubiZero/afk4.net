using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PosEndpoints
{
    public static void MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/pos/categories", async (
            Guid branchId,
            CreateProductCategoryRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManagePosCatalog,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateProductCategory,
                    "PosProductCategory",
                    null,
                    AuditOutcome.Denied,
                    new { request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await inventoryService.CreateCategoryAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateProductCategory,
                "PosProductCategory",
                result.Response!.CategoryId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Name },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("branches/{branchId:guid}/pos/products", async (
            Guid branchId,
            CreateProductRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManagePosCatalog,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateProduct,
                    "PosProduct",
                    null,
                    AuditOutcome.Denied,
                    new { request.Sku, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await inventoryService.CreateProductAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateProduct,
                "PosProduct",
                result.Response!.ProductId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Sku, request.Price },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPatch("branches/{branchId:guid}/pos/products/{productId:guid}", async (
            Guid branchId,
            Guid productId,
            UpdateProductRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManagePosCatalog,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateProduct,
                    "PosProduct",
                    productId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Sku, request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await inventoryService.UpdateProductAsync(
                branchId,
                productId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateProduct,
                "PosProduct",
                result.Response!.ProductId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Sku, request.Price, request.IsActive },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("branches/{branchId:guid}/pos/catalog", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewInventory,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await inventoryService.GetCatalogAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return ToHttpResult(result);
        });

        app.MapGet("branches/{branchId:guid}/inventory/stock-movements", async (
            Guid branchId,
            Guid? productId,
            int? limit,
            StaffAuthorizationService authorizationService,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewInventory,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await inventoryService.GetStockMovementsAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                productId,
                Math.Clamp(limit ?? 50, 1, 200),
                cancellationToken);

            return ToHttpResult(result);
        });

        app.MapPost("branches/{branchId:guid}/inventory/stock-movements", async (
            Guid branchId,
            CreateStockMovementRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageInventoryStock,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateStockMovement,
                    "StockMovement",
                    null,
                    AuditOutcome.Denied,
                    new { request.ProductId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await inventoryService.CreateStockMovementAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateStockMovement,
                "StockMovement",
                result.Response!.StockMovementId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.ProductId, request.MovementType, request.QuantityDelta },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("branches/{branchId:guid}/pos/products/{productId:guid}/barcodes", async (
            Guid branchId,
            Guid productId,
            StaffAuthorizationService authorizationService,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewInventory,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await inventoryService.GetProductBarcodesAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                productId,
                cancellationToken);

            return ToHttpResult(result);
        });

        app.MapPost("branches/{branchId:guid}/pos/products/{productId:guid}/barcodes", async (
            Guid branchId,
            Guid productId,
            AddProductBarcodeRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageInventoryStock,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.AddProductBarcode,
                    "ProductBarcode",
                    null,
                    AuditOutcome.Denied,
                    new { productId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await inventoryService.AddProductBarcodeAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                productId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.AddProductBarcode,
                "ProductBarcode",
                result.Response!.BarcodeId.ToString("D"),
                AuditOutcome.Succeeded,
                new { productId, result.Response.Code },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapDelete("branches/{branchId:guid}/pos/products/{productId:guid}/barcodes/{barcodeId:guid}", async (
            Guid branchId,
            Guid productId,
            Guid barcodeId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageInventoryStock,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.DeleteProductBarcode,
                    "ProductBarcode",
                    null,
                    AuditOutcome.Denied,
                    new { productId, barcodeId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await inventoryService.DeleteProductBarcodeAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                productId,
                barcodeId,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.DeleteProductBarcode,
                "ProductBarcode",
                barcodeId.ToString("D"),
                AuditOutcome.Succeeded,
                new { productId, barcodeId },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("branches/{branchId:guid}/pos/sales", async (
            Guid branchId,
            CreatePosSaleRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPosService posService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.CreatePosSale,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreatePosSale,
                    "PosSale",
                    null,
                    AuditOutcome.Denied,
                    new { request.ShiftId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await posService.CreateSaleAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreatePosSale,
                "PosSale",
                result.Response!.PosSaleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.ShiftId, LineCount = request.Lines.Count },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("pos/sales/{saleId:guid}/payments/manual", async (
            Guid saleId,
            ManualPaymentRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPosService posService,
            CancellationToken cancellationToken) =>
        {
            var sale = await LoadPosSaleScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                saleId,
                OrganizationPermissionNames.PayPosSale,
                cancellationToken);
            if (sale.Result is not null)
            {
                return sale.Result;
            }

            var authorization = sale.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    sale.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.PayPosSale,
                    "PosSale",
                    saleId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await posService.PaySaleAsync(
                saleId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                sale.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.PayPosSale,
                "PosSale",
                saleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.PaymentMethod, request.Amount },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("pos/sales/{saleId:guid}/settlements", async (
            Guid saleId,
            SettlePosSaleRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPosSettlementService settlementService,
            CancellationToken cancellationToken) =>
        {
            var sale = await LoadPosSaleScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                saleId,
                OrganizationPermissionNames.PayPosSale,
                cancellationToken);
            if (sale.Result is not null)
            {
                return sale.Result;
            }

            var authorization = sale.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    sale.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.PayPosSale,
                    "PosSale",
                    saleId.ToString("D"),
                    AuditOutcome.Denied,
                    new { paymentPartCount = request.Payments?.Count ?? 0, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "organization_scope_mismatch" });
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                return Results.BadRequest(new { Error = "idempotency_key_required" });
            }

            var result = await settlementService.SettleAsync(
                saleId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToPosSettlementHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                sale.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.PayPosSale,
                "PosSale",
                saleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { paymentPartCount = request.Payments.Count },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("pos/sales/{saleId:guid}/refunds", async (
            Guid saleId,
            RefundPosSaleRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShopCommerceCoordinator commerceCoordinator,
            CancellationToken cancellationToken) =>
        {
            var sale = await LoadPosSaleScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                saleId,
                OrganizationPermissionNames.RefundPosSale,
                cancellationToken);
            if (sale.Result is not null)
            {
                return sale.Result;
            }

            var authorization = sale.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    sale.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.RefundPosSale,
                    "PosSale",
                    saleId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await commerceCoordinator.RefundLinkedSaleAsync(
                saleId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                sale.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.RefundPosSale,
                "PosSale",
                saleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("pos/sales/{saleId:guid}/void", async (
            Guid saleId,
            VoidPosSaleRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPosService posService,
            CancellationToken cancellationToken) =>
        {
            var sale = await LoadPosSaleScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                saleId,
                OrganizationPermissionNames.VoidPosSale,
                cancellationToken);
            if (sale.Result is not null)
            {
                return sale.Result;
            }

            var authorization = sale.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    sale.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.VoidPosSale,
                    "PosSale",
                    saleId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await posService.VoidSaleAsync(
                saleId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                sale.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.VoidPosSale,
                "PosSale",
                saleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("pos/sales/{saleId:guid}", async (
            Guid saleId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IPosService posService,
            CancellationToken cancellationToken) =>
        {
            var sale = await LoadPosSaleScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                saleId,
                OrganizationPermissionNames.ViewReceipt,
                cancellationToken);
            if (sale.Result is not null)
            {
                return sale.Result;
            }

            var authorization = sale.Authorization!;
            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await posService.GetSaleAsync(
                authorization.StaffContext!.OrganizationId,
                saleId,
                cancellationToken);

            return ToHttpResult(result);
        });

        app.MapGet("receipts/{receiptId:guid}", async (
            Guid receiptId,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            var receipt = await LoadReceiptScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                receiptId,
                OrganizationPermissionNames.ViewReceipt,
                cancellationToken);
            if (receipt.Result is not null)
            {
                return receipt.Result;
            }

            if (!receipt.Authorization!.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var shopOrderId = receipt.Entity!.PosSaleId is Guid posSaleId
                ? await dbContext.ShopOrders.AsNoTracking()
                    .Where(order =>
                        order.OrganizationId == receipt.Entity.OrganizationId &&
                        order.BranchId == receipt.Entity.BranchId &&
                        order.PosSaleId == posSaleId)
                    .Select(order => (Guid?)order.ShopOrderId)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            return Results.Ok(ToDto(receipt.Entity, shopOrderId));
        });

    }

    private static IResult ToPosSettlementHttpResult(BillingCommandServiceResult<PosSaleDto> result)
    {
        if (result.NotFound)
        {
            return Results.NotFound(new { Error = "pos_sale_not_found" });
        }

        var error = result.Error switch
        {
            "wallet_player_required" or "wallet_player_invalid" => "player_required_for_wallet",
            "insufficient_stock" => "out_of_stock",
            "version_conflict" => "version_conflict",
            "idempotency_conflict" => "idempotency_conflict",
            "mixed_currency" => "mixed_currency",
            "invalid_payment_split" => "invalid_payment_split",
            "open_shift_required" => "open_shift_required",
            "sale_not_payable" => "sale_not_payable",
            "insufficient_funds" => "insufficient_funds",
            "product_unavailable" => "product_unavailable",
            _ => "settlement_failed"
        };

        return error is "version_conflict" or "idempotency_conflict" or "open_shift_required" or "sale_not_payable" or
            "insufficient_funds" or "out_of_stock" or "product_unavailable"
            ? Results.Conflict(new { Error = error })
            : Results.BadRequest(new { Error = error });
    }
}
