using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tournaments;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Tournaments;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// События клуба со стороны стойки: расписание, правка, публикация, отмена и список записавшихся.
/// Сторона игрока живёт в <see cref="PlayerTournamentEndpoints"/> — у неё другие правила доступа
/// и другой набор полей.
/// </summary>
internal static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/tournaments", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var items = await tournaments.ListForClubAsync(
                authorization.StaffContext!.OrganizationId, branchId, ct);
            return Results.Ok(items);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageTournaments);

        app.MapPost("tournaments", async (
            CreateTournamentRequest request,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await tournaments.CreateAsync(staff.OrganizationId, staff.StaffUserId, request, ct);
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                request.BranchId,
                staff.StaffUserId,
                AuditActionNames.CreateTournament,
                "Tournament",
                result.Value!.TournamentId.ToString("N"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Value);
        });

        app.MapPatch("tournaments/{tournamentId:guid}", async (
            Guid tournamentId,
            UpdateTournamentRequest request,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await tournaments.UpdateAsync(staff.OrganizationId, tournamentId, request, ct);
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.BadRequest(new { error = result.Error });

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                result.Value!.BranchId,
                staff.StaffUserId,
                AuditActionNames.UpdateTournament,
                "Tournament",
                tournamentId.ToString("N"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Value);
        });

        app.MapPost("tournaments/{tournamentId:guid}/publish", async (
            Guid tournamentId,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await tournaments.PublishAsync(staff.OrganizationId, tournamentId, ct);
            if (result.NotFound) return Results.NotFound();
            // Опубликовать нельзя не потому, что запрос кривой, а потому, что событие в таком
            // состоянии — это конфликт, и стойка должна прочитать причину.
            if (!result.Succeeded) return Results.Conflict(new { error = result.Error });

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                result.Value!.BranchId,
                staff.StaffUserId,
                AuditActionNames.PublishTournament,
                "Tournament",
                tournamentId.ToString("N"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                DetailsJson: "{}"), ct);

            return Results.Ok(result.Value);
        });

        app.MapPost("tournaments/{tournamentId:guid}/cancel", async (
            Guid tournamentId,
            CancelTournamentRequest request,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await tournaments.CancelAsync(
                staff.OrganizationId, tournamentId, staff.StaffUserId, request.Reason, ct);
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.Conflict(new { error = result.Error });

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                result.Value!.BranchId,
                staff.StaffUserId,
                AuditActionNames.CancelTournament,
                "Tournament",
                tournamentId.ToString("N"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Value);
        });

        app.MapGet("tournaments/{tournamentId:guid}/participants", async (
            Guid tournamentId,
            StaffAuthorizationService authorizationService,
            ITournamentService tournaments,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(
                OrganizationPermissionNames.ManageTournaments);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await tournaments.ListParticipantsAsync(
                authorization.StaffContext!.OrganizationId, tournamentId, ct);
            return result.NotFound ? Results.NotFound() : Results.Ok(result.Value);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageTournaments);
    }
}
