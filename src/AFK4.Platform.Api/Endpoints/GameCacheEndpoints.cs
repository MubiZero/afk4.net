using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Games;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Кеш игр клуба (docs/operations/game-cache-lancache.md): включён ли, где он (встроенный на ПК администратора
/// или свой сервер LANCache клуба) и сколько ПК его видят. Право — то же, что у библиотеки игр: кто решает, что на ПК запускается, решает и откуда это качать.
/// Поддержка платформы адрес видит, но не меняет: он ведёт загрузки всех ПК клуба.
/// </summary>
internal static class GameCacheEndpoints
{
    public static void MapGameCacheEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("branches/{branchId:guid}/game-cache", async (
            Guid branchId, StaffAuthorizationService authorizationService, PlatformDbContext dbContext, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ManageGameLibrary, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var dto = await ReadAsync(dbContext, authorization.StaffContext!.OrganizationId, branchId, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).AllowPlatformSupportAccess(OrganizationPermissionNames.ManageGameLibrary);

        organizations.MapPut("branches/{branchId:guid}/game-cache", async (
            Guid branchId, UpdateBranchGameCacheRequest request, StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter, PlatformDbContext dbContext, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ManageGameLibrary, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            var staff = authorization.StaffContext!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                    AuditActionNames.UpdateGameCache, "Branch", branchId.ToString("D"), AuditOutcome.Denied, new { authorization.DenialReason }, ct);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            if (address is not null && !GameCacheNames.IsValidAddress(address))
            {
                return Results.BadRequest(new { error = "The cache address must be a private IPv4 address in the club network.", code = "game_cache_address_invalid" });
            }

            var branch = await dbContext.Branches.SingleOrDefaultAsync(
                candidate => candidate.BranchId == branchId && candidate.OrganizationId == staff.OrganizationId, ct);
            if (branch is null) return Results.NotFound();

            if (branch.GameCacheAddress != address || branch.GameCacheDisabled == request.Enabled)
            {
                branch.GameCacheAddress = address;
                branch.GameCacheDisabled = !request.Enabled;
                // Прежняя отметка «видит кеш» была о другом адресе: пусть ПК проверят новый.
                foreach (var device in await dbContext.Devices.Where(device => device.BranchId == branchId && device.GameCacheReachable != null).ToListAsync(ct))
                {
                    device.GameCacheReachable = null;
                }

                await dbContext.SaveChangesAsync(ct);
            }

            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.UpdateGameCache, "Branch", branchId.ToString("D"), AuditOutcome.Succeeded, new { address, request.Enabled }, ct);
            return Results.Ok(await ReadAsync(dbContext, staff.OrganizationId, branchId, ct));
        });
    }

    private static async Task<BranchGameCacheDto?> ReadAsync(PlatformDbContext dbContext, Guid organizationId, Guid branchId, CancellationToken ct)
    {
        var branch = await dbContext.Branches.AsNoTracking()
            .Where(candidate => candidate.BranchId == branchId && candidate.OrganizationId == organizationId)
            .Select(candidate => new { candidate.GameCacheAddress, candidate.GameCacheDisabled })
            .SingleOrDefaultAsync(ct);
        if (branch is null) return null;

        var host = branch.GameCacheDisabled || branch.GameCacheAddress is not null ? null : await BranchGameCache.HostAsync(dbContext, branchId, ct);

        var reports = await dbContext.Devices.AsNoTracking()
            .Where(device => device.BranchId == branchId && device.GameCacheReachable != null)
            .GroupBy(device => device.GameCacheReachable)
            .Select(group => new { Reachable = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        return new BranchGameCacheDto(
            branch.GameCacheAddress,
            reports.Where(row => row.Reachable == true).Sum(row => row.Count),
            reports.Sum(row => row.Count),
            Enabled: !branch.GameCacheDisabled,
            HostName: host?.MachineName,
            EffectiveAddress: branch.GameCacheDisabled ? null : branch.GameCacheAddress ?? host?.NetworkAddress,
            StoredBytes: host?.GameCacheStoredBytes ?? 0);
    }
}
