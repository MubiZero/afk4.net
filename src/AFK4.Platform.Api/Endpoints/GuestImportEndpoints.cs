using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>Перенос гостей из прежней программы: пробный прогон и перенос одной ручкой.</summary>
internal static class GuestImportEndpoints
{
    public static void MapGuestImportEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/players/import", async (
            Guid branchId,
            GuestImportRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            GuestImport import,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ImportPlayers, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || request.OrganizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.Rows.Count == 0 || request.Rows.Count > GuestImportLimits.MaxRows)
                return Results.BadRequest(new { error = $"Send between 1 and {GuestImportLimits.MaxRows} rows." });
            if (string.IsNullOrWhiteSpace(request.CurrencyCode) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
                return Results.BadRequest(new { error = "Currency and idempotency key are required." });

            var staff = authorization.StaffContext;
            var result = await import.RunAsync(staff.OrganizationId, branchId, staff.StaffUserId, request, ct);
            if (result.Committed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    staff.OrganizationId, branchId, staff.StaffUserId, AuditActionNames.ImportPlayers, "PlayerImport",
                    request.IdempotencyKey, AuditOutcome.Succeeded, "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.Source, result.Total, result.Created, result.Matched, result.Skipped,
                        Balance = result.BalanceTotal.MinorUnits, Bonus = result.BonusTotal.MinorUnits
                    }))
                {
                    AmountMinorUnits = result.BalanceTotal.MinorUnits + result.BonusTotal.MinorUnits
                }, ct);
            }

            return Results.Ok(result);
        });
    }
}
