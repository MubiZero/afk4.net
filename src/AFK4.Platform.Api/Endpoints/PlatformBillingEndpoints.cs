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
using AFK4.Shared.Contracts.Platform.Organizations;
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

internal static class PlatformBillingEndpoints
{
    public static void MapPlatformBillingEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
        app.MapGet("/api/platform/plans", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlanCatalogService planCatalogService,
            IAuditRecordWriter auditRecordWriter,
            bool? includeInactive,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "SubscriptionPlan",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var plans = await planCatalogService.ListAsync(includeInactive ?? true, cancellationToken);
            return Results.Ok(plans);
        });

        app.MapPost("/api/platform/plans", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlanCatalogService planCatalogService,
            IAuditRecordWriter auditRecordWriter,
            CreatePlanRequest request,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.CreatePlan,
                    targetType: "SubscriptionPlan",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await planCatalogService.CreateAsync(request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.CreatePlan,
                targetType: "SubscriptionPlan",
                targetId: result.Value!.PlanCode,
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value.PlanCode, result.Value.PriceMinorUnits, result.Value.BillingInterval },
                cancellationToken);
            return Results.Ok(result.Value);
        });

        app.MapPatch("/api/platform/plans/{planCode}", async (
            string planCode,
            PlatformAdminAuthorizationService authorizationService,
            IPlanCatalogService planCatalogService,
            IAuditRecordWriter auditRecordWriter,
            UpdatePlanRequest request,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.UpdatePlan,
                    targetType: "SubscriptionPlan",
                    targetId: planCode,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await planCatalogService.UpdateAsync(planCode, request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.UpdatePlan,
                targetType: "SubscriptionPlan",
                targetId: planCode,
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value!.PlanCode, result.Value.PriceMinorUnits, result.Value.IsActive },
                cancellationToken);
            return Results.Ok(result.Value);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/subscription", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IOrganizationSubscriptionService subscriptionService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "OrganizationSubscription",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await subscriptionService.GetAsync(organizationId, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapPatch("/api/platform/organizations/{organizationId:guid}/subscription", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IOrganizationSubscriptionService subscriptionService,
            IAuditRecordWriter auditRecordWriter,
            UpdateSubscriptionRequest request,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageSubscriptions);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.UpdateSubscription,
                    targetType: "OrganizationSubscription",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await subscriptionService.UpdateAsync(organizationId, request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.UpdateSubscription,
                targetType: "OrganizationSubscription",
                targetId: organizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value!.PlanCode, result.Value.Status, result.Value.CancelAtPeriodEnd },
                cancellationToken);
            return Results.Ok(result.Value);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/invoices", async (
            Guid organizationId,
            string? status,
            PlatformAdminAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "Invoice",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await invoiceService.ListForOrganizationAsync(organizationId, status, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        // --- Club-side (owner) read-only billing (SP3 Plan 7) ---
        organizations.MapGet("subscription", async (
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            IOrganizationSubscriptionService subscriptionService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await subscriptionService.GetAsync(authorization.StaffContext!.OrganizationId, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        organizations.MapGet("invoices", async (
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await invoiceService.ListForOrganizationAsync(authorization.StaffContext!.OrganizationId, status: null, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapGet("/api/platform/subscriptions", async (
            string? status,
            string? planCode,
            PlatformAdminAuthorizationService authorizationService,
            IOrganizationSubscriptionService subscriptionService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "OrganizationSubscription",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await subscriptionService.ListAsync(status, planCode, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapGet("/api/platform/invoices", async (
            string? status,
            PlatformAdminAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "Invoice",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await invoiceService.ListAllAsync(status, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapGet("/api/platform/metrics", async (
            PlatformAdminAuthorizationService authorizationService,
            IBillingMetricsService metricsService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewBilling,
                    targetType: "BillingMetrics",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var metrics = await metricsService.GetAsync(cancellationToken);
            return Results.Ok(metrics);
        });

        app.MapPost("/api/platform/organizations/{organizationId:guid}/invoices/generate", async (
            Guid organizationId,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            IPlatformIdempotencyStore idempotencyStore,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.GenerateInvoice,
                    targetType: "Invoice",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var requestHash = IdempotencyKeyHelper.HashRequest(new { organizationId });
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (idempotencyKey.Length > 128)
                {
                    return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
                }

                var prior = await idempotencyStore.TryReadAsync(
                    scope: "platform.invoices.generate",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    cancellationToken);
                if (prior.RequestHashMismatch)
                    return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
                if (prior.Stored is not null)
                {
                    httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
                }
            }

            var result = await invoiceService.GenerateAsync(organizationId, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.GenerateInvoice,
                targetType: "Invoice",
                targetId: result.Value!.InvoiceId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value.Number, result.Value.AmountMinorUnits },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
                await idempotencyStore.WriteAsync(
                    scope: "platform.invoices.generate",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    statusCode: StatusCodes.Status200OK,
                    responseBody: responseBody,
                    retention: TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return Results.Ok(result.Value);
        });

        app.MapPost("/api/platform/invoices/{invoiceId:guid}/mark-paid", async (
            Guid invoiceId,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            IPlatformIdempotencyStore idempotencyStore,
            IAuditRecordWriter auditRecordWriter,
            MarkInvoicePaidRequest request,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.MarkInvoicePaid,
                    targetType: "Invoice",
                    targetId: invoiceId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (idempotencyKey.Length > 128)
                {
                    return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
                }

                var prior = await idempotencyStore.TryReadAsync(
                    scope: "platform.invoices.mark_paid",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    cancellationToken);
                if (prior.RequestHashMismatch)
                    return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
                if (prior.Stored is not null)
                {
                    httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
                }
            }

            var result = await invoiceService.MarkPaidAsync(invoiceId, request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: result.Value!.OrganizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.MarkInvoicePaid,
                targetType: "Invoice",
                targetId: invoiceId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value.Number, request.Reference },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
                await idempotencyStore.WriteAsync(
                    scope: "platform.invoices.mark_paid",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    statusCode: StatusCodes.Status200OK,
                    responseBody: responseBody,
                    retention: TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return Results.Ok(result.Value);
        });

        app.MapPost("/api/platform/invoices/{invoiceId:guid}/void", async (
            Guid invoiceId,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            IPlatformIdempotencyStore idempotencyStore,
            IAuditRecordWriter auditRecordWriter,
            VoidInvoiceRequest request,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.VoidInvoice,
                    targetType: "Invoice",
                    targetId: invoiceId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (idempotencyKey.Length > 128)
                {
                    return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
                }

                var prior = await idempotencyStore.TryReadAsync(
                    scope: "platform.invoices.void",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    cancellationToken);
                if (prior.RequestHashMismatch)
                    return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
                if (prior.Stored is not null)
                {
                    httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
                }
            }

            var result = await invoiceService.VoidAsync(invoiceId, request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: result.Value!.OrganizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.VoidInvoice,
                targetType: "Invoice",
                targetId: invoiceId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value.Number, request.Reason },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
                await idempotencyStore.WriteAsync(
                    scope: "platform.invoices.void",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    statusCode: StatusCodes.Status200OK,
                    responseBody: responseBody,
                    retention: TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return Results.Ok(result.Value);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/health", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformOrganizationHealthService healthService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizationHealth);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOrganizationHealth,
                    targetType: "Organization",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var health = await healthService.GetAsync(organizationId, cancellationToken);
            if (health is null)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOrganizationHealth,
                    targetType: "Organization",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = "Organization was not found." },
                    cancellationToken);
                return Results.NotFound(new { Error = "Organization was not found." });
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewOrganizationHealth,
                targetType: "Organization",
                targetId: organizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    health.BranchCount,
                    health.DeviceCount,
                    health.ActiveStaffUserCount,
                    health.RecentErrorCount
                },
                cancellationToken);

            return Results.Ok(health);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/support-notes", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformSupportNoteService supportNoteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizationSupportNotes);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOrganizationSupportNotes,
                    targetType: "OrganizationSupportNote",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await supportNoteService.ListAsync(organizationId, cancellationToken);
            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOrganizationSupportNotes,
                    targetType: "OrganizationSupportNote",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);
                return result.Status switch
                {
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var notes = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewOrganizationSupportNotes,
                targetType: "OrganizationSupportNote",
                targetId: null,
                outcome: AuditOutcome.Succeeded,
                details: new { Count = notes.Count },
                cancellationToken);

            return Results.Ok(notes);
        });

        app.MapPost("/api/platform/organizations/{organizationId:guid}/support-notes", async (
            Guid organizationId,
            CreateOrganizationSupportNoteRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformSupportNoteService supportNoteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOrganizationSupportNotes);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.CreateOrganizationSupportNote,
                    targetType: "OrganizationSupportNote",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await supportNoteService.CreateAsync(
                organizationId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.CreateOrganizationSupportNote,
                    targetType: "OrganizationSupportNote",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);
                return result.Status switch
                {
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var note = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.CreateOrganizationSupportNote,
                targetType: "OrganizationSupportNote",
                targetId: note.OrganizationSupportNoteId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { note.OrganizationSupportNoteId, BodyLength = note.Body.Length },
                cancellationToken);

            return Results.Ok(note);
        });

        app.MapPatch("/api/platform/organizations/{organizationId:guid}/support-notes/{organizationSupportNoteId:guid}", async (
            Guid organizationId,
            Guid organizationSupportNoteId,
            UpdateOrganizationSupportNoteRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformSupportNoteService supportNoteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOrganizationSupportNotes);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.UpdateOrganizationSupportNote,
                    targetType: "OrganizationSupportNote",
                    targetId: organizationSupportNoteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await supportNoteService.UpdateAsync(
                organizationId,
                organizationSupportNoteId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.UpdateOrganizationSupportNote,
                    targetType: "OrganizationSupportNote",
                    targetId: organizationSupportNoteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);
                return result.Status switch
                {
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var note = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.UpdateOrganizationSupportNote,
                targetType: "OrganizationSupportNote",
                targetId: note.OrganizationSupportNoteId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { note.OrganizationSupportNoteId, BodyLength = note.Body.Length },
                cancellationToken);

            return Results.Ok(note);
        });

    }
}
