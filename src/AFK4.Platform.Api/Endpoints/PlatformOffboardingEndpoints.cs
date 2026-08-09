using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Offboarding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Уход клуба: состояние, выгрузка данных и стирание. Оба действия видны наружу и необратимы по
/// последствиям — выгрузка выносит персональные данные, стирание уничтожает их, — поэтому у них
/// своё право и полный след в аудите.
/// </summary>
public static class PlatformOffboardingEndpoints
{
    public static void MapPlatformOffboardingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/organizations/{organizationId:guid}/offboarding", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOffboarding);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var organization = await dbContext.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
            if (organization is null) return Results.NotFound();

            var now = timeProvider.GetUtcNow();
            return Results.Ok(new OrganizationOffboardingDto(
                organization.OrganizationId,
                organization.Slug,
                organization.Status,
                organization.PurgeEligibleAtUtc,
                organization.PurgedAtUtc,
                CanPurge: organization.Status == OrganizationStatusNames.DeletionPending
                    && organization.PurgeEligibleAtUtc is not null
                    && organization.PurgeEligibleAtUtc <= now));
        });

        app.MapGet("/api/platform/organizations/{organizationId:guid}/export", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            OrganizationExportService exportService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOffboarding);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.ExportOrganizationData,
                    organizationId, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organization = await dbContext.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
            if (organization is null) return Results.NotFound();

            var archive = await exportService.BuildArchiveAsync(organizationId, cancellationToken);

            // Аудит пишется до отдачи файла: это вынос персональных данных наружу, и запись о нём
            // не должна зависеть от того, дочитал ли клиент ответ.
            await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.ExportOrganizationData,
                organizationId, AuditOutcome.Succeeded, new { Bytes = archive.Length }, cancellationToken);

            return Results.File(archive, "application/zip", $"{organization.Slug}-export.zip");
        });

        app.MapPost("/api/platform/organizations/{organizationId:guid}/purge", async (
            Guid organizationId,
            PurgeOrganizationRequest request,
            PlatformAdminAuthorizationService authorizationService,
            OrganizationPurgeService purgeService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOffboarding);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.PurgeOrganizationRequested,
                    organizationId, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Намерение фиксируется ДО стирания: если что-то пойдёт не так посреди операции, в
            // журнале всё равно останется, кто и когда её начал.
            await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.PurgeOrganizationRequested,
                organizationId, AuditOutcome.Succeeded, new { }, cancellationToken);

            var result = await purgeService.PurgeAsync(organizationId, request.Slug, cancellationToken);
            if (!result.Succeeded)
            {
                await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.PurgeOrganizationCompleted,
                    organizationId, AuditOutcome.Denied, new { Reason = result.ErrorCode }, cancellationToken);
                return MapFailure(result.ErrorCode!);
            }

            // Запись о факте переживает собственное стирание: у неё есть актор-платформа, а по
            // этому признаку аудит платформы и остаётся в базе.
            await WriteAuditAsync(auditRecordWriter, authorization, AuditActionNames.PurgeOrganizationCompleted,
                organizationId, AuditOutcome.Succeeded, result.Value!, cancellationToken);

            return Results.Ok(result.Value);
        });
    }

    private static IResult MapFailure(string errorCode) =>
        errorCode switch
        {
            OffboardingErrorCodes.SlugMismatch => Results.BadRequest(Body(errorCode)),
            _ => Results.Conflict(Body(errorCode))
        };

    private static object Body(string code) => new { Error = code, Code = code };

    private static Task WriteAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        Guid organizationId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: organizationId,
            actorPlatformAdminUserId: authorization.PlatformAdminContext?.PlatformAdminUserId,
            action: action,
            targetType: "Organization",
            targetId: organizationId.ToString(),
            outcome: outcome,
            details: details,
            cancellationToken);
}
