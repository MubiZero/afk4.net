using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.SupportNotes;
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
            EfPlanCatalogService planCatalogService,
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
            EfPlanCatalogService planCatalogService,
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
            EfPlanCatalogService planCatalogService,
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
                details: new
                {
                    result.Value!.PlanCode, result.Value.PriceMinorUnits, result.Value.IsActive, result.Value.PricePerDeviceMinorUnits,
                    result.Value.IncludedDevices, result.Value.MaxDevices, request.IncludedFeatures, request.ApplyLimitsToClubs
                },
                cancellationToken);
            return Results.Ok(result.Value);
        });

        // Условия оплаты для клубов: пробный период, обещанный платёж, льгота до бесплатного тарифа.
        app.MapGet(BillingTermsRoutes.Terms, async (
            PlatformAdminAuthorizationService authorizationService, PlatformDbContext db, CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await BillingTerms.LoadAsync(db, cancellationToken));
        });

        app.MapPut(BillingTermsRoutes.Terms, async (
            UpdateBillingTermsRequest request, PlatformAdminAuthorizationService authorizationService, PlatformDbContext db,
            IAuditRecordWriter auditRecordWriter, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (BillingTerms.Validate(request) is { } invalid) return Results.BadRequest(new { error = invalid });

            var actor = authorization.PlatformAdminContext!.PlatformAdminUserId;
            var before = await BillingTerms.LoadAsync(db, cancellationToken);
            var saved = await BillingTerms.SaveAsync(db, request, actor, clock.GetUtcNow(), cancellationToken);
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: actor,
                action: AuditActionNames.UpdateBillingTerms,
                targetType: "BillingTerms",
                targetId: null,
                outcome: AuditOutcome.Succeeded,
                details: new { Before = before, After = request },
                cancellationToken);
            return Results.Ok(saved);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/subscription", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            EfOrganizationSubscriptionService subscriptionService,
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

            // Просмотр денег конкретного клуба — такой же след, как правка: клуб вправе узнать, кто
            // смотрел его подписку. Раньше писался только отказ, и успешное чтение не оставляло
            // ничего. Сводные витрины платформы (все счета, метрики) так не аудируются: там нет
            // данных одного клуба, и запись на каждый взгляд превратила бы журнал в шум.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewBilling,
                targetType: "OrganizationSubscription",
                targetId: organizationId.ToString("D"),
                outcome: result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                details: new { result.Succeeded },
                cancellationToken);

            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapPatch("/api/platform/organizations/{organizationId:guid}/subscription", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            EfOrganizationSubscriptionService subscriptionService,
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
                details: new
                {
                    result.Value!.PlanCode,
                    result.Value.Status,
                    result.Value.CancelAtPeriodEnd,
                    result.Value.AmountMinorUnits,
                    result.Value.CurrentPeriodEndUtc,
                    result.Value.PaymentGraceUntilUtc
                },
                cancellationToken);
            return Results.Ok(result.Value);
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/invoices", async (
            Guid organizationId,
            string? status,
            PlatformAdminAuthorizationService authorizationService,
            EfInvoiceService invoiceService,
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

            // Счета конкретного клуба — его деньги: просмотр оставляет след наравне с правкой.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewBilling,
                targetType: "Invoice",
                targetId: organizationId.ToString("D"),
                outcome: result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                details: new { Status = status, Count = result.Value?.Count ?? 0 },
                cancellationToken);

            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        // --- Club-side (owner) read-only billing (SP3 Plan 7) ---
        organizations.MapGet("subscription", async (
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            EfOrganizationSubscriptionService subscriptionService,
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
            EfInvoiceService invoiceService,
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

        organizations.MapGet("billing/status", async (
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            EfInvoiceService invoiceService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await invoiceService.GetBillingStatusAsync(authorization.StaffContext!.OrganizationId, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });

        app.MapGet("/api/platform/subscriptions", async (
            string? status,
            string? planCode,
            PlatformAdminAuthorizationService authorizationService,
            EfOrganizationSubscriptionService subscriptionService,
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
            EfInvoiceService invoiceService,
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

        app.MapPost("/api/platform/organizations/{organizationId:guid}/invoices/generate", async (
            Guid organizationId,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            EfInvoiceService invoiceService,
            EfPlatformIdempotencyStore idempotencyStore,
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

        app.MapPost("/api/platform/organizations/{organizationId:guid}/invoices", async (
            Guid organizationId,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            EfInvoiceService invoiceService,
            EfPlatformIdempotencyStore idempotencyStore,
            IAuditRecordWriter auditRecordWriter,
            CreateInvoiceRequest request,
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
                    action: AuditActionNames.CreateInvoice,
                    targetType: "Invoice",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var requestHash = IdempotencyKeyHelper.HashRequest(new { organizationId, request });
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (idempotencyKey.Length > 128)
                {
                    return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
                }

                var prior = await idempotencyStore.TryReadAsync(
                    scope: "platform.invoices.create",
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

            var result = await invoiceService.CreateAsync(organizationId, request, cancellationToken);
            if (!result.Succeeded)
                return BillingResults.From(result);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.CreateInvoice,
                targetType: "Invoice",
                targetId: result.Value!.InvoiceId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value.Kind, result.Value.Number, result.Value.AmountMinorUnits },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var responseBody = JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
                await idempotencyStore.WriteAsync(
                    scope: "platform.invoices.create",
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
            EfInvoiceService invoiceService,
            EfPlatformIdempotencyStore idempotencyStore,
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
            EfInvoiceService invoiceService,
            EfPlatformIdempotencyStore idempotencyStore,
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
            EfPlatformOrganizationHealthService healthService,
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
            EfPlatformSupportNoteService supportNoteService,
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
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error, result.Code }),
                    _ => Results.BadRequest(new { Error = result.Error, result.Code })
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
            EfPlatformSupportNoteService supportNoteService,
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
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error, result.Code }),
                    PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error, result.Code }),
                    _ => Results.BadRequest(new { Error = result.Error, result.Code })
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
            EfPlatformSupportNoteService supportNoteService,
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
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error, result.Code }),
                    PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error, result.Code }),
                    _ => Results.BadRequest(new { Error = result.Error, result.Code })
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
