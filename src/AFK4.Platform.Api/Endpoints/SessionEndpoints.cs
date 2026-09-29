using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // Started sessions (running and completed) overlapping a day window — feeds the booking
        // timeline's session layer. Defaults to today when the window is omitted.
        app.MapGet("branches/{branchId:guid}/sessions", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfSessionTimelineReadService sessionTimelineReadService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewSession,
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
                    AuditActionNames.ViewSessions,
                    "Session",
                    null,
                    AuditOutcome.Denied,
                    new { fromUtc, toUtc, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var from = fromUtc ?? new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
            var to = toUtc ?? from.AddDays(1);
            var result = await sessionTimelineReadService.GetSessionsAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                from,
                to,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewSessions,
                "Session",
                null,
                AuditOutcome.Succeeded,
                new { fromUtc = from, toUtc = to, ResultCount = result.Sessions.Count },
                cancellationToken);

            return Results.Ok(result);
        });

        app.MapPost("branches/{branchId:guid}/sessions/start", async (
            Guid branchId,
            StartGuestSessionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.StartSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.StartSession,
                    "Session",
                    null,
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.SeatId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await sessionCommandService.StartGuestSessionAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                SessionOriginNames.Operator,
                cancellationToken,
                actorCanApproveComp: authorization.StaffContext.Permissions.Contains(OrganizationPermissionNames.ApproveMoneyAction));

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion, result.PlanLimit });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error, result.Code });
            }

            // §5.4: a comp (free session) is audited as a first-class session.comp with its reason and its
            // assessed value, so the owner summary / Review screen can surface free sessions in money terms.
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                request.IsComp ? AuditActionNames.SessionComp : AuditActionNames.StartSession,
                "Session",
                result.Response!.Session.SessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                request.IsComp
                    ? JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes, request.CompReason, CompValueMinorUnits = result.Response.CompValueMinorUnits })
                    : JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes }))
            {
                AmountMinorUnits = request.IsComp ? result.Response.CompValueMinorUnits : null
            },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        MapSessionAction<ExtendSessionRequest>(
            app, "extend", OrganizationPermissionNames.ExtendSession, AuditActionNames.ExtendSession,
            (service, sessionId, staffUserId, request, ct) => service.ExtendSessionAsync(sessionId, staffUserId, request, ct),
            (_, denialReason) => new { DenialReason = denialReason },
            (request, _) => new { request.AdditionalMinutes, request.TariffRuleVersionId });

        MapSessionAction<TransferSessionRequest>(
            app, "transfer", OrganizationPermissionNames.TransferSession, AuditActionNames.TransferSession,
            (service, sessionId, staffUserId, request, ct) => service.TransferSessionAsync(sessionId, staffUserId, request, ct),
            (request, denialReason) => new { request.TargetSeatId, DenialReason = denialReason },
            (request, _) => new { request.TargetSeatId });

        // Пауза и снятие ходят парой и живут по одним правилам: право то же, что у продления
        // (обе правят время сессии), версия сессии проверяется, отказ пишется в журнал.
        MapSessionAction<PauseSessionRequest>(
            app, "pause", OrganizationPermissionNames.PauseSession, AuditActionNames.PauseSession,
            (service, sessionId, staffUserId, request, ct) => service.PauseSessionAsync(sessionId, staffUserId, request, ct),
            (request, denialReason) => new { request.Reason, DenialReason = denialReason },
            (request, _) => new { request.Reason });

        MapSessionAction<ResumeSessionRequest>(
            app, "resume", OrganizationPermissionNames.PauseSession, AuditActionNames.ResumeSession,
            (service, sessionId, staffUserId, request, ct) => service.ResumeSessionAsync(sessionId, staffUserId, request, ct),
            (request, denialReason) => new { request.Reason, DenialReason = denialReason },
            (request, _) => new { request.Reason });

        MapSessionAction<EndSessionRequest>(
            app, "end", OrganizationPermissionNames.EndSession, AuditActionNames.EndSession,
            (service, sessionId, staffUserId, request, ct) => service.EndSessionAsync(sessionId, staffUserId, request, ct),
            (request, denialReason) => new { request.Reason, DenialReason = denialReason },
            (request, result) => new
            {
                request.Reason,
                // Ранний выход у стойки возвращает игроку неиграное — в журнале видно, сколько.
                RefundedMinorUnits = result.EarlyEnd?.Money.RefundMinorUnits,
                PackageMinutesReturned = result.EarlyEnd?.PackageSecondsReturned / 60
            });

        app.MapPost("sessions/{sessionId:guid}/checkout", async (
            Guid sessionId,
            SessionCheckoutRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfSessionCheckoutService sessionCheckoutService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                OrganizationPermissionNames.EndSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    session.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CheckoutSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCheckoutService.CheckoutAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error, result.Code });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CheckoutSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    GrandTotal = result.Response!.GrandTotal.MinorUnits,
                    result.Response.GrandTotal.CurrencyCode,
                    PaymentParts = result.Response.Payments.Count
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("sessions/{sessionId:guid}/checkout/quote", async (
            Guid sessionId,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            EfSessionCheckoutService sessionCheckoutService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                OrganizationPermissionNames.EndSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCheckoutService.QuoteAsync(
                sessionId,
                authorization.StaffContext!.OrganizationId,
                cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error, result.Code });
            }

            return Results.Ok(result.Response);
        });

    }

    /// <summary>Отказ команды сессии одним ответом: конфликт версий, «нет такой», «так нельзя».</summary>
    /// <summary>
    /// Действие стойки над идущей сессией: найти сессию, проверить право в её филиале (отказ — в
    /// журнал), выполнить, записать успех. Пять действий делали это пятью копиями.
    /// </summary>
    private static void MapSessionAction<TRequest>(
        IEndpointRouteBuilder app,
        string verb,
        string permission,
        string auditAction,
        Func<ISessionCommandService, Guid, Guid, TRequest, CancellationToken, Task<SessionCommandServiceResult>> action,
        Func<TRequest, string?, object> deniedDetails,
        Func<TRequest, SessionCommandServiceResult, object> succeededDetails)
    {
        app.MapPost($"sessions/{{sessionId:guid}}/{verb}", async (
            Guid sessionId,
            TRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                permission,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteSessionAuditAsync(
                    auditRecordWriter,
                    authorization,
                    session.BranchId,
                    auditAction,
                    sessionId,
                    AuditOutcome.Denied,
                    deniedDetails(request, authorization.DenialReason),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await action(
                sessionCommandService,
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (ToSessionCommandFailure(result) is { } failure)
            {
                return failure;
            }

            await WriteSessionAuditAsync(
                auditRecordWriter,
                authorization,
                session.BranchId,
                auditAction,
                sessionId,
                AuditOutcome.Succeeded,
                succeededDetails(request, result),
                cancellationToken);

            return Results.Ok(result.Response);
        });
    }

    private static IResult? ToSessionCommandFailure(SessionCommandServiceResult result)
    {
        if (result.Conflict)
        {
            return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
        }

        if (result.NotFound)
        {
            return Results.NotFound(new { Error = result.Error });
        }

        return result.Succeeded ? null : Results.BadRequest(new { Error = result.Error, result.Code });
    }

    private static Task WriteSessionAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        StaffAuthorizationResult authorization,
        Guid branchId,
        string action,
        Guid sessionId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            authorization.StaffContext!.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            action,
            "Session",
            sessionId.ToString("D"),
            outcome,
            "PlatformApi",
            JsonSerializer.Serialize(details)),
            cancellationToken);
}
