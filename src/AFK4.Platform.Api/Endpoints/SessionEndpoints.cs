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
            ISessionTimelineReadService sessionTimelineReadService,
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
                return Results.BadRequest(new { Error = result.Error });
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

        app.MapPost("sessions/{sessionId:guid}/extend", async (
            Guid sessionId,
            ExtendSessionRequest request,
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
                OrganizationPermissionNames.ExtendSession,
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
                    AuditActionNames.ExtendSession,
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

            var result = await sessionCommandService.ExtendSessionAsync(
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
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ExtendSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.AdditionalMinutes,
                    request.TariffRuleVersionId
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("sessions/{sessionId:guid}/transfer", async (
            Guid sessionId,
            TransferSessionRequest request,
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
                OrganizationPermissionNames.TransferSession,
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
                    AuditActionNames.TransferSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.TargetSeatId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.TransferSessionAsync(
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
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.TransferSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.TargetSeatId
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        // Пауза и снятие ходят парой и живут по одним правилам: право то же, что у продления
        // (обе правят время сессии), версия сессии проверяется, отказ пишется в журнал.
        app.MapPost("sessions/{sessionId:guid}/pause", async (
            Guid sessionId,
            PauseSessionRequest request,
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
                OrganizationPermissionNames.PauseSession,
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
                    AuditActionNames.PauseSession,
                    sessionId,
                    AuditOutcome.Denied,
                    new { request.Reason, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.PauseSessionAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (ToSessionCommandFailure(result) is { } pauseFailure)
            {
                return pauseFailure;
            }

            await WriteSessionAuditAsync(
                auditRecordWriter,
                authorization,
                session.BranchId,
                AuditActionNames.PauseSession,
                sessionId,
                AuditOutcome.Succeeded,
                new { request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("sessions/{sessionId:guid}/resume", async (
            Guid sessionId,
            ResumeSessionRequest request,
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
                OrganizationPermissionNames.PauseSession,
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
                    AuditActionNames.ResumeSession,
                    sessionId,
                    AuditOutcome.Denied,
                    new { request.Reason, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.ResumeSessionAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (ToSessionCommandFailure(result) is { } resumeFailure)
            {
                return resumeFailure;
            }

            await WriteSessionAuditAsync(
                auditRecordWriter,
                authorization,
                session.BranchId,
                AuditActionNames.ResumeSession,
                sessionId,
                AuditOutcome.Succeeded,
                new { request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("sessions/{sessionId:guid}/end", async (
            Guid sessionId,
            EndSessionRequest request,
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
                    AuditActionNames.EndSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.Reason,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.EndSessionAsync(
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
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.EndSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.Reason,
                    // Ранний выход у стойки возвращает игроку неиграное — в журнале видно, сколько.
                    RefundedMinorUnits = result.EarlyEnd?.Money.RefundMinorUnits,
                    PackageMinutesReturned = result.EarlyEnd?.PackageSecondsReturned / 60
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("sessions/{sessionId:guid}/checkout", async (
            Guid sessionId,
            SessionCheckoutRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCheckoutService sessionCheckoutService,
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
                return Results.BadRequest(new { Error = result.Error });
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
            ISessionCheckoutService sessionCheckoutService,
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
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(result.Response);
        });

    }

    /// <summary>Отказ команды сессии одним ответом: конфликт версий, «нет такой», «так нельзя».</summary>
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

        return result.Succeeded ? null : Results.BadRequest(new { Error = result.Error });
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
