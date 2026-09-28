using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Tariffs;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class TariffEndpoints
{
    public static void MapTariffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/tariffs", async (
            Guid branchId,
            CreateTariffRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfTariffService tariffService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageTariffs,
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
                    AuditActionNames.CreateTariff,
                    "Tariff",
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

            var result = await tariffService.CreateTariffAsync(
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
                AuditActionNames.CreateTariff,
                "Tariff",
                result.Response!.TariffId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Name },
                cancellationToken);

            return Results.Ok(result.Response);
        })
            // Первый тариф клуб заводит в мастере установки, а не только в панели.
            .AllowNonOrganizationAdminClients();

        app.MapPost("branches/{branchId:guid}/tariffs/{tariffId:guid}/versions", async (
            Guid branchId,
            Guid tariffId,
            CreateTariffVersionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfTariffService tariffService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageTariffs,
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
                    AuditActionNames.CreateTariffVersion,
                    "Tariff",
                    tariffId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (request.TariffId != tariffId)
            {
                return Results.BadRequest(new { Error = "Route tariffId must match request TariffId." });
            }

            var result = await tariffService.CreateTariffVersionAsync(
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
                AuditActionNames.CreateTariffVersion,
                "TariffVersion",
                result.Response!.TariffVersionId.ToString("D"),
                AuditOutcome.Succeeded,
                new { tariffId, result.Response.VersionNumber },
                cancellationToken);

            return Results.Ok(result.Response);
        })
            // Цена первого тарифа ставится там же, в мастере установки.
            .AllowNonOrganizationAdminClients();

        app.MapPatch("branches/{branchId:guid}/tariffs/{tariffId:guid}", async (
            Guid branchId,
            Guid tariffId,
            UpdateTariffRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfTariffService tariffService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageTariffs,
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
                    AuditActionNames.UpdateTariff,
                    "Tariff",
                    tariffId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Name, request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await tariffService.UpdateTariffAsync(
                branchId,
                tariffId,
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
                AuditActionNames.UpdateTariff,
                "Tariff",
                tariffId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response!.Name, result.Response.IsActive },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPatch("branches/{branchId:guid}/tariffs/{tariffId:guid}/versions/{tariffVersionId:guid}", async (
            Guid branchId,
            Guid tariffId,
            Guid tariffVersionId,
            UpdateTariffVersionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfTariffService tariffService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageTariffs,
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
                    AuditActionNames.UpdateTariffVersion,
                    "TariffVersion",
                    tariffVersionId.ToString("D"),
                    AuditOutcome.Denied,
                    new { tariffId, request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await tariffService.UpdateTariffVersionAsync(
                branchId,
                tariffId,
                tariffVersionId,
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
                AuditActionNames.UpdateTariffVersion,
                "TariffVersion",
                tariffVersionId.ToString("D"),
                AuditOutcome.Succeeded,
                new
                {
                    tariffId,
                    result.Response!.VersionNumber,
                    result.Response.PricePerMinuteMinorUnits,
                    result.Response.RetiredAtUtc
                },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("branches/{branchId:guid}/tariffs/options", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfOperatorReferenceDataService referenceDataService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewTariffs,
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
                    AuditActionNames.ViewTariffs,
                    "Tariff",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var options = await referenceDataService.GetTariffOptionsAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return Results.Ok(options);
        });

    }
}
