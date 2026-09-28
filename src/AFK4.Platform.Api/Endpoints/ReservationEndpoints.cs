using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reservations;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/reservations", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            string? state,
            string? source,
            int? limit,
            Guid? playerAccountId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewReservations,
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
                    AuditActionNames.ViewReservations,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { fromUtc, toUtc, state, source, limit, playerAccountId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await reservationService.SearchAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                new ReservationSearchQuery(fromUtc, toUtc, state, source, limit, playerAccountId),
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewReservations,
                "Reservation",
                null,
                AuditOutcome.Succeeded,
                new { fromUtc, toUtc, state, source, limit, playerAccountId, ResultCount = result.Reservations.Count },
                cancellationToken);

            return Results.Ok(result);
        });

        // Куда можно поставить бронь в её окне. Панель брала места, свободные прямо сейчас, и под
        // перенос завтрашней брони предлагала не тот зал: занятое сейчас, но свободное завтра место
        // пряталось, а свободное сейчас и занятое завтра чужой бронью — предлагалось и отклонялось.
        app.MapGet("branches/{branchId:guid}/reservations/free-seats", async (
            Guid branchId,
            DateTimeOffset startsAtUtc,
            DateTimeOffset endsAtUtc,
            Guid? excludeReservationId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewReservations,
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
                    AuditActionNames.ViewReservations,
                    "Reservation",
                    excludeReservationId?.ToString("D"),
                    AuditOutcome.Denied,
                    new { startsAtUtc, endsAtUtc, excludeReservationId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (endsAtUtc <= startsAtUtc)
            {
                return Results.BadRequest(new { Error = "endsAtUtc must be later than startsAtUtc." });
            }

            var result = await reservationService.FindFreeSeatsAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                startsAtUtc,
                endsAtUtc,
                excludeReservationId,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewReservations,
                "Reservation",
                excludeReservationId?.ToString("D"),
                AuditOutcome.Succeeded,
                new { startsAtUtc, endsAtUtc, excludeReservationId, ResultCount = result.FreeSeatIds.Count },
                cancellationToken);

            return Results.Ok(result);
        });

        app.MapPost("branches/{branchId:guid}/reservations", async (
            Guid branchId,
            CreateReservationRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageReservations,
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
                    AuditActionNames.CreateReservation,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { request.CustomerName, request.StartsAtUtc, request.SeatId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.CreateAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateReservation,
                "Reservation",
                result.Response!.ReservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.CustomerName, request.StartsAtUtc, request.SeatId, result.Response.State, result.Response.Source },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        // Group create: several seats booked together as one reservation (drag across timeline rows).
        // All-or-nothing — any conflicting seat returns 409 with the conflict list, nothing is written.
        app.MapPost("branches/{branchId:guid}/reservations/group", async (
            Guid branchId,
            CreateReservationGroupRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageReservations,
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
                    AuditActionNames.CreateReservation,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { request.CustomerName, request.StartsAtUtc, SeatCount = request.SeatIds?.Count ?? 0, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.CreateGroupAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            switch (result.Status)
            {
                case ReservationGroupStatus.Invalid:
                    return Results.BadRequest(new { Error = result.Error });
                case ReservationGroupStatus.Conflict:
                    return Results.Json(result.Result, statusCode: StatusCodes.Status409Conflict);
                default:
                    await WriteAuditAsync(
                        auditRecordWriter,
                        authorization.StaffContext.OrganizationId,
                        branchId,
                        authorization.StaffContext.StaffUserId,
                        AuditActionNames.CreateReservation,
                        "Reservation",
                        result.Result!.ReservationGroupId?.ToString("D"),
                        AuditOutcome.Succeeded,
                        new { request.CustomerName, request.StartsAtUtc, SeatCount = result.Result.Reservations.Count, result.Result.ReservationGroupId },
                        cancellationToken);

                    return Results.Ok(result.Result);
            }
        });

        MapReservationAction<UpdateReservationRequest>(
            app, HttpMethods.Patch, string.Empty, AuditActionNames.UpdateReservation,
            (service, reservationId, staffUserId, request, ct) => service.UpdateAsync(reservationId, staffUserId, request, ct),
            (reservation, _) => new { reservation.SeatId, reservation.StartsAtUtc, reservation.State },
            request => request.OrganizationId,
            deniedDetails: (request, denialReason) => new { request.SeatId, request.StartsAtUtc, DenialReason = denialReason });

        MapReservationAction<ConfirmReservationRequest>(
            app, HttpMethods.Post, "/confirm", AuditActionNames.ConfirmReservation,
            (service, reservationId, staffUserId, request, ct) => service.ConfirmAsync(reservationId, staffUserId, request, ct),
            (reservation, _) => new { reservation.State },
            request => request.OrganizationId,
            afterSuccess: (push, reservation, ct) => NotifyReservationAnsweredAsync(push, reservation, confirmed: true, ct));

        MapReservationAction<SeatReservationRequest>(
            app, HttpMethods.Post, "/seat", AuditActionNames.SeatReservation,
            (service, reservationId, staffUserId, request, ct) => service.SeatAsync(reservationId, staffUserId, request, ct),
            (reservation, _) => new { reservation.SeatId, reservation.State },
            request => request.OrganizationId);

        // Причина в журнале та же, что уехала игроку: у вопроса «почему клубу отказали
        // третий вечер подряд» должен быть один ответ, а не два.
        MapReservationAction<RejectReservationRequest>(
            app, HttpMethods.Post, "/reject", AuditActionNames.RejectReservation,
            (service, reservationId, staffUserId, request, ct) => service.RejectAsync(reservationId, staffUserId, request, ct),
            (reservation, _) => new { reservation.RejectReasonCode, reservation.RejectReasonNote },
            request => request.OrganizationId,
            afterSuccess: (push, reservation, ct) => NotifyReservationAnsweredAsync(push, reservation, confirmed: false, ct));

        // Удержанная сумма пишется в аудит: это деньги, оставшиеся клубу по решению человека,
        // и вопрос «кто отметил неявку, за которую игрок заплатил» обязан иметь ответ.
        MapReservationAction<MarkReservationNoShowRequest>(
            app, HttpMethods.Post, "/no-show", AuditActionNames.MarkReservationNoShow,
            (service, reservationId, staffUserId, request, ct) => service.MarkNoShowAsync(reservationId, staffUserId, request, ct),
            (reservation, _) => new { reservation.State, reservation.RetainedAmountMinorUnits },
            request => request.OrganizationId);

        MapReservationAction<CancelReservationRequest>(
            app, HttpMethods.Post, "/cancel", AuditActionNames.CancelReservation,
            (service, reservationId, staffUserId, request, ct) => service.CancelAsync(reservationId, staffUserId, request, ct),
            (reservation, request) => new { reservation.State, request.Reason },
            request => request.OrganizationId);

        app.MapPost("reservations/{reservationId:guid}/start-session", async (
            Guid reservationId,
            StartReservationSessionRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationSessionCoordinator reservationSessionCoordinator,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(
                dbContext,
                staffContextAccessor,
                reservationId,
                cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                OrganizationPermissionNames.ManageReservations,
                cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var denialReason = authorization.DenialReason;
            if (authorization.IsAllowed)
            {
                var sessionAuthorization = await authorizationService.RequireBranchPermissionAsync(
                    scoped.Reservation.BranchId,
                    OrganizationPermissionNames.StartSession,
                    cancellationToken);
                authorization = sessionAuthorization;
                denialReason = sessionAuthorization.DenialReason;
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.StartReservationSession,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.ExpectedVersion, denialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new
                {
                    Error = "OrganizationId must match the authenticated staff organization.",
                    Code = "organization_mismatch",
                    CurrentVersion = (int?)null
                });
            }

            var result = await reservationSessionCoordinator.StartAsync(
                reservationId,
                authorization.StaffContext.StaffUserId,
                authorization.StaffContext.Permissions.Contains(OrganizationPermissionNames.ApproveMoneyAction),
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { result.Error, result.Code, result.CurrentVersion, result.PlanLimit });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { result.Error, result.Code, result.CurrentVersion });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { result.Error, result.Code, result.CurrentVersion });
            }

            return Results.Ok(result.Response);
        });

    }

    /// <summary>
    /// Действие стойки над одной бронью: найти бронь в своей организации, проверить право в её
    /// филиале (отказ — в журнал), сверить организацию запроса, выполнить, записать успех. Шесть
    /// действий делали это шестью копиями по семьдесят строк.
    /// </summary>
    private static void MapReservationAction<TRequest>(
        IEndpointRouteBuilder app,
        string httpMethod,
        string suffix,
        string auditAction,
        Func<IReservationService, Guid, Guid, TRequest, CancellationToken, Task<ReservationServiceResult<ReservationDto>>> action,
        Func<ReservationDto, TRequest, object> succeededDetails,
        Func<TRequest, Guid> organizationOf,
        Func<TRequest, string?, object>? deniedDetails = null,
        Func<PlayerPushNotifier, ReservationDto, CancellationToken, Task>? afterSuccess = null)
    {
        app.MapMethods($"reservations/{{reservationId:guid}}{suffix}", [httpMethod], async (
            Guid reservationId,
            TRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            PlayerPushNotifier playerPush,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                OrganizationPermissionNames.ManageReservations,
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
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    auditAction,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    deniedDetails is null
                        ? new { authorization.DenialReason }
                        : deniedDetails(request, authorization.DenialReason),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (organizationOf(request) != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await action(
                reservationService,
                reservationId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                result.Response!.BranchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "Reservation",
                reservationId.ToString("D"),
                AuditOutcome.Succeeded,
                succeededDetails(result.Response, request),
                cancellationToken);

            if (afterSuccess is not null)
            {
                await afterSuccess(playerPush, result.Response, cancellationToken);
            }

            return Results.Ok(result.Response);
        });
    }

    /// <summary>
    /// Сказать игроку, что клуб ответил. Заявку могли завести и на стойке — тогда адресата нет,
    /// и писать некому. Сбой доставки ответ клуба не отменяет: решение уже записано.
    /// </summary>
    private static async Task NotifyReservationAnsweredAsync(
        PlayerPushNotifier playerPush,
        ReservationDto reservation,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (reservation.PlayerAccountId is not { } playerAccountId)
        {
            return;
        }

        await playerPush.ReservationAnsweredAsync(
            playerAccountId,
            reservation.OrganizationId,
            reservation.BranchId,
            reservation.StartsAtUtc,
            confirmed,
            reservation.RejectReasonNote,
            cancellationToken);
    }
}
