using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Панель досылает журнал кассы, который вела без интернета (docs/roadmap/offline-front-desk.md). Право —
/// сажать гостей: журнал пишет тот, кто сажал. Ответ называет судьбу каждой записи; Панель убирает из
/// журнала всё, на что пришёл окончательный ответ, — отказанные записи остаются у сервера. Продажа и смена
/// сверх того проверяются своими правами — по записи.
/// </summary>
internal static class OfflineJournalEndpoints
{
    public static void MapOfflineJournalEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/offline-journal", async (
            Guid branchId, OfflineJournalUploadRequest request, StaffAuthorizationService authorizationService,
            OfflineJournalIngest ingest, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.StartSession, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.Records is null || request.Records.Count is 0 or > OfflineJournalIngest.MaxRecordsPerUpload)
            {
                return Results.BadRequest(new { error = $"Send 1 to {OfflineJournalIngest.MaxRecordsPerUpload} records." });
            }

            // Продажа и смена — свои права: кто сажает гостей, не обязательно продаёт или сдаёт смену.
            var granted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permission in OfflineJournalIngest.CashPermissions)
            {
                if ((await authorizationService.RequireBranchPermissionAsync(branchId, permission, ct)).IsAllowed) granted.Add(permission);
            }

            var staff = authorization.StaffContext!;
            return Results.Ok(await ingest.IngestAsync(staff.OrganizationId, branchId, staff.StaffUserId, request.Records, granted, ct));
        });
    }
}
